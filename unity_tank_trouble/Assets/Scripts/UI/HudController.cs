using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TankTrouble.Core;
using TankTrouble.Entities;

namespace TankTrouble.UI
{
    public sealed class HudController : MonoBehaviour
    {
        private const float HudHeight = 56f;

        [SerializeField] private RoundManager roundManager;
        [SerializeField] private Text[] tankLabels;
        [SerializeField] private Text roundLabel;
        [SerializeField] private Text goalLabel;
        [SerializeField] private GameObject hudRoot;

        private readonly StringBuilder ammoBuilder = new StringBuilder(16);

        private void Awake()
        {
            if (roundManager == null)
                roundManager = FindObjectOfType<RoundManager>();
        }

        private void OnEnable()
        {
            ApplyHudLayout();
        }

        private void Start()
        {
            ApplyHudLayout();
        }

        private void Update()
        {
            if (roundManager == null)
                return;

            if (hudRoot != null)
                hudRoot.SetActive(roundManager.Phase != RoundPhase.Inactive);

            ApplyHudLayout();
            UpdateTankLabels();
            UpdateCenterLabels();
        }

        private void UpdateTankLabels()
        {
            if (tankLabels == null || roundManager.ActiveTanks == null)
                return;

            var scoreManager = roundManager.ScoreManager;
            for (var i = 0; i < tankLabels.Length; i++)
            {
                var label = tankLabels[i];
                if (label == null)
                    continue;

                if (i >= roundManager.ActiveTanks.Count || roundManager.ActiveTanks[i] == null)
                {
                    label.gameObject.SetActive(false);
                    continue;
                }

                var tank = roundManager.ActiveTanks[i];
                label.gameObject.SetActive(true);
                var status = tank.Alive ? "LIVE" : "KO";
                var score = scoreManager != null ? scoreManager.GetScore(tank) : 0;
                label.text = $"P{i + 1}  {score} 分  {status}  {BuildAmmoText(tank)}";
                label.color = tank.Alive ? new Color32(35, 35, 35, 255) : new Color32(120, 120, 120, 255);
            }
        }

        private void UpdateCenterLabels()
        {
            if (roundLabel != null)
                roundLabel.text = $"第 {roundManager.RoundNumber} 回合";
            if (goalLabel != null)
                goalLabel.text = $"目标 {roundManager.TargetWinScore} 分";
        }

        private void ApplyHudLayout()
        {
            if (hudRoot != null)
            {
                var hudRect = hudRoot.GetComponent<RectTransform>();
                if (hudRect != null)
                {
                    hudRect.anchorMin = new Vector2(0f, 1f);
                    hudRect.anchorMax = new Vector2(1f, 1f);
                    hudRect.pivot = new Vector2(0.5f, 1f);
                    hudRect.anchoredPosition = Vector2.zero;
                    hudRect.sizeDelta = new Vector2(0f, HudHeight);
                }
            }

            LayoutTankLabels();
            LayoutCenterLabels();
        }

        private void LayoutTankLabels()
        {
            if (tankLabels == null)
                return;

            ConfigureTankLabel(0, new Vector2(0f, 0f), new Vector2(0.34f, 1f), new Vector2(12f, 0f), new Vector2(-18f, 0f), TextAnchor.MiddleLeft, 16);

            var activeCount = roundManager != null && roundManager.ActiveTanks != null ? roundManager.ActiveTanks.Count : 2;
            if (activeCount > 2)
            {
                ConfigureTankLabel(1, new Vector2(0.68f, 0.5f), new Vector2(1f, 1f), new Vector2(8f, 0f), new Vector2(-12f, -2f), TextAnchor.MiddleRight, 14);
                ConfigureTankLabel(2, new Vector2(0.68f, 0f), new Vector2(1f, 0.5f), new Vector2(8f, 2f), new Vector2(-12f, 0f), TextAnchor.MiddleRight, 14);
            }
            else
            {
                ConfigureTankLabel(1, new Vector2(0.66f, 0f), new Vector2(1f, 1f), new Vector2(12f, 0f), new Vector2(-12f, 0f), TextAnchor.MiddleRight, 16);
                if (tankLabels.Length > 2 && tankLabels[2] != null)
                    tankLabels[2].gameObject.SetActive(false);
            }
        }

        private void ConfigureTankLabel(int index, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, TextAnchor alignment, int fontSize)
        {
            if (tankLabels == null || index < 0 || index >= tankLabels.Length || tankLabels[index] == null)
                return;

            var label = tankLabels[index];
            var rect = label.GetComponent<RectTransform>();
            if (rect == null)
                return;

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            label.alignment = alignment;
            label.fontSize = fontSize;
            label.color = new Color32(35, 35, 35, 255);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 11;
            label.resizeTextMaxSize = fontSize;
        }

        private void LayoutCenterLabels()
        {
            ConfigureCenterLabel(roundLabel, new Vector2(0.34f, 0.5f), new Vector2(0.66f, 1f), new Vector2(0f, -2f), "round");
            ConfigureCenterLabel(goalLabel, new Vector2(0.34f, 0f), new Vector2(0.66f, 0.5f), new Vector2(0f, 2f), "goal");
        }

        private void ConfigureCenterLabel(Text label, Vector2 anchorMin, Vector2 anchorMax, Vector2 offset, string labelName)
        {
            if (label == null)
                return;

            var rect = label.GetComponent<RectTransform>();
            if (rect == null)
                return;

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = new Vector2(8f, offset.y);
            rect.offsetMax = new Vector2(-8f, offset.y);
            label.alignment = TextAnchor.MiddleCenter;
            label.fontSize = labelName == "round" ? 15 : 14;
            label.color = new Color32(35, 35, 35, 255);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 11;
            label.resizeTextMaxSize = label.fontSize;
        }

        private string BuildAmmoText(TankController tank)
        {
            ammoBuilder.Length = 0;
            for (var i = 0; i < TankTrouble.Config.GameConfig.MaxAmmo; i++)
                ammoBuilder.Append(i < tank.Ammo ? '\u25cf' : '\u25cb');
            return ammoBuilder.ToString();
        }
    }
}
