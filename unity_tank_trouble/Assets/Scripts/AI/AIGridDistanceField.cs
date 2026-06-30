using System.Collections.Generic;
using UnityEngine;
using TankTrouble.Map;

namespace TankTrouble.AI
{
    public sealed class AIGridDistanceField
    {
        private readonly Queue<Vector2Int> queue = new Queue<Vector2Int>(128);
        private readonly List<Vector2Int> neighbors = new List<Vector2Int>(4);
        private int[,] distances;
        private int rows;
        private int cols;

        public void Build(GridMap grid, Vector2Int start, AIBlockedEdgeSet blockedEdges = null)
        {
            if (grid == null || !grid.IsInside(start.x, start.y))
            {
                EnsureSize(0, 0);
                return;
            }

            EnsureSize(grid.Rows, grid.Cols);
            ClearDistances();
            queue.Clear();

            distances[start.y, start.x] = 0;
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var nextDistance = distances[current.y, current.x] + 1;
                grid.FillNeighbors(current.x, current.y, neighbors);
                for (var i = 0; i < neighbors.Count; i++)
                {
                    var next = neighbors[i];
                    if (blockedEdges != null && blockedEdges.Contains(current, next))
                        continue;
                    if (distances[next.y, next.x] >= 0)
                        continue;

                    distances[next.y, next.x] = nextDistance;
                    queue.Enqueue(next);
                }
            }
        }

        public int GetDistance(Vector2Int cell)
        {
            if (distances == null || cell.x < 0 || cell.y < 0 || cell.x >= cols || cell.y >= rows)
                return -1;

            return distances[cell.y, cell.x];
        }

        private void EnsureSize(int newRows, int newCols)
        {
            if (distances != null && rows == newRows && cols == newCols)
                return;

            rows = newRows;
            cols = newCols;
            distances = rows > 0 && cols > 0 ? new int[rows, cols] : null;
        }

        private void ClearDistances()
        {
            for (var row = 0; row < rows; row++)
            {
                for (var col = 0; col < cols; col++)
                    distances[row, col] = -1;
            }
        }
    }
}
