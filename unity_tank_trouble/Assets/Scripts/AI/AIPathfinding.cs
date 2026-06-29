using System.Collections.Generic;
using UnityEngine;
using TankTrouble.Map;

namespace TankTrouble.AI
{
    public static class AIPathfinding
    {
        private const float StepCost = 1f;
        private const float OuterRingCost = 0.75f;
        private const float MissingExitCost = 0.18f;
        private const float LowOpennessCost = 0.55f;
        private const float DeadEndCost = 1.35f;
        private const float CornerCost = 0.35f;

        private static readonly List<Vector2Int> SearchNeighbors = new List<Vector2Int>(4);
        private static readonly List<Vector2Int> ComfortNeighbors = new List<Vector2Int>(4);

        public static bool TryFindPath(GridMap grid, Vector2Int start, Vector2Int goal, HashSet<Vector2Int> dangerCells, List<Vector2Int> output)
        {
            output.Clear();
            if (grid == null || !grid.IsInside(start.x, start.y) || !grid.IsInside(goal.x, goal.y))
                return false;
            if (start == goal)
                return true;

            var queue = new Queue<Vector2Int>(grid.Cols * grid.Rows);
            var visited = new bool[grid.Rows, grid.Cols];
            var parent = new Vector2Int[grid.Rows, grid.Cols];
            queue.Enqueue(start);
            visited[start.y, start.x] = true;
            parent[start.y, start.x] = new Vector2Int(-1, -1);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                grid.FillNeighbors(current.x, current.y, SearchNeighbors);
                for (var i = 0; i < SearchNeighbors.Count; i++)
                {
                    var next = SearchNeighbors[i];
                    if (visited[next.y, next.x])
                        continue;
                    if (dangerCells != null && dangerCells.Contains(next) && next != goal)
                        continue;

                    visited[next.y, next.x] = true;
                    parent[next.y, next.x] = current;

                    if (next == goal)
                    {
                        RebuildPath(parent, start, goal, output);
                        return true;
                    }

                    queue.Enqueue(next);
                }
            }

            return false;
        }

        public static bool TryFindComfortPath(GridMap grid, Vector2Int start, Vector2Int goal, HashSet<Vector2Int> dangerCells, List<Vector2Int> output)
        {
            output.Clear();
            if (grid == null || !grid.IsInside(start.x, start.y) || !grid.IsInside(goal.x, goal.y))
                return false;
            if (start == goal)
                return true;

            var bestCost = new float[grid.Rows, grid.Cols];
            var visited = new bool[grid.Rows, grid.Cols];
            var parent = new Vector2Int[grid.Rows, grid.Cols];
            for (var row = 0; row < grid.Rows; row++)
            {
                for (var col = 0; col < grid.Cols; col++)
                {
                    bestCost[row, col] = float.PositiveInfinity;
                    parent[row, col] = new Vector2Int(-1, -1);
                }
            }

            bestCost[start.y, start.x] = 0f;
            var totalCells = grid.Cols * grid.Rows;
            for (var step = 0; step < totalCells; step++)
            {
                var current = FindCheapestUnvisited(grid, bestCost, visited);
                if (current.x < 0)
                    break;
                if (current == goal)
                {
                    RebuildPath(parent, start, goal, output);
                    return true;
                }

                visited[current.y, current.x] = true;
                grid.FillNeighbors(current.x, current.y, SearchNeighbors);
                for (var i = 0; i < SearchNeighbors.Count; i++)
                {
                    var next = SearchNeighbors[i];
                    if (visited[next.y, next.x])
                        continue;
                    if (dangerCells != null && dangerCells.Contains(next) && next != goal)
                        continue;

                    var cost = bestCost[current.y, current.x] + StepCost + GetCellComfortCost(grid, next);
                    if (cost >= bestCost[next.y, next.x])
                        continue;

                    bestCost[next.y, next.x] = cost;
                    parent[next.y, next.x] = current;
                }
            }

            return false;
        }

        public static float GetCellComfortCost(GridMap grid, Vector2Int cell)
        {
            if (grid == null || !grid.IsInside(cell.x, cell.y))
                return 12f;

            grid.FillNeighbors(cell.x, cell.y, ComfortNeighbors);
            var openness = ComfortNeighbors.Count;
            var cost = 0f;
            if (IsOuterRing(grid, cell))
                cost += OuterRingCost;

            cost += Mathf.Max(0, 4 - openness) * MissingExitCost;
            cost += Mathf.Max(0, 3 - openness) * LowOpennessCost;
            if (openness <= 1)
                cost += DeadEndCost;
            if (openness == 2 && IsCornerCell(cell))
                cost += CornerCost;

            return cost;
        }

        public static bool TryFindNearestSafeCell(GridMap grid, Vector2Int start, HashSet<Vector2Int> dangerCells, out Vector2Int safeCell)
        {
            safeCell = start;
            if (grid == null || dangerCells == null || !dangerCells.Contains(start))
                return true;

            var queue = new Queue<Vector2Int>(grid.Cols * grid.Rows);
            var visited = new bool[grid.Rows, grid.Cols];
            queue.Enqueue(start);
            visited[start.y, start.x] = true;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                grid.FillNeighbors(current.x, current.y, SearchNeighbors);
                for (var i = 0; i < SearchNeighbors.Count; i++)
                {
                    var next = SearchNeighbors[i];
                    if (visited[next.y, next.x])
                        continue;

                    if (!dangerCells.Contains(next))
                    {
                        safeCell = next;
                        return true;
                    }

                    visited[next.y, next.x] = true;
                    queue.Enqueue(next);
                }
            }

            return false;
        }

        public static Vector2Int FindNearestEnemyCell(GridMap grid, Vector2Int currentCell, IReadOnlyList<TankTrouble.Entities.TankController> enemies)
        {
            var best = currentCell;
            var bestDist = int.MaxValue;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.Alive)
                    continue;

                var enemyCell = TankTrouble.Core.CoordinateUtil.PixelToCell(TankTrouble.Core.CoordinateUtil.WorldToPixel(enemy.transform.position));
                var dist = Mathf.Abs(enemyCell.x - currentCell.x) + Mathf.Abs(enemyCell.y - currentCell.y);
                if (dist >= bestDist)
                    continue;

                bestDist = dist;
                best = enemyCell;
            }

            return best;
        }

        public static Vector2Int FindBestApproachCell(GridMap grid, Vector2Int currentCell, IReadOnlyList<TankTrouble.Entities.TankController> enemies, HashSet<Vector2Int> dangerCells)
        {
            var enemyCell = FindNearestEnemyCell(grid, currentCell, enemies);
            var best = enemyCell;
            var bestScore = float.PositiveInfinity;

            for (var row = 0; row < grid.Rows; row++)
            {
                for (var col = 0; col < grid.Cols; col++)
                {
                    var candidate = new Vector2Int(col, row);
                    if (candidate == currentCell)
                        continue;
                    if (dangerCells != null && dangerCells.Contains(candidate))
                        continue;

                    var distanceToEnemy = Manhattan(candidate, enemyCell);
                    if (distanceToEnemy < 2 || distanceToEnemy > 6)
                        continue;

                    var distanceFromAi = Manhattan(currentCell, candidate);
                    var opennessPenalty = 4 - CountOpenNeighbors(grid, candidate);
                    var rangePenalty = Mathf.Abs(distanceToEnemy - 4) * 4f;
                    var comfortPenalty = GetCellComfortCost(grid, candidate) * 5f;
                    var score = distanceFromAi + rangePenalty + opennessPenalty * 3f + comfortPenalty;
                    if (score >= bestScore)
                        continue;

                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        public static bool TryFindBestRepositionCell(GridMap grid, Vector2Int currentCell, IReadOnlyList<TankTrouble.Entities.TankController> enemies, HashSet<Vector2Int> dangerCells, out Vector2Int bestCell)
        {
            bestCell = currentCell;
            if (grid == null || !grid.IsInside(currentCell.x, currentCell.y))
                return false;

            var enemyCell = FindNearestEnemyCell(grid, currentCell, enemies);
            var bestScore = float.NegativeInfinity;
            for (var row = 0; row < grid.Rows; row++)
            {
                for (var col = 0; col < grid.Cols; col++)
                {
                    var candidate = new Vector2Int(col, row);
                    if (dangerCells != null && dangerCells.Contains(candidate))
                        continue;

                    var distanceFromCurrent = Manhattan(currentCell, candidate);
                    if (distanceFromCurrent < 1 || distanceFromCurrent > 5)
                        continue;

                    var distanceFromEnemy = Manhattan(candidate, enemyCell);
                    var openness = CountOpenNeighbors(grid, candidate);
                    var spacingScore = Mathf.Clamp(distanceFromEnemy, 0, 7) * 3f;
                    var travelPenalty = Mathf.Abs(distanceFromCurrent - 3) * 3f;
                    var comfortPenalty = GetCellComfortCost(grid, candidate) * 6f;
                    var score = spacingScore + openness * 5f - travelPenalty - comfortPenalty;
                    if (score <= bestScore)
                        continue;

                    bestScore = score;
                    bestCell = candidate;
                }
            }

            return bestScore > float.NegativeInfinity;
        }

        private static Vector2Int FindCheapestUnvisited(GridMap grid, float[,] bestCost, bool[,] visited)
        {
            var best = new Vector2Int(-1, -1);
            var cost = float.PositiveInfinity;
            for (var row = 0; row < grid.Rows; row++)
            {
                for (var col = 0; col < grid.Cols; col++)
                {
                    if (visited[row, col] || bestCost[row, col] >= cost)
                        continue;

                    cost = bestCost[row, col];
                    best = new Vector2Int(col, row);
                }
            }

            return best;
        }

        private static int Manhattan(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        private static int CountOpenNeighbors(GridMap grid, Vector2Int cell)
        {
            grid.FillNeighbors(cell.x, cell.y, ComfortNeighbors);
            return ComfortNeighbors.Count;
        }

        private static bool IsOuterRing(GridMap grid, Vector2Int cell)
        {
            return cell.x == 0 || cell.y == 0 || cell.x == grid.Cols - 1 || cell.y == grid.Rows - 1;
        }

        private static bool IsCornerCell(Vector2Int cell)
        {
            if (ComfortNeighbors.Count != 2)
                return false;

            var a = ComfortNeighbors[0] - cell;
            var b = ComfortNeighbors[1] - cell;
            return a.x != -b.x || a.y != -b.y;
        }

        private static void RebuildPath(Vector2Int[,] parent, Vector2Int start, Vector2Int goal, List<Vector2Int> output)
        {
            output.Clear();
            var current = goal;
            while (current != start && current.x >= 0)
            {
                output.Add(current);
                current = parent[current.y, current.x];
            }

            output.Reverse();
        }
    }
}
