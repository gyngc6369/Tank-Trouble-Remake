using System.Collections.Generic;
using UnityEngine;
using TankTrouble.Config;
using TankTrouble.Core;
using TankTrouble.Entities;
using TankTrouble.Map;

namespace TankTrouble.AI
{
    public static class AIMotionPlanner
    {
        private const float SimulationStep = 0.1f;
        private const float GoodClearance = 0.1f;

        private static readonly TankInputCommand Forward = new TankInputCommand(1f, 0f, false);
        private static readonly TankInputCommand Reverse = new TankInputCommand(-0.85f, 0f, false);
        private static readonly TankInputCommand ForwardLeft = new TankInputCommand(0.85f, 1f, false);
        private static readonly TankInputCommand ForwardRight = new TankInputCommand(0.85f, -1f, false);
        private static readonly TankInputCommand ReverseLeft = new TankInputCommand(-0.75f, 1f, false);
        private static readonly TankInputCommand ReverseRight = new TankInputCommand(-0.75f, -1f, false);
        private static readonly TankInputCommand PivotLeft = new TankInputCommand(0f, 1f, false);
        private static readonly TankInputCommand PivotRight = new TankInputCommand(0f, -1f, false);

        public static AIMotionPlan BuildPlan(
            TankController tank,
            AIMotionIntent intent,
            Vector2 desiredDirection,
            GridMap grid,
            HashSet<Vector2Int> dangerCells,
            DangerField dangerField,
            IReadOnlyList<TankController> enemies)
        {
            if (tank == null)
                return AIMotionPlan.Invalid;

            var desired = desiredDirection.sqrMagnitude > 0.0001f ? desiredDirection.normalized : (Vector2)tank.transform.up;
            var best = AIMotionPlan.Invalid;

            EvaluateCandidate(tank, intent, desired, grid, dangerCells, dangerField, enemies, Forward, 0.62f, Forward, 0.18f, ref best);
            EvaluateCandidate(tank, intent, desired, grid, dangerCells, dangerField, enemies, ForwardLeft, 0.55f, Forward, 0.2f, ref best);
            EvaluateCandidate(tank, intent, desired, grid, dangerCells, dangerField, enemies, ForwardRight, 0.55f, Forward, 0.2f, ref best);
            EvaluateCandidate(tank, intent, desired, grid, dangerCells, dangerField, enemies, PivotLeft, 0.22f, Forward, 0.48f, ref best);
            EvaluateCandidate(tank, intent, desired, grid, dangerCells, dangerField, enemies, PivotRight, 0.22f, Forward, 0.48f, ref best);

            if (intent != AIMotionIntent.Pressure)
            {
                EvaluateCandidate(tank, intent, desired, grid, dangerCells, dangerField, enemies, Reverse, 0.42f, Reverse, 0.16f, ref best);
                EvaluateCandidate(tank, intent, desired, grid, dangerCells, dangerField, enemies, ReverseLeft, 0.42f, Reverse, 0.14f, ref best);
                EvaluateCandidate(tank, intent, desired, grid, dangerCells, dangerField, enemies, ReverseRight, 0.42f, Reverse, 0.14f, ref best);
                EvaluateCandidate(tank, intent, desired, grid, dangerCells, dangerField, enemies, Reverse, 0.26f, ForwardLeft, 0.42f, ref best);
                EvaluateCandidate(tank, intent, desired, grid, dangerCells, dangerField, enemies, Reverse, 0.26f, ForwardRight, 0.42f, ref best);
            }

            return best;
        }

        public static float EstimatePlanRisk(TankController tank, AIMotionPlan plan, DangerField dangerField)
        {
            if (tank == null || dangerField == null || !dangerField.HasDanger || !plan.Valid || plan.Expired)
                return 0f;

            var position = (Vector2)tank.transform.position;
            var rotation = tank.transform.eulerAngles.z;
            var planElapsed = plan.Elapsed;
            var queryTime = 0f;
            var maxRisk = 0f;

            while (planElapsed < plan.TotalDuration - 0.0001f)
            {
                var command = planElapsed < plan.FirstDuration ? plan.FirstCommand : plan.SecondCommand;
                var remainingInStep = planElapsed < plan.FirstDuration ? plan.FirstDuration - planElapsed : plan.TotalDuration - planElapsed;
                var dt = Mathf.Min(SimulationStep, remainingInStep);
                var prediction = tank.PredictCommandFrom(position, rotation, command, dt);

                queryTime += dt;
                position = prediction.Position;
                rotation = prediction.Rotation;
                maxRisk = Mathf.Max(maxRisk, dangerField.GetRisk(position, queryTime));
                planElapsed += dt;
            }

            return maxRisk;
        }

        private static void EvaluateCandidate(
            TankController tank,
            AIMotionIntent intent,
            Vector2 desired,
            GridMap grid,
            HashSet<Vector2Int> dangerCells,
            DangerField dangerField,
            IReadOnlyList<TankController> enemies,
            TankInputCommand first,
            float firstDuration,
            TankInputCommand second,
            float secondDuration,
            ref AIMotionPlan best)
        {
            var score = SimulateAndScore(tank, intent, desired, grid, dangerCells, dangerField, enemies, first, firstDuration, second, secondDuration);
            if (best.Valid && score <= best.Score)
                return;

            best = AIMotionPlan.Create(first, firstDuration, second, secondDuration, score, intent);
        }

        private static float SimulateAndScore(
            TankController tank,
            AIMotionIntent intent,
            Vector2 desired,
            GridMap grid,
            HashSet<Vector2Int> dangerCells,
            DangerField dangerField,
            IReadOnlyList<TankController> enemies,
            TankInputCommand first,
            float firstDuration,
            TankInputCommand second,
            float secondDuration)
        {
            var start = (Vector2)tank.transform.position;
            var position = start;
            var rotation = tank.transform.eulerAngles.z;
            var score = 0f;
            var blockedSteps = 0;
            var totalMoved = 0f;
            var minClearance = float.MaxValue;
            var elapsed = 0f;
            var totalDuration = firstDuration + secondDuration;
            var maxTrajectoryRisk = 0f;
            var totalTrajectoryRisk = 0f;
            var riskSamples = 0;

            while (elapsed < totalDuration - 0.0001f)
            {
                var command = elapsed < firstDuration ? first : second;
                var remainingInStep = elapsed < firstDuration ? firstDuration - elapsed : totalDuration - elapsed;
                var dt = Mathf.Min(SimulationStep, remainingInStep);
                var prediction = tank.PredictCommandFrom(position, rotation, command, dt);
                var moved = Vector2.Distance(position, prediction.Position);
                totalMoved += moved;
                position = prediction.Position;
                rotation = prediction.Rotation;
                minClearance = Mathf.Min(minClearance, prediction.WallClearance);

                if (prediction.RotationBlocked || prediction.MoveBlocked || (prediction.RequestedDistance > 0f && prediction.AllowedDistance <= prediction.RequestedDistance * 0.25f))
                    blockedSteps++;

                var queryTime = elapsed + dt;
                var trajectoryRisk = dangerField != null && dangerField.HasDanger ? dangerField.GetRisk(position, queryTime) : 0f;
                maxTrajectoryRisk = Mathf.Max(maxTrajectoryRisk, trajectoryRisk);
                totalTrajectoryRisk += trajectoryRisk;
                riskSamples++;

                score += ScorePosition(intent, desired, grid, dangerCells, enemies, start, position, prediction.Forward, prediction.WallClearance, moved, trajectoryRisk);
                elapsed += dt;
            }

            var netDisplacement = position - start;
            score += Vector2.Dot(netDisplacement, desired) * (intent == AIMotionIntent.Evade ? 950f : 620f);
            score += totalMoved * (intent == AIMotionIntent.Evade ? 420f : 180f);
            score -= blockedSteps * (intent == AIMotionIntent.Evade ? 1250f : 860f);

            if (minClearance < GoodClearance)
                score -= (GoodClearance - minClearance) / GoodClearance * (intent == AIMotionIntent.Evade ? 900f : 650f);

            if (intent == AIMotionIntent.Pressure && first.Move < -0.1f)
                score -= 800f;

            if (intent == AIMotionIntent.Evade && totalMoved < 0.08f)
                score -= 900f;

            var averageTrajectoryRisk = riskSamples > 0 ? totalTrajectoryRisk / riskSamples : 0f;
            score -= maxTrajectoryRisk * RiskPenalty(intent, peak: true);
            score -= averageTrajectoryRisk * RiskPenalty(intent, peak: false);

            return score;
        }

        private static float ScorePosition(
            AIMotionIntent intent,
            Vector2 desired,
            GridMap grid,
            HashSet<Vector2Int> dangerCells,
            IReadOnlyList<TankController> enemies,
            Vector2 start,
            Vector2 position,
            Vector2 forward,
            float clearance,
            float moved,
            float trajectoryRisk)
        {
            var score = 0f;
            score += Vector2.Dot(position - start, desired) * (intent == AIMotionIntent.Evade ? 260f : 130f);
            score += ScoreDanger(grid, dangerCells, position, intent);
            score -= trajectoryRisk * RiskPenalty(intent, peak: false) * 0.22f;
            score += ScoreCenter(grid, position, intent);
            score += Mathf.Clamp01(clearance / GoodClearance) * 35f;

            var enemy = FindClosestEnemy(position, enemies, out var distance);
            if (enemy != null)
            {
                var enemyDirection = ((Vector2)enemy.transform.position - position).normalized;
                var angle = Mathf.Abs(Vector2.SignedAngle(forward, enemyDirection));
                score -= angle * (distance < 1.7f ? 0.8f : 0.18f);

                if (intent == AIMotionIntent.Pressure)
                    score -= Mathf.Abs(distance - 2.8f) * 32f;
                if (intent == AIMotionIntent.Reposition)
                    score += Mathf.Clamp(distance, 0f, 4f) * 18f;
            }

            if (moved <= 0.002f)
                score -= 60f;

            return score;
        }

        private static float RiskPenalty(AIMotionIntent intent, bool peak)
        {
            switch (intent)
            {
                case AIMotionIntent.Evade:
                    return peak ? 2800f : 1300f;
                case AIMotionIntent.Reposition:
                    return peak ? 2100f : 950f;
                default:
                    return peak ? 1700f : 720f;
            }
        }

        private static float ScoreDanger(GridMap grid, HashSet<Vector2Int> dangerCells, Vector2 worldPosition, AIMotionIntent intent)
        {
            if (grid == null || dangerCells == null || dangerCells.Count == 0)
                return 0f;

            var cell = CoordinateUtil.PixelToCell(CoordinateUtil.WorldToPixel(worldPosition));
            if (!grid.IsInside(cell.x, cell.y))
                return -700f;

            if (dangerCells.Contains(cell))
                return intent == AIMotionIntent.Evade ? -1500f : -850f;

            return 0f;
        }

        private static float ScoreCenter(GridMap grid, Vector2 worldPosition, AIMotionIntent intent)
        {
            if (grid == null)
                return 0f;

            var cell = CoordinateUtil.PixelToCell(CoordinateUtil.WorldToPixel(worldPosition));
            if (!grid.IsInside(cell.x, cell.y))
                return -500f;

            var center = (Vector2)CoordinateUtil.CellToWorld(cell.x, cell.y);
            var distance = Vector2.Distance(worldPosition, center);
            var tolerance = GameConfig.CellSize * 0.16f / CoordinateUtil.PixelsPerUnit;
            var excess = Mathf.Max(0f, distance - tolerance);
            return -excess * (intent == AIMotionIntent.Evade ? 80f : 230f);
        }

        private static TankController FindClosestEnemy(Vector2 position, IReadOnlyList<TankController> enemies, out float distance)
        {
            distance = float.MaxValue;
            TankController best = null;
            if (enemies == null)
                return null;

            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.Alive)
                    continue;

                var current = Vector2.Distance(position, enemy.transform.position);
                if (current >= distance)
                    continue;

                distance = current;
                best = enemy;
            }

            return best;
        }
    }
}
