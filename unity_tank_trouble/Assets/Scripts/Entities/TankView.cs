using UnityEngine;
using TankTrouble.Config;
using TankTrouble.Core;

namespace TankTrouble.Entities
{
    [ExecuteAlways]
    public sealed class TankView : MonoBehaviour
    {
        private const int BodySortingOrder = 20;
        private const int BarrelSortingOrder = 21;

        [SerializeField] private Color bodyColor = new Color32(60, 100, 220, 255);
        [SerializeField] private Color barrelColor = new Color32(30, 60, 160, 255);
        [SerializeField] private Transform bodyVisual;
        [SerializeField] private Transform barrelVisual;

        private static Sprite whiteSprite;

        public Color BodyColor => ForceOpaque(bodyColor);
        public Color BarrelColor => ForceOpaque(barrelColor);

        private void Awake()
        {
            Rebuild();
        }

        private void OnEnable()
        {
            Rebuild();
        }

        private void Start()
        {
            Rebuild();
        }

        private void OnValidate()
        {
            Rebuild();
        }

        public void SetColors(Color newBodyColor, Color newBarrelColor)
        {
            bodyColor = ForceOpaque(newBodyColor);
            barrelColor = ForceOpaque(newBarrelColor);
            Rebuild();
        }

        public void SetVisible(bool visible)
        {
            EnsureVisuals();
            SetVisualActive(bodyVisual, visible);
            SetVisualActive(barrelVisual, visible);
        }

        public void Rebuild()
        {
            EnsureVisuals();

            var bodyScale = new Vector3(
                Mathf.Max(0.001f, GameConfig.TankBodyWidth / CoordinateUtil.PixelsPerUnit),
                Mathf.Max(0.001f, GameConfig.TankBodyHeight / CoordinateUtil.PixelsPerUnit),
                1f);
            var barrelScale = new Vector3(
                Mathf.Max(0.001f, GameConfig.BarrelWidth / CoordinateUtil.PixelsPerUnit),
                Mathf.Max(0.001f, GameConfig.BarrelLength / CoordinateUtil.PixelsPerUnit),
                1f);

            bodyVisual.localPosition = Vector3.zero;
            bodyVisual.localRotation = Quaternion.identity;
            bodyVisual.localScale = bodyScale;

            var barrelOffset = (GameConfig.TankBodyHeight * 0.5f + GameConfig.BarrelLength * 0.5f) / CoordinateUtil.PixelsPerUnit;
            barrelVisual.localPosition = new Vector3(0f, barrelOffset, 0f);
            barrelVisual.localRotation = Quaternion.identity;
            barrelVisual.localScale = barrelScale;

            ApplyRenderer(bodyVisual, BodyColor, BodySortingOrder);
            ApplyRenderer(barrelVisual, BarrelColor, BarrelSortingOrder);
        }

        private void EnsureVisuals()
        {
            if (bodyVisual == null)
                bodyVisual = FindOrCreateVisual("Body");
            if (barrelVisual == null)
                barrelVisual = FindOrCreateVisual("Barrel");

            EnsureRenderer(bodyVisual, BodySortingOrder);
            EnsureRenderer(barrelVisual, BarrelSortingOrder);
        }

        private Transform FindOrCreateVisual(string objectName)
        {
            var existing = transform.Find(objectName);
            if (existing != null)
                return existing;

            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        private static void SetVisualActive(Transform visual, bool visible)
        {
            if (visual != null)
                visual.gameObject.SetActive(visible);
        }

        private void EnsureRenderer(Transform visual, int sortingOrder)
        {
            if (visual == null)
                return;

            var sr = visual.GetComponent<SpriteRenderer>();
            if (sr == null)
                sr = visual.gameObject.AddComponent<SpriteRenderer>();

            sr.sprite = GetWhiteSprite();
            sr.sortingOrder = sortingOrder;
            sr.enabled = true;
        }

        private void ApplyRenderer(Transform visual, Color color, int sortingOrder)
        {
            if (visual == null)
                return;

            var sr = visual.GetComponent<SpriteRenderer>();
            if (sr == null)
                sr = visual.gameObject.AddComponent<SpriteRenderer>();

            sr.sprite = GetWhiteSprite();
            sr.color = ForceOpaque(color);
            sr.sortingOrder = sortingOrder;
            sr.enabled = true;
        }

        private static Color ForceOpaque(Color color)
        {
            color.a = 1f;
            return color;
        }

        private static Sprite GetWhiteSprite()
        {
            if (whiteSprite != null)
                return whiteSprite;

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            whiteSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return whiteSprite;
        }
    }
}
