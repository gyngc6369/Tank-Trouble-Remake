using System.Collections.Generic;
using UnityEngine;
using TankTrouble.Config;
using TankTrouble.Core;
using TankTrouble.Entities;
using TankTrouble.Map;

namespace TankTrouble.AI
{
    public static class AIGoalPlanner
    {
        private const int MinPursueRange = 2;
        private const int MaxPursueRange = 5;
        private const int PreferredPursueRange = 3;
        private const int MaxEvadePathLength = 6;

        private static readonly List<Vector2Int> ScratchPath = new List<Vector2Int>(64);
        private static readonly List<Vector2Int> ScratchNeighbors = new List<Vector2Int>(4);

        public static bool TryFindPursueGoal(GridMap grid, Vector2Int currentCell, IReadOnlyList<TankController> enemies, HashSet<Vector2Int> dangerCells, out Vector2Int goal)
        {
            goal = currentCell;
            if (grid == null || enemies == null || enemies.Count == 0 || !grid.IsInside(currentCell.x, currentCell.y))
                return false;

            var enemyCell = FindNearestEnemyCell(currentCell, enemies);
            var bestScore = float.PositiveInfinity;
            var found = false;
            var currentDistanceToEnemy = Manhattan(currentCell, enemyCell);

            for (var row = 0; row < grid.Rows; row++)
            {
                for (var col = 0; col < grid.Cols; col++)
                {
                    var candidate = new Vector2Int(col, row);
                    if (dangerCells != null && dangerCells.Contains(candidate))
                        continue;

                    var range = Manhattan(candidate, enemyCell);
                    if (range < MinPursueRange || range > MaxPursueRange)
                        continue;

                    if (!AIPathfinding.TryFindComfortPath(grid, currentCell, candidate, dangerCells, ScratchPath)
                        && !AIPathfinding.TryFindPath(grid, currentCell, candidate, dangerCells, ScratchPath))
                        continue;

                    var pathLength = ScratchPath.Count;
                    var openness = CountOpenNeighbors(grid, candidate);
                    var rangePenalty = Mathf.Abs(range - PreferredPursueRange) * 12f;
                    var pathPenalty = pathLength * 14f;
                    var deadEndPenalty = (4 - openness) * 9f;
                    var comfortPenalty = AIPathfinding.GetCellComfortCost(grid, candidate) * 18f;
                    var overTravelPenalty = Mathf.Max(0, pathLength - currentDistanceToEnemy) * 4f;
                    var score = pathPenalty + rangePenalty + deadEndPenalty + comfortPenalty + overTravelPenalty;

                    if (score >= bestScore)
                        continue;

                    bestScore = score;
                    goal = candidate;
                    found = true;
                }
            }

            if (found)
                return true;

            goal = enemyCell;
            return grid.IsInside(goal.x, goal.y)
                && (AIPathfinding.TryFindComfortPath(grid, currentCell, goal, dangerCells, ScratchPath)
                    || AIPathfinding.TryFindPath(grid, currentCell, goal, dangerCells, ScratchPath));
        }

        public static bool TryFindEvadeGoal(GridMap grid, Vector2Int currentCell, DangerField dangerField, HashSet<Vector2Int> dangerCells, out Vector2Int goal)
        {
            goal = currentCell;
            if (grid == null || dangerField == null || !grid.IsInside(currentCell.x, currentCell.y))
                return false;

            var currentRisk = dangerField.GetRisk(CoordinateUtil.CellToWorld(currentCell.x, currentCell.y), 0f);
            var bestScore = float.PositiveInfinity;
            var found = false;

            for (var row = 0; row < grid.Rows; row++)
            {
                for (var col = 0; col < grid.Cols; col++)
                {
                    var candidate = new Vector2Int(col, row);
                    if (candidate == currentCell)
                        continue;
                    if (dangerCells != null && dangerCells.Contains(candidate))
                        continue;

                    if (!AIPathfinding.TryFindPath(grid, currentCell, candidate, dangerCells, ScratchPath))
                        continue;

                    var pathLength = ScratchPath.Count;
                    if (pathLength <= 0 || pathLength > MaxEvadePathLength)
                        continue;

                    var arrivalTime = Mathf.Min(1.6f, pathLength * 0.32f);
                    var center = CoordinateUtil.CellToWorld(candidate.x, candidate.y);
                    var risk = dangerField.GetRisk(center, arrivalTime);
                    var openness = CountOpenNeighbors(grid, candidate);
                    var improvement = Mathf.Max(0f, currentRisk - risk);
                    var score = risk * 2400f + pathLength * 32f + (4 - openness) * 35f - improvement * 900f;

                    if (score >= bestScore)
                        continue;

                    bestScore = score;
                    goal = candidate;
                    found = true;
                }
            }

            return found;
        }

        private static Vector2Int FindNearestEnemyCell(Vector2Int currentCell, IReadOnlyList<TankController> enemies)
        {
            var best = currentCell;
            var bestDistance = int.MaxValue;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.Alive)
                    continue;

                var cell = CoordinateUtil.PixelToCell(CoordinateUtil.WorldToPixel(enemy.transform.position));
                var distance = Manhattan(currentCell, cell);
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                best = cell;
            }

            return best;
        }

        private static int CountOpenNeighbors(GridMap grid, Vector2Int cell)
        {
            grid.FillNeighbors(cell.x, cell.y, ScratchNeighbors);
            return ScratchNeighbors.Count;
        }

        private static int Manhattan(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }
    }
}
