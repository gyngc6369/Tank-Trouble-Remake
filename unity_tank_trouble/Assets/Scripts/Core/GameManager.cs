using UnityEngine;
using UnityEngine.SceneManagement;
using TankTrouble.Config;
using TankTrouble.Map;

namespace TankTrouble.Core
{
    public sealed class GameManager : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private RoundManager roundManager;
        [SerializeField] private ScoreManager scoreManager;

        [Header("Match Settings")]
        [SerializeField] private GameMode mode = GameMode.Pvp;
        [SerializeField] private AiDifficulty difficulty = AiDifficulty.Hard;
        [SerializeField] private MapKind mapKind = MapKind.Random;
        [SerializeField] private int winScore = GameConfig.DefaultWinScore;
        [SerializeField] private bool autoStart;

        public GameState State { get; private set; } = GameState.MainMenu;
        public GameMode Mode => mode;
        public AiDifficulty Difficulty => difficulty;
        public MapKind MapKind => mapKind;
        public int WinScore => winScore;

        private void Awake()
        {
            Application.targetFrameRate = GameConfig.TargetFps;
            if (roundManager == null)
                roundManager = FindObjectOfType<RoundManager>();
            if (scoreManager == null)
                scoreManager = FindObjectOfType<ScoreManager>();
        }

        private void Start()
        {
            if (autoStart)
                StartConfiguredGame();
        }

        private void Update()
        {
            SyncStateFromRoundPhase();

            if (Input.GetKeyDown(KeyCode.P))
                TogglePause();

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (State == GameState.GamePlaying)
                    Pause();
                else if (State == GameState.Paused)
                    Resume();
                else if (State == GameState.GameOver)
                    ReturnToMainMenu();
            }
        }

        public void StartGame(GameMode selectedMode, MapKind selectedMap, AiDifficulty selectedDifficulty, int selectedWinScore)
        {
            mode = selectedMode;
            mapKind = selectedMap;
            difficulty = selectedDifficulty;
            winScore = selectedWinScore;
            StartConfiguredGame();
        }

        public void StartConfiguredGame()
        {
            Time.timeScale = 1f;
            if (roundManager == null || scoreManager == null)
            {
                Debug.LogError("GameManager requires RoundManager and ScoreManager references.");
                return;
            }

            roundManager.SetMapKind(mapKind);
            roundManager.SetAiDifficulty(difficulty);
            roundManager.BeginMatch(mode, scoreManager, winScore);
            SyncStateFromRoundPhase();
        }

        public void ReturnToMainMenu()
        {
            Time.timeScale = 1f;
            if (roundManager != null)
                roundManager.StopMatch();
            State = GameState.MainMenu;
            if (SceneManager.GetActiveScene().name != "MainMenu")
                SceneManager.LoadScene("MainMenu");
        }

        public void RestartMatch()
        {
            Time.timeScale = 1f;
            StartConfiguredGame();
        }

        public void QuitGame()
        {
            ApplicationQuitService.Quit();
        }

        public void SetMode(GameMode newMode)
        {
            mode = newMode;
        }

        public void SetDifficulty(AiDifficulty newDifficulty)
        {
            difficulty = newDifficulty;
        }

        public void SetMapKind(MapKind newMapKind)
        {
            mapKind = newMapKind;
            if (roundManager != null)
                roundManager.SetMapKind(newMapKind);
        }

        public void SetWinScore(int score)
        {
            winScore = score;
        }

        public void Pause()
        {
            if (State != GameState.GamePlaying)
                return;

            State = GameState.Paused;
            Time.timeScale = 0f;
        }

        public void Resume()
        {
            if (State != GameState.Paused)
                return;

            Time.timeScale = 1f;
            ForceSyncStateFromRoundPhase();
        }

        public void TogglePause()
        {
            if (State == GameState.Paused)
                Resume();
            else if (State == GameState.GamePlaying)
                Pause();
        }

        private void SyncStateFromRoundPhase()
        {
            if (State == GameState.Paused)
                return;

            ForceSyncStateFromRoundPhase();
        }

        private void ForceSyncStateFromRoundPhase()
        {
            if (roundManager == null)
                return;

            State = roundManager.Phase switch
            {
                RoundPhase.Countdown => GameState.GamePlaying,
                RoundPhase.Playing => GameState.GamePlaying,
                RoundPhase.RoundEnd => GameState.RoundEnd,
                RoundPhase.MatchOver => GameState.GameOver,
                _ => State == GameState.MainMenu ? GameState.MainMenu : GameState.MainMenu
            };
        }
    }
}
