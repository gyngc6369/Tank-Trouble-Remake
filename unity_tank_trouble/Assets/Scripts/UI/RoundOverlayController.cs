using UnityEngine;
using UnityEngine.UI;
using TankTrouble.Core;

namespace TankTrouble.UI
{
    public sealed class RoundOverlayController : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private RoundManager roundManager;
        [SerializeField] private GameObject overlayRoot;
        [SerializeField] private Text titleText;
        [SerializeField] private Text subtitleText;
        [SerializeField] private GameObject pauseActionsRoot;
        [SerializeField] private GameObject gameOverActionsRoot;

        private void Awake()
        {
            if (gameManager == null)
                gameManager = FindObjectOfType<GameManager>();
            if (roundManager == null)
                roundManager = FindObjectOfType<RoundManager>();
            EnsureActionButtons();
        }

        private void Update()
        {
            if (gameManager == null || roundManager == null || overlayRoot == null)
                return;

            switch (gameManager.State)
            {
                case GameState.Paused:
                    Show("暂停", "选择操作", true, false);
                    break;
                case GameState.GameOver:
                    Show("游戏结束", roundManager.MatchWinner != null ? "胜者已达到目标分数" : "无人获胜", false, true);
                    break;
                case GameState.RoundEnd:
                    Show(
                        "回合结束",
                        roundManager.LastRoundWinner != null
                            ? $"幸存者得分，下一回合 {Mathf.CeilToInt(roundManager.RoundEndRemaining)}"
                            : $"下一回合 {Mathf.CeilToInt(roundManager.RoundEndRemaining)}",
                        false,
                        false);
                    break;
                case GameState.GamePlaying when roundManager.Phase == RoundPhase.Countdown:
                    var value = roundManager.CountdownRemaining > 0.6f ? Mathf.CeilToInt(roundManager.CountdownRemaining).ToString() : "开始";
                    Show(value, string.Empty, false, false);
                    break;
                default:
                    overlayRoot.SetActive(false);
                    SetActionRoots(false, false);
                    break;
            }
        }

        private void Show(string title, string subtitle, bool showPauseActions, bool showGameOverActions)
        {
            overlayRoot.SetActive(true);
            if (titleText != null)
                titleText.text = title;
            if (subtitleText != null)
                subtitleText.text = subtitle;
            SetActionRoots(showPauseActions, showGameOverActions);
        }

        private void SetActionRoots(bool showPauseActions, bool showGameOverActions)
        {
            if (pauseActionsRoot != null)
                pauseActionsRoot.SetActive(showPauseActions);
            if (gameOverActionsRoot != null)
                gameOverActionsRoot.SetActive(showGameOverActions);
        }

        private void EnsureActionButtons()
        {
            if (overlayRoot == null)
                return;

            if (pauseActionsRoot == null)
                pauseActionsRoot = CreateActionRoot("PauseActions", -128f, 780f);
            SetActionRootSize(pauseActionsRoot, 780f);
            EnsureActionButton(pauseActionsRoot.transform, "ResumeButton", "继续", () => gameManager.Resume(), -285f);
            EnsureActionButton(pauseActionsRoot.transform, "RestartButton", "重新开始", () => gameManager.RestartMatch(), -95f);
            EnsureActionButton(pauseActionsRoot.transform, "MenuButton", "返回主菜单", () => gameManager.ReturnToMainMenu(), 95f);
            EnsureActionButton(pauseActionsRoot.transform, "QuitButton", "退出游戏", () => gameManager.QuitGame(), 285f);

            if (gameOverActionsRoot == null)
                gameOverActionsRoot = CreateActionRoot("GameOverActions", -128f, 620f);
            SetActionRootSize(gameOverActionsRoot, 620f);
            EnsureActionButton(gameOverActionsRoot.transform, "RematchButton", "再来一局", () => gameManager.RestartMatch(), -190f);
            EnsureActionButton(gameOverActionsRoot.transform, "GameOverMenuButton", "返回主菜单", () => gameManager.ReturnToMainMenu(), 0f);
            EnsureActionButton(gameOverActionsRoot.transform, "GameOverQuitButton", "退出游戏", () => gameManager.QuitGame(), 190f);

            SetActionRoots(false, false);
        }

        private GameObject CreateActionRoot(string objectName, float y, float width)
        {
            var root = new GameObject(objectName);
            root.transform.SetParent(overlayRoot.transform, false);
            var rect = root.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(width, 54f);
            return root;
        }

        private static void SetActionRootSize(GameObject root, float width)
        {
            if (root != null && root.TryGetComponent<RectTransform>(out var rect))
                rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
        }

        private static void EnsureActionButton(Transform parent, string objectName, string label, UnityEngine.Events.UnityAction action, float x)
        {
            if (parent == null || parent.Find(objectName) != null)
                return;

            CreateActionButton(parent, objectName, label, action, x);
        }

        private static void CreateActionButton(Transform parent, string objectName, string label, UnityEngine.Events.UnityAction action, float x)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(150f, 46f);

            var image = go.AddComponent<Image>();
            image.color = new Color32(245, 245, 245, 245);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);

            var text = new GameObject("Text");
            text.transform.SetParent(go.transform, false);
            var textRect = text.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var labelText = text.AddComponent<Text>();
            labelText.text = label;
            labelText.font = GetDefaultFont();
            labelText.fontSize = 18;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = new Color32(30, 30, 30, 255);
        }

        private static Font GetDefaultFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
                return font;

            return Font.CreateDynamicFontFromOSFont("Arial", 18);
        }
    }
}
