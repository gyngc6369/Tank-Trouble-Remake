using UnityEngine;
using TankTrouble.Config;
using TankTrouble.Core;
using TankTrouble.Audio;
using TankTrouble.Effects;

namespace TankTrouble.Entities
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class BulletController : MonoBehaviour
    {
        private enum DespawnReason
        {
            None,
            HitTank,
            MaxBounces,
            PoolFallback
        }

        private const float OwnerHitGraceTime = 0.16f;
        private const float OwnerHitGraceDistance = 0.34f;
        private const float WallBounceDebounceTime = 0.035f;
        private const float WallBounceDebounceDistance = 0.035f;

        [SerializeField] private LayerMask wallMask;
        [SerializeField] private LayerMask tankMask;
        [SerializeField] private SpriteRenderer spriteRenderer;

        private Rigidbody2D body;
        private CircleCollider2D circleCollider;
        private BulletPool pool;
        private TankController owner;
        private int bounceCount;
        private bool alive;
        private Vector2 previousVelocity;
        private Vector2 spawnPosition;
        private Vector2 lastWallHitPosition;
        private float spawnTime;
        private float lastWallHitTime;
        private Collider2D lastWallCollider;
        private DespawnReason lastDespawnReason;

        public TankController Owner => owner;
        public int BounceCount => bounceCount;
        public bool Alive => alive;
        public Vector2 WorldPosition => body != null ? body.position : (Vector2)transform.position;
        public Vector2 Velocity => body != null ? body.velocity : Vector2.zero;
        public string LastDespawnReason => lastDespawnReason.ToString();

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            circleCollider = GetComponent<CircleCollider2D>();
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.drag = 0f;
            body.angularDrag = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            circleCollider.radius = GameConfig.BulletRadius / CoordinateUtil.PixelsPerUnit;
            circleCollider.isTrigger = false;

            if (wallMask.value == 0)
                wallMask = LayerMask.GetMask("Wall");
            if (tankMask.value == 0)
                tankMask = LayerMask.GetMask("Tank");
        }

        public void Initialize(BulletPool owningPool, TankController bulletOwner, Vector2 worldPosition, Vector2 direction, Color color)
        {
            pool = owningPool;
            owner = bulletOwner;
            bounceCount = 0;
            alive = true;
            lastDespawnReason = DespawnReason.None;
            spawnTime = Time.time;
            spawnPosition = worldPosition;
            lastWallHitTime = -10f;
            lastWallHitPosition = worldPosition;
            lastWallCollider = null;
            transform.position = worldPosition;
            transform.rotation = Quaternion.identity;
            gameObject.SetActive(true);

            if (spriteRenderer != null)
                spriteRenderer.color = color;

            var velocity = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.up;
            body.velocity = velocity * (GameConfig.BulletSpeed / CoordinateUtil.PixelsPerUnit);
            previousVelocity = body.velocity;
            body.angularVelocity = 0f;
        }

        private void FixedUpdate()
        {
            if (!alive)
                return;

            KeepSpeedConstant();
            previousVelocity = body.velocity;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!alive)
                return;

            var other = collision.collider;
            if (other == null)
                return;

            if (IsInMask(other.gameObject.layer, tankMask))
            {
                var tank = other.GetComponentInParent<TankController>();
                if (tank != null && tank.Alive)
                {
                    if (tank == owner && ShouldIgnoreOwnerHit())
                        return;

                    var color = spriteRenderer != null ? spriteRenderer.color : Color.white;
                    ImpactEffect.Spawn(body.position, color);
                    AudioManager.PlayExplosion();
                    tank.ApplyHit(owner);
                    Despawn(DespawnReason.HitTank);
                    return;
                }
            }

            if (IsInMask(other.gameObject.layer, wallMask))
                HandleWallBounce(collision);
        }

        private void HandleWallBounce(Collision2D collision)
        {
            if (ShouldIgnoreDuplicateWallBounce(collision.collider))
                return;

            if (bounceCount >= GameConfig.MaxBounces)
            {
                Despawn(DespawnReason.MaxBounces);
                return;
            }

            bounceCount++;
            var normal = collision.contactCount > 0 ? collision.GetContact(0).normal : -body.velocity.normalized;
            var incoming = previousVelocity.sqrMagnitude > 0.0001f ? previousVelocity.normalized : body.velocity.normalized;
            var reflected = Vector2.Reflect(incoming, normal);
            body.velocity = reflected.normalized * (GameConfig.BulletSpeed / CoordinateUtil.PixelsPerUnit);

            if (collision.contactCount > 0)
            {
                var contact = collision.GetContact(0);
                var radius = GameConfig.BulletRadius / CoordinateUtil.PixelsPerUnit;
                body.position = contact.point + reflected.normalized * (radius + 0.002f);
                transform.position = body.position;
                lastWallHitPosition = contact.point;
            }
            else
            {
                lastWallHitPosition = body.position;
            }

            lastWallHitTime = Time.time;
            lastWallCollider = collision.collider;
        }

        private bool ShouldIgnoreOwnerHit()
        {
            var age = Time.time - spawnTime;
            if (age <= OwnerHitGraceTime)
                return true;

            return age <= OwnerHitGraceTime * 1.5f
                && Vector2.Distance(body.position, spawnPosition) <= OwnerHitGraceDistance;
        }

        private bool ShouldIgnoreDuplicateWallBounce(Collider2D wallCollider)
        {
            if (wallCollider == null)
                return false;

            if (wallCollider != lastWallCollider)
                return false;

            if (Time.time - lastWallHitTime > WallBounceDebounceTime)
                return false;

            return Vector2.Distance(body.position, lastWallHitPosition) <= WallBounceDebounceDistance;
        }

        private void KeepSpeedConstant()
        {
            var speed = GameConfig.BulletSpeed / CoordinateUtil.PixelsPerUnit;
            if (body.velocity.sqrMagnitude < 0.0001f)
                return;

            body.velocity = body.velocity.normalized * speed;
        }

        private void Despawn(DespawnReason reason)
        {
            lastDespawnReason = reason;
            alive = false;
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;

            if (pool != null)
                pool.Release(this);
            else
            {
                lastDespawnReason = DespawnReason.PoolFallback;
                gameObject.SetActive(false);
            }
        }

        private static bool IsInMask(int layer, LayerMask mask)
        {
            return (mask.value & (1 << layer)) != 0;
        }
    }
}
