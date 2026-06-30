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
        private const int CoarseAngleCount = 96;
        private const int VirtualCoarseAngleCount = 32;
        private const float FineAngleStep = 0.5f;
        private const int FineStepsPerSide = 3;
        private const float RejectSelfRisk = 0.86f;

        private static readonly List<HitSolution> Solutions = new List<HitSolution>(16);
        private static readonly RaycastHit2D[] CastHits = new RaycastHit2D[16];

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
            if (TrySelectBestSolution(out best))
                return true;

            AddCoarseSweep(shooter, targets, origin, currentForward, combinedMask, wallMaskValue);

            return TrySelectBestSolution(out best);
        }

        public static bool TryFindBestShotFromCenter(
            TankController shooter,
            Vector2 shooterCenter,
            Vector2 preferredForward,
            IReadOnlyList<TankController> targets,
            LayerMask wallMask,
            LayerMask tankMask,
            out HitSolution best)
        {
            Solutions.Clear();
            best = default;
            if (shooter == null || !shooter.Alive || targets == null)
                return false;

            var forward = preferredForward.sqrMagnitude > 0.0001f ? preferredForward.normalized : Vector2.up;
            var wallMaskValue = wallMask.value;
            var combinedMask = wallMask.value | tankMask.value;

            AddVirtualTargetDirections(shooter, targets, shooterCenter, forward, combinedMask, wallMaskValue);
            if (TrySelectBestSolution(out best))
                return true;

            AddVirtualCoarseSweep(shooter, targets, shooterCenter, forward, combinedMask, wallMaskValue);

            return TrySelectBestSolution(out best);
        }

        public static bool TryValidateShot(TankController shooter, IReadOnlyList<TankController> targets, Vector2 direction, LayerMask wallMask, LayerMask tankMask, out HitSolution solution)
        {
            solution = default;
            if (shooter == null || !shooter.Alive || targets == null || direction.sqrMagnitude < 0.0001f)
                return false;

            var combinedMask = wallMask.value | tankMask.value;
            return TrySimulate(shooter, targets, shooter.BarrelTipWorld, direction.normalized, shooter.VelocityForward, combinedMask, wallMask.value, shooter.transform.position, false, out solution);
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

                // Predictive aim: add a lead position based on enemy's movement direction
                var enemySpeedUnits = GameConfig.TankSpeed / CoordinateUtil.PixelsPerUnit;
                var bulletSpeedUnits = GameConfig.BulletSpeed / CoordinateUtil.PixelsPerUnit;
                var distanceToEnemy = Vector2.Distance(origin, center);
                var travelTime = distanceToEnemy / bulletSpeedUnits;
                var leadPosition = center + target.VelocityForward * (enemySpeedUnits * travelTime);
                if (Vector2.Distance(leadPosition, center) > 0.1f)
                    TestDirectionToPoint(shooter, targets, origin, currentForward, leadPosition, combinedMask, wallMask);
            }
        }

        private static void TestDirectionToPoint(TankController shooter, IReadOnlyList<TankController> targets, Vector2 origin, Vector2 currentForward, Vector2 point, int combinedMask, int wallMask)
        {
            var toPoint = point - origin;
            if (toPoint.sqrMagnitude < 0.0001f)
                return;

            TestFineFan(shooter, targets, origin, currentForward, toPoint.normalized, combinedMask, wallMask);
        }

        private static void AddVirtualTargetDirections(TankController shooter, IReadOnlyList<TankController> targets, Vector2 center, Vector2 preferredForward, int combinedMask, int wallMask)
        {
            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target == null || !target.Alive || target == shooter)
                    continue;

                TestVirtualDirectionToPoint(shooter, targets, center, preferredForward, target.transform.position, combinedMask, wallMask);

                var right = (Vector2)target.transform.right * (GameConfig.TankBodyWidth * 0.38f / CoordinateUtil.PixelsPerUnit);
                var up = (Vector2)target.transform.up * (GameConfig.TankBodyHeight * 0.38f / CoordinateUtil.PixelsPerUnit);
                var targetCenter = (Vector2)target.transform.position;
                TestVirtualDirectionToPoint(shooter, targets, center, preferredForward, targetCenter + right, combinedMask, wallMask);
                TestVirtualDirectionToPoint(shooter, targets, center, preferredForward, targetCenter - right, combinedMask, wallMask);
                TestVirtualDirectionToPoint(shooter, targets, center, preferredForward, targetCenter + up, combinedMask, wallMask);
                TestVirtualDirectionToPoint(shooter, targets, center, preferredForward, targetCenter - up, combinedMask, wallMask);
                TestVirtualDirectionToPoint(shooter, targets, center, preferredForward, targetCenter + right + up, combinedMask, wallMask);
                TestVirtualDirectionToPoint(shooter, targets, center, preferredForward, targetCenter - right + up, combinedMask, wallMask);
                TestVirtualDirectionToPoint(shooter, targets, center, preferredForward, targetCenter + right - up, combinedMask, wallMask);
                TestVirtualDirectionToPoint(shooter, targets, center, preferredForward, targetCenter - right - up, combinedMask, wallMask);
            }
        }

        private static void TestVirtualDirectionToPoint(TankController shooter, IReadOnlyList<TankController> targets, Vector2 center, Vector2 preferredForward, Vector2 point, int combinedMask, int wallMask)
        {
            var toPoint = point - center;
            if (toPoint.sqrMagnitude < 0.0001f)
                return;

            TestVirtualFineFan(shooter, targets, center, preferredForward, toPoint.normalized, combinedMask, wallMask);
        }

        private static void AddCoarseSweep(TankController shooter, IReadOnlyList<TankController> targets, Vector2 origin, Vector2 currentForward, int combinedMask, int wallMask)
        {
            for (var i = 0; i < CoarseAngleCount; i++)
            {
                var angle = i * (360f / CoarseAngleCount);
                var direction = Quaternion.Euler(0f, 0f, angle) * Vector2.up;
                if (TrySimulate(shooter, targets, origin, direction, currentForward, combinedMask, wallMask, shooter.transform.position, false, out var solution))
                {
                    Solutions.Add(solution);
                    TestFineFan(shooter, targets, origin, currentForward, direction, combinedMask, wallMask);
                }
            }
        }

        private static void AddVirtualCoarseSweep(TankController shooter, IReadOnlyList<TankController> targets, Vector2 center, Vector2 preferredForward, int combinedMask, int wallMask)
        {
            for (var i = 0; i < VirtualCoarseAngleCount; i++)
            {
                var angle = i * (360f / VirtualCoarseAngleCount);
                var direction = Quaternion.Euler(0f, 0f, angle) * Vector2.up;
                TestVirtualDirection(shooter, targets, center, preferredForward, direction, combinedMask, wallMask);
            }
        }

        private static void TestFineFan(TankController shooter, IReadOnlyList<TankController> targets, Vector2 origin, Vector2 currentForward, Vector2 centerDirection, int combinedMask, int wallMask)
        {
            for (var i = -FineStepsPerSide; i <= FineStepsPerSide; i++)
            {
                var direction = Quaternion.Euler(0f, 0f, i * FineAngleStep) * centerDirection;
                if (TrySimulate(shooter, targets, origin, direction.normalized, currentForward, combinedMask, wallMask, shooter.transform.position, false, out var solution))
                    Solutions.Add(solution);
            }
        }

        private static void TestVirtualFineFan(TankController shooter, IReadOnlyList<TankController> targets, Vector2 center, Vector2 preferredForward, Vector2 centerDirection, int combinedMask, int wallMask)
        {
            for (var i = -FineStepsPerSide; i <= FineStepsPerSide; i++)
            {
                var direction = Quaternion.Euler(0f, 0f, i * FineAngleStep) * centerDirection;
                TestVirtualDirection(shooter, targets, center, preferredForward, direction.normalized, combinedMask, wallMask);
            }
        }

        private static void TestVirtualDirection(TankController shooter, IReadOnlyList<TankController> targets, Vector2 center, Vector2 preferredForward, Vector2 direction, int combinedMask, int wallMask)
        {
            if (direction.sqrMagnitude < 0.0001f)
                return;

            var normalized = direction.normalized;
            var origin = center + normalized * GetBarrelOffsetWorld();
            if (TrySimulate(shooter, targets, origin, normalized, preferredForward, combinedMask, wallMask, center, true, out var solution))
                Solutions.Add(solution);
        }

        private static bool TrySimulate(
            TankController shooter,
            IReadOnlyList<TankController> targets,
            Vector2 origin,
            Vector2 initialDirection,
            Vector2 currentForward,
            int combinedMask,
            int wallMask,
            Vector2 selfCenter,
            bool ignoreShooterCollider,
            out HitSolution solution)
        {
            solution = default;
            var position = origin;
            var direction = initialDirection.sqrMagnitude > 0f ? initialDirection.normalized : Vector2.up;
            var radius = GameConfig.BulletRadius / CoordinateUtil.PixelsPerUnit;
            var maxDistance = Mathf.Sqrt(GameConfig.ScreenWidth * GameConfig.ScreenWidth + GameConfig.ScreenHeight * GameConfig.ScreenHeight) / CoordinateUtil.PixelsPerUnit * 1.2f;
            var pathLength = 0f;

            for (var bounce = 0; bounce <= MaxAimBounces; bounce++)
            {
                var hit = CircleCast(position, radius, direction, maxDistance, combinedMask, shooter, ignoreShooterCollider);
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
                        var selfRisk = EstimateSelfRisk(shooter, selfCenter, origin, initialDirection, wallMask, ignoreShooterCollider);
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

        private static bool TrySelectBestSolution(out HitSolution best)
        {
            best = default;
            if (Solutions.Count == 0)
                return false;

            Solutions.Sort(CompareSolutions);
            best = Solutions[0];
            return true;
        }

        private static float ScoreSolution(HitSolution solution)
        {
            return solution.SelfRisk * 520f + solution.Bounces * 135f + solution.PathLength * 10f + solution.AngleError * 2.2f;
        }

        private static float EstimateSelfRisk(TankController shooter, Vector2 selfCenter, Vector2 origin, Vector2 initialDirection, int wallMask, bool ignoreShooterCollider)
        {
            var position = origin;
            var direction = initialDirection.sqrMagnitude > 0f ? initialDirection.normalized : Vector2.up;
            var radius = GameConfig.BulletRadius / CoordinateUtil.PixelsPerUnit;
            var unsafeRadius = Mathf.Max(GameConfig.TankBodyWidth, GameConfig.TankBodyHeight) * 0.72f / CoordinateUtil.PixelsPerUnit + radius;
            var ignoreDistance = (GameConfig.TankBodyHeight + GameConfig.BarrelLength) / CoordinateUtil.PixelsPerUnit;
            var maxDistance = Mathf.Sqrt(GameConfig.ScreenWidth * GameConfig.ScreenWidth + GameConfig.ScreenHeight * GameConfig.ScreenHeight) / CoordinateUtil.PixelsPerUnit * 1.2f;
            var travelled = 0f;
            var risk = 0f;

            for (var bounce = 0; bounce <= GameConfig.MaxBounces; bounce++)
            {
                var hit = CircleCast(position, radius, direction, maxDistance, wallMask, shooter, ignoreShooterCollider);
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

        private static RaycastHit2D CircleCast(Vector2 position, float radius, Vector2 direction, float maxDistance, int mask, TankController shooter, bool ignoreShooterCollider)
        {
            if (!ignoreShooterCollider)
                return Physics2D.CircleCast(position, radius, direction, maxDistance, mask);

            var hitCount = Physics2D.CircleCastNonAlloc(position, radius, direction, CastHits, maxDistance, mask);
            var best = default(RaycastHit2D);
            var bestDistance = float.PositiveInfinity;
            for (var i = 0; i < hitCount; i++)
            {
                var hit = CastHits[i];
                if (hit.collider == null || hit.distance >= bestDistance)
                    continue;

                var tank = hit.collider.GetComponentInParent<TankController>();
                if (tank == shooter)
                    continue;

                best = hit;
                bestDistance = hit.distance;
            }

            return best;
        }

        private static float GetBarrelOffsetWorld()
        {
            return (GameConfig.TankBodyHeight * 0.5f + GameConfig.BarrelLength) / CoordinateUtil.PixelsPerUnit;
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
