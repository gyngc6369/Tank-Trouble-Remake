using System.Collections.Generic;
using UnityEngine;
using TankTrouble.Core;
using TankTrouble.Entities;
using TankTrouble.Map;

namespace TankTrouble.AI
{
    public sealed class AICenterTaskFollower
    {
        private const float CenterTolerance = 0.055f;
        private const float TargetCenterTolerance = 0.075f;
        private const float AxisTolerance = 0.045f;
        private const float LocalCorrectionTolerance = 0.11f;
        private const float LargeCenterOffset = 0.14f;
        private const float AlignToleranceDegrees = 6f;
        private const float MoveTurnAngleDegrees = 18f;
        private const float TurnInPlaceAngleDegrees = 36f;
        private const float ProgressEpsilon = 0.0025f;
        private const float RotationProgressEpsilon = 0.2f;
        private const float BlockProbeTime = 0.18f;
        private const float BlockedDuration = 0.82f;

        private enum CenterTaskState
        {
            Idle,
            MoveToCurrentCenter,
            AlignToAxis,
            AlignToNextCenter,
            DriveToNextCenter,
            Blocked
        }

        private readonly List<Vector2Int> path = new List<Vector2Int>(32);
        private CenterTaskState state;
        private Vector2Int goalCell;
        private Vector2Int committedTargetCell;
        private Vector2 lastPosition;
        private float lastRotation;
        private bool hasLastPose;
        private bool canReplan = true;
        private float blockedTimer;
        private Vector2 lastDesiredDirection;
        private Vector2 recoverDirection;

        public bool HasPath => path.Count > 0;
        public bool IsBlocked => state == CenterTaskState.Blocked;
        public bool CanReplan => canReplan || state == CenterTaskState.Idle || state == CenterTaskState.Blocked || path.Count == 0;
        public Vector2Int GoalCell => goalCell;
        public int RemainingWaypoints => path.Count;
        public Vector2 LastDesiredDirection => lastDesiredDirection;
        public Vector2 RecoverDirection => recoverDirection;

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

            state = path.Count > 0 ? CenterTaskState.MoveToCurrentCenter : CenterTaskState.Idle;
            canReplan = path.Count == 0;
        }

        public void Clear()
        {
            path.Clear();
            state = CenterTaskState.Idle;
            goalCell = default;
            committedTargetCell = default;
            hasLastPose = false;
            canReplan = true;
            blockedTimer = 0f;
            lastDesiredDirection = Vector2.zero;
            recoverDirection = Vector2.zero;
        }

        public TankInputCommand BuildCommand(TankController tank, GridMap grid, float dt)
        {
            if (tank == null || grid == null || path.Count == 0)
            {
                ClearProgress();
                canReplan = true;
                return TankInputCommand.None;
            }

            var position = (Vector2)tank.transform.position;
            var currentCell = CoordinateUtil.PixelToCell(CoordinateUtil.WorldToPixel(position));
            if (!grid.IsInside(currentCell.x, currentCell.y))
            {
                ClearProgress();
                canReplan = true;
                return TankInputCommand.None;
            }

            NormalizePathForCurrentCell(position, currentCell);
            if (path.Count == 0)
            {
                ClearProgress();
                canReplan = true;
                return TankInputCommand.None;
            }

            if (state == CenterTaskState.Blocked)
            {
                canReplan = true;
                return TankInputCommand.None;
            }

            var command = BuildStateCommand(tank, position, currentCell);
            UpdateProgress(tank, command, position, dt);
            return command;
        }

        public Vector2 PeekDirection(TankController tank, GridMap grid)
        {
            if (tank == null || grid == null || path.Count == 0)
                return Vector2.zero;

            if (lastDesiredDirection.sqrMagnitude > 0.0001f)
                return lastDesiredDirection;

            var position = (Vector2)tank.transform.position;
            var currentCell = CoordinateUtil.PixelToCell(CoordinateUtil.WorldToPixel(position));
            if (!grid.IsInside(currentCell.x, currentCell.y))
                return Vector2.zero;

            NormalizePathForCurrentCell(position, currentCell);
            if (path.Count == 0)
                return Vector2.zero;

            var currentCenter = (Vector2)CoordinateUtil.CellToWorld(currentCell.x, currentCell.y);
            var targetCell = path[0];
            if (targetCell == currentCell)
                return DirectionTo(currentCenter - position);

            if (!AreAdjacent(currentCell, targetCell))
                return DirectionTo(currentCenter - position);

            if (state == CenterTaskState.MoveToCurrentCenter)
                return DirectionTo(currentCenter - position);

            if (state == CenterTaskState.AlignToAxis)
                return GetAxisCorrectionDirection(position, currentCell, targetCell);

            return GetMoveAlongAxisDirection(position, currentCell, targetCell);
        }

        public bool TryGetLocalCorrectionDirection(TankController tank, GridMap grid, out Vector2 direction)
        {
            direction = Vector2.zero;
            if (tank == null || grid == null || path.Count == 0)
                return false;

            var position = (Vector2)tank.transform.position;
            var currentCell = CoordinateUtil.PixelToCell(CoordinateUtil.WorldToPixel(position));
            if (!grid.IsInside(currentCell.x, currentCell.y))
                return false;

            var currentCenter = (Vector2)CoordinateUtil.CellToWorld(currentCell.x, currentCell.y);
            var offset = currentCenter - position;
            if (offset.sqrMagnitude <= LocalCorrectionTolerance * LocalCorrectionTolerance)
                return false;

            direction = DirectionTo(offset);
            return direction != Vector2.zero;
        }

        private TankInputCommand BuildStateCommand(TankController tank, Vector2 position, Vector2Int currentCell)
        {
            var currentCenter = (Vector2)CoordinateUtil.CellToWorld(currentCell.x, currentCell.y);
            var targetCell = path[0];

            if (targetCell == currentCell)
                return BuildMoveToCurrentCenterCommand(tank, position, currentCell);

            if (!AreAdjacent(currentCell, targetCell))
                return BuildInvalidPathCommand(tank, position, currentCenter);

            var centerOffset = currentCenter - position;
            var centerDistance = centerOffset.magnitude;
            var nearCenter = centerDistance <= CenterTolerance;
            canReplan = nearCenter && state != CenterTaskState.DriveToNextCenter;

            if (state != CenterTaskState.DriveToNextCenter && centerDistance > LargeCenterOffset)
            {
                state = CenterTaskState.MoveToCurrentCenter;
                lastDesiredDirection = DirectionTo(centerOffset);
                return BuildDriveCommand(tank, lastDesiredDirection, allowMove: true);
            }

            var axisOffset = GetAxisOffset(position, currentCenter, currentCell, targetCell);
            if (!nearCenter && Mathf.Abs(axisOffset) > AxisTolerance)
            {
                state = CenterTaskState.AlignToAxis;
                lastDesiredDirection = GetAxisCorrectionDirection(position, currentCell, targetCell);
                return BuildDriveCommand(tank, lastDesiredDirection, allowMove: true);
            }

            var segmentDirection = GetWorldDirection(currentCell, targetCell);
            if (state != CenterTaskState.DriveToNextCenter)
            {
                state = CenterTaskState.AlignToNextCenter;
                lastDesiredDirection = segmentDirection;
                var angle = Mathf.Abs(Vector2.SignedAngle(tank.VelocityForward, segmentDirection));
                if (angle > AlignToleranceDegrees)
                    return BuildDriveCommand(tank, segmentDirection, allowMove: false);

                state = CenterTaskState.DriveToNextCenter;
                committedTargetCell = targetCell;
                canReplan = false;
            }

            if (committedTargetCell != targetCell)
                committedTargetCell = targetCell;

            lastDesiredDirection = GetMoveAlongAxisDirection(position, currentCell, committedTargetCell);
            return BuildDriveCommand(tank, lastDesiredDirection, allowMove: true);
        }

        private TankInputCommand BuildMoveToCurrentCenterCommand(TankController tank, Vector2 position, Vector2Int currentCell)
        {
            var currentCenter = (Vector2)CoordinateUtil.CellToWorld(currentCell.x, currentCell.y);
            var toCenter = currentCenter - position;
            canReplan = false;

            if (toCenter.sqrMagnitude <= TargetCenterTolerance * TargetCenterTolerance)
            {
                path.RemoveAt(0);
                state = path.Count > 0 ? CenterTaskState.AlignToNextCenter : CenterTaskState.Idle;
                committedTargetCell = default;
                canReplan = true;
                blockedTimer = 0f;
                lastDesiredDirection = Vector2.zero;
                recoverDirection = Vector2.zero;
                return path.Count > 0 ? BuildStateCommand(tank, position, currentCell) : TankInputCommand.None;
            }

            state = CenterTaskState.MoveToCurrentCenter;
            lastDesiredDirection = DirectionTo(toCenter);
            return BuildDriveCommand(tank, lastDesiredDirection, allowMove: true);
        }

        private TankInputCommand BuildInvalidPathCommand(TankController tank, Vector2 position, Vector2 currentCenter)
        {
            var toCenter = currentCenter - position;
            if (toCenter.sqrMagnitude > CenterTolerance * CenterTolerance)
            {
                state = CenterTaskState.MoveToCurrentCenter;
                canReplan = false;
                lastDesiredDirection = DirectionTo(toCenter);
                return BuildDriveCommand(tank, lastDesiredDirection, allowMove: true);
            }

            state = CenterTaskState.Blocked;
            canReplan = true;
            recoverDirection = lastDesiredDirection.sqrMagnitude > 0.0001f ? -lastDesiredDirection.normalized : Vector2.zero;
            return TankInputCommand.None;
        }

        private void NormalizePathForCurrentCell(Vector2 position, Vector2Int currentCell)
        {
            while (path.Count > 0)
            {
                var waypoint = path[0];
                var waypointCenter = (Vector2)CoordinateUtil.CellToWorld(waypoint.x, waypoint.y);
                var reached = waypoint == currentCell
                    ? Vector2.Distance(position, waypointCenter) <= TargetCenterTolerance
                    : Vector2.Distance(position, waypointCenter) <= TargetCenterTolerance * 0.8f;

                if (!reached)
                    break;

                path.RemoveAt(0);
                state = path.Count > 0 ? CenterTaskState.AlignToNextCenter : CenterTaskState.Idle;
                committedTargetCell = default;
                blockedTimer = 0f;
                lastDesiredDirection = Vector2.zero;
                recoverDirection = Vector2.zero;
            }

            if (path.Count == 0)
                return;

            var currentIndex = path.IndexOf(currentCell);
            if (currentIndex > 0)
            {
                path.RemoveRange(0, currentIndex);
                state = CenterTaskState.MoveToCurrentCenter;
                committedTargetCell = default;
                blockedTimer = 0f;
                lastDesiredDirection = Vector2.zero;
                recoverDirection = Vector2.zero;
            }

            if (path.Count == 0 || path[0] == currentCell || AreAdjacent(currentCell, path[0]))
                return;

            for (var i = 1; i < path.Count; i++)
            {
                if (!AreAdjacent(currentCell, path[i]))
                    continue;

                path.RemoveRange(0, i);
                state = CenterTaskState.AlignToNextCenter;
                committedTargetCell = default;
                blockedTimer = 0f;
                lastDesiredDirection = Vector2.zero;
                recoverDirection = Vector2.zero;
                return;
            }
        }

        private static float GetAxisOffset(Vector2 position, Vector2 currentCenter, Vector2Int currentCell, Vector2Int targetCell)
        {
            var step = targetCell - currentCell;
            return step.x != 0 ? position.y - currentCenter.y : position.x - currentCenter.x;
        }

        private static Vector2 GetAxisCorrectionDirection(Vector2 position, Vector2Int currentCell, Vector2Int targetCell)
        {
            var currentCenter = (Vector2)CoordinateUtil.CellToWorld(currentCell.x, currentCell.y);
            var step = targetCell - currentCell;
            var target = step.x != 0
                ? new Vector2(position.x, currentCenter.y)
                : new Vector2(currentCenter.x, position.y);

            return DirectionTo(target - position);
        }

        private static Vector2 GetMoveAlongAxisDirection(Vector2 position, Vector2Int currentCell, Vector2Int targetCell)
        {
            var currentCenter = (Vector2)CoordinateUtil.CellToWorld(currentCell.x, currentCell.y);
            var targetCenter = (Vector2)CoordinateUtil.CellToWorld(targetCell.x, targetCell.y);
            var step = targetCell - currentCell;
            if (step.x != 0)
                return DirectionTo(new Vector2(targetCenter.x, currentCenter.y) - position);

            if (step.y != 0)
                return DirectionTo(new Vector2(currentCenter.x, targetCenter.y) - position);

            return DirectionTo(currentCenter - position);
        }

        private TankInputCommand BuildDriveCommand(TankController tank, Vector2 desiredDirection, bool allowMove)
        {
            if (tank == null || desiredDirection.sqrMagnitude < 0.0001f)
                return TankInputCommand.None;

            var desired = desiredDirection.normalized;
            var signedAngle = Vector2.SignedAngle(tank.VelocityForward, desired);
            var absAngle = Mathf.Abs(signedAngle);
            var rotate = absAngle > AlignToleranceDegrees ? RotateInputForAngle(signedAngle) : 0f;
            var move = 0f;

            if (allowMove)
            {
                if (absAngle <= MoveTurnAngleDegrees)
                    move = 1f;
                else if (absAngle <= TurnInPlaceAngleDegrees)
                    move = 0.45f;
            }

            return new TankInputCommand(move, rotate, false);
        }

        private void UpdateProgress(TankController tank, TankInputCommand command, Vector2 position, float dt)
        {
            var rotation = tank != null ? tank.transform.eulerAngles.z : 0f;
            if (!hasLastPose)
            {
                hasLastPose = true;
                lastPosition = position;
                lastRotation = rotation;
                return;
            }

            var moved = Vector2.Distance(position, lastPosition);
            var rotated = Mathf.Abs(Mathf.DeltaAngle(lastRotation, rotation));
            lastPosition = position;
            lastRotation = rotation;

            var wantsMove = Mathf.Abs(command.Move) > 0.1f;
            var wantsRotate = Mathf.Abs(command.Rotate) > 0.1f;
            var movementStalled = wantsMove && moved < ProgressEpsilon;
            var rotationStalled = !wantsMove && wantsRotate && rotated < RotationProgressEpsilon;
            if ((movementStalled || rotationStalled) && tank != null && tank.IsCommandBlocked(command, BlockProbeTime))
                blockedTimer += Mathf.Max(0f, dt);
            else
                blockedTimer = 0f;

            if (blockedTimer < BlockedDuration)
                return;

            state = CenterTaskState.Blocked;
            canReplan = true;
            recoverDirection = lastDesiredDirection.sqrMagnitude > 0.0001f ? -lastDesiredDirection.normalized : Vector2.zero;
        }

        private void ClearProgress()
        {
            hasLastPose = false;
            blockedTimer = 0f;
            lastDesiredDirection = Vector2.zero;
            recoverDirection = Vector2.zero;
            committedTargetCell = default;
            if (state == CenterTaskState.Blocked)
                state = path.Count > 0 ? CenterTaskState.MoveToCurrentCenter : CenterTaskState.Idle;
        }

        private static Vector2 GetWorldDirection(Vector2Int from, Vector2Int to)
        {
            return DirectionTo((Vector2)CoordinateUtil.CellToWorld(to.x, to.y) - (Vector2)CoordinateUtil.CellToWorld(from.x, from.y));
        }

        private static bool AreAdjacent(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1;
        }

        private static Vector2 DirectionTo(Vector2 vector)
        {
            return vector.sqrMagnitude > 0.0001f ? vector.normalized : Vector2.zero;
        }

        private static float RotateInputForAngle(float signedAngle)
        {
            return signedAngle > 0f ? -1f : 1f;
        }
    }
}
