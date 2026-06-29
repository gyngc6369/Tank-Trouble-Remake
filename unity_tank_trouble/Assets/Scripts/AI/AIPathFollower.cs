using System.Collections.Generic;
using UnityEngine;
using TankTrouble.Core;
using TankTrouble.Entities;
using TankTrouble.Map;

namespace TankTrouble.AI
{
    public static class AIPathFollower
    {
        private static readonly List<Vector2Int> ScratchNeighbors = new List<Vector2Int>(4);

        public static Vector2 GetPathDirection(List<Vector2Int> path, TankController tank, float waypointReachDistance, float waypointLookAheadDistance)
        {
            if (path == null || path.Count == 0 || tank == null)
                return Vector2.zero;

            var currentCell = CoordinateUtil.PixelToCell(CoordinateUtil.WorldToPixel(tank.transform.position));
            while (path.Count > 0)
            {
                var waypointWorld = CoordinateUtil.CellToWorld(path[0].x, path[0].y);
                var closeEnough = Vector2.Distance(tank.transform.position, waypointWorld) <= waypointReachDistance;
                if (path[0] != currentCell && !closeEnough)
                    break;

                path.RemoveAt(0);
            }

            if (path.Count == 0)
                return Vector2.zero;

            var targetIndex = 0;
            if (path.Count > 1)
            {
                var firstWorld = CoordinateUtil.CellToWorld(path[0].x, path[0].y);
                if (Vector2.Distance(tank.transform.position, firstWorld) <= waypointLookAheadDistance)
                    targetIndex = 1;
            }

            var targetWorld = CoordinateUtil.CellToWorld(path[targetIndex].x, path[targetIndex].y);
            var direction = (Vector2)targetWorld - (Vector2)tank.transform.position;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.zero;
        }

        public static Vector2 GetBestOpenNeighborDirection(GridMap grid, TankController tank, IReadOnlyList<TankController> enemies, HashSet<Vector2Int> dangerCells)
        {
            if (grid == null || tank == null)
                return Vector2.zero;

            var currentCell = CoordinateUtil.PixelToCell(CoordinateUtil.WorldToPixel(tank.transform.position));
            if (!grid.IsInside(currentCell.x, currentCell.y))
                return Vector2.zero;

            grid.FillNeighbors(currentCell.x, currentCell.y, ScratchNeighbors);
            if (ScratchNeighbors.Count == 0)
                return Vector2.zero;

            var enemyDirection = GetNearestEnemyDirection(tank, enemies);
            var bestScore = float.NegativeInfinity;
            var bestDirection = Vector2.zero;
            for (var i = 0; i < ScratchNeighbors.Count; i++)
            {
                var neighbor = ScratchNeighbors[i];
                if (dangerCells != null && dangerCells.Contains(neighbor))
                    continue;

                var targetWorld = (Vector2)CoordinateUtil.CellToWorld(neighbor.x, neighbor.y);
                var direction = targetWorld - (Vector2)tank.transform.position;
                if (direction.sqrMagnitude < 0.0001f)
                    continue;

                direction.Normalize();
                var score = enemyDirection != Vector2.zero ? Vector2.Dot(direction, enemyDirection) : 0f;
                score += Vector2.Dot(direction, tank.VelocityForward) * 0.12f;
                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestDirection = direction;
            }

            return bestDirection;
        }

        private static Vector2 GetNearestEnemyDirection(TankController tank, IReadOnlyList<TankController> enemies)
        {
            var bestDistance = float.MaxValue;
            TankController best = null;
            if (enemies == null)
                return Vector2.zero;

            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.Alive)
                    continue;

                var distance = Vector2.SqrMagnitude(enemy.transform.position - tank.transform.position);
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                best = enemy;
            }

            return best != null ? ((Vector2)best.transform.position - (Vector2)tank.transform.position).normalized : Vector2.zero;
        }
    }
}
