using System.Collections.Generic;
using UnityEngine;
using TankTrouble.Config;
using TankTrouble.Core;
using TankTrouble.Entities;
using TankTrouble.Map;

namespace TankTrouble.AI
{
    public static class AIMovementEvaluator
    {
        private const float LookAheadTime = 0.28f;
        private const float CloseEnemyDistance = 1.65f;
        private const float SwitchScoreMargin = 32f;
        private const float DesiredWallClearance = 0.11f;

        private static readonly TankInputCommand[] Candidates =
        {
            new TankInputCommand(1f, 0f, false),
            new TankInputCommand(-1f, 0f, false),
            new TankInputCommand(0.65f, 1f, false),
            new TankInputCommand(0.65f, -1f, false),
            new TankInputCommand(-0.75f, 1f, false),
            new TankInputCommand(-0.75f, -1f, false),
            new TankInputCommand(0f, 1f, false),
            new TankInputCommand(0f, -1f, false),
            TankInputCommand.None
        };

        public static TankInputCommand ChooseMovement(
            TankController tank,
            Vector2 desiredDirection,
            GridMap grid,
            HashSet<Vector2Int> dangerCells,
            IReadOnlyList<TankController> enemies,
            LayerMask wallMask,
            bool urgentEvade,
            bool preferReverse,
            bool stuckAssist,
            TankInputCommand previousCommand)
        {
            if (tank == null)
                return TankInputCommand.None;

            var desired = desiredDirection.sqrMagnitude > 0.0001f ? desiredDirection.normalized : Vector2.zero;
            var position = (Vector2)tank.transform.position;
            var currentForward = (Vector2)tank.transform.up;
            var closestEnemy = FindClosestEnemy(position, enemies, out var closestEnemyDistance);
            var bestScore = float.NegativeInfinity;
            var previousScore = float.NegativeInfinity;
            var best = TankInputCommand.None;

            for (var i = 0; i < Candidates.Length; i++)
            {
                var candidate = Candidates[i];
                var score = ScoreCandidate(
                    tank,
                    candidate,
                    position,
                    currentForward,
                    desired,
                    grid,
                    dangerCells,
                    closestEnemy,
                    closestEnemyDistance,
                    urgentEvade,
                    preferReverse,
                    stuckAssist);

                if (SameCommand(candidate, previousCommand))
                    previousScore = score;

                if (score <= bestScore)
                    continue;

                bestScore = score;
                best = candidate;
            }

            if (!urgentEvade
                && Mathf.Abs(previousCommand.Move) > 0.01f
                && previousScore > -250f
                && bestScore - previousScore < SwitchScoreMargin)
                return previousCommand;

            return best;
        }

        private static float ScoreCandidate(
            TankController tank,
            TankInputCommand command,
            Vector2 position,
            Vector2 currentForward,
            Vector2 desired,
            GridMap grid,
            HashSet<Vector2Int> dangerCells,
            TankController closestEnemy,
            float closestEnemyDistance,
            bool urgentEvade,
            bool preferReverse,
            bool stuckAssist)
        {
            var prediction = tank.PredictCommand(command, LookAheadTime);
            var predictedPosition = prediction.Position;
            var predictedForward = prediction.Forward;

            var score = 0f;
            if (prediction.RotationBlocked)
                score -= urgentEvade ? 1300f : 850f;
            if (prediction.MoveBlocked)
                score -= urgentEvade ? 1100f : 720f;
            if (prediction.RequestedDistance > 0f && prediction.AllowedDistance <= prediction.RequestedDistance * 0.25f)
                score -= urgentEvade ? 900f : 620f;
            if (urgentEvade && Mathf.Abs(command.Move) > 0.01f)
                score += prediction.AllowedDistance * 920f;

            var currentRisk = GetCellRisk(grid, dangerCells, position);
            var predictedRisk = GetCellRisk(grid, dangerCells, predictedPosition);
            score -= predictedRisk * (urgentEvade ? 950f : 420f);
            score += Mathf.Max(0, currentRisk - predictedRisk) * (urgentEvade ? 520f : 260f);
            score += ScoreWallClearance(prediction.WallClearance, urgentEvade);
            score += ScoreCorridorCenter(grid, predictedPosition, urgentEvade);
            score += ScoreCellComfort(grid, predictedPosition, urgentEvade);

            if (desired != Vector2.zero)
            {
                var displacement = predictedPosition - position;
                score += Vector2.Dot(displacement, desired) * (urgentEvade ? 300f : 260f);

                var headingAngle = Mathf.Abs(Vector2.SignedAngle(predictedForward, desired));
                if (Mathf.Abs(command.Move) < 0.01f)
                    score -= headingAngle * 0.16f;
                else
                    score -= headingAngle * 0.025f;
            }

            if (closestEnemy != null)
            {
                var enemyDirection = ((Vector2)closestEnemy.transform.position - predictedPosition).normalized;
                var angleToEnemy = Mathf.Abs(Vector2.SignedAngle(predictedForward, enemyDirection));
                var enemyWeight = closestEnemyDistance <= CloseEnemyDistance ? 1.8f : 0.35f;
                score -= angleToEnemy * enemyWeight;

                if (closestEnemyDistance <= CloseEnemyDistance && command.Move < -0.1f && angleToEnemy <= 85f)
                    score += 85f;
                if (closestEnemyDistance > CloseEnemyDistance && command.Move > 0.1f)
                    score += 90f;
            }

            if (preferReverse && command.Move < -0.1f)
                score += 70f;
            if (stuckAssist && command.Move < -0.1f)
                score += 230f;
            if (stuckAssist && command.Move > 0.1f)
                score -= 160f;

            if (Mathf.Abs(command.Move) < 0.01f && !urgentEvade)
                score -= desired == Vector2.zero ? 120f : 95f;
            if (Mathf.Abs(command.Move) < 0.01f && urgentEvade)
                score -= 500f;
            if (Mathf.Abs(command.Rotate) > 0.01f && Mathf.Abs(command.Move) < 0.01f && urgentEvade)
                score -= 260f;

            return score;
        }

        private static float ScoreWallClearance(float clearance, bool urgentEvade)
        {
            if (clearance <= 0f)
                return urgentEvade ? -1400f : -900f;

            var shortage = Mathf.Max(0f, DesiredWallClearance - clearance);
            if (shortage <= 0f)
                return 90f;

            var normalized = shortage / DesiredWallClearance;
            return -normalized * (urgentEvade ? 820f : 680f);
        }

        private static float ScoreCellComfort(GridMap grid, Vector2 worldPosition, bool urgentEvade)
        {
            if (grid == null)
                return 0f;

            var cell = CoordinateUtil.PixelToCell(CoordinateUtil.WorldToPixel(worldPosition));
            if (!grid.IsInside(cell.x, cell.y))
                return -700f;

            var comfortCost = AIPathfinding.GetCellComfortCost(grid, cell);
            return -comfortCost * (urgentEvade ? 35f : 85f);
        }

        private static float ScoreCorridorCenter(GridMap grid, Vector2 worldPosition, bool urgentEvade)
        {
            if (grid == null)
                return 0f;

            var cell = CoordinateUtil.PixelToCell(CoordinateUtil.WorldToPixel(worldPosition));
            if (!grid.IsInside(cell.x, cell.y))
                return -500f;

            var center = (Vector2)CoordinateUtil.CellToWorld(cell.x, cell.y);
            var distance = Vector2.Distance(worldPosition, center);
            var tolerance = GameConfig.CellSize * 0.18f / CoordinateUtil.PixelsPerUnit;
            var excess = Mathf.Max(0f, distance - tolerance);
            return -excess * (urgentEvade ? 120f : 340f);
        }

        private static bool SameCommand(TankInputCommand a, TankInputCommand b)
        {
            return Mathf.Approximately(a.Move, b.Move)
                && Mathf.Approximately(a.Rotate, b.Rotate)
                && a.FireHeld == b.FireHeld;
        }

        private static int GetCellRisk(GridMap grid, HashSet<Vector2Int> dangerCells, Vector2 worldPosition)
        {
            if (grid == null || dangerCells == null || dangerCells.Count == 0)
                return 0;

            var cell = CoordinateUtil.PixelToCell(CoordinateUtil.WorldToPixel(worldPosition));
            if (!grid.IsInside(cell.x, cell.y))
                return 3;

            if (dangerCells.Contains(cell))
                return 3;

            var risk = 0;
            if (dangerCells.Contains(new Vector2Int(cell.x + 1, cell.y)))
                risk = 1;
            if (dangerCells.Contains(new Vector2Int(cell.x - 1, cell.y)))
                risk = 1;
            if (dangerCells.Contains(new Vector2Int(cell.x, cell.y + 1)))
                risk = 1;
            if (dangerCells.Contains(new Vector2Int(cell.x, cell.y - 1)))
                risk = 1;

            return risk;
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

                var current = Vector2.Distance(position, (Vector2)enemy.transform.position);
                if (current >= distance)
                    continue;

                distance = current;
                best = enemy;
            }

            return best;
        }
    }
}
