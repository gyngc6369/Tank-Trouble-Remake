namespace TankTrouble.Config
{
    public static class GameConfig
    {
        public const int ScreenWidth = 960;
        public const int ScreenHeight = 744;
        public const int UiBarHeight = 56;
        public const int GridOffsetY = UiBarHeight;
        public const int TargetFps = 60;

        public const int GridCols = 15;
        public const int GridRows = 11;
        public const int CellSize = 64;
        public const int WallThickness = 4;

        public const float TankBodyWidth = 20f;
        public const float TankBodyHeight = 28f;
        public const float TankSpeed = 110f;
        public const float TankRotationSpeedDeg = 150f;
        public const float BarrelLength = 16f;
        public const float BarrelWidth = 4f;

        public const float BulletSpeed = 165f;
        public const float BulletRadius = 2f;
        public const int MaxBounces = 7;
        public const int MaxAmmo = 7;
        public const float AmmoRegenTime = 1.0f;
        public const float ShootCooldown = 0.15f;

        public const int DefaultWinScore = 5;
        public const int SpawnMinDistance = 5;
        public const float RoundCountdownDuration = 3f;
        public const float RoundEndDuration = 3f;

        public static readonly int[] WinScoreOptions = { 1, 3, 5, 7, 10 };
    }
}
