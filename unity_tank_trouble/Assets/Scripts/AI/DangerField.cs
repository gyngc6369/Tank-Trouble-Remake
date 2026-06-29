using System.Collections.Generic;
using UnityEngine;
using TankTrouble.Config;
using TankTrouble.Core;
using TankTrouble.Entities;

namespace TankTrouble.AI
{
    public readonly struct DangerQuery
    {
        public readonly bool HasRisk;
        public readonly float Risk;
        public readonly Vector2 EscapeDirection;
        public readonly float TimeToImpact;
        public readonly float Distance;

        public DangerQuery(bool hasRisk, float risk, Vector2 escapeDirection, float timeToImpact, float distance)
        {
            HasRisk = hasRisk;
            Risk = risk;
            EscapeDirection = escapeDirection;
            TimeToImpact = timeToImpact;
            Distance = distance;
        }

        public static DangerQuery None => new DangerQuery(false, 0f, Vector2.zero, float.MaxValue, float.MaxValue);
    }

    public sealed class DangerField
    {
        private const float CollisionPadding = 0.015f;
        private const float AvoidancePadding = 0.12f;
        private const float TimeSyncWindow = 0.55f;
        private const float IncomingUrgencyScale = 0.85f;
        private const int MaxTrajectorySegments = 96;

        private readonly List<DangerSegment> segments = new List<DangerSegment>(MaxTrajectorySegments);

        public bool HasDanger => segments.Count > 0;

        public void Clear()
        {
            segments.Clear();
        }

        public void Build(IReadOnlyList<BulletController> bullets, LayerMask wallMask, float predictTime)
        {
            if (bullets == null || predictTime <= 0f)
                return;

            for (var i = 0; i < bullets.Count; i++)
            {
                var bullet = bullets[i];
                if (bullet == null || !bullet.Alive || bullet.Velocity.sqrMagnitude < 0.0001f)
                    continue;

                AddTrajectory(bullet.WorldPosition, bullet.Velocity, bullet.BounceCount, wallMask, predictTime, 0f);
            }
        }

        public void AddPredictedShot(Vector2 startWorld, Vector2 direction, LayerMask wallMask, float predictTime, float ignoreStartDistance = 0f)
        {
            if (direction.sqrMagnitude < 0.0001f || predictTime <= 0f)
                return;

            var velocity = direction.normalized * (GameConfig.BulletSpeed / CoordinateUtil.PixelsPerUnit);
            AddTrajectory(startWorld, velocity, 0, wallMask, predictTime, ignoreStartDistance);
        }

        public DangerQuery QueryIncoming(Vector2 worldPosition)
        {
            var best = DangerQuery.None;
            var bestScore = 0f;

            for (var i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                var closest = ClosestPointOnSegment(worldPosition, segment.Start, segment.End, out var t);
                var distance = Vector2.Distance(worldPosition, closest);
                var distanceWeight = CalculateDistanceWeight(distance);
                if (distanceWeight <= 0f)
                    continue;

                var timeToImpact = Mathf.Lerp(segment.StartTime, segment.EndTime, t);
                var urgency = 1f / (1f + timeToImpact * IncomingUrgencyScale);
                var risk = distanceWeight * urgency;
                if (risk <= bestScore)
                    continue;

                var escape = worldPosition - closest;
                if (escape.sqrMagnitude < 0.0001f)
                    escape = Vector2.Perpendicular(segment.Direction);

                bestScore = risk;
                best = new DangerQuery(true, risk, escape.normalized, timeToImpact, distance);
            }

            return best;
        }

        public float GetRisk(Vector2 worldPosition, float queryTime)
        {
            var bestRisk = 0f;
            for (var i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                var closest = ClosestPointOnSegment(worldPosition, segment.Start, segment.End, out var t);
                var distanceWeight = CalculateDistanceWeight(Vector2.Distance(worldPosition, closest));
                if (distanceWeight <= 0f)
                    continue;

                var timeToImpact = Mathf.Lerp(segment.StartTime, segment.EndTime, t);
                var syncWeight = Mathf.Exp(-Mathf.Abs(timeToImpact - queryTime) / TimeSyncWindow);
                var urgency = Mathf.Lerp(0.62f, 1f, Mathf.Exp(-timeToImpact / 1.4f));
                bestRisk = Mathf.Max(bestRisk, distanceWeight * syncWeight * urgency);
            }

            return bestRisk;
        }

        public Vector2 GetEscapeDirection(Vector2 worldPosition)
        {
            var query = QueryIncoming(worldPosition);
            return query.HasRisk ? query.EscapeDirection : Vector2.zero;
        }

        private void AddTrajectory(Vector2 startWorld, Vector2 velocity, int existingBounces, LayerMask wallMask, float predictTime, float ignoreStartDistance)
        {
            var speed = velocity.magnitude;
            if (speed <= 0.0001f)
                return;

            var position = startWorld;
            var direction = velocity.normalized;
            var remaining = predictTime;
            var elapsed = 0f;
            var travelled = 0f;
            var bounceCount = existingBounces;
            var radius = GameConfig.BulletRadius / CoordinateUtil.PixelsPerUnit;

            while (remaining > 0f && bounceCount <= GameConfig.MaxBounces && segments.Count < MaxTrajectorySegments)
            {
                var distance = speed * remaining;
                var hit = Physics2D.CircleCast(position, radius, direction, distance, wallMask);
                var end = hit.collider != null ? hit.centroid : position + direction * distance;
                var segmentLength = Vector2.Distance(position, end);

                AddSegmentAfterDistance(position, end, direction, elapsed, speed, travelled, ignoreStartDistance);

                if (hit.collider == null || bounceCount >= GameConfig.MaxBounces)
                    break;

                var travelTime = Mathf.Max(0f, segmentLength / speed);
                remaining -= travelTime;
                elapsed += travelTime;
                travelled += segmentLength;
                direction = Vector2.Reflect(direction, hit.normal).normalized;
                position = hit.centroid + direction * (radius + 0.001f);

                bounceCount++;
                if (travelTime <= 0.0001f)
                    remaining -= 0.01f;
            }
        }

        private void AddSegmentAfterDistance(Vector2 start, Vector2 end, Vector2 direction, float startTime, float speed, float travelledBeforeSegment, float ignoreStartDistance)
        {
            var segmentLength = Vector2.Distance(start, end);
            if (segmentLength <= 0.0001f || travelledBeforeSegment + segmentLength <= ignoreStartDistance)
                return;

            var trimmedStart = start;
            var trimmedStartTime = startTime;
            if (travelledBeforeSegment < ignoreStartDistance)
            {
                var trimDistance = ignoreStartDistance - travelledBeforeSegment;
                var trimT = Mathf.Clamp01(trimDistance / segmentLength);
                trimmedStart = Vector2.Lerp(start, end, trimT);
                trimmedStartTime += trimDistance / speed;
            }

            var trimmedLength = Vector2.Distance(trimmedStart, end);
            if (trimmedLength <= 0.0001f)
                return;

            segments.Add(new DangerSegment(trimmedStart, end, direction, trimmedStartTime, trimmedStartTime + trimmedLength / speed));
        }

        private static float CalculateDistanceWeight(float distance)
        {
            var innerRadius = Mathf.Max(GameConfig.TankBodyWidth, GameConfig.TankBodyHeight) * 0.5f / CoordinateUtil.PixelsPerUnit
                + GameConfig.BulletRadius / CoordinateUtil.PixelsPerUnit
                + CollisionPadding;
            var outerRadius = innerRadius + AvoidancePadding;

            if (distance <= innerRadius)
                return 1f;
            if (distance >= outerRadius)
                return 0f;

            var normalized = 1f - (distance - innerRadius) / (outerRadius - innerRadius);
            return Mathf.SmoothStep(0f, 1f, normalized);
        }

        private static Vector2 ClosestPointOnSegment(Vector2 point, Vector2 start, Vector2 end, out float t)
        {
            var segment = end - start;
            var lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= 0.000001f)
            {
                t = 0f;
                return start;
            }

            t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
            return start + segment * t;
        }

        private readonly struct DangerSegment
        {
            public readonly Vector2 Start;
            public readonly Vector2 End;
            public readonly Vector2 Direction;
            public readonly float StartTime;
            public readonly float EndTime;

            public DangerSegment(Vector2 start, Vector2 end, Vector2 direction, float startTime, float endTime)
            {
                Start = start;
                End = end;
                Direction = direction;
                StartTime = startTime;
                EndTime = endTime;
            }
        }
    }
}
