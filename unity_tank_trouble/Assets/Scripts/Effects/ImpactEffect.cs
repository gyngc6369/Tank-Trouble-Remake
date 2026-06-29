using UnityEngine;
using TankTrouble.Core;

namespace TankTrouble.Effects
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class ImpactEffect : MonoBehaviour
    {
        public const float DefaultDuration = 0.2f;
        public const float DefeatDuration = 0.45f;

        private static Sprite whiteSprite;

        private SpriteRenderer spriteRenderer;
        private Color baseColor;
        private float elapsed;
        private float duration = DefaultDuration;
        private float baseScale;
        private Vector2 drift;

        public static void Spawn(Vector2 worldPosition, Color color)
        {
            CreateEffect("ImpactEffect", worldPosition, new Color(color.r, color.g, color.b, 0.7f), 18f / CoordinateUtil.PixelsPerUnit, DefaultDuration, Vector2.zero);
        }

        public static void SpawnDefeat(Vector2 worldPosition, Color color)
        {
            var flashColor = new Color(color.r, color.g, color.b, 0.85f);
            CreateEffect("DefeatFlash", worldPosition, flashColor, 36f / CoordinateUtil.PixelsPerUnit, DefeatDuration, Vector2.zero);

            const int fragmentCount = 8;
            for (var i = 0; i < fragmentCount; i++)
            {
                var angle = i * (360f / fragmentCount);
                var direction = (Vector2)(Quaternion.Euler(0f, 0f, angle) * Vector2.up);
                CreateEffect("DefeatFragment", worldPosition + direction * 0.03f, flashColor, 8f / CoordinateUtil.PixelsPerUnit, DefeatDuration * 0.85f, direction * 1.15f);
            }
        }

        private static void CreateEffect(string effectName, Vector2 worldPosition, Color color, float scale, float effectDuration, Vector2 effectDrift)
        {
            var go = new GameObject(effectName);
            go.transform.position = worldPosition;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = GetWhiteSprite();
            renderer.color = color;
            renderer.sortingOrder = 30;

            var effect = go.AddComponent<ImpactEffect>();
            effect.Initialize(renderer.color, scale, effectDuration, effectDrift);
        }

        private void Awake()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void Initialize(Color color, float scale, float effectDuration, Vector2 effectDrift)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            baseColor = color;
            baseScale = scale;
            drift = effectDrift;
            duration = Mathf.Max(0.01f, effectDuration);
            transform.localScale = Vector3.one * (baseScale * 0.35f);
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            elapsed += dt;
            var t = Mathf.Clamp01(elapsed / duration);
            var scale = Mathf.Lerp(baseScale * 0.35f, baseScale * 1.45f, t);
            transform.localScale = Vector3.one * scale;
            transform.position += (Vector3)(drift * dt);

            if (spriteRenderer != null)
            {
                var color = baseColor;
                color.a = Mathf.Lerp(baseColor.a, 0f, t);
                spriteRenderer.color = color;
            }

            if (elapsed >= duration)
                Destroy(gameObject);
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
