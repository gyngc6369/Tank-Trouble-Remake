using System.Collections.Generic;
using UnityEngine;
using TankTrouble.Core;
using TankTrouble.Entities;
using TankTrouble.Map;

namespace TankTrouble.AI
{
    public sealed class AIGridPathPlanner
    {
        private const float MoveCost = 1f;
        private const float TurnCost = 0.22f;
        private const float CandidateEnemyDistanceWeight = 0.2f;

        private readonly List<Vector2Int> enemyNeighbors = new List<Vector2Int>(4);
        private readonly List<Vector2Int> candidates = new List<Vector2Int>(8);
        private readonly List<Vector2Int> scratchPath = new List<Vector2Int>(32);

        public bool TryFindPathToNearestEnemy(
            GridMap grid,
            Vector2Int start,
            AIGridDirection startFacing,
            IReadOnlyList<TankController> enemies,
            AIBlockedEdgeSet blockedEdges,
            List<Vector2Int> output,
            out Vector2Int goal)
        {
            output.Clear();
            goal = start;
            if (grid == null || enemies == null || enemies.Count == 0 || !grid.IsInside(start.x, start.y))
                return false;

            var enemyCell = FindNearestEnemyCell(start, enemies);
            BuildGoalCandidates(grid, enemyCell);
            var bestScore = float.PositiveInfinity;
            var found = false;

            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate == start)
                    continue;

                if (!TryFindPath(grid, start, startFacing, candidate, blockedEdges, scratchPath))
                    continue;

                var score = scratchPath.Count + Manhattan(candidate, enemyCell) * CandidateEnemyDistanceWeight;
                if (score >= bestScore)
                    continue;

                bestScore = score;
                goal = candidate;
                output.Clear();
                output.AddRange(scratchPath);
                found = true;
            }

            return found;
        }

        public bool TryFindPath(
            GridMap grid,
            Vector2Int start,
            AIGridDirection startFacing,
            Vector2Int goal,
            AIBlockedEdgeSet blockedEdges,
            List<Vector2Int> output)
        {
            output.Clear();
            if (grid == null || !grid.IsInside(start.x, start.y) || !grid.IsInside(goal.x, goal.y))
                return false;
            if (start == goal)
                return true;

            var rows = grid.Rows;
            var cols = grid.Cols;
            var bestCost = new float[rows, cols, 4];
            var visited = new bool[rows, cols, 4];
            var parentCell = new Vector2Int[rows, cols, 4];
            var parentDirection = new AIGridDirection[rows, cols, 4];
            var hasParent = new bool[rows, cols, 4];

            for (var row = 0; row < rows; row++)
            {
                for (var col = 0; col < cols; col++)
                {
                    for (var dir = 0; dir < 4; dir++)
                    {
                        bestCost[row, col, dir] = float.PositiveInfinity;
                        parentCell[row, col, dir] = new Vector2Int(-1, -1);
                    }
                }
            }

            bestCost[start.y, start.x, (int)startFacing] = 0f;
            var totalNodes = rows * cols * 4;
            for (var step = 0; step < totalNodes; step++)
            {
                var current = FindCheapestUnvisited(grid, bestCost, visited, out var currentDirection);
                if (current.x < 0)
                    break;

                visited[current.y, current.x, (int)currentDirection] = true;
                if (current == goal)
                {
                    RebuildPath(parentCell, parentDirection, hasParent, start, startFacing, current, currentDirection, output);
                    return true;
                }

                TryRelaxTurn(current, currentDirection, AIGridDirections.TurnLeft(currentDirection), TurnCost);
                TryRelaxTurn(current, currentDirection, AIGridDirections.TurnRight(currentDirection), TurnCost);
                TryRelaxMove(current, currentDirection);
            }

            return false;

            void TryRelaxTurn(Vector2Int cell, AIGridDirection fromDirection, AIGridDirection toDirection, float cost)
            {
                var newCost = bestCost[cell.y, cell.x, (int)fromDirection] + cost;
                if (newCost >= bestCost[cell.y, cell.x, (int)toDirection])
                    return;

                bestCost[cell.y, cell.x, (int)toDirection] = newCost;
                parentCell[cell.y, cell.x, (int)toDirection] = cell;
                parentDirection[cell.y, cell.x, (int)toDirection] = fromDirection;
                hasParent[cell.y, cell.x, (int)toDirection] = true;
            }

            void TryRelaxMove(Vector2Int cell, AIGridDirection direction)
            {
                if (grid.HasWall(cell.x, cell.y, AIGridDirections.ToWallDirection(direction)))
                    return;

                var next = cell + AIGridDirections.ToDelta(direction);
                if (!grid.IsInside(next.x, next.y))
                    return;
                if (blockedEdges != null && blockedEdges.Contains(cell, next))
                    return;

                var newCost = bestCost[cell.y, cell.x, (int)direction] + MoveCost;
                if (newCost >= bestCost[next.y, next.x, (int)direction])
                    return;

                bestCost[next.y, next.x, (int)direction] = newCost;
                parentCell[next.y, next.x, (int)direction] = cell;
                parentDirection[next.y, next.x, (int)direction] = direction;
                hasParent[next.y, next.x, (int)direction] = true;
            }
        }

        private void BuildGoalCandidates(GridMap grid, Vector2Int enemyCell)
        {
            candidates.Clear();
            if (!grid.IsInside(enemyCell.x, enemyCell.y))
                return;

            candidates.Add(enemyCell);
            grid.FillNeighbors(enemyCell.x, enemyCell.y, enemyNeighbors);
            for (var i = 0; i < enemyNeighbors.Count; i++)
                candidates.Add(enemyNeighbors[i]);
        }

        private static Vector2Int FindCheapestUnvisited(GridMap grid, float[,,] bestCost, bool[,,] visited, out AIGridDirection direction)
        {
            var best = new Vector2Int(-1, -1);
            direction = AIGridDirection.Up;
            var cost = float.PositiveInfinity;
            for (var row = 0; row < grid.Rows; row++)
            {
                for (var col = 0; col < grid.Cols; col++)
                {
                    for (var dir = 0; dir < 4; dir++)
                    {
                        if (visited[row, col, dir] || bestCost[row, col, dir] >= cost)
                            continue;

                        cost = bestCost[row, col, dir];
                        best = new Vector2Int(col, row);
                        direction = (AIGridDirection)dir;
                    }
                }
            }

            return best;
        }

        private static void RebuildPath(
            Vector2Int[,,] parentCell,
            AIGridDirection[,,] parentDirection,
            bool[,,] hasParent,
            Vector2Int start,
            AIGridDirection startFacing,
            Vector2Int end,
            AIGridDirection endDirection,
            List<Vector2Int> output)
        {
            output.Clear();
            var reverse = new List<Vector2Int>(32);
            var cell = end;
            var direction = endDirection;

            while (!(cell == start && direction == startFacing))
            {
                if (!hasParent[cell.y, cell.x, (int)direction])
                    break;

                var previousCell = parentCell[cell.y, cell.x, (int)direction];
                var previousDirection = parentDirection[cell.y, cell.x, (int)direction];
                if (previousCell != cell)
                    reverse.Add(cell);

                cell = previousCell;
                direction = previousDirection;
            }

            for (var i = reverse.Count - 1; i >= 0; i--)
                output.Add(reverse[i]);
        }

        private static Vector2Int FindNearestEnemyCell(Vector2Int start, IReadOnlyList<TankController> enemies)
        {
            var best = start;
            var bestDistance = int.MaxValue;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.Alive)
                    continue;

                var cell = CoordinateUtil.PixelToCell(CoordinateUtil.WorldToPixel(enemy.transform.position));
                var distance = Manhattan(start, cell);
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                best = cell;
            }

            return best;
        }

        private static int Manhattan(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }
    }
}
