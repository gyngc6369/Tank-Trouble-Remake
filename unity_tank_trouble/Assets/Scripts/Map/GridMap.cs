using System;
using System.Collections.Generic;
using UnityEngine;
using TankTrouble.Config;

namespace TankTrouble.Map
{
    public sealed class GridMap
    {
        private readonly bool[,,] walls;
        private List<WallSegment> cachedWallSegments;

        private static readonly int[] Dc = { 0, 1, 0, -1 };
        private static readonly int[] Dr = { -1, 0, 1, 0 };
        private static readonly WallDirection[] Opposite =
        {
            WallDirection.Bottom,
            WallDirection.Left,
            WallDirection.Top,
            WallDirection.Right
        };

        public int Cols { get; }
        public int Rows { get; }

        public GridMap(int cols = GameConfig.GridCols, int rows = GameConfig.GridRows)
        {
            Cols = cols;
            Rows = rows;
            walls = new bool[rows, cols, 4];
            FillAllWalls(true);
        }

        public void FillAllWalls(bool value)
        {
            for (var r = 0; r < Rows; r++)
            for (var c = 0; c < Cols; c++)
            for (var d = 0; d < 4; d++)
                walls[r, c, d] = value;

            InvalidateCache();
        }

        public bool HasWall(int col, int row, WallDirection direction)
        {
            if (!IsInside(col, row))
                return true;

            return walls[row, col, (int)direction];
        }

        public void SetWall(int col, int row, WallDirection direction, bool exists)
        {
            if (!IsInside(col, row))
                return;

            walls[row, col, (int)direction] = exists;

            var d = (int)direction;
            var nc = col + Dc[d];
            var nr = row + Dr[d];
            if (IsInside(nc, nr))
                walls[nr, nc, (int)Opposite[d]] = exists;

            InvalidateCache();
        }

        public void RemoveWall(int col, int row, WallDirection direction)
        {
            SetWall(col, row, direction, false);
        }

        public bool IsInside(int col, int row)
        {
            return col >= 0 && col < Cols && row >= 0 && row < Rows;
        }

        public void FillNeighbors(int col, int row, List<Vector2Int> results)
        {
            results.Clear();
            for (var d = 0; d < 4; d++)
            {
                var nc = col + Dc[d];
                var nr = row + Dr[d];
                if (IsInside(nc, nr) && !HasWall(col, row, (WallDirection)d))
                    results.Add(new Vector2Int(nc, nr));
            }
        }

        public List<Vector2Int> GetNeighbors(int col, int row)
        {
            var results = new List<Vector2Int>(4);
            FillNeighbors(col, row, results);
            return results;
        }

        public int BfsReachableCount(int startCol, int startRow, bool[,] visited = null)
        {
            if (!IsInside(startCol, startRow))
                return 0;

            if (visited == null)
                visited = new bool[Rows, Cols];
            Array.Clear(visited, 0, visited.Length);

            var queue = new Queue<Vector2Int>(Cols * Rows);
            var scratch = new List<Vector2Int>(4);
            queue.Enqueue(new Vector2Int(startCol, startRow));
            visited[startRow, startCol] = true;

            var count = 0;
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                count++;
                FillNeighbors(current.x, current.y, scratch);
                for (var i = 0; i < scratch.Count; i++)
                {
                    var n = scratch[i];
                    if (visited[n.y, n.x])
                        continue;

                    visited[n.y, n.x] = true;
                    queue.Enqueue(n);
                }
            }

            return count;
        }

        public bool CheckAllReachable()
        {
            if (Rows <= 0 || Cols <= 0)
                return true;

            return BfsReachableCount(0, 0) == Cols * Rows;
        }

        public List<Vector2Int> PickSpawnPoints(int count, int minDistance = GameConfig.SpawnMinDistance, System.Random rng = null)
        {
            rng ??= new System.Random();
            var allCells = new List<Vector2Int>(Cols * Rows);
            for (var r = 0; r < Rows; r++)
            for (var c = 0; c < Cols; c++)
                allCells.Add(new Vector2Int(c, r));

            for (var attempt = 0; attempt < 100; attempt++)
            {
                Shuffle(allCells, rng);
                var candidates = new List<Vector2Int>(count);
                for (var i = 0; i < allCells.Count && candidates.Count < count; i++)
                {
                    var cell = allCells[i];
                    var ok = true;
                    for (var j = 0; j < candidates.Count; j++)
                    {
                        if (Manhattan(cell, candidates[j]) < minDistance)
                        {
                            ok = false;
                            break;
                        }
                    }

                    if (ok)
                        candidates.Add(cell);
                }

                if (candidates.Count == count)
                    return candidates;
            }

            return FallbackSpawnPoints(count);
        }

        public List<WallSegment> GetWallSegments(bool mergeContiguous = true)
        {
            if (cachedWallSegments != null)
                return cachedWallSegments;

            cachedWallSegments = BuildWallSegments(mergeContiguous);
            return cachedWallSegments;
        }

        private List<WallSegment> BuildWallSegments(bool mergeContiguous)
        {
            var unique = new HashSet<SegmentKey>();
            for (var r = 0; r < Rows; r++)
            for (var c = 0; c < Cols; c++)
            {
                var x0 = c * GameConfig.CellSize;
                var y0 = r * GameConfig.CellSize + GameConfig.GridOffsetY;
                var x1 = x0 + GameConfig.CellSize;
                var y1 = y0 + GameConfig.CellSize;

                if (walls[r, c, (int)WallDirection.Top]) unique.Add(SegmentKey.Create(x0, y0, x1, y0));
                if (walls[r, c, (int)WallDirection.Right]) unique.Add(SegmentKey.Create(x1, y0, x1, y1));
                if (walls[r, c, (int)WallDirection.Bottom]) unique.Add(SegmentKey.Create(x0, y1, x1, y1));
                if (walls[r, c, (int)WallDirection.Left]) unique.Add(SegmentKey.Create(x0, y0, x0, y1));
            }

            if (!mergeContiguous)
            {
                var raw = new List<WallSegment>(unique.Count);
                foreach (var s in unique)
                    raw.Add(s.ToWallSegment());
                return raw;
            }

            return MergeSegments(unique);
        }

        private static List<WallSegment> MergeSegments(HashSet<SegmentKey> unique)
        {
            var horizontal = new Dictionary<int, List<Vector2Int>>();
            var vertical = new Dictionary<int, List<Vector2Int>>();

            foreach (var s in unique)
            {
                if (s.Y1 == s.Y2)
                {
                    if (!horizontal.TryGetValue(s.Y1, out var list))
                    {
                        list = new List<Vector2Int>();
                        horizontal[s.Y1] = list;
                    }
                    list.Add(new Vector2Int(s.X1, s.X2));
                }
                else
                {
                    if (!vertical.TryGetValue(s.X1, out var list))
                    {
                        list = new List<Vector2Int>();
                        vertical[s.X1] = list;
                    }
                    list.Add(new Vector2Int(s.Y1, s.Y2));
                }
            }

            var merged = new List<WallSegment>(unique.Count);
            foreach (var pair in horizontal)
                MergeIntervals(pair.Value, (start, end) => merged.Add(new WallSegment(new Vector2(start, pair.Key), new Vector2(end, pair.Key))));
            foreach (var pair in vertical)
                MergeIntervals(pair.Value, (start, end) => merged.Add(new WallSegment(new Vector2(pair.Key, start), new Vector2(pair.Key, end))));

            return merged;
        }

        private static void MergeIntervals(List<Vector2Int> intervals, Action<int, int> emit)
        {
            intervals.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            var start = intervals[0].x;
            var end = intervals[0].y;

            for (var i = 1; i < intervals.Count; i++)
            {
                var next = intervals[i];
                if (next.x <= end)
                {
                    end = Math.Max(end, next.y);
                    continue;
                }

                emit(start, end);
                start = next.x;
                end = next.y;
            }

            emit(start, end);
        }

        private void InvalidateCache()
        {
            cachedWallSegments = null;
        }

        private static int Manhattan(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        private static void Shuffle<T>(IList<T> list, System.Random rng)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private List<Vector2Int> FallbackSpawnPoints(int count)
        {
            var basePoints = new[]
            {
                new Vector2Int(1, 1),
                new Vector2Int(Cols - 2, Rows - 2),
                new Vector2Int(Cols - 2, 1),
                new Vector2Int(1, Rows - 2),
                new Vector2Int(Cols / 2, Rows / 2)
            };

            var result = new List<Vector2Int>(count);
            for (var i = 0; i < count; i++)
                result.Add(basePoints[i % basePoints.Length]);
            return result;
        }

        private readonly struct SegmentKey : IEquatable<SegmentKey>
        {
            public readonly int X1;
            public readonly int Y1;
            public readonly int X2;
            public readonly int Y2;

            private SegmentKey(int x1, int y1, int x2, int y2)
            {
                X1 = x1;
                Y1 = y1;
                X2 = x2;
                Y2 = y2;
            }

            public static SegmentKey Create(int x1, int y1, int x2, int y2)
            {
                if (x1 > x2 || y1 > y2)
                    return new SegmentKey(x2, y2, x1, y1);
                return new SegmentKey(x1, y1, x2, y2);
            }

            public WallSegment ToWallSegment()
            {
                return new WallSegment(new Vector2(X1, Y1), new Vector2(X2, Y2));
            }

            public bool Equals(SegmentKey other)
            {
                return X1 == other.X1 && Y1 == other.Y1 && X2 == other.X2 && Y2 == other.Y2;
            }

            public override bool Equals(object obj)
            {
                return obj is SegmentKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = X1;
                    hash = (hash * 397) ^ Y1;
                    hash = (hash * 397) ^ X2;
                    hash = (hash * 397) ^ Y2;
                    return hash;
                }
            }
        }
    }
}
