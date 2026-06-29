using System.Collections.Generic;
using UnityEngine;
using TankTrouble.Config;
using TankTrouble.Core;

namespace TankTrouble.Entities
{
    public sealed class BulletPool : MonoBehaviour
    {
        [SerializeField] private BulletController bulletPrefab;
        [SerializeField] private int preloadCount = 16;
        [SerializeField] private Color defaultBulletColor = Color.white;

        private readonly Queue<BulletController> inactive = new Queue<BulletController>();
        private readonly List<BulletController> active = new List<BulletController>(32);
        private static Sprite whiteSprite;

        public IReadOnlyList<BulletController> ActiveBullets => active;

        private void Awake()
        {
            if (bulletPrefab == null)
                bulletPrefab = CreateRuntimePrefab();

            for (var i = 0; i < preloadCount; i++)
                inactive.Enqueue(CreateInstance());
        }

        public BulletController Spawn(TankController owner, Vector2 worldPosition, Vector2 direction, Color color)
        {
            var bullet = inactive.Count > 0 ? inactive.Dequeue() : CreateInstance();
            active.Add(bullet);
            bullet.Initialize(this, owner, worldPosition, direction, color);
            return bullet;
        }

        public void Release(BulletController bullet)
        {
            if (bullet == null)
                return;

            active.Remove(bullet);
            bullet.gameObject.SetActive(false);
            inactive.Enqueue(bullet);
        }

        public void ClearActive()
        {
            for (var i = active.Count - 1; i >= 0; i--)
                Release(active[i]);
        }

        private BulletController CreateInstance()
        {
            var bullet = Instantiate(bulletPrefab, transform);
            bullet.gameObject.SetActive(false);
            return bullet;
        }

        private BulletController CreateRuntimePrefab()
        {
            var go = new GameObject("BulletRuntimePrefab");
            go.SetActive(false);
            go.layer = LayerMask.NameToLayer("Bullet");

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetWhiteSprite();
            sr.color = defaultBulletColor;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var collider = go.AddComponent<CircleCollider2D>();
            collider.radius = GameConfig.BulletRadius / CoordinateUtil.PixelsPerUnit;

            var controller = go.AddComponent<BulletController>();
            go.transform.SetParent(transform, false);
            return controller;
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
            var diameterWorld = (GameConfig.BulletRadius * 2f) / CoordinateUtil.PixelsPerUnit;
            var pixelsPerUnit = 1f / diameterWorld;
            whiteSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), pixelsPerUnit);
            return whiteSprite;
        }
    }
}
