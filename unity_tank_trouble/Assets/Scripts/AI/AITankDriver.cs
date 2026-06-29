using UnityEngine;
using TankTrouble.Entities;

namespace TankTrouble.AI
{
    public static class AITankDriver
    {
        private const float TurnInPlaceAngle = 58f;
        private const float SlowTurnAngle = 20f;
        private const float AimDeadZone = 4f;
        private const float TurnLockDuration = 0.24f;
        private const float BlockProbeTime = 0.18f;

        public struct Memory
        {
            public float TurnLockTimer;
            public float TurnLockInput;
            public bool NeedsRepath;

            public void Reset()
            {
                TurnLockTimer = 0f;
                TurnLockInput = 0f;
                NeedsRepath = false;
            }
        }

        public static TankInputCommand DriveToDirection(TankController tank, Vector2 desiredDirection, bool allowReverse, ref Memory memory, float dt, bool fire = false)
        {
            memory.NeedsRepath = false;
            memory.TurnLockTimer = Mathf.Max(0f, memory.TurnLockTimer - Mathf.Max(0f, dt));

            if (tank == null || desiredDirection.sqrMagnitude < 0.0001f)
                return new TankInputCommand(0f, 0f, fire);

            var desired = desiredDirection.normalized;
            var forwardAngle = Vector2.SignedAngle(tank.VelocityForward, desired);
            var reverseAngle = Vector2.SignedAngle(-tank.VelocityForward, desired);
            var useReverse = allowReverse && Mathf.Abs(reverseAngle) + 24f < Mathf.Abs(forwardAngle);
            var steeringAngle = useReverse ? reverseAngle : forwardAngle;
            var absAngle = Mathf.Abs(steeringAngle);
            var rotate = absAngle > AimDeadZone ? RotateInputForAngle(steeringAngle) : 0f;

            var move = 0f;
            if (absAngle > TurnInPlaceAngle)
            {
                rotate = GetLockedTurn(rotate, ref memory);
                move = 0f;
            }
            else if (absAngle > SlowTurnAngle)
            {
                move = useReverse ? -0.42f : 0.55f;
            }
            else
            {
                move = useReverse ? -0.75f : 1f;
            }

            var command = new TankInputCommand(move, rotate, fire);
            if (Mathf.Abs(move) <= 0.01f)
                return command;

            var prediction = tank.PredictCommand(command, BlockProbeTime);
            var poorlyBlocked = prediction.RequestedDistance > 0f && prediction.AllowedDistance <= prediction.RequestedDistance * 0.35f;
            if (!prediction.MoveBlocked && !poorlyBlocked)
                return command;

            memory.NeedsRepath = true;
            if (absAngle > AimDeadZone)
                return new TankInputCommand(0f, GetLockedTurn(rotate, ref memory), fire);

            return new TankInputCommand(0f, 0f, fire);
        }

        private static float GetLockedTurn(float desiredRotate, ref Memory memory)
        {
            if (Mathf.Abs(desiredRotate) <= 0.01f)
                return 0f;

            if (memory.TurnLockTimer <= 0f || Mathf.Abs(memory.TurnLockInput) <= 0.01f)
            {
                memory.TurnLockInput = desiredRotate;
                memory.TurnLockTimer = TurnLockDuration;
            }

            return memory.TurnLockInput;
        }

        private static float RotateInputForAngle(float signedAngle)
        {
            return signedAngle > 0f ? -1f : 1f;
        }
    }
}
