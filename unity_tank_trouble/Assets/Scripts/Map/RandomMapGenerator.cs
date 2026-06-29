using System;
using System.Collections.Generic;
using UnityEngine;
using TankTrouble.Config;

namespace TankTrouble.Map
{
    public static class RandomMapGenerator
    {
        private static readonly (int dc, int dr, WallDirection dir)[] Directions =
        {
            (0, -1, WallDirection.Top),
            (1, 0, WallDirection.Right),
            (0, 1, WallDirection.Bottom),
            (-1, 0, WallDirection.Left)
        };

        public static GridMap Generate(int? seed = null, int cols = GameConfig.GridCols, int rows = GameConfig.GridRows, float extraRatio = 0.20f)
        {
            var rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
            var grid = new GridMap(cols, rows);
            var visited = new bool[rows, cols];
            var stack = new Stack<Vector2Int>(cols * rows);

            var start = new Vector2Int(rng.Next(cols), rng.Next(rows));
            visited[start.y, start.x] = true;
            stack.Push(start);

            var neighbors = new List<(Vector2Int cell, WallDirection dir)>(4);
            while (stack.Count > 0)
            {
                var current = stack.Peek();
                neighbors.Clear();

                foreach (var d in Directions)
                {
                    var next = new Vector2Int(current.x + d.dc, current.y + d.dr);
                    if (next.x >= 0 && next.x < cols && next.y >= 0 && next.y < rows && !visited[next.y, next.x])
                        neighbors.Add((next, d.dir));
                }

                if (neighbors.Count == 0)
                {
                    stack.Pop();
                    continue;
                }

                var chosen = neighbors[rng.Next(neighbors.Count)];
                grid.RemoveWall(current.x, current.y, chosen.dir);
                visited[chosen.cell.y, chosen.cell.x] = true;
                stack.Push(chosen.cell);
            }

            var internalWalls = new List<(int col, int row, WallDirection dir)>();
            for (var r = 0; r < rows; r++)
            for (var c = 0; c < cols; c++)
            {
                if (c < cols - 1 && grid.HasWall(c, r, WallDirection.Right))
                    internalWalls.Add((c, r, WallDirection.Right));
                if (r < rows - 1 && grid.HasWall(c, r, WallDirection.Bottom))
                    internalWalls.Add((c, r, WallDirection.Bottom));
            }

            Shuffle(internalWalls, rng);
            var extraCount = Mathf.FloorToInt(internalWalls.Count * extraRatio);
            for (var i = 0; i < extraCount; i++)
                grid.RemoveWall(internalWalls[i].col, internalWalls[i].row, internalWalls[i].dir);

            return grid;
        }

        private static void Shuffle<T>(IList<T> list, System.Random rng)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
