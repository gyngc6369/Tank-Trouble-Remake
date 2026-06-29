using System.Collections.Generic;
using UnityEngine;
using TankTrouble.Core;
using TankTrouble.Entities;
using TankTrouble.Map;

namespace TankTrouble.AI
{
    public sealed class AIPathExecutor
    {
        private const float AxisTolerance = 0.075f;
        private const float TurnCenterTolerance = 0.095f;
        private const float LocalCorrectionTolerance = 0.11f;
        private const float ProgressEpsilon = 0.003f;
        private const float BlockedDuration = 0.72f;

        private readonly List<Vector2Int> path = new List<Vector2Int>(32);
        private Vector2 lastPosition;
        private Vector2 lastDesiredDirection;
        private Vector2 recoverDirection;
        private Vector2Int goalCell;
        private bool hasLastPosition;
        private bool blocked;
        private float blockedTimer;

        public bool HasPath => path.Count > 0;
        public bool IsBlocked => blocked;
        public Vector2 LastDesiredDirection => lastDesiredDirection;
        public Vector2 RecoverDirection => recoverDirection;
        public Vector2Int GoalCell => goalCell;
        public int RemainingWaypoints => path.Count;

        public void SetPath(IReadOnlyList<Vector2Int> source, Vector2Int currentCell, Vector2Int goal)
        {
            Clear();
            goalCell = goal;
            if (source == null)
                return;

            for (var i = 0; i < source.Count; i++)
            {
                var cell = source[i];
                if (i == 0 && cell == currentCell)
                    continue;
                path.Add(cell);
            }
        }

        public void Clear()
        {
            path.Clear();
            hasLastPosition = false;
            blocked = false;
            blockedTimer = 0f;
            lastDesiredDirection = Vector2.zero;
            recoverDirection = Vector2.zero;
            goalCell = default;
        }

        public Vector2 GetDirection(TankController tank, GridMap grid, float waypointReachDistance, float dt)
        {
            if (tank == null || path.Count == 0)
            {
                ClearProgress();
                return Vector2.zero;
            }

            var position = (Vector2)tank.transform.position;
            var currentCell = CoordinateUtil.PixelToCell(CoordinateUtil.WorldToPixel(position));
            if (grid != null && !grid.IsInside(currentCell.x, currentCell.y))
            {
                ClearProgress();
                return Vector2.zero;
            }

            PruneReachedWaypoints(position, currentCell, waypointReachDistance);
            if (path.Count == 0)
            {
                ClearProgress();
                return Vector2.zero;
            }

            var direction = CalculateDirection(position, currentCell);
            UpdateProgress(position, direction, dt);
            lastDesiredDirection = direction;
            return direction;
        }

        public Vector2 PeekDirection(TankController tank, GridMap grid)
        {
            if (tank == null)
                return Vector2.zero;
            if (lastDesiredDirection.sqrMagnitude > 0.0001f)
                return lastDesiredDirection;
            if (path.Count == 0)
                return Vector2.zero;

            var position = (Vector2)tank.transform.position;
            var currentCell = CoordinateUtil.PixelToCell(CoordinateUtil.WorldToPixel(position));
            if (grid != null && !grid.IsInside(currentCell.x, currentCell.y))
                return Vector2.zero;

            return CalculateDirection(position, currentCell);
        }

        public bool TryGetLocalCorrectionDirection(TankController tank, GridMap grid, out Vector2 direction)
        {
            direction = Vector2.zero;
            if (tank == null || path.Count == 0)
                return false;

            var position = (Vector2)tank.transform.position;
            var currentCell = CoordinateUtil.PixelToCell(CoordinateUtil.WorldToPixel(position));
            if (grid != null && !grid.IsInside(currentCell.x, currentCell.y))
                return false;

            var currentCenter = (Vector2)CoordinateUtil.CellToWorld(currentCell.x, currentCell.y);
            var offset = currentCenter - position;
            if (offset.sqrMagnitude <= LocalCorrectionTolerance * LocalCorrectionTolerance)
                return false;

            direction = DirectionTo(offset);
            return direction != Vector2.zero;
        }

        private void PruneReachedWaypoints(Vector2 position, Vector2Int currentCell, float waypointReachDistance)
        {
            while (path.Count > 0)
            {
                var waypoint = path[0];
                var waypointWorld = (Vector2)CoordinateUtil.CellToWorld(waypoint.x, waypoint.y);
                var closeEnough = Vector2.Distance(position, waypointWorld) <= waypointReachDistance;
                if (waypoint == currentCell && !closeEnough)
                    return;
                if (waypoint != currentCell && !closeEnough)
                    return;

                path.RemoveAt(0);
                blocked = false;
                blockedTimer = 0f;
            }
        }

        private Vector2 CalculateDirection(Vector2 position, Vector2Int currentCell)
        {
            var targetCell = path[0];
            if (!AreAdjacent(currentCell, targetCell))
                return DirectionTo((Vector2)CoordinateUtil.CellToWorld(targetCell.x, targetCell.y) - position);

            var currentCenter = (Vector2)CoordinateUtil.CellToWorld(currentCell.x, currentCell.y);
            var step = targetCell - currentCell;
            if (RequiresTurnAtTarget(currentCell, targetCell) && Vector2.Distance(position, currentCenter) > TurnCenterTolerance)
                return DirectionTo(currentCenter - position);

            if (step.x != 0)
            {
                if (Mathf.Abs(position.y - currentCenter.y) > AxisTolerance)
                    return DirectionTo(new Vector2(position.x, currentCenter.y) - position);

                var targetCenter = (Vector2)CoordinateUtil.CellToWorld(targetCell.x, targetCell.y);
                return DirectionTo(new Vector2(targetCenter.x, currentCenter.y) - position);
            }

            if (step.y != 0)
            {
                if (Mathf.Abs(position.x - currentCenter.x) > AxisTolerance)
                    return DirectionTo(new Vector2(currentCenter.x, position.y) - position);

                var targetCenter = (Vector2)CoordinateUtil.CellToWorld(targetCell.x, targetCell.y);
                return DirectionTo(new Vector2(currentCenter.x, targetCenter.y) - position);
            }

            return DirectionTo(currentCenter - position);
        }

        private void UpdateProgress(Vector2 position, Vector2 desiredDirection, float dt)
        {
            if (!hasLastPosition)
            {
                hasLastPosition = true;
                lastPosition = position;
                return;
            }

            var moved = Vector2.Distance(position, lastPosition);
            lastPosition = position;
            if (desiredDirection.sqrMagnitude < 0.0001f || moved >= ProgressEpsilon)
            {
                blockedTimer = 0f;
                blocked = false;
                return;
            }

            blockedTimer += Mathf.Max(0f, dt);
            if (blockedTimer < BlockedDuration)
                return;

            blocked = true;
            recoverDirection = desiredDirection.sqrMagnitude > 0.0001f ? -desiredDirection.normalized : Vector2.zero;
        }

        private void ClearProgress()
        {
            hasLastPosition = false;
            blocked = false;
            blockedTimer = 0f;
            lastDesiredDirection = Vector2.zero;
            recoverDirection = Vector2.zero;
        }

        private bool RequiresTurnAtTarget(Vector2Int currentCell, Vector2Int targetCell)
        {
            if (path.Count < 2)
                return false;

            var currentStep = targetCell - currentCell;
            var nextStep = path[1] - targetCell;
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
