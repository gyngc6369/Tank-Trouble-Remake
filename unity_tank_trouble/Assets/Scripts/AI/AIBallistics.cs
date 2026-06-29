using System.Collections.Generic;
using UnityEngine;
using TankTrouble.Config;
using TankTrouble.Core;
using TankTrouble.Entities;

namespace TankTrouble.AI
{
    public static class AIBallistics
    {
        public const int MaxAimBounces = 4;
        private const int CoarseAngleCount = 180;
        private const float FineAngleStep = 0.5f;
        private const int FineStepsPerSide = 3;
        private const float RejectSelfRisk = 0.86f;

        private static readonly List<HitSolution> Solutions = new List<HitSolution>(16);

        public static bool TryFindBestShot(TankController shooter, IReadOnlyList<TankController> targets, LayerMask wallMask, LayerMask tankMask, out HitSolution best)
        {
            Solutions.Clear();
            best = default;
            if (shooter == null || !shooter.Alive || targets == null)
                return false;

            var origin = shooter.BarrelTipWorld;
            var currentForward = shooter.VelocityForward;
            var wallMaskValue = wallMask.value;
            var combinedMask = wallMask.value | tankMask.value;

            AddTargetDirections(shooter, targets, origin, currentForward, combinedMask, wallMaskValue);
            AddCoarseSweep(shooter, targets, origin, currentForward, combinedMask, wallMaskValue);

            if (Solutions.Count == 0)
                return false;

            Solutions.Sort(CompareSolutions);
            best = Solutions[0];
            return true;
        }

        public static bool TryValidateShot(TankController shooter, IReadOnlyList<TankController> targets, Vector2 direction, LayerMask wallMask, LayerMask tankMask, out HitSolution solution)
        {
            solution = default;
            if (shooter == null || !shooter.Alive || targets == null || direction.sqrMagnitude < 0.0001f)
                return false;

            var combinedMask = wallMask.value | tankMask.value;
            return TrySimulate(shooter, targets, shooter.BarrelTipWorld, direction.normalized, shooter.VelocityForward, combinedMask, wallMask.value, out solution);
        }

        private static void AddTargetDirections(TankController shooter, IReadOnlyList<TankController> targets, Vector2 origin, Vector2 currentForward, int combinedMask, int wallMask)
        {
            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target == null || !target.Alive || target == shooter)
                    continue;

                TestDirectionToPoint(shooter, targets, origin, currentForward, target.transform.position, combinedMask, wallMask);

                var right = (Vector2)target.transform.right * (GameConfig.TankBodyWidth * 0.38f / CoordinateUtil.PixelsPerUnit);
                var up = (Vector2)target.transform.up * (GameConfig.TankBodyHeight * 0.38f / CoordinateUtil.PixelsPerUnit);
                var center = (Vector2)target.transform.position;
                TestDirectionToPoint(shooter, targets, origin, currentForward, center + right, combinedMask, wallMask);
                TestDirectionToPoint(shooter, targets, origin, currentForward, center - right, combinedMask, wallMask);
                TestDirectionToPoint(shooter, targets, origin, currentForward, center + up, combinedMask, wallMask);
                TestDirectionToPoint(shooter, targets, origin, currentForward, center - up, combinedMask, wallMask);
                TestDirectionToPoint(shooter, targets, origin, currentForward, center + right + up, combinedMask, wallMask);
                TestDirectionToPoint(shooter, targets, origin, currentForward, center - right + up, combinedMask, wallMask);
                TestDirectionToPoint(shooter, targets, origin, currentForward, center + right - up, combinedMask, wallMask);
                TestDirectionToPoint(shooter, targets, origin, currentForward, center - right - up, combinedMask, wallMask);
            }
        }

        private static void TestDirectionToPoint(TankController shooter, IReadOnlyList<TankController> targets, Vector2 origin, Vector2 currentForward, Vector2 point, int combinedMask, int wallMask)
        {
            var toPoint = point - origin;
            if (toPoint.sqrMagnitude < 0.0001f)
                return;

            TestFineFan(shooter, targets, origin, currentForward, toPoint.normalized, combinedMask, wallMask);
        }

        private static void AddCoarseSweep(TankController shooter, IReadOnlyList<TankController> targets, Vector2 origin, Vector2 currentForward, int combinedMask, int wallMask)
        {
            for (var i = 0; i < CoarseAngleCount; i++)
            {
                var angle = i * (360f / CoarseAngleCount);
                var direction = Quaternion.Euler(0f, 0f, angle) * Vector2.up;
                if (TrySimulate(shooter, targets, origin, direction, currentForward, combinedMask, wallMask, out var solution))
                {
                    Solutions.Add(solution);
                    TestFineFan(shooter, targets, origin, currentForward, direction, combinedMask, wallMask);
                }
            }
        }

        private static void TestFineFan(TankController shooter, IReadOnlyList<TankController> targets, Vector2 origin, Vector2 currentForward, Vector2 centerDirection, int combinedMask, int wallMask)
        {
            for (var i = -FineStepsPerSide; i <= FineStepsPerSide; i++)
            {
                var direction = Quaternion.Euler(0f, 0f, i * FineAngleStep) * centerDirection;
                if (TrySimulate(shooter, targets, origin, direction.normalized, currentForward, combinedMask, wallMask, out var solution))
                    Solutions.Add(solution);
            }
        }

        private static bool TrySimulate(TankController shooter, IReadOnlyList<TankController> targets, Vector2 origin, Vector2 initialDirection, Vector2 currentForward, int combinedMask, int wallMask, out HitSolution solution)
        {
            solution = default;
            var position = origin;
            var direction = initialDirection.sqrMagnitude > 0f ? initialDirection.normalized : Vector2.up;
            var radius = GameConfig.BulletRadius / CoordinateUtil.PixelsPerUnit;
            var maxDistance = Mathf.Sqrt(GameConfig.ScreenWidth * GameConfig.ScreenWidth + GameConfig.ScreenHeight * GameConfig.ScreenHeight) / CoordinateUtil.PixelsPerUnit * 1.2f;
            var pathLength = 0f;

            for (var bounce = 0; bounce <= MaxAimBounces; bounce++)
            {
                var hit = Physics2D.CircleCast(position, radius, direction, maxDistance, combinedMask);
                if (hit.collider == null)
                    return false;

                pathLength += hit.distance;
                var tank = hit.collider.GetComponentInParent<TankController>();
                if (tank != null)
                {
                    if (tank == shooter)
                        return false;
                    if (ContainsTarget(targets, tank))
                    {
                        var angleError = Mathf.Abs(Vector2.SignedAngle(currentForward, initialDirection));
                        var selfRisk = EstimateSelfRisk(shooter, origin, initialDirection, wallMask);
                        if (selfRisk >= RejectSelfRisk)
                            return false;

                        solution = new HitSolution(initialDirection.normalized, tank, bounce, pathLength, angleError, selfRisk);
                        return true;
                    }

                    return false;
                }

                direction = Vector2.Reflect(direction, hit.normal).normalized;
                position = hit.centroid + direction * (radius + 0.001f);
            }

            return false;
        }

        private static bool ContainsTarget(IReadOnlyList<TankController> targets, TankController candidate)
        {
            for (var i = 0; i < targets.Count; i++)
            {
                if (targets[i] == candidate)
                    return true;
            }

            return false;
        }

        private static int CompareSolutions(HitSolution a, HitSolution b)
        {
            var scoreCompare = ScoreSolution(a).CompareTo(ScoreSolution(b));
            if (scoreCompare != 0)
                return scoreCompare;

            var bounceCompare = a.Bounces.CompareTo(b.Bounces);
            if (bounceCompare != 0)
                return bounceCompare;

            return a.AngleError.CompareTo(b.AngleError);
        }

        private static float ScoreSolution(HitSolution solution)
        {
            return solution.SelfRisk * 520f + solution.Bounces * 135f + solution.PathLength * 10f + solution.AngleError * 2.2f;
        }

        private static float EstimateSelfRisk(TankController shooter, Vector2 origin, Vector2 initialDirection, int wallMask)
        {
            var position = origin;
            var direction = initialDirection.sqrMagnitude > 0f ? initialDirection.normalized : Vector2.up;
            var selfCenter = (Vector2)shooter.transform.position;
            var radius = GameConfig.BulletRadius / CoordinateUtil.PixelsPerUnit;
            var unsafeRadius = Mathf.Max(GameConfig.TankBodyWidth, GameConfig.TankBodyHeight) * 0.72f / CoordinateUtil.PixelsPerUnit + radius;
            var ignoreDistance = (GameConfig.TankBodyHeight + GameConfig.BarrelLength) / CoordinateUtil.PixelsPerUnit;
            var maxDistance = Mathf.Sqrt(GameConfig.ScreenWidth * GameConfig.ScreenWidth + GameConfig.ScreenHeight * GameConfig.ScreenHeight) / CoordinateUtil.PixelsPerUnit * 1.2f;
            var travelled = 0f;
            var risk = 0f;

            for (var bounce = 0; bounce <= GameConfig.MaxBounces; bounce++)
            {
                var hit = Physics2D.CircleCast(position, radius, direction, maxDistance, wallMask);
                var end = hit.collider != null ? hit.centroid : position + direction * maxDistance;
                var segmentLength = Vector2.Distance(position, end);

                if (travelled + segmentLength > ignoreDistance)
                {
                    var start = position;
                    if (travelled < ignoreDistance && segmentLength > 0.0001f)
                    {
                        var trim = (ignoreDistance - travelled) / segmentLength;
                        start = Vector2.Lerp(position, end, Mathf.Clamp01(trim));
                    }

                    var distance = DistancePointToSegment(selfCenter, start, end);
                    if (distance < unsafeRadius)
                    {
                        var proximity = 1f - Mathf.Clamp01(distance / unsafeRadius);
                        var bounceWeight = bounce == 0 ? 0.55f : 1f;
                        risk = Mathf.Max(risk, proximity * bounceWeight);
                    }
                }

                if (hit.collider == null || bounce >= GameConfig.MaxBounces)
                    break;

                travelled += hit.distance;
                direction = Vector2.Reflect(direction, hit.normal).normalized;
                position = hit.centroid + direction * (radius + 0.001f);
            }

            return risk;
        }

        private static float DistancePointToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            var segment = end - start;
            var lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= 0.000001f)
                return Vector2.Distance(point, start);

            var t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
            return Vector2.Distance(point, start + segment * t);
        }
    }
}
