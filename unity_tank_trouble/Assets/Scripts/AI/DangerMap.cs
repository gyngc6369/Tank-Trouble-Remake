using System.Collections.Generic;
using UnityEngine;
using TankTrouble.Config;
using TankTrouble.Core;
using TankTrouble.Entities;
using TankTrouble.Map;

namespace TankTrouble.AI
{
    public static class DangerMap
    {
        private const int MaxPredictionBounces = GameConfig.MaxBounces;

        public static void CollectDangerCells(GridMap grid, IReadOnlyList<BulletController> bullets, LayerMask wallMask, float predictTime, HashSet<Vector2Int> output)
        {
            output.Clear();
            if (grid == null || bullets == null || predictTime <= 0f)
                return;

            for (var i = 0; i < bullets.Count; i++)
            {
                var bullet = bullets[i];
                if (bullet == null || !bullet.Alive)
                    continue;

                PredictBullet(grid, bullet.WorldPosition, bullet.Velocity, bullet.BounceCount, wallMask, predictTime, output, 0f);
            }
        }

        public static void AddPredictedShot(GridMap grid, Vector2 startWorld, Vector2 direction, LayerMask wallMask, float predictTime, HashSet<Vector2Int> output, float ignoreStartDistance = 0f)
        {
            if (grid == null || output == null || direction.sqrMagnitude < 0.0001f)
                return;

            var velocity = direction.normalized * (GameConfig.BulletSpeed / CoordinateUtil.PixelsPerUnit);
            PredictBullet(grid, startWorld, velocity, 0, wallMask, predictTime, output, ignoreStartDistance);
        }

        private static void PredictBullet(GridMap grid, Vector2 startWorld, Vector2 velocity, int existingBounces, LayerMask wallMask, float predictTime, HashSet<Vector2Int> output, float ignoreStartDistance)
        {
            if (velocity.sqrMagnitude < 0.0001f)
                return;

            var position = startWorld;
            var direction = velocity.normalized;
            var speed = GameConfig.BulletSpeed / CoordinateUtil.PixelsPerUnit;
            var remaining = predictTime;
            var bounceCount = existingBounces;
            var simulatedBounces = 0;
            var radius = GameConfig.BulletRadius / CoordinateUtil.PixelsPerUnit;
            var travelled = 0f;

            if (ignoreStartDistance <= 0f)
                MarkCellAndNeighbors(grid, position, output);

            while (remaining > 0f && simulatedBounces <= MaxPredictionBounces && bounceCount <= GameConfig.MaxBounces)
            {
                var distance = speed * remaining;
                var hit = Physics2D.CircleCast(position, radius, direction, distance, wallMask);
                if (hit.collider == null)
                {
                    MarkSegmentCellsAfterDistance(grid, position, position + direction * distance, travelled, ignoreStartDistance, output);
                    break;
                }

                MarkSegmentCellsAfterDistance(grid, position, hit.centroid, travelled, ignoreStartDistance, output);
                if (bounceCount >= GameConfig.MaxBounces)
                    break;

                var travelTime = Mathf.Max(0f, hit.distance / speed);
                remaining -= travelTime;
                travelled += hit.distance;
                direction = Vector2.Reflect(direction, hit.normal).normalized;
                position = hit.centroid + direction * (radius + 0.001f);
                bounceCount++;
                simulatedBounces++;
            }
        }

        private static void MarkSegmentCellsAfterDistance(GridMap grid, Vector2 fromWorld, Vector2 toWorld, float travelledBeforeSegment, float ignoreStartDistance, HashSet<Vector2Int> output)
        {
            var segmentDistance = Vector2.Distance(fromWorld, toWorld);
            if (travelledBeforeSegment + segmentDistance <= ignoreStartDistance)
                return;

            var start = fromWorld;
            if (travelledBeforeSegment < ignoreStartDistance && segmentDistance > 0.0001f)
            {
                var trim = (ignoreStartDistance - travelledBeforeSegment) / segmentDistance;
                start = Vector2.Lerp(fromWorld, toWorld, Mathf.Clamp01(trim));
            }

            MarkSegmentCells(grid, start, toWorld, output);
        }

        private static void MarkSegmentCells(GridMap grid, Vector2 fromWorld, Vector2 toWorld, HashSet<Vector2Int> output)
        {
            var fromPixel = CoordinateUtil.WorldToPixel(fromWorld);
            var toPixel = CoordinateUtil.WorldToPixel(toWorld);
            var distance = Vector2.Distance(fromPixel, toPixel);
            var step = Mathf.Max(8f, GameConfig.CellSize * 0.25f);
            var steps = Mathf.Max(1, Mathf.CeilToInt(distance / step));
            for (var i = 0; i <= steps; i++)
            {
                var pixel = Vector2.Lerp(fromPixel, toPixel, i / (float)steps);
                MarkCellAndNeighbors(grid, CoordinateUtil.PixelToWorld(pixel), output);
            }
        }

        private static void MarkCellAndNeighbors(GridMap grid, Vector2 worldPosition, HashSet<Vector2Int> output)
        {
            var cell = CoordinateUtil.PixelToCell(CoordinateUtil.WorldToPixel(worldPosition));
            if (!grid.IsInside(cell.x, cell.y))
                return;

            output.Add(cell);
            TryAdd(grid, output, cell.x + 1, cell.y);
            TryAdd(grid, output, cell.x - 1, cell.y);
            TryAdd(grid, output, cell.x, cell.y + 1);
            TryAdd(grid, output, cell.x, cell.y - 1);
        }

        private static void TryAdd(GridMap grid, HashSet<Vector2Int> output, int col, int row)
        {
            if (grid.IsInside(col, row))
                output.Add(new Vector2Int(col, row));
        }
    }
}
