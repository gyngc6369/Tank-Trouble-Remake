using UnityEngine;
using UnityEngine.UI;
using TankTrouble.Config;
using TankTrouble.Core;
using TankTrouble.Map;

namespace TankTrouble.UI
{
    public sealed class MenuController : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private GameManager gameManager;

        [Header("Panels")]
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject mapPanel;
        [SerializeField] private GameObject difficultyPanel;

        [Header("Map Selection")]
        [SerializeField] private Text selectedMapText;
        [SerializeField] private Text winScoreText;
        [SerializeField] private Button[] mapButtons;

        private GameMode pendingMode = GameMode.Pvp;
        private AiDifficulty pendingDifficulty = AiDifficulty.Hard;
        private MapKind pendingMap = MapKind.Random;
        private int winScoreIndex = 2;

        private int PendingWinScore => GameConfig.WinScoreOptions[Mathf.Clamp(winScoreIndex, 0, GameConfig.WinScoreOptions.Length - 1)];

        private void Awake()
        {
            if (gameManager == null)
                gameManager = FindObjectOfType<GameManager>();
            EnsureMainQuitButton();
        }

        private void Start()
        {
            ShowMain();
            UpdateMapLabels();
        }

        private void Update()
        {
            if (gameManager == null)
                return;

            if (gameManager.State == GameState.MainMenu && !IsAnyMenuPanelVisible())
                ShowMain();

            if (mapPanel != null && mapPanel.activeSelf)
                HandleMapKeyboard();
            else if (mainPanel != null && mainPanel.activeSelf)
                HandleMainKeyboard();
            else if (difficultyPanel != null && difficultyPanel.activeSelf)
                HandleDifficultyKeyboard();
        }

        public void ShowMain()
        {
            SetPanel(mainPanel, true);
            SetPanel(mapPanel, false);
            SetPanel(difficultyPanel, false);
        }

        public void SelectPvp()
        {
            pendingMode = GameMode.Pvp;
            ShowMapSelect();
        }

        public void SelectPvAi()
        {
            pendingMode = GameMode.PvAi;
            ShowMapSelect();
        }

        public void SelectPvpAi()
        {
            pendingMode = GameMode.PvpAi;
            ShowMapSelect();
        }

        public void ShowMapSelect()
        {
            SetPanel(mainPanel, false);
            SetPanel(mapPanel, true);
            SetPanel(difficultyPanel, false);
            UpdateMapLabels();
        }

        public void SelectMapByIndex(int index)
        {
            pendingMap = (MapKind)Mathf.Clamp(index, 0, 5);
            UpdateMapLabels();
        }

        public void PreviousWinScore()
        {
            winScoreIndex = (winScoreIndex - 1 + GameConfig.WinScoreOptions.Length) % GameConfig.WinScoreOptions.Length;
            UpdateMapLabels();
        }

        public void NextWinScore()
        {
            winScoreIndex = (winScoreIndex + 1) % GameConfig.WinScoreOptions.Length;
            UpdateMapLabels();
        }

        public void ConfirmMapSelection()
        {
            if (pendingMode == GameMode.Pvp)
                StartPendingGame();
            else
                ShowDifficultySelect();
        }

        public void ShowDifficultySelect()
        {
            SetPanel(mainPanel, false);
            SetPanel(mapPanel, false);
            SetPanel(difficultyPanel, true);
        }

        public void SelectNormalDifficulty()
        {
            pendingDifficulty = AiDifficulty.Normal;
            StartPendingGame();
        }

        public void SelectHardDifficulty()
        {
            pendingDifficulty = AiDifficulty.Hard;
            StartPendingGame();
        }

        public void BackToMain()
        {
            ShowMain();
        }

        public void BackToMapSelect()
        {
            ShowMapSelect();
        }

        public void QuitGame()
        {
            ApplicationQuitService.Quit();
        }

        private void StartPendingGame()
        {
            SetPanel(mainPanel, false);
            SetPanel(mapPanel, false);
            SetPanel(difficultyPanel, false);
            gameManager.StartGame(pendingMode, pendingMap, pendingDifficulty, PendingWinScore);
        }

        private void UpdateMapLabels()
        {
            if (selectedMapText != null)
                selectedMapText.text = $"当前地图：{GetMapName(pendingMap)}";
            if (winScoreText != null)
                winScoreText.text = PendingWinScore.ToString();

            if (mapButtons == null)
                return;

            for (var i = 0; i < mapButtons.Length; i++)
            {
                if (mapButtons[i] == null)
                    continue;
                var colors = mapButtons[i].colors;
                colors.normalColor = i == (int)pendingMap ? new Color(0.78f, 0.86f, 1f) : Color.white;
                mapButtons[i].colors = colors;
            }
        }

        private void HandleMainKeyboard()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) SelectPvp();
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) SelectPvAi();
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) SelectPvpAi();
            else if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.Escape)) QuitGame();
        }

        private void HandleMapKeyboard()
        {
            var mapKeys = new[]
            {
                KeyCode.Alpha1,
                KeyCode.Alpha2,
                KeyCode.Alpha3,
                KeyCode.Alpha4,
                KeyCode.Alpha5,
                KeyCode.Alpha6
            };

            for (var i = 0; i < mapKeys.Length; i++)
            {
                if (Input.GetKeyDown(mapKeys[i]))
                    SelectMapByIndex(i);
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow)) PreviousWinScore();
            else if (Input.GetKeyDown(KeyCode.RightArrow)) NextWinScore();
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) ConfirmMapSelection();
            else if (Input.GetKeyDown(KeyCode.B)) BackToMain();
        }

        private void HandleDifficultyKeyboard()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) SelectNormalDifficulty();
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) SelectHardDifficulty();
            else if (Input.GetKeyDown(KeyCode.B)) BackToMapSelect();
        }

        private bool IsAnyMenuPanelVisible()
        {
            return IsActive(mainPanel) || IsActive(mapPanel) || IsActive(difficultyPanel);
        }

        private static bool IsActive(GameObject panel)
        {
            return panel != null && panel.activeSelf;
        }

        private static void SetPanel(GameObject panel, bool active)
        {
            if (panel != null)
                panel.SetActive(active);
        }

        private void EnsureMainQuitButton()
        {
            if (mainPanel == null || mainPanel.transform.Find("QuitButton") != null)
                return;

            if (mainPanel.TryGetComponent<RectTransform>(out var panelRect) && panelRect.sizeDelta.y < 430f)
                panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, 430f);

            var go = new GameObject("QuitButton");
            go.transform.SetParent(mainPanel.transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(320f, 48f);

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = 320f;
            layout.preferredHeight = 48f;
            layout.minWidth = 320f;
            layout.minHeight = 48f;

            var image = go.AddComponent<Image>();
            image.color = new Color32(248, 248, 248, 255);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(QuitGame);

            var text = new GameObject("Text");
            text.transform.SetParent(go.transform, false);
            var textRect = text.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var label = text.AddComponent<Text>();
            label.text = "退出游戏";
            label.font = GetDefaultFont();
            label.fontSize = 20;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color32(28, 28, 28, 255);
        }

        private static Font GetDefaultFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
                return font;

            return Font.CreateDynamicFontFromOSFont("Arial", 18);
        }

        private static string GetMapName(MapKind mapKind)
        {
            return mapKind == MapKind.Random ? "随机生成" : PresetMaps.Names[(int)mapKind];
        }
    }
}
