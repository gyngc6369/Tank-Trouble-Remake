using System.Collections.Generic;
using UnityEngine;

namespace TankTrouble.Map
{
    public static class PresetMaps
    {
        public static readonly string[] Names =
        {
            "开放空间",
            "走廊迷宫",
            "对称战场",
            "螺旋路径",
            "格栅街区"
        };

        public static GridMap Build(int index)
        {
            var grid = new GridMap();
            switch (index)
            {
                case 0: BuildOpenMap(grid); break;
                case 1: BuildCorridors(grid); break;
                case 2: BuildSymmetric(grid); break;
                case 3: BuildSpiral(grid); break;
                case 4: BuildGrid(grid); break;
                default: throw new System.ArgumentOutOfRangeException(nameof(index), index, "Preset map index must be 0-4.");
            }

            if (!grid.CheckAllReachable())
                EnsureConnected(grid);

            return grid;
        }

        private static void BuildOpenMap(GridMap grid)
        {
            KeepOnlyWalls(grid, new[]
            {
                (2, 2, WallDirection.Top), (5, 3, WallDirection.Right), (8, 2, WallDirection.Top), (11, 4, WallDirection.Right),
                (3, 6, WallDirection.Top), (7, 5, WallDirection.Right), (10, 7, WallDirection.Top), (13, 3, WallDirection.Right),
                (4, 8, WallDirection.Top), (9, 6, WallDirection.Right), (1, 4, WallDirection.Left), (6, 9, WallDirection.Left),
                (12, 6, WallDirection.Left), (8, 8, WallDirection.Left)
            });
        }

        private static void BuildCorridors(GridMap grid)
        {
            grid.FillAllWalls(true);
            for (var c = 0; c < grid.Cols - 1; c++)
            foreach (var row in new[] { 2, 5, 8 })
                grid.RemoveWall(c, row, WallDirection.Right);

            for (var r = 0; r < grid.Rows - 1; r++)
            foreach (var col in new[] { 3, 7, 11 })
                grid.RemoveWall(col, r, WallDirection.Bottom);

            foreach (var open in new[]
            {
                (1, 3), (4, 3), (9, 3), (13, 3),
                (1, 7), (4, 7), (9, 7), (13, 7)
            })
                grid.RemoveWall(open.Item1, open.Item2, WallDirection.Right);
        }

        private static void BuildSymmetric(GridMap grid)
        {
            grid.FillAllWalls(true);
            var mid = grid.Cols / 2;
            for (var c = 0; c < grid.Cols - 1; c++)
            foreach (var row in new[] { 1, 3, 5, 7, 9 })
                grid.RemoveWall(c, row, WallDirection.Right);

            for (var r = 0; r < grid.Rows - 1; r++)
            foreach (var col in new[] { 2, 5, 7, 9, 12 })
                grid.RemoveWall(col, r, WallDirection.Bottom);

            foreach (var row in new[] { 2, 4, 6, 8 })
                grid.RemoveWall(mid - 1, row, WallDirection.Right);
        }

        private static void BuildSpiral(GridMap grid)
        {
            grid.FillAllWalls(true);
            for (var c = 1; c < grid.Cols - 2; c++) grid.RemoveWall(c, 1, WallDirection.Right);
            for (var r = 1; r < grid.Rows - 2; r++) grid.RemoveWall(grid.Cols - 2, r, WallDirection.Bottom);
            for (var c = 2; c < grid.Cols - 1; c++) grid.RemoveWall(c, grid.Rows - 2, WallDirection.Right);
            for (var r = 2; r < grid.Rows - 1; r++) grid.RemoveWall(1, r, WallDirection.Bottom);

            for (var c = 3; c < grid.Cols - 4; c++) grid.RemoveWall(c, 3, WallDirection.Right);
            for (var r = 3; r < grid.Rows - 4; r++) grid.RemoveWall(grid.Cols - 4, r, WallDirection.Bottom);
            for (var c = 4; c < grid.Cols - 3; c++) grid.RemoveWall(c, grid.Rows - 4, WallDirection.Right);
            for (var r = 4; r < grid.Rows - 3; r++) grid.RemoveWall(3, r, WallDirection.Bottom);

            grid.RemoveWall(5, 2, WallDirection.Bottom);
            grid.RemoveWall(5, 4, WallDirection.Top);
            grid.RemoveWall(9, 6, WallDirection.Bottom);
        }

        private static void BuildGrid(GridMap grid)
        {
            grid.FillAllWalls(true);
            foreach (var row in new[] { 1, 2, 4, 5, 7, 8, 10 })
            for (var c = 0; c < grid.Cols - 1; c++)
                grid.RemoveWall(c, row, WallDirection.Right);

            foreach (var col in new[] { 1, 2, 4, 5, 7, 8, 10, 12, 13 })
            for (var r = 0; r < grid.Rows - 1; r++)
                grid.RemoveWall(col, r, WallDirection.Bottom);

            grid.RemoveWall(0, 0, WallDirection.Right);
            grid.RemoveWall(0, 0, WallDirection.Bottom);
        }

        private static void KeepOnlyWalls(GridMap grid, IEnumerable<(int col, int row, WallDirection dir)> wallsToKeep)
        {
            grid.FillAllWalls(true);
            var keep = new HashSet<(int, int, WallDirection)>();
            foreach (var wall in wallsToKeep)
            {
                keep.Add(wall);
                var opposite = Opposite(wall.dir);
                var neighbor = Neighbor(wall.col, wall.row, wall.dir);
                if (grid.IsInside(neighbor.x, neighbor.y))
                    keep.Add((neighbor.x, neighbor.y, opposite));
            }

            for (var r = 0; r < grid.Rows; r++)
            for (var c = 0; c < grid.Cols; c++)
            for (var d = 0; d < 4; d++)
            {
                var dir = (WallDirection)d;
                if (!keep.Contains((c, r, dir)))
                    grid.RemoveWall(c, r, dir);
            }
        }

        private static void EnsureConnected(GridMap grid)
        {
            var visited = new bool[grid.Rows, grid.Cols];
            grid.BfsReachableCount(0, 0, visited);

            var changed = true;
            while (changed && !grid.CheckAllReachable())
            {
                changed = false;
                for (var r = 0; r < grid.Rows && !changed; r++)
                for (var c = 0; c < grid.Cols && !changed; c++)
                {
                    if (visited[r, c])
                        continue;

                    for (var d = 0; d < 4; d++)
                    {
                        var n = Neighbor(c, r, (WallDirection)d);
                        if (!grid.IsInside(n.x, n.y) || !visited[n.y, n.x])
                            continue;

                        grid.RemoveWall(c, r, (WallDirection)d);
                        changed = true;
                        break;
                    }
                }

                grid.BfsReachableCount(0, 0, visited);
            }
        }

        private static Vector2Int Neighbor(int col, int row, WallDirection dir)
        {
            return dir switch
            {
                WallDirection.Top => new Vector2Int(col, row - 1),
                WallDirection.Right => new Vector2Int(col + 1, row),
                WallDirection.Bottom => new Vector2Int(col, row + 1),
                WallDirection.Left => new Vector2Int(col - 1, row),
                _ => new Vector2Int(col, row)
            };
        }

        private static WallDirection Opposite(WallDirection dir)
        {
            return dir switch
            {
                WallDirection.Top => WallDirection.Bottom,
                WallDirection.Right => WallDirection.Left,
                WallDirection.Bottom => WallDirection.Top,
                WallDirection.Left => WallDirection.Right,
                _ => dir
            };
        }
    }
}
