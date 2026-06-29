using TankTrouble.Core;

namespace TankTrouble.AI
{
    public readonly struct AiDifficultySettings
    {
        public readonly float BallisticsInterval;
        public readonly float PathfindInterval;
        public readonly float ShootCooldown;
        public readonly float DangerPredictTime;
        public readonly int BurstCount;
        public readonly float AimToleranceDegrees;

        private AiDifficultySettings(float ballisticsInterval, float pathfindInterval, float shootCooldown, float dangerPredictTime, int burstCount, float aimToleranceDegrees)
        {
            BallisticsInterval = ballisticsInterval;
            PathfindInterval = pathfindInterval;
            ShootCooldown = shootCooldown;
            DangerPredictTime = dangerPredictTime;
            BurstCount = burstCount;
            AimToleranceDegrees = aimToleranceDegrees;
        }

        public static AiDifficultySettings FromDifficulty(AiDifficulty difficulty)
        {
            return difficulty == AiDifficulty.Hard
                ? new AiDifficultySettings(0.18f, 0.12f, 0.32f, 2.0f, 3, 7f)
                : new AiDifficultySettings(0.55f, 0.32f, 0.85f, 1.2f, 2, 9f);
        }
    }
}
