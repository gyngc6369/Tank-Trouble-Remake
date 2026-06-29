namespace TankTrouble.Core
{
    public enum GameState
    {
        MainMenu,
        MapSelect,
        DifficultySelect,
        GamePlaying,
        RoundEnd,
        GameOver,
        Paused
    }

    public enum GameMode
    {
        Pvp,
        PvAi,
        PvpAi
    }

    public enum AiDifficulty
    {
        Normal,
        Hard
    }
}
