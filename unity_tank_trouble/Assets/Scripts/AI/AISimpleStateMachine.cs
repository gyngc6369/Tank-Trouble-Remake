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
        Recover
    }

    public sealed class AISimpleStateMachine
    {
        private const float ReplanCooldown = 0.5f;
        private const float BlockedEdgeDuration = 2.4f;
        private const float RecoverDuration = 0.18f;

        private readonly AIGridPathPlanner planner = new AIGridPathPlanner();
        private readonly AIPathMotor motor = new AIPathMotor();
        private readonly AIBlockedEdgeSet blockedEdges = new AIBlockedEdgeSet();
        private readonly List<Vector2Int> currentPath = new List<Vector2Int>(32);

        private float replanTimer;
        private float recoverTimer;
        private TankInputCommand recoverCommand;
        private Vector2Int currentGoal;
        private bool hasCurrentGoal;

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
            recoverCommand = TankInputCommand.None;
            currentGoal = default;
            hasCurrentGoal = false;
            State = AISimpleState.Idle;
        }

        public TankInputCommand Tick(
            TankController tank,
            GridMap grid,
            IReadOnlyList<TankController> enemies,
            bool dangerInterrupt,
            float dt)
        {
            return Tick(tank, grid, enemies, dangerInterrupt, false, default, dt);
        }

        public TankInputCommand Tick(
            TankController tank,
            GridMap grid,
            IReadOnlyList<TankController> enemies,
            bool dangerInterrupt,
            bool hasPreferredGoal,
            Vector2Int preferredGoal,
            float dt)
        {
            blockedEdges.Tick(dt);
            replanTimer = Mathf.Max(0f, replanTimer - Mathf.Max(0f, dt));

            if (tank == null || grid == null || enemies == null || enemies.Count == 0)
            {
                Reset();
                return TankInputCommand.None;
            }

            // Danger interrupt: clear navigation state so the controller can take over with evasion.
            // The actual evasion movement is handled by AIController, not here.
            if (dangerInterrupt)
            {
                motor.Clear();
                currentPath.Clear();
                hasCurrentGoal = false;
                replanTimer = ReplanCooldown;
                State = AISimpleState.Idle;
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
                hasCurrentGoal = false;
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

            var preferredChanged = hasPreferredGoal && (!hasCurrentGoal || currentGoal != preferredGoal);
            if (replanTimer > 0f && !preferredChanged)
            {
                State = AISimpleState.Idle;
                return TankInputCommand.None;
            }

            if (!TryPlanPath(tank, grid, enemies, hasPreferredGoal, preferredGoal))
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

        private bool TryPlanPath(TankController tank, GridMap grid, IReadOnlyList<TankController> enemies, bool hasPreferredGoal, Vector2Int preferredGoal)
        {
            var currentCell = CoordinateUtil.PixelToCell(CoordinateUtil.WorldToPixel(tank.transform.position));
            var facing = AIGridDirections.FromForward(tank.VelocityForward);
            if (hasPreferredGoal && grid.IsInside(preferredGoal.x, preferredGoal.y))
            {
                if (planner.TryFindPath(grid, currentCell, facing, preferredGoal, blockedEdges, currentPath))
                {
                    currentGoal = preferredGoal;
                    hasCurrentGoal = true;
                    motor.SetPath(currentPath);
                    return true;
                }
            }

            if (!planner.TryFindPathToNearestEnemy(grid, currentCell, facing, enemies, blockedEdges, currentPath, out currentGoal))
                return false;

            hasCurrentGoal = true;
            motor.SetPath(currentPath);
            return true;
        }

        private static bool HasCommandInput(TankInputCommand command)
        {
            return Mathf.Abs(command.Move) > 0.01f || Mathf.Abs(command.Rotate) > 0.01f || command.FireHeld;
        }
    }
}
