using System.Collections.Generic;
using UnityEngine;
using TankTrouble.Config;
using TankTrouble.Core;
using TankTrouble.Entities;
using TankTrouble.Map;

namespace TankTrouble.AI
{
    public static class AIEvadeController
    {
        private const float SimulationStep = 0.05f;
        private const float GoodClearance = 0.1f;
        private const float Horizon = 0.85f;

        private static readonly TankInputCommand Forward = new TankInputCommand(1f, 0f, false);
        private static readonly TankInputCommand ForwardLeft = new TankInputCommand(0.85f, 1f, false);
        private static readonly TankInputCommand ForwardRight = new TankInputCommand(0.85f, -1f, false);
        private static readonly TankInputCommand Reverse = new TankInputCommand(-0.85f, 0f, false);
        private static readonly TankInputCommand ReverseLeft = new TankInputCommand(-0.75f, 1f, false);
        private static readonly TankInputCommand ReverseRight = new TankInputCommand(-0.75f, -1f, false);
        private static readonly TankInputCommand PivotLeft = new TankInputCommand(0f, 1f, false);
        private static readonly TankInputCommand PivotRight = new TankInputCommand(0f, -1f, false);

        public static TankInputCommand BuildCommand(TankController tank, DangerField dangerField, GridMap grid, HashSet<Vector2Int> dangerCells)
        {
            if (tank == null || dangerField == null || !dangerField.HasDanger)
                return TankInputCommand.None;

            var escapeDirection = dangerField.GetEscapeDirection(tank.transform.position);
            if (escapeDirection.sqrMagnitude < 0.0001f)
                escapeDirection = -tank.VelocityForward;
            escapeDirection.Normalize();

            var bestScore = float.NegativeInfinity;
            var bestCommand = TankInputCommand.None;

            Evaluate(tank, dangerField, grid, dangerCells, escapeDirection, Forward, 0.42f, Forward, 0.32f, ref bestScore, ref bestCommand);
            Evaluate(tank, dangerField, grid, dangerCells, escapeDirection, ForwardLeft, 0.42f, Forward, 0.32f, ref bestScore, ref bestCommand);
            Evaluate(tank, dangerField, grid, dangerCells, escapeDirection, ForwardRight, 0.42f, Forward, 0.32f, ref bestScore, ref bestCommand);
            Evaluate(tank, dangerField, grid, dangerCells, escapeDirection, Reverse, 0.36f, Reverse, 0.28f, ref bestScore, ref bestCommand);
            Evaluate(tank, dangerField, grid, dangerCells, escapeDirection, ReverseLeft, 0.36f, Reverse, 0.28f, ref bestScore, ref bestCommand);
            Evaluate(tank, dangerField, grid, dangerCells, escapeDirection, ReverseRight, 0.36f, Reverse, 0.28f, ref bestScore, ref bestCommand);
            Evaluate(tank, dangerField, grid, dangerCells, escapeDirection, PivotLeft, 0.14f, ForwardLeft, 0.48f, ref bestScore, ref bestCommand);
            Evaluate(tank, dangerField, grid, dangerCells, escapeDirection, PivotRight, 0.14f, ForwardRight, 0.48f, ref bestScore, ref bestCommand);
            Evaluate(tank, dangerField, grid, dangerCells, escapeDirection, Reverse, 0.16f, ForwardLeft, 0.44f, ref bestScore, ref bestCommand);
            Evaluate(tank, dangerField, grid, dangerCells, escapeDirection, Reverse, 0.16f, ForwardRight, 0.44f, ref bestScore, ref bestCommand);

            return bestCommand;
        }

        private static void Evaluate(
            TankController tank,
            DangerField dangerField,
            GridMap grid,
            HashSet<Vector2Int> dangerCells,
            Vector2 escapeDirection,
            TankInputCommand first,
            float firstDuration,
            TankInputCommand second,
            float secondDuration,
            ref float bestScore,
            ref TankInputCommand bestCommand)
        {
            var score = Simulate(tank, dangerField, grid, dangerCells, escapeDirection, first, firstDuration, second, secondDuration);
            if (score <= bestScore)
                return;

            bestScore = score;
            bestCommand = first;
        }

        private static float Simulate(
            TankController tank,
            DangerField dangerField,
            GridMap grid,
            HashSet<Vector2Int> dangerCells,
            Vector2 escapeDirection,
            TankInputCommand first,
            float firstDuration,
            TankInputCommand second,
            float secondDuration)
        {
            var start = (Vector2)tank.transform.position;
            var startRisk = dangerField.GetRisk(start, 0f);
            var position = start;
            var rotation = tank.transform.eulerAngles.z;
            var elapsed = 0f;
            var totalDuration = Mathf.Min(Horizon, firstDuration + secondDuration);
            var maxRisk = 0f;
            var totalRisk = 0f;
            var earlyRisk = 0f;
            var samples = 0;
            var blockedSteps = 0;
            var totalMoved = 0f;
            var totalRequested = 0f;
            var minClearance = float.MaxValue;

            while (elapsed < totalDuration - 0.0001f)
            {
                var command = elapsed < firstDuration ? first : second;
                var remaining = elapsed < firstDuration ? firstDuration - elapsed : totalDuration - elapsed;
                var dt = Mathf.Min(SimulationStep, remaining);
                var prediction = tank.PredictCommandFrom(position, rotation, command, dt);
                var moved = Vector2.Distance(position, prediction.Position);

                elapsed += dt;
                position = prediction.Position;
                rotation = prediction.Rotation;
                totalMoved += moved;
                totalRequested += prediction.RequestedDistance;
                minClearance = Mathf.Min(minClearance, prediction.WallClearance);

                if (prediction.RotationBlocked || prediction.MoveBlocked || (prediction.RequestedDistance > 0f && prediction.AllowedDistance <= prediction.RequestedDistance * 0.25f))
                    blockedSteps++;

                var risk = dangerField.GetRisk(position, elapsed);
                maxRisk = Mathf.Max(maxRisk, risk);
                totalRisk += risk;
                if (elapsed <= 0.25f)
                    earlyRisk = Mathf.Max(earlyRisk, risk);
                samples++;
            }

            var averageRisk = samples > 0 ? totalRisk / samples : 0f;
            var endRisk = dangerField.GetRisk(position, totalDuration);
            var displacement = position - start;
            var escapeProgress = Vector2.Dot(displacement, escapeDirection);
            var score = 0f;
            score -= maxRisk * 3800f;
            score -= averageRisk * 2000f;
            score -= earlyRisk * 1700f;
            score -= endRisk * 1500f;
            score += Mathf.Max(0f, startRisk - endRisk) * 1800f;
            score += escapeProgress * 420f;
            score += totalMoved * 260f;
            score -= blockedSteps * 1000f;

            if (totalRequested > 0.0001f)
            {
                var efficiency = totalMoved / totalRequested;
                if (efficiency < 0.42f)
                    score -= (0.42f - efficiency) * 2400f;
            }

            if (minClearance < GoodClearance)
                score -= (GoodClearance - minClearance) / GoodClearance * 650f;
            if (first.Move == 0f)
                score -= 180f;
            if (totalMoved < 0.06f)
                score -= 900f;

            score += ScoreGrid(grid, dangerCells, position);
            return score;
        }

        private static float ScoreGrid(GridMap grid, HashSet<Vector2Int> dangerCells, Vector2 worldPosition)
        {
            if (grid == null)
                return 0f;

            var cell = CoordinateUtil.PixelToCell(CoordinateUtil.WorldToPixel(worldPosition));
            if (!grid.IsInside(cell.x, cell.y))
                return -1200f;
            if (dangerCells != null && dangerCells.Contains(cell))
                return -650f;

            return 0f;
        }
    }
}
