using System.Collections.Generic;
using UnityEngine;
using TankTrouble.Core;
using TankTrouble.Entities;
using TankTrouble.Map;

namespace TankTrouble.AI
{
    public enum AISimpleState
    {
        Idle,
        Navigate,
        DangerPause,
        Recover
    }

    public sealed class AISimpleStateMachine
    {
        private const float ReplanCooldown = 0.5f;
        private const float BlockedEdgeDuration = 2.4f;
        private const float RecoverDuration = 0.18f;
        private const float DangerPauseDuration = 0.16f;

        private readonly AIGridPathPlanner planner = new AIGridPathPlanner();
        private readonly AIPathMotor motor = new AIPathMotor();
        private readonly AIBlockedEdgeSet blockedEdges = new AIBlockedEdgeSet();
        private readonly List<Vector2Int> currentPath = new List<Vector2Int>(32);

        private float replanTimer;
        private float recoverTimer;
        private float dangerPauseTimer;
        private TankInputCommand recoverCommand;
        private Vector2Int currentGoal;

        public AISimpleState State { get; private set; }
        public int BlacklistedEdgeCount => blockedEdges.Count;
        public int RemainingWaypoints => motor.RemainingWaypoints;
        public Vector2Int CurrentGoal => currentGoal;

        public void Reset()
        {
            motor.Clear();
            blockedEdges.Clear();
            currentPath.Clear();
            replanTimer = 0f;
            recoverTimer = 0f;
            dangerPauseTimer = 0f;
            recoverCommand = TankInputCommand.None;
            currentGoal = default;
            State = AISimpleState.Idle;
        }

        public TankInputCommand Tick(
            TankController tank,
            GridMap grid,
            IReadOnlyList<TankController> enemies,
            bool dangerInterrupt,
            float dt)
        {
            blockedEdges.Tick(dt);
            replanTimer = Mathf.Max(0f, replanTimer - Mathf.Max(0f, dt));

            if (tank == null || grid == null || enemies == null || enemies.Count == 0)
            {
                Reset();
                return TankInputCommand.None;
            }

            if (dangerInterrupt)
            {
                motor.Clear();
                currentPath.Clear();
                dangerPauseTimer = DangerPauseDuration;
                replanTimer = ReplanCooldown;
                State = AISimpleState.DangerPause;
                return TankInputCommand.None;
            }

            if (dangerPauseTimer > 0f)
            {
                dangerPauseTimer = Mathf.Max(0f, dangerPauseTimer - Mathf.Max(0f, dt));
                State = AISimpleState.DangerPause;
                return TankInputCommand.None;
            }

            if (recoverTimer > 0f)
            {
                recoverTimer = Mathf.Max(0f, recoverTimer - Mathf.Max(0f, dt));
                State = AISimpleState.Recover;
                return recoverCommand;
            }

            var command = motor.Tick(tank, grid, dt);
            if (motor.Failed)
            {
                if (motor.FailedEdge.IsValid)
                    blockedEdges.Add(motor.FailedEdge, BlockedEdgeDuration);

                motor.Clear();
                currentPath.Clear();
                replanTimer = ReplanCooldown;
                recoverTimer = RecoverDuration;
                recoverCommand = new TankInputCommand(-0.45f, 0f, false);
                State = AISimpleState.Recover;
                return TankInputCommand.None;
            }

            if (HasCommandInput(command) || motor.HasPath)
            {
                State = AISimpleState.Navigate;
                return command;
            }

            if (replanTimer > 0f)
            {
                State = AISimpleState.Idle;
                return TankInputCommand.None;
            }

            if (!TryPlanPath(tank, grid, enemies))
            {
                State = AISimpleState.Idle;
                replanTimer = ReplanCooldown;
                return TankInputCommand.None;
            }

            replanTimer = ReplanCooldown;
            command = motor.Tick(tank, grid, dt);
            State = HasCommandInput(command) || motor.HasPath ? AISimpleState.Navigate : AISimpleState.Idle;
            return command;
        }

        private bool TryPlanPath(TankController tank, GridMap grid, IReadOnlyList<TankController> enemies)
        {
            var currentCell = CoordinateUtil.PixelToCell(CoordinateUtil.WorldToPixel(tank.transform.position));
            var facing = AIGridDirections.FromForward(tank.VelocityForward);
            if (!planner.TryFindPathToNearestEnemy(grid, currentCell, facing, enemies, blockedEdges, currentPath, out currentGoal))
                return false;

            motor.SetPath(currentPath);
            return true;
        }

        private static bool HasCommandInput(TankInputCommand command)
        {
            return Mathf.Abs(command.Move) > 0.01f || Mathf.Abs(command.Rotate) > 0.01f || command.FireHeld;
        }
    }
}
