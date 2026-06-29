using System.Collections.Generic;
using UnityEngine;
using TankTrouble.Core;
using TankTrouble.Entities;
using TankTrouble.Map;

namespace TankTrouble.AI
{
    public sealed class AIPathSegmentFollower
    {
        private const float CenterTolerance = 0.045f;
        private const float TargetCenterTolerance = 0.075f;
        private const float AxisTolerance = 0.045f;
        private const float LocalCorrectionTolerance = 0.11f;
        private const float AlignToleranceDegrees = 6f;
        private const float MoveTurnAngleDegrees = 18f;
        private const float TurnInPlaceAngleDegrees = 36f;
        private const float ProgressEpsilon = 0.0025f;
        private const float RotationProgressEpsilon = 0.2f;
        private const float BlockProbeTime = 0.18f;
        private const float BlockedDuration = 0.82f;
        private const float JunctionSpinFuseDuration = 0.45f;

        private enum FollowState
        {
            Idle,
            MoveToCellCenter,
            AlignToSegment,
            MoveAlongSegment,
            MoveToJunctionCenter,
            LockJunctionExit,
            FollowLockedJunctionExit,
            Blocked
        }

        private readonly List<Vector2Int> path = new List<Vector2Int>(32);
        private readonly List<Vector2Int> scratchNeighbors = new List<Vector2Int>(4);
        private FollowState state;
        private Vector2Int goalCell;
        private Vector2Int lockedJunctionCell;
        private Vector2Int lockedExitCell;
        private Vector2 lastPosition;
        private float lastRotation;
        private bool hasLastPose;
        private bool hasLockedJunctionExit;
        private float blockedTimer;
        private float junctionSpinTimer;
        private Vector2 lastDesiredDirection;
        private Vector2 recoverDirection;

        public bool HasPath => path.Count > 0;
        public bool IsBlocked => state == FollowState.Blocked;
        public bool HasLockedJunctionExit => hasLockedJunctionExit;
        public Vector2Int GoalCell => goalCell;
        public Vector2Int LockedExitCell => lockedExitCell;
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

            state = path.Count > 0 ? FollowState.MoveToCellCenter : FollowState.Idle;
        }

        public void Clear()
        {
            path.Clear();
            state = FollowState.Idle;
            goalCell = default;
            lockedJunctionCell = default;
            lockedExitCell = default;
            hasLastPose = false;
            hasLockedJunctionExit = false;
            blockedTimer = 0f;
            junctionSpinTimer = 0f;
            lastDesiredDirection = Vector2.zero;
            recoverDirection = Vector2.zero;
        }

        public TankInputCommand BuildCommand(TankController tank, GridMap grid, float dt)
        {
            if (tank == null || grid == null || path.Count == 0)
            {
                ClearProgress();
                return TankInputCommand.None;
            }

            var position = (Vector2)tank.transform.position;
            var currentCell = CoordinateUtil.PixelToCell(CoordinateUtil.WorldToPixel(position));
            if (!grid.IsInside(currentCell.x, currentCell.y))
            {
                ClearProgress();
                return TankInputCommand.None;
            }

            PruneReachedWaypoints(position, currentCell);
            if (path.Count == 0)
            {
                ClearJunctionLock();
                ClearProgress();
                return TankInputCommand.None;
            }

            if (state == FollowState.Blocked)
                return TankInputCommand.None;

            UpdateJunctionLock(grid, position, currentCell);

            var command = hasLockedJunctionExit
                ? BuildLockedJunctionCommand(tank, grid, position, currentCell)
                : BuildStateCommand(tank, grid, position, currentCell);
            UpdateProgress(tank, grid, currentCell, command, position, dt);
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

            return GetDirectionForCurrentState(position, currentCell);
        }

        public bool IsJunctionLockedTo(Vector2Int cell)
        {
            return hasLockedJunctionExit && lockedExitCell == cell;
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

        private void UpdateJunctionLock(GridMap grid, Vector2 position, Vector2Int currentCell)
        {
            if (hasLockedJunctionExit)
            {
                if (path.Count == 0)
                {
                    ClearJunctionLock();
                    return;
                }

                if (currentCell == lockedExitCell && !IsJunction(grid, currentCell))
                {
                    var exitCenter = (Vector2)CoordinateUtil.CellToWorld(currentCell.x, currentCell.y);
                    if (Vector2.Distance(position, exitCenter) <= TargetCenterTolerance)
                        ClearJunctionLock();
                }

                return;
            }

            if (!IsJunction(grid, currentCell) || path.Count == 0)
                return;

            lockedJunctionCell = currentCell;
            lockedExitCell = FindJunctionExitTarget(grid, currentCell);
            hasLockedJunctionExit = true;
            junctionSpinTimer = 0f;
            state = FollowState.MoveToJunctionCenter;
        }

        private TankInputCommand BuildLockedJunctionCommand(TankController tank, GridMap grid, Vector2 position, Vector2Int currentCell)
        {
            if (path.Count == 0)
                return TankInputCommand.None;

            var currentCenter = (Vector2)CoordinateUtil.CellToWorld(currentCell.x, currentCell.y);
            var centerOffset = currentCenter - position;
            var currentIsJunction = IsJunction(grid, currentCell);
            if ((currentIsJunction || currentCell == lockedJunctionCell) && centerOffset.sqrMagnitude > CenterTolerance * CenterTolerance)
            {
                state = FollowState.MoveToJunctionCenter;
                lastDesiredDirection = DirectionTo(centerOffset);
                return BuildDriveCommand(tank, lastDesiredDirection, allowMove: true);
            }

            var targetCell = GetNextPathStep(currentCell);
            if (targetCell == currentCell)
            {
                state = FollowState.MoveToJunctionCenter;
                lastDesiredDirection = DirectionTo(centerOffset);
                return BuildDriveCommand(tank, lastDesiredDirection, allowMove: true);
            }

            if (!AreAdjacent(currentCell, targetCell))
            {
                lastDesiredDirection = DirectionTo((Vector2)CoordinateUtil.CellToWorld(lockedExitCell.x, lockedExitCell.y) - position);
                return BuildDriveCommand(tank, lastDesiredDirection, allowMove: true);
            }

            var segmentDirection = GetWorldDirection(currentCell, targetCell);
            if (state == FollowState.MoveToJunctionCenter)
                state = FollowState.LockJunctionExit;

            if (state == FollowState.LockJunctionExit)
            {
                lastDesiredDirection = segmentDirection;
                var angle = Mathf.Abs(Vector2.SignedAngle(tank.VelocityForward, segmentDirection));
                if (angle > AlignToleranceDegrees)
                    return BuildDriveCommand(tank, segmentDirection, allowMove: junctionSpinTimer >= JunctionSpinFuseDuration);

                state = FollowState.FollowLockedJunctionExit;
            }

            lastDesiredDirection = GetMoveAlongSegmentDirection(position, currentCell, targetCell);
            return BuildDriveCommand(tank, lastDesiredDirection, allowMove: true);
        }

        private TankInputCommand BuildStateCommand(TankController tank, GridMap grid, Vector2 position, Vector2Int currentCell)
        {
            var targetCell = path[0];
            var currentCenter = (Vector2)CoordinateUtil.CellToWorld(currentCell.x, currentCell.y);

            if (targetCell == currentCell)
            {
                state = FollowState.MoveAlongSegment;
                var toCenter = currentCenter - position;
                lastDesiredDirection = DirectionTo(toCenter);
                return BuildDriveCommand(tank, lastDesiredDirection, allowMove: true);
            }

            if (!AreAdjacent(currentCell, targetCell))
            {
                state = FollowState.MoveToCellCenter;
                var targetCenter = (Vector2)CoordinateUtil.CellToWorld(targetCell.x, targetCell.y);
                lastDesiredDirection = DirectionTo(targetCenter - position);
                return BuildDriveCommand(tank, lastDesiredDirection, allowMove: true);
            }

            var segmentDirection = GetWorldDirection(currentCell, targetCell);
            if (state == FollowState.MoveToCellCenter)
            {
                var toCenter = currentCenter - position;
                if (toCenter.sqrMagnitude > CenterTolerance * CenterTolerance)
                {
                    lastDesiredDirection = DirectionTo(toCenter);
                    return BuildDriveCommand(tank, lastDesiredDirection, allowMove: true);
                }

                state = FollowState.AlignToSegment;
            }

            if (state == FollowState.AlignToSegment)
            {
                lastDesiredDirection = segmentDirection;
                var angle = Mathf.Abs(Vector2.SignedAngle(tank.VelocityForward, segmentDirection));
                if (angle > AlignToleranceDegrees)
                    return BuildDriveCommand(tank, segmentDirection, allowMove: false);

                state = FollowState.MoveAlongSegment;
            }

            lastDesiredDirection = GetMoveAlongSegmentDirection(position, currentCell, targetCell);
            return BuildDriveCommand(tank, lastDesiredDirection, allowMove: true);
        }

        private Vector2 GetDirectionForCurrentState(Vector2 position, Vector2Int currentCell)
        {
            if (path.Count == 0)
                return Vector2.zero;

            var targetCell = path[0];
            if (targetCell == currentCell)
                return DirectionTo((Vector2)CoordinateUtil.CellToWorld(currentCell.x, currentCell.y) - position);

            if (!AreAdjacent(currentCell, targetCell))
                return DirectionTo((Vector2)CoordinateUtil.CellToWorld(targetCell.x, targetCell.y) - position);

            if (state == FollowState.MoveToCellCenter)
                return DirectionTo((Vector2)CoordinateUtil.CellToWorld(currentCell.x, currentCell.y) - position);

            return state == FollowState.AlignToSegment
                ? GetWorldDirection(currentCell, targetCell)
                : GetMoveAlongSegmentDirection(position, currentCell, targetCell);
        }

        private void PruneReachedWaypoints(Vector2 position, Vector2Int currentCell)
        {
            while (path.Count > 0)
            {
                var waypoint = path[0];
                var waypointWorld = (Vector2)CoordinateUtil.CellToWorld(waypoint.x, waypoint.y);
                var reached = waypoint == currentCell
                    ? Vector2.Distance(position, waypointWorld) <= TargetCenterTolerance
                    : Vector2.Distance(position, waypointWorld) <= TargetCenterTolerance * 0.8f;

                if (!reached)
                    return;

                path.RemoveAt(0);
                state = path.Count > 0
                    ? hasLockedJunctionExit ? FollowState.MoveToJunctionCenter : FollowState.MoveToCellCenter
                    : FollowState.Idle;
                blockedTimer = 0f;
                junctionSpinTimer = 0f;
                lastDesiredDirection = Vector2.zero;
                recoverDirection = Vector2.zero;
            }
        }

        private Vector2 GetMoveAlongSegmentDirection(Vector2 position, Vector2Int currentCell, Vector2Int targetCell)
        {
            var currentCenter = (Vector2)CoordinateUtil.CellToWorld(currentCell.x, currentCell.y);
            var targetCenter = (Vector2)CoordinateUtil.CellToWorld(targetCell.x, targetCell.y);
            var step = targetCell - currentCell;

            if (step.x != 0)
            {
                var target = new Vector2(targetCenter.x, currentCenter.y);
                if (Mathf.Abs(position.y - currentCenter.y) > AxisTolerance)
                    target.y = currentCenter.y;

                return DirectionTo(target - position);
            }

            if (step.y != 0)
            {
                var target = new Vector2(currentCenter.x, targetCenter.y);
                if (Mathf.Abs(position.x - currentCenter.x) > AxisTolerance)
                    target.x = currentCenter.x;

                return DirectionTo(target - position);
            }

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

        private void UpdateProgress(TankController tank, GridMap grid, Vector2Int currentCell, TankInputCommand command, Vector2 position, float dt)
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

            if (hasLockedJunctionExit
                && IsJunction(grid, currentCell)
                && wantsRotate
                && !wantsMove
                && moved < ProgressEpsilon * 2f)
            {
                junctionSpinTimer += Mathf.Max(0f, dt);
            }
            else if (!hasLockedJunctionExit || !IsJunction(grid, currentCell) || wantsMove)
            {
                junctionSpinTimer = 0f;
            }

            if (blockedTimer < BlockedDuration)
                return;

            state = FollowState.Blocked;
            recoverDirection = lastDesiredDirection.sqrMagnitude > 0.0001f ? -lastDesiredDirection.normalized : Vector2.zero;
            ClearJunctionLock();
        }

        private void ClearProgress()
        {
            hasLastPose = false;
            blockedTimer = 0f;
            junctionSpinTimer = 0f;
            lastDesiredDirection = Vector2.zero;
            recoverDirection = Vector2.zero;
            if (state == FollowState.Blocked)
                state = path.Count > 0 ? FollowState.MoveToCellCenter : FollowState.Idle;
        }

        private Vector2Int FindJunctionExitTarget(GridMap grid, Vector2Int currentCell)
        {
            var fallback = path.Count > 0 ? path[0] : currentCell;
            for (var i = 0; i < path.Count; i++)
            {
                var candidate = path[i];
                fallback = candidate;
                if (candidate == currentCell)
                    continue;
                if (!IsJunction(grid, candidate))
                    return candidate;
            }

            return fallback;
        }

        private Vector2Int GetNextPathStep(Vector2Int currentCell)
        {
            if (path.Count == 0)
                return currentCell;

            for (var i = 0; i < path.Count; i++)
            {
                var candidate = path[i];
                if (candidate == currentCell)
                    return i + 1 < path.Count ? path[i + 1] : candidate;
                if (AreAdjacent(currentCell, candidate))
                    return candidate;
            }

            return path[0];
        }

        private bool IsJunction(GridMap grid, Vector2Int cell)
        {
            if (grid == null || !grid.IsInside(cell.x, cell.y))
                return false;

            grid.FillNeighbors(cell.x, cell.y, scratchNeighbors);
            return scratchNeighbors.Count >= 3;
        }

        private void ClearJunctionLock()
        {
            hasLockedJunctionExit = false;
            lockedJunctionCell = default;
            lockedExitCell = default;
            junctionSpinTimer = 0f;
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
