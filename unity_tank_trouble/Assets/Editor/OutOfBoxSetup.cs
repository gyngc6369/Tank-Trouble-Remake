using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TankTrouble.AI;
using TankTrouble.Audio;
using TankTrouble.Config;
using TankTrouble.Core;
using TankTrouble.Entities;
using TankTrouble.Map;
using TankTrouble.UI;

namespace TankTrouble.Editor
{
    [InitializeOnLoad]
    public static class OutOfBoxSetup
    {
        private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string GameScenePath = "Assets/Scenes/Game.unity";
        private const string TankPrefabPath = "Assets/Prefabs/Tank.prefab";
        private const string SessionKey = "TankTrouble.OutOfBoxSetup.Ran";

        static OutOfBoxSetup()
        {
            EditorApplication.delayCall += SetupIfNeeded;
        }

        [MenuItem("Tank Trouble/Setup Out-of-the-box Project")]
        public static void SetupProjectMenu()
        {
            SetupOutOfBoxProject();
        }

        public static void SetupOutOfBoxProject()
        {
            SetupProject(force: true);
        }

        private static void SetupIfNeeded()
        {
            if (SessionState.GetBool(SessionKey, false))
                return;

            SessionState.SetBool(SessionKey, true);
            if (!File.Exists(MainMenuScenePath) || !File.Exists(GameScenePath) || !File.Exists(TankPrefabPath) || !BuildSettingsConfigured())
                SetupProject(force: false);
        }

        private static void SetupProject(bool force)
        {
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory("Assets/Prefabs");

            var tankPrefab = CreateOrUpdateTankPrefab(force);
            CreateScene(MainMenuScenePath, tankPrefab, includeMenu: true, autoStart: false);
            CreateScene(GameScenePath, tankPrefab, includeMenu: false, autoStart: true);
            ConfigureBuildSettings();
            ConfigureInputSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);

            var errors = BuildValidation.CollectErrors();
            if (errors.Count == 0)
                Debug.Log("Tank Trouble out-of-the-box setup completed. Project validation passed.");
            else
                Debug.LogWarning("Tank Trouble setup completed with validation warnings: " + string.Join("; ", errors));
        }

        private static GameObject CreateOrUpdateTankPrefab(bool force)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(TankPrefabPath);
            if (existing != null && !force)
                return existing;

            var go = new GameObject("Tank");
            go.layer = LayerMask.NameToLayer("Tank");
            var view = go.AddComponent<TankView>();
            var controller = go.AddComponent<TankController>();
            var input = go.AddComponent<PlayerTankInput>();
            var ai = go.AddComponent<AIController>();
            ai.enabled = false;
            view.SetColors(new Color32(60, 100, 220, 255), new Color32(30, 60, 160, 255));
            view.Rebuild();
            view.SetVisible(true);

            SetObject(controller, "tankView", view);
            SetLayerMask(controller, "wallMask", LayerMask.GetMask("Wall"));
            SetEnum(input, "player", (int)PlayerIndex.Player1);
            SetLayerMask(ai, "wallMask", LayerMask.GetMask("Wall"));
            SetLayerMask(ai, "tankMask", LayerMask.GetMask("Tank"));

            var prefab = existing == null
                ? PrefabUtility.SaveAsPrefabAsset(go, TankPrefabPath)
                : PrefabUtility.SaveAsPrefabAssetAndConnect(go, TankPrefabPath, InteractionMode.AutomatedAction);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static void CreateScene(string scenePath, GameObject tankPrefab, bool includeMenu, bool autoStart)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = Path.GetFileNameWithoutExtension(scenePath);

            CreateCamera();
            var systems = CreateGameplayRig(tankPrefab, autoStart);
            CreateUiRig(systems, includeMenu);
            CreateEventSystem();

            EditorSceneManager.SaveScene(scene, scenePath);
        }

        private static void CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.white;
            camera.orthographic = true;
            camera.orthographicSize = GameConfig.ScreenHeight / (2f * CoordinateUtil.PixelsPerUnit);
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.position = new Vector3(
                GameConfig.ScreenWidth / (2f * CoordinateUtil.PixelsPerUnit),
                -GameConfig.ScreenHeight / (2f * CoordinateUtil.PixelsPerUnit),
                -10f);
        }

        private static SceneSystems CreateGameplayRig(GameObject tankPrefab, bool autoStart)
        {
            var root = new GameObject("Gameplay");
            var mapObject = new GameObject("MapRenderer");
            mapObject.transform.SetParent(root.transform, false);
            var mapRenderer = mapObject.AddComponent<MapRenderer>();
            var wallParent = new GameObject("Walls").transform;
            wallParent.SetParent(mapObject.transform, false);
            SetObject(mapRenderer, "wallParent", wallParent);
            SetBool(mapRenderer, "buildOnStart", false);

            var bulletPoolObject = new GameObject("BulletPool");
            bulletPoolObject.transform.SetParent(root.transform, false);
            var bulletPool = bulletPoolObject.AddComponent<BulletPool>();

            var scoreObject = new GameObject("ScoreManager");
            scoreObject.transform.SetParent(root.transform, false);
            var scoreManager = scoreObject.AddComponent<ScoreManager>();

            var roundObject = new GameObject("RoundManager");
            roundObject.transform.SetParent(root.transform, false);
            var roundManager = roundObject.AddComponent<RoundManager>();

            var gameObject = new GameObject("GameManager");
            gameObject.transform.SetParent(root.transform, false);
            var gameManager = gameObject.AddComponent<GameManager>();

            var audioObject = new GameObject("AudioManager");
            audioObject.AddComponent<AudioSource>();
            audioObject.AddComponent<AudioManager>();

            var tanksRoot = new GameObject("Tanks");
            tanksRoot.transform.SetParent(root.transform, false);
            var tanks = new TankController[3];
            for (var i = 0; i < tanks.Length; i++)
                tanks[i] = CreateTankInstance(tankPrefab, tanksRoot.transform, i, bulletPool, roundManager);

            SetObject(roundManager, "mapRenderer", mapRenderer);
            SetObject(roundManager, "bulletPool", bulletPool);
            SetObjectArray(roundManager, "tanks", tanks);

            SetObject(gameManager, "roundManager", roundManager);
            SetObject(gameManager, "scoreManager", scoreManager);
            SetEnum(gameManager, "mode", (int)GameMode.Pvp);
            SetEnum(gameManager, "difficulty", (int)AiDifficulty.Hard);
            SetEnum(gameManager, "mapKind", (int)MapKind.Random);
            SetInt(gameManager, "winScore", GameConfig.DefaultWinScore);
            SetBool(gameManager, "autoStart", autoStart);

            return new SceneSystems(gameManager, roundManager, scoreManager, mapRenderer, bulletPool, tanks);
        }

        private static TankController CreateTankInstance(GameObject prefab, Transform parent, int index, BulletPool bulletPool, RoundManager roundManager)
        {
            var tankObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            tankObject.name = $"Tank_{index + 1}";
            tankObject.transform.SetParent(parent, false);
            tankObject.SetActive(true);
            tankObject.layer = LayerMask.NameToLayer("Tank");

            var view = tankObject.GetComponent<TankView>();
            var controller = tankObject.GetComponent<TankController>();
            var input = tankObject.GetComponent<PlayerTankInput>();
            var ai = tankObject.GetComponent<AIController>();

            if (index == 0)
                view.SetColors(new Color32(60, 100, 220, 255), new Color32(30, 60, 160, 255));
            else if (index == 1)
                view.SetColors(new Color32(220, 60, 60, 255), new Color32(160, 30, 30, 255));
            else
                view.SetColors(new Color32(220, 150, 50, 255), new Color32(160, 100, 20, 255));
            view.Rebuild();
            view.SetVisible(true);

            SetObject(controller, "bulletPool", bulletPool);
            SetObject(controller, "tankView", view);
            SetLayerMask(controller, "wallMask", LayerMask.GetMask("Wall"));
            SetEnum(input, "player", index == 1 ? (int)PlayerIndex.Player2 : (int)PlayerIndex.Player1);
            input.enabled = false;

            SetObject(ai, "roundManager", roundManager);
            SetObject(ai, "bulletPool", bulletPool);
            SetLayerMask(ai, "wallMask", LayerMask.GetMask("Wall"));
            SetLayerMask(ai, "tankMask", LayerMask.GetMask("Tank"));
            ai.enabled = false;
            return controller;
        }

        private static void CreateUiRig(SceneSystems systems, bool includeMenu)
        {
            var canvasObject = new GameObject("Canvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<GraphicRaycaster>();
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(GameConfig.ScreenWidth, GameConfig.ScreenHeight);
            scaler.matchWidthOrHeight = 0.5f;

            CreateHud(canvasObject.transform, systems, includeMenu);
            CreateOverlay(canvasObject.transform, systems);
            if (includeMenu)
                CreateMenus(canvasObject.transform, systems.GameManager);
        }

        private static void CreateHud(Transform canvas, SceneSystems systems, bool hiddenByDefault)
        {
            var hudControllerObject = new GameObject("HUDController");
            hudControllerObject.transform.SetParent(canvas, false);
            var controllerRect = hudControllerObject.AddComponent<RectTransform>();
            controllerRect.anchorMin = Vector2.zero;
            controllerRect.anchorMax = Vector2.one;
            controllerRect.anchoredPosition = Vector2.zero;
            controllerRect.sizeDelta = Vector2.zero;
            var controller = hudControllerObject.AddComponent<HudController>();

            var hudRoot = CreatePanel("HUD", hudControllerObject.transform, new Color32(245, 245, 245, 230), new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 56f));
            var labels = new Text[3];
            labels[0] = CreateText("P1", hudRoot.transform, "P1  0 分  LIVE", 16, TextAnchor.MiddleLeft, new Vector2(0f, 0f), new Vector2(0.34f, 1f), Vector2.zero, Vector2.zero);
            labels[1] = CreateText("P2", hudRoot.transform, "P2  0 分  LIVE", 16, TextAnchor.MiddleRight, new Vector2(0.66f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            labels[2] = CreateText("AI", hudRoot.transform, "P3  0 分  LIVE", 14, TextAnchor.MiddleRight, new Vector2(0.68f, 0f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
            var round = CreateText("Round", hudRoot.transform, "第 0 回合", 15, TextAnchor.MiddleCenter, new Vector2(0.34f, 0.5f), new Vector2(0.66f, 1f), Vector2.zero, Vector2.zero);
            var goal = CreateText("Goal", hudRoot.transform, "目标 5 分", 14, TextAnchor.MiddleCenter, new Vector2(0.34f, 0f), new Vector2(0.66f, 0.5f), Vector2.zero, Vector2.zero);

            SetObject(controller, "roundManager", systems.RoundManager);
            SetObjectArray(controller, "tankLabels", labels);
            SetObject(controller, "roundLabel", round);
            SetObject(controller, "goalLabel", goal);
            SetObject(controller, "hudRoot", hudRoot);
            hudRoot.SetActive(!hiddenByDefault);
        }

        private static void CreateOverlay(Transform canvas, SceneSystems systems)
        {
            var overlayControllerObject = new GameObject("RoundOverlayController");
            overlayControllerObject.transform.SetParent(canvas, false);
            var controllerRect = overlayControllerObject.AddComponent<RectTransform>();
            controllerRect.anchorMin = Vector2.zero;
            controllerRect.anchorMax = Vector2.one;
            controllerRect.anchoredPosition = Vector2.zero;
            controllerRect.sizeDelta = Vector2.zero;
            var controller = overlayControllerObject.AddComponent<RoundOverlayController>();

            var overlay = CreatePanel("RoundOverlay", overlayControllerObject.transform, new Color32(0, 0, 0, 120), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            overlay.SetActive(false);
            var title = CreateText("Title", overlay.transform, "3", 72, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(700f, 100f));
            title.color = Color.white;
            var subtitle = CreateText("Subtitle", overlay.transform, string.Empty, 26, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -45f), new Vector2(760f, 60f));
            subtitle.color = Color.white;

            var pauseActions = CreatePanel("PauseActions", overlay.transform, new Color32(0, 0, 0, 0), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -128f), new Vector2(620f, 54f));
            var resume = CreateButton("ResumeButton", pauseActions.transform, "继续", new Vector2(0.5f, 0.5f), new Vector2(150f, 46f));
            var restart = CreateButton("RestartButton", pauseActions.transform, "重新开始", new Vector2(0.5f, 0.5f), new Vector2(150f, 46f));
            var pauseMenu = CreateButton("MenuButton", pauseActions.transform, "返回主菜单", new Vector2(0.5f, 0.5f), new Vector2(150f, 46f));
            SetAnchoredX(resume, -170f);
            SetAnchoredX(restart, 0f);
            SetAnchoredX(pauseMenu, 170f);
            UnityEventTools.AddPersistentListener(resume.onClick, systems.GameManager.Resume);
            UnityEventTools.AddPersistentListener(restart.onClick, systems.GameManager.RestartMatch);
            UnityEventTools.AddPersistentListener(pauseMenu.onClick, systems.GameManager.ReturnToMainMenu);
            pauseActions.SetActive(false);

            var gameOverActions = CreatePanel("GameOverActions", overlay.transform, new Color32(0, 0, 0, 0), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -128f), new Vector2(430f, 54f));
            var rematch = CreateButton("RematchButton", gameOverActions.transform, "再来一局", new Vector2(0.5f, 0.5f), new Vector2(150f, 46f));
            var gameOverMenu = CreateButton("GameOverMenuButton", gameOverActions.transform, "返回主菜单", new Vector2(0.5f, 0.5f), new Vector2(150f, 46f));
            SetAnchoredX(rematch, -95f);
            SetAnchoredX(gameOverMenu, 95f);
            UnityEventTools.AddPersistentListener(rematch.onClick, systems.GameManager.RestartMatch);
            UnityEventTools.AddPersistentListener(gameOverMenu.onClick, systems.GameManager.ReturnToMainMenu);
            gameOverActions.SetActive(false);

            SetObject(controller, "gameManager", systems.GameManager);
            SetObject(controller, "roundManager", systems.RoundManager);
            SetObject(controller, "overlayRoot", overlay);
            SetObject(controller, "titleText", title);
            SetObject(controller, "subtitleText", subtitle);
            SetObject(controller, "pauseActionsRoot", pauseActions);
            SetObject(controller, "gameOverActionsRoot", gameOverActions);
        }

        private static void CreateMenus(Transform canvas, GameManager gameManager)
        {
            var menuRoot = new GameObject("MenuController");
            menuRoot.transform.SetParent(canvas, false);
            var rootRect = menuRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = Vector2.zero;
            var menu = menuRoot.AddComponent<MenuController>();

            var main = CreatePanel("MainMenuPanel", menuRoot.transform, new Color32(255, 255, 255, 245), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 360f));
            ConfigureVerticalLayout(main, 28, 18, TextAnchor.MiddleCenter);
            var title = CreateText("Title", main.transform, "坦克动荡", 42, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayoutElement(title.gameObject, 360f, 72f);
            var pvp = CreateButton("PvpButton", main.transform, "1  双人对战", Vector2.zero, new Vector2(320f, 48f));
            var pvai = CreateButton("PvAiButton", main.transform, "2  单人 vs AI", Vector2.zero, new Vector2(320f, 48f));
            var pvpai = CreateButton("PvpAiButton", main.transform, "3  双人 + AI", Vector2.zero, new Vector2(320f, 48f));
            AddLayoutElement(pvp.gameObject, 320f, 48f);
            AddLayoutElement(pvai.gameObject, 320f, 48f);
            AddLayoutElement(pvpai.gameObject, 320f, 48f);
            UnityEventTools.AddPersistentListener(pvp.onClick, menu.SelectPvp);
            UnityEventTools.AddPersistentListener(pvai.onClick, menu.SelectPvAi);
            UnityEventTools.AddPersistentListener(pvpai.onClick, menu.SelectPvpAi);

            var map = CreatePanel("MapSelectPanel", menuRoot.transform, new Color32(255, 255, 255, 245), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            CreateText("Title", map.transform, "选择地图", 38, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(500f, 60f));
            var selected = CreateText("SelectedMap", map.transform, "当前地图：随机生成", 22, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(500f, 40f));
            var mapButtons = new Button[6];
            var names = new[] { "1 开放", "2 走廊", "3 对称", "4 螺旋", "5 格栅", "6 随机" };
            for (var i = 0; i < mapButtons.Length; i++)
            {
                var col = i % 3;
                var row = i / 3;
                var button = CreateButton($"MapButton_{i + 1}", map.transform, names[i], new Vector2(0.25f + col * 0.25f, 0.66f - row * 0.22f), new Vector2(190f, 120f));
                AddThumbnail(button.transform, (MapKind)i);
                UnityEventTools.AddIntPersistentListener(button.onClick, menu.SelectMapByIndex, i);
                mapButtons[i] = button;
            }
            var prev = CreateButton("PrevScore", map.transform, "<", new Vector2(0.39f, 0.18f), new Vector2(50f, 42f));
            var scoreText = CreateText("WinScore", map.transform, GameConfig.DefaultWinScore.ToString(), 28, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.18f), new Vector2(0.5f, 0.18f), Vector2.zero, new Vector2(90f, 42f));
            var next = CreateButton("NextScore", map.transform, ">", new Vector2(0.61f, 0.18f), new Vector2(50f, 42f));
            CreateText("ScoreLabel", map.transform, "胜利分数", 20, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.25f), new Vector2(0.5f, 0.25f), Vector2.zero, new Vector2(180f, 34f));
            var confirm = CreateButton("ConfirmMap", map.transform, "开始", new Vector2(0.58f, 0.08f), new Vector2(150f, 48f));
            var backMap = CreateButton("BackMap", map.transform, "返回", new Vector2(0.42f, 0.08f), new Vector2(150f, 48f));
            UnityEventTools.AddPersistentListener(prev.onClick, menu.PreviousWinScore);
            UnityEventTools.AddPersistentListener(next.onClick, menu.NextWinScore);
            UnityEventTools.AddPersistentListener(confirm.onClick, menu.ConfirmMapSelection);
            UnityEventTools.AddPersistentListener(backMap.onClick, menu.BackToMain);

            var difficulty = CreatePanel("DifficultyPanel", menuRoot.transform, new Color32(255, 255, 255, 245), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            CreateText("Title", difficulty.transform, "选择 AI 难度", 42, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(520f, 70f));
            var normal = CreateButton("NormalButton", difficulty.transform, "1  普通", new Vector2(0.5f, 0.54f), new Vector2(260f, 56f));
            var hard = CreateButton("HardButton", difficulty.transform, "2  困难", new Vector2(0.5f, 0.43f), new Vector2(260f, 56f));
            var backDifficulty = CreateButton("BackDifficulty", difficulty.transform, "返回", new Vector2(0.5f, 0.30f), new Vector2(180f, 48f));
            UnityEventTools.AddPersistentListener(normal.onClick, menu.SelectNormalDifficulty);
            UnityEventTools.AddPersistentListener(hard.onClick, menu.SelectHardDifficulty);
            UnityEventTools.AddPersistentListener(backDifficulty.onClick, menu.BackToMapSelect);

            SetObject(menu, "gameManager", gameManager);
            SetObject(menu, "mainPanel", main);
            SetObject(menu, "mapPanel", map);
            SetObject(menu, "difficultyPanel", difficulty);
            SetObject(menu, "selectedMapText", selected);
            SetObject(menu, "winScoreText", scoreText);
            SetObjectArray(menu, "mapButtons", mapButtons);
            main.SetActive(true);
            map.SetActive(false);
            difficulty.SetActive(false);
        }

        private static void ConfigureVerticalLayout(GameObject panel, int padding, float spacing, TextAnchor alignment)
        {
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.spacing = spacing;
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        private static void AddLayoutElement(GameObject go, float width, float height)
        {
            var layout = go.GetComponent<LayoutElement>();
            if (layout == null)
                layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = height;
            layout.minWidth = width;
            layout.minHeight = height;
        }

        private static void SetAnchoredX(Component component, float x)
        {
            if (component == null)
                return;

            var rect = component.GetComponent<RectTransform>();
            if (rect != null)
                rect.anchoredPosition = new Vector2(x, 0f);
        }

        private static void AddThumbnail(Transform buttonTransform, MapKind mapKind)
        {
            var go = new GameObject("Thumbnail");
            go.transform.SetParent(buttonTransform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -50f);
            rt.sizeDelta = new Vector2(160f, 70f);
            go.AddComponent<RawImage>();
            var thumbnail = go.AddComponent<MapThumbnailRenderer>();
            SetEnum(thumbnail, "mapKind", (int)mapKind);
        }

        private static GameObject CreatePanel(string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var image = go.AddComponent<Image>();
            image.color = color;
            return go;
        }

        private static Text CreateText(string name, Transform parent, string value, int size, TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 rectSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = pos;
            rt.sizeDelta = rectSize;
            var text = go.AddComponent<Text>();
            text.text = value;
            var font = GetDefaultUiFont();
            if (font != null)
                text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = new Color32(35, 35, 35, 255);
            text.raycastTarget = false;
            return text;
        }

        private static Font GetDefaultUiFont()
        {
            var font = TryGetBuiltinFont("LegacyRuntime.ttf");
            if (font != null)
                return font;

            font = TryGetBuiltinFont("Arial.ttf");
            if (font != null)
                return font;

            Debug.LogWarning("Tank Trouble setup could not load a built-in UI font. Text objects will keep Unity's default font reference.");
            return null;
        }

        private static Font TryGetBuiltinFont(string fontName)
        {
            try
            {
                return Resources.GetBuiltinResource<Font>(fontName);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Tank Trouble setup skipped built-in font '{fontName}': {ex.Message}");
                return null;
            }
        }

        private static Button CreateButton(string name, Transform parent, string label, Vector2 anchor, Vector2 size)
        {
            var go = CreatePanel(name, parent, new Color32(248, 248, 248, 255), anchor, anchor, Vector2.zero, size);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.raycastTarget = true;

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = new Color32(248, 248, 248, 255);
            colors.highlightedColor = new Color32(220, 232, 255, 255);
            colors.pressedColor = new Color32(190, 210, 245, 255);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color32(210, 210, 210, 160);
            button.colors = colors;

            var text = CreateText("Label", go.transform, label, 20, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var textRect = text.GetComponent<RectTransform>();
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            text.color = new Color32(35, 35, 35, 255);
            text.transform.SetAsLastSibling();
            return button;
        }

        private static void CreateEventSystem()
        {
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainMenuScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true)
            };
        }

        private static bool BuildSettingsConfigured()
        {
            var scenes = EditorBuildSettings.scenes;
            if (scenes == null || scenes.Length < 2)
                return false;
            return scenes[0].path == MainMenuScenePath && scenes[0].enabled && scenes[1].path == GameScenePath && scenes[1].enabled;
        }

        private static void ConfigureInputSettings()
        {
            var projectSettingsPath = "ProjectSettings/ProjectSettings.asset";
            if (!File.Exists(projectSettingsPath))
                return;
            var text = File.ReadAllText(projectSettingsPath);
            text = text.Replace("activeInputHandler: 0", "activeInputHandler: 2");
            text = text.Replace("activeInputHandler: 1", "activeInputHandler: 2");
            File.WriteAllText(projectSettingsPath, text);
        }

        private static void SetObject(Component target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property != null)
                property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetObjectArray(Component target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property != null)
            {
                property.arraySize = values.Length;
                for (var i = 0; i < values.Length; i++)
                    property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetBool(Component target, string field, bool value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property != null)
                property.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetInt(Component target, string field, int value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property != null)
                property.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetEnum(Component target, string field, int value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property != null)
                property.enumValueIndex = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetLayerMask(Component target, string field, int value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property != null)
                property.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private readonly struct SceneSystems
        {
            public readonly GameManager GameManager;
            public readonly RoundManager RoundManager;
            public readonly ScoreManager ScoreManager;
            public readonly MapRenderer MapRenderer;
            public readonly BulletPool BulletPool;
            public readonly TankController[] Tanks;

            public SceneSystems(GameManager gameManager, RoundManager roundManager, ScoreManager scoreManager, MapRenderer mapRenderer, BulletPool bulletPool, TankController[] tanks)
            {
                GameManager = gameManager;
                RoundManager = roundManager;
                ScoreManager = scoreManager;
                MapRenderer = mapRenderer;
                BulletPool = bulletPool;
                Tanks = tanks;
            }
        }
    }
}
