using System.Collections.Generic;
using UnityEngine;
using TankTrouble.Core;
using TankTrouble.Entities;
using TankTrouble.Map;

namespace TankTrouble.AI
{
    public static class AICenterlineFollower
    {
        private const float CenterTolerance = 0.055f;
        private const float TurnCenterTolerance = 0.09f;
        private const float AxisTolerance = 0.045f;

        public static Vector2 GetPathDirection(List<Vector2Int> path, TankController tank, GridMap grid, float waypointReachDistance)
        {
            if (path == null || path.Count == 0 || tank == null)
                return Vector2.zero;

            var position = (Vector2)tank.transform.position;
            var currentCell = CoordinateUtil.PixelToCell(CoordinateUtil.WorldToPixel(position));
            while (path.Count > 0)
            {
                var waypointWorld = (Vector2)CoordinateUtil.CellToWorld(path[0].x, path[0].y);
                var closeEnough = Vector2.Distance(position, waypointWorld) <= waypointReachDistance;
                if (path[0] == currentCell && !closeEnough)
                    return DirectionTo(waypointWorld - position);
                if (path[0] != currentCell && !closeEnough)
                    break;

                path.RemoveAt(0);
            }

            if (path.Count == 0)
                return Vector2.zero;

            var nextCell = path[0];
            if (!AreAdjacent(currentCell, nextCell))
                return DirectionTo((Vector2)CoordinateUtil.CellToWorld(nextCell.x, nextCell.y) - position);

            var currentCenter = (Vector2)CoordinateUtil.CellToWorld(currentCell.x, currentCell.y);
            var step = nextCell - currentCell;
            if (RequiresTurnAtCurrentCell(currentCell, nextCell, path) && Vector2.Distance(position, currentCenter) > TurnCenterTolerance)
                return DirectionTo(currentCenter - position);

            if (step.x != 0)
            {
                var axisTarget = new Vector2(position.x, currentCenter.y);
                if (Mathf.Abs(position.y - currentCenter.y) > AxisTolerance)
                    return DirectionTo(axisTarget - position);

                var nextCenter = (Vector2)CoordinateUtil.CellToWorld(nextCell.x, nextCell.y);
                return DirectionTo(new Vector2(nextCenter.x, currentCenter.y) - position);
            }

            if (step.y != 0)
            {
                var axisTarget = new Vector2(currentCenter.x, position.y);
                if (Mathf.Abs(position.x - currentCenter.x) > AxisTolerance)
                    return DirectionTo(axisTarget - position);

                var nextCenter = (Vector2)CoordinateUtil.CellToWorld(nextCell.x, nextCell.y);
                return DirectionTo(new Vector2(currentCenter.x, nextCenter.y) - position);
            }

            return Vector2.zero;
        }

        public static Vector2 GetRecenterDirection(TankController tank, GridMap grid, float tolerance = CenterTolerance)
        {
            if (tank == null)
                return Vector2.zero;

            var position = (Vector2)tank.transform.position;
            var currentCell = CoordinateUtil.PixelToCell(CoordinateUtil.WorldToPixel(position));
            if (grid != null && !grid.IsInside(currentCell.x, currentCell.y))
                return Vector2.zero;

            var center = (Vector2)CoordinateUtil.CellToWorld(currentCell.x, currentCell.y);
            var delta = center - position;
            return delta.magnitude > tolerance ? delta.normalized : Vector2.zero;
        }

        public static bool IsCentered(TankController tank, GridMap grid, float tolerance = CenterTolerance)
        {
            return GetRecenterDirection(tank, grid, tolerance) == Vector2.zero;
        }

        private static bool RequiresTurnAtCurrentCell(Vector2Int currentCell, Vector2Int nextCell, IReadOnlyList<Vector2Int> path)
        {
            if (path == null || path.Count < 2)
                return false;

            var currentStep = nextCell - currentCell;
            var nextStep = path[1] - nextCell;
            return currentStep != nextStep;
        }

        private static bool AreAdjacent(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1;
        }

        private static Vector2 DirectionTo(Vector2 vector)
        {
            return vector.sqrMagnitude > 0.0001f ? vector.normalized : Vector2.zero;
        }
    }
}
