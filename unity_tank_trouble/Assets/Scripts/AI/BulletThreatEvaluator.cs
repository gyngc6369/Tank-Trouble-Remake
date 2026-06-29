using System.Collections.Generic;
using UnityEngine;
using TankTrouble.Config;
using TankTrouble.Core;
using TankTrouble.Entities;

namespace TankTrouble.AI
{
    public readonly struct BulletThreat
    {
        public readonly bool IsThreat;
        public readonly Vector2 EscapeDirection;
        public readonly float TimeToImpact;
        public readonly float Distance;

        public BulletThreat(bool isThreat, Vector2 escapeDirection, float timeToImpact, float distance)
        {
            IsThreat = isThreat;
            EscapeDirection = escapeDirection;
            TimeToImpact = timeToImpact;
            Distance = distance;
        }

        public static BulletThreat None => new BulletThreat(false, Vector2.zero, float.MaxValue, float.MaxValue);
    }

    public static class BulletThreatEvaluator
    {
        private const float ImmediateThreatTime = 0.75f;
        private const float ThreatPredictTime = 2.2f;
        private const float SegmentPadding = 0.07f;

        public static BulletThreat Evaluate(TankController tank, IReadOnlyList<BulletController> bullets, LayerMask wallMask)
        {
            if (tank == null || bullets == null)
                return BulletThreat.None;

            var tankPosition = (Vector2)tank.transform.position;
            var unsafeRadius = Mathf.Max(GameConfig.TankBodyWidth, GameConfig.TankBodyHeight) * 0.65f / CoordinateUtil.PixelsPerUnit
                + GameConfig.BulletRadius / CoordinateUtil.PixelsPerUnit
                + SegmentPadding;

            var bestThreat = BulletThreat.None;
            var bestScore = float.NegativeInfinity;
            for (var i = 0; i < bullets.Count; i++)
            {
                var bullet = bullets[i];
                if (bullet == null || !bullet.Alive || bullet.Velocity.sqrMagnitude < 0.0001f)
                    continue;

                var candidate = EvaluateBullet(tankPosition, unsafeRadius, bullet.WorldPosition, bullet.Velocity, bullet.BounceCount, wallMask);
                if (!candidate.IsThreat)
                    continue;

                var score = (ImmediateThreatTime - candidate.TimeToImpact) * 4f + (unsafeRadius - candidate.Distance) * 8f;
                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestThreat = candidate;
            }

            return bestThreat;
        }

        private static BulletThreat EvaluateBullet(Vector2 tankPosition, float unsafeRadius, Vector2 startWorld, Vector2 velocity, int existingBounces, LayerMask wallMask)
        {
            var position = startWorld;
            var direction = velocity.normalized;
            var speed = GameConfig.BulletSpeed / CoordinateUtil.PixelsPerUnit;
            var remaining = ThreatPredictTime;
            var bounceCount = existingBounces;
            var radius = GameConfig.BulletRadius / CoordinateUtil.PixelsPerUnit;
            var elapsed = 0f;

            while (remaining > 0f && bounceCount <= GameConfig.MaxBounces)
            {
                var distance = speed * remaining;
                var hit = Physics2D.CircleCast(position, radius, direction, distance, wallMask);
                var end = hit.collider != null ? hit.centroid : position + direction * distance;
                var segmentLength = Vector2.Distance(position, end);
                var closest = ClosestPointOnSegment(tankPosition, position, end, out var normalizedDistanceAlongSegment);
                var closestDistance = Vector2.Distance(tankPosition, closest);
                var timeToClosest = elapsed + (segmentLength * normalizedDistanceAlongSegment) / speed;

                if (closestDistance <= unsafeRadius && timeToClosest <= ImmediateThreatTime)
                {
                    var escape = tankPosition - closest;
                    if (escape.sqrMagnitude < 0.0001f)
                        escape = Vector2.Perpendicular(direction);

                    return new BulletThreat(true, escape.normalized, timeToClosest, closestDistance);
                }

                if (hit.collider == null || bounceCount >= GameConfig.MaxBounces)
                    break;

                var travelTime = Mathf.Max(0f, hit.distance / speed);
                elapsed += travelTime;
                remaining -= travelTime;
                direction = Vector2.Reflect(direction, hit.normal).normalized;
                position = hit.centroid + direction * (radius + 0.001f);
                bounceCount++;
            }

            return BulletThreat.None;
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
    }
}
