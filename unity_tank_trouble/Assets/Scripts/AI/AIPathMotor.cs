using System.Collections.Generic;
using UnityEngine;
using TankTrouble.Config;
using TankTrouble.Core;
using TankTrouble.Entities;
using TankTrouble.Map;

namespace TankTrouble.AI
{
    public sealed class AIPathMotor
    {
        private const float CenterTolerance = 0.055f;
        private const float WaypointTolerance = 0.075f;
        private const float AlignToleranceDegrees = 6f;
        private const float MoveAlignToleranceDegrees = 8f;
        private const float RotateTimeout = 0.75f;
        private const float RecenterTimeout = 1.0f;
        private const float ForwardTimeoutPadding = 0.75f;
        private const float NoProgressTimeout = 0.48f;
        private const float ProgressEpsilon = 0.008f;
        private const float RotationLockDuration = 0.28f;

        private enum MotorState
        {
            Idle,
            MoveToCellCenter,
            RotateToNextCell,
            DriveToNextCell,
            Blocked
        }

        private readonly List<Vector2Int> waypoints = new List<Vector2Int>(32);
        private MotorState state;
        private Vector2Int segmentFrom;
        private Vector2Int segmentTo;
        private float taskTimer;
        private float noProgressTimer;
        private float bestDistanceToTaskTarget;
        private float rotationLockTimer;
        private float lockedRotateInput;
        private bool hasRotationLock;
        private bool failed;
        private AIGridEdge failedEdge;

        public bool HasPath => waypoints.Count > 0;
        public bool Failed => failed;
        public AIGridEdge FailedEdge => failedEdge;
        public int RemainingWaypoints => waypoints.Count;
        public bool IsIdle => state == MotorState.Idle && waypoints.Count == 0;

        public void SetPath(IReadOnlyList<Vector2Int> path)
        {
            Clear();
            if (path == null)
                return;

            for (var i = 0; i < path.Count; i++)
                waypoints.Add(path[i]);

            state = waypoints.Count > 0 ? MotorState.MoveToCellCenter : MotorState.Idle;
        }

        public void Clear()
        {
            waypoints.Clear();
            state = MotorState.Idle;
            segmentFrom = default;
            segmentTo = default;
            taskTimer = 0f;
            noProgressTimer = 0f;
            bestDistanceToTaskTarget = float.PositiveInfinity;
            rotationLockTimer = 0f;
            lockedRotateInput = 0f;
            hasRotationLock = false;
            failed = false;
            failedEdge = default;
        }

        public TankInputCommand Tick(TankController tank, GridMap grid, float dt)
        {
            if (tank == null || grid == null || waypoints.Count == 0)
            {
                ResetTask(MotorState.Idle);
                return TankInputCommand.None;
            }

            if (state == MotorState.Blocked)
                return TankInputCommand.None;

            var position = (Vector2)tank.transform.position;
            var currentCell = CoordinateUtil.PixelToCell(CoordinateUtil.WorldToPixel(position));
            if (!grid.IsInside(currentCell.x, currentCell.y))
            {
                MarkFailed(default);
                return TankInputCommand.None;
            }

            ConsumeReachedWaypoints(position);
            if (waypoints.Count == 0)
            {
                ResetTask(MotorState.Idle);
                return TankInputCommand.None;
            }

            if (state == MotorState.Idle)
                ResetTask(MotorState.MoveToCellCenter);

            var targetCell = waypoints[0];
            if (targetCell == currentCell)
                return MoveToPoint(tank, position, (Vector2)CoordinateUtil.CellToWorld(targetCell.x, targetCell.y), dt, default, RecenterTimeout);

            if (!AIGeometryUtils.AreAdjacent(currentCell, targetCell))
            {
                MarkFailed(default);
                return TankInputCommand.None;
            }

            if (state == MotorState.MoveToCellCenter)
            {
                var currentCenter = (Vector2)CoordinateUtil.CellToWorld(currentCell.x, currentCell.y);
                if (Vector2.Distance(position, currentCenter) > CenterTolerance)
                    return MoveToPoint(tank, position, currentCenter, dt, new AIGridEdge(currentCell, targetCell), RecenterTimeout);

                ResetTask(MotorState.RotateToNextCell);
            }

            if (state == MotorState.RotateToNextCell)
                return RotateTowardNextCell(tank, currentCell, targetCell, dt);

            if (state == MotorState.DriveToNextCell)
                return DriveToNextCell(tank, position, dt);

            return TankInputCommand.None;
        }

        private TankInputCommand RotateTowardNextCell(TankController tank, Vector2Int currentCell, Vector2Int targetCell, float dt)
        {
            segmentFrom = currentCell;
            segmentTo = targetCell;
            var direction = AIGridDirections.ToWorld(AIGridDirections.FromDelta(targetCell - currentCell));
            var signedAngle = Vector2.SignedAngle(tank.VelocityForward, direction);
            var absAngle = Mathf.Abs(signedAngle);
            if (absAngle <= AlignToleranceDegrees)
            {
                ResetTask(MotorState.DriveToNextCell);
                segmentFrom = currentCell;
                segmentTo = targetCell;
                bestDistanceToTaskTarget = Vector2.Distance(tank.transform.position, CoordinateUtil.CellToWorld(targetCell.x, targetCell.y));
                return DriveToNextCell(tank, tank.transform.position, dt);
            }

            taskTimer += Mathf.Max(0f, dt);
            if (taskTimer >= RotateTimeout)
            {
                MarkFailed(new AIGridEdge(currentCell, targetCell));
                return TankInputCommand.None;
            }

            var rotate = GetLockedRotateInput(signedAngle, dt);
            return new TankInputCommand(0f, rotate, false);
        }

        private TankInputCommand DriveToNextCell(TankController tank, Vector2 position, float dt)
        {
            var targetCenter = (Vector2)CoordinateUtil.CellToWorld(segmentTo.x, segmentTo.y);
            var distance = Vector2.Distance(position, targetCenter);
            if (distance <= WaypointTolerance)
            {
                ConsumeReachedWaypoints(position);
                ResetTask(waypoints.Count > 0 ? MotorState.MoveToCellCenter : MotorState.Idle);
                return TankInputCommand.None;
            }

            taskTimer += Mathf.Max(0f, dt);
            var speedUnits = GameConfig.TankSpeed / CoordinateUtil.PixelsPerUnit;
            var expectedDuration = Vector2.Distance(
                CoordinateUtil.CellToWorld(segmentFrom.x, segmentFrom.y),
                CoordinateUtil.CellToWorld(segmentTo.x, segmentTo.y)) / speedUnits + ForwardTimeoutPadding;

            UpdateProgress(distance, dt);
            if (taskTimer >= expectedDuration || noProgressTimer >= NoProgressTimeout)
            {
                MarkFailed(new AIGridEdge(segmentFrom, segmentTo));
                return TankInputCommand.None;
            }

            return new TankInputCommand(1f, 0f, false);
        }

        private TankInputCommand MoveToPoint(TankController tank, Vector2 position, Vector2 target, float dt, AIGridEdge failureEdge, float timeout)
        {
            var offset = target - position;
            var distance = offset.magnitude;
            if (distance <= CenterTolerance)
            {
                ResetTask(MotorState.RotateToNextCell);
                return TankInputCommand.None;
            }

            taskTimer += Mathf.Max(0f, dt);
            UpdateProgress(distance, dt);
            if (taskTimer >= timeout || noProgressTimer >= NoProgressTimeout)
            {
                MarkFailed(failureEdge);
                return TankInputCommand.None;
            }

            var desired = offset.normalized;
            var signedAngle = Vector2.SignedAngle(tank.VelocityForward, desired);
            var absAngle = Mathf.Abs(signedAngle);
            if (absAngle > MoveAlignToleranceDegrees)
                return new TankInputCommand(0f, GetLockedRotateInput(signedAngle, dt), false);

            ClearRotationLock();
            return new TankInputCommand(0.85f, 0f, false);
        }

        private void ConsumeReachedWaypoints(Vector2 position)
        {
            while (waypoints.Count > 0)
            {
                var target = waypoints[0];
                var targetCenter = (Vector2)CoordinateUtil.CellToWorld(target.x, target.y);
                if (Vector2.Distance(position, targetCenter) > WaypointTolerance)
                    return;

                waypoints.RemoveAt(0);
                ResetTask(waypoints.Count > 0 ? MotorState.MoveToCellCenter : MotorState.Idle);
            }
        }

        private void UpdateProgress(float distance, float dt)
        {
            if (distance + ProgressEpsilon < bestDistanceToTaskTarget)
            {
                bestDistanceToTaskTarget = distance;
                noProgressTimer = 0f;
                return;
            }

            noProgressTimer += Mathf.Max(0f, dt);
        }

        private void ResetTask(MotorState nextState)
        {
            state = nextState;
            taskTimer = 0f;
            noProgressTimer = 0f;
            bestDistanceToTaskTarget = float.PositiveInfinity;
            ClearRotationLock();
        }

        private void MarkFailed(AIGridEdge edge)
        {
            failed = true;
            failedEdge = edge;
            state = MotorState.Blocked;
            taskTimer = 0f;
            noProgressTimer = 0f;
            ClearRotationLock();
        }

        private float GetLockedRotateInput(float signedAngle, float dt)
        {
            var desired = AIGeometryUtils.RotateInputForAngle(signedAngle);
            rotationLockTimer = Mathf.Max(0f, rotationLockTimer - Mathf.Max(0f, dt));
            if (!hasRotationLock || rotationLockTimer <= 0f)
            {
                lockedRotateInput = desired;
                hasRotationLock = true;
                rotationLockTimer = RotationLockDuration;
            }

            return lockedRotateInput;
        }

        private void ClearRotationLock()
        {
            hasRotationLock = false;
            rotationLockTimer = 0f;
            lockedRotateInput = 0f;
        }
    }
}
