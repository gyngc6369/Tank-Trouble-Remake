using System;
using UnityEngine;
using TankTrouble.Config;
using TankTrouble.Core;
using TankTrouble.Audio;
using TankTrouble.Effects;

namespace TankTrouble.Entities
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class TankController : MonoBehaviour
    {
        [SerializeField] private LayerMask wallMask;
        [SerializeField] private float collisionSkinPixels = 1f;
        [SerializeField] private BulletPool bulletPool;
        [SerializeField] private TankView tankView;

        private const float RotationStuckThresholdDegrees = 0.25f;
        private const float DepenetrationStepWorld = 0.006f;
        private const float MaxDepenetrationWorld = 0.036f;

        private readonly RaycastHit2D[] castHits = new RaycastHit2D[8];
        private readonly Collider2D[] overlapHits = new Collider2D[8];
        private Rigidbody2D body;
        private BoxCollider2D bodyCollider;
        private ContactFilter2D wallFilter;
        private TankInputCommand command;
        private float ammoTimer;
        private bool initialized;

        public event Action<TankController, TankController> Died;

        public bool Alive { get; private set; } = true;
        public int Ammo { get; private set; } = GameConfig.MaxAmmo;
        public float ShootCooldownRemaining { get; private set; }
        public TankController LastAttacker { get; private set; }

        public Vector2 VelocityForward => (Vector2)transform.up;
        private float CollisionSkinWorld => collisionSkinPixels / CoordinateUtil.PixelsPerUnit;

        private static readonly Vector2[] DepenetrationDirections =
        {
            Vector2.up,
            Vector2.down,
            Vector2.right,
            Vector2.left,
            new Vector2(1f, 1f).normalized,
            new Vector2(-1f, 1f).normalized,
            new Vector2(1f, -1f).normalized,
            new Vector2(-1f, -1f).normalized
        };

        public readonly struct MotionPrediction
        {
            public readonly Vector2 Position;
            public readonly float Rotation;
            public readonly Vector2 Forward;
            public readonly float RequestedDistance;
            public readonly float AllowedDistance;
            public readonly bool RotationBlocked;
            public readonly bool MoveBlocked;
            public readonly float WallClearance;

            public MotionPrediction(Vector2 position, float rotation, Vector2 forward, float requestedDistance, float allowedDistance, bool rotationBlocked, bool moveBlocked, float wallClearance)
            {
                Position = position;
                Rotation = rotation;
                Forward = forward;
                RequestedDistance = requestedDistance;
                AllowedDistance = allowedDistance;
                RotationBlocked = rotationBlocked;
                MoveBlocked = moveBlocked;
                WallClearance = wallClearance;
            }
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (initialized && body != null && bodyCollider != null)
                return;

            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<BoxCollider2D>();
            var tankLayer = LayerMask.NameToLayer("Tank");
            if (tankLayer >= 0)
                gameObject.layer = tankLayer;
            if (tankView == null)
                tankView = GetComponent<TankView>();

            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = false;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            bodyCollider.size = new Vector2(
                GameConfig.TankBodyWidth / CoordinateUtil.PixelsPerUnit,
                GameConfig.TankBodyHeight / CoordinateUtil.PixelsPerUnit);
            bodyCollider.offset = Vector2.zero;
            bodyCollider.isTrigger = false;

            if (wallMask.value == 0)
                wallMask = LayerMask.GetMask("Wall");

            wallFilter = new ContactFilter2D();
            wallFilter.SetLayerMask(wallMask);
            wallFilter.useLayerMask = true;
            wallFilter.useTriggers = false;

            if (bulletPool == null)
                bulletPool = FindObjectOfType<BulletPool>();

            initialized = body != null && bodyCollider != null;
        }

        private void FixedUpdate()
        {
            if (!Alive)
                return;

            var dt = Time.fixedDeltaTime;
            UpdateAmmo(dt);

            ApplyPredictedMotion(PredictCommand(command, dt));
            ResolveWallOverlap();

            if (ShootCooldownRemaining > 0f)
                ShootCooldownRemaining = Mathf.Max(0f, ShootCooldownRemaining - dt);

            if (command.FireHeld)
                TryShoot();
        }

        public void SetCommand(TankInputCommand inputCommand)
        {
            command = inputCommand;
        }

        public void ResetTank(Vector2 pixelPosition, float pygameAngleRadians = 0f)
        {
            EnsureInitialized();
            gameObject.SetActive(true);
            var world = CoordinateUtil.PixelToWorld(pixelPosition);
            var rotation = -pygameAngleRadians * Mathf.Rad2Deg;
            if (body != null)
            {
                body.position = world;
                body.rotation = rotation;
            }
            transform.SetPositionAndRotation(world, Quaternion.Euler(0f, 0f, rotation));
            Alive = true;
            LastAttacker = null;
            Ammo = GameConfig.MaxAmmo;
            ammoTimer = 0f;
            ShootCooldownRemaining = 0f;
            command = TankInputCommand.None;
            if (bodyCollider != null)
                bodyCollider.enabled = true;
            if (tankView != null)
                tankView.SetVisible(true);
        }

        public void ApplyHit(TankController attacker)
        {
            EnsureInitialized();
            if (!Alive)
                return;

            Alive = false;
            LastAttacker = attacker;
            command = TankInputCommand.None;
            if (bodyCollider != null)
                bodyCollider.enabled = false;
            var defeatColor = tankView != null ? tankView.BodyColor : Color.white;
            var defeatPosition = body != null ? body.position : (Vector2)transform.position;
            ImpactEffect.SpawnDefeat(defeatPosition, defeatColor);
            if (tankView != null)
                tankView.SetVisible(false);

            Died?.Invoke(this, attacker);
        }

        public bool TryShoot()
        {
            EnsureInitialized();
            if (!Alive || Ammo <= 0 || ShootCooldownRemaining > 0f || bulletPool == null)
                return false;

            var direction = VelocityForward;
            if (!TryGetSafeBulletSpawn(direction, out var spawnPosition))
                return false;

            Ammo--;
            ShootCooldownRemaining = GameConfig.ShootCooldown;
            var color = tankView != null ? tankView.BodyColor : Color.white;
            bulletPool.Spawn(this, spawnPosition, direction, color);
            AudioManager.PlayShoot();
            return true;
        }

        public MotionPrediction PredictCommand(TankInputCommand inputCommand, float dt, float clearanceProbeDistance = 0.18f)
        {
            EnsureInitialized();
            var startPosition = body != null ? body.position : (Vector2)transform.position;
            var startRotation = body != null ? body.rotation : transform.eulerAngles.z;
            return PredictCommandFrom(startPosition, startRotation, inputCommand, dt, clearanceProbeDistance);
        }

        public MotionPrediction PredictCommandFrom(Vector2 startPosition, float startRotation, TankInputCommand inputCommand, float dt, float clearanceProbeDistance = 0.18f)
        {
            EnsureInitialized();
            var predictedRotation = startRotation;
            var rotationBlocked = false;

            if (!Mathf.Approximately(inputCommand.Rotate, 0f))
            {
                var targetAngle = startRotation - Mathf.Sign(inputCommand.Rotate) * GameConfig.TankRotationSpeedDeg * dt;
                rotationBlocked = WouldOverlapWall(startPosition, targetAngle);
                if (!rotationBlocked)
                    predictedRotation = targetAngle;
            }

            var forward = (Vector2)(Quaternion.Euler(0f, 0f, predictedRotation) * Vector2.up);
            var requestedDistance = GameConfig.TankSpeed * Mathf.Abs(inputCommand.Move) * dt / CoordinateUtil.PixelsPerUnit;
            var allowedDistance = requestedDistance;
            var predictedPosition = startPosition;
            var moveBlocked = false;

            if (requestedDistance > 0f)
            {
                var direction = forward * Mathf.Sign(inputCommand.Move);
                allowedDistance = PredictAllowedMoveDistance(startPosition, predictedRotation, direction, requestedDistance, out moveBlocked);
                predictedPosition = startPosition + direction * allowedDistance;
            }

            var clearance = EstimateWallClearance(predictedPosition, predictedRotation, clearanceProbeDistance);
            return new MotionPrediction(predictedPosition, predictedRotation, forward, requestedDistance, allowedDistance, rotationBlocked, moveBlocked, clearance);
        }

        public bool IsCommandBlocked(TankInputCommand inputCommand, float dt)
        {
            var prediction = PredictCommand(inputCommand, dt);
            if (prediction.RotationBlocked || prediction.MoveBlocked)
                return true;

            if (Mathf.Abs(inputCommand.Move) > 0.01f
                && prediction.RequestedDistance > 0f
                && prediction.AllowedDistance <= prediction.RequestedDistance * 0.28f)
                return true;

            return Mathf.Abs(inputCommand.Rotate) > 0.01f
                && Mathf.Abs(Mathf.DeltaAngle(body.rotation, prediction.Rotation)) <= RotationStuckThresholdDegrees;
        }

        public Vector2 BarrelTipWorld
        {
            get
            {
                EnsureInitialized();
                var distancePixels = GameConfig.TankBodyHeight * 0.5f + GameConfig.BarrelLength;
                var position = body != null ? body.position : (Vector2)transform.position;
                return position + (Vector2)transform.up * (distancePixels / CoordinateUtil.PixelsPerUnit);
            }
        }

        private void UpdateAmmo(float dt)
        {
            if (Ammo >= GameConfig.MaxAmmo)
            {
                ammoTimer = 0f;
                return;
            }

            ammoTimer += dt;
            while (ammoTimer >= GameConfig.AmmoRegenTime && Ammo < GameConfig.MaxAmmo)
            {
                ammoTimer -= GameConfig.AmmoRegenTime;
                Ammo++;
            }
        }

        private void TryMove(float moveInput, float dt)
        {
            var direction = (Vector2)transform.up * Mathf.Sign(moveInput);
            var distance = GameConfig.TankSpeed * Mathf.Abs(moveInput) * dt / CoordinateUtil.PixelsPerUnit;
            if (distance <= 0f)
                return;

            var hitCount = bodyCollider.Cast(direction, wallFilter, castHits, distance + CollisionSkinWorld);
            var allowedDistance = distance;
            for (var i = 0; i < hitCount; i++)
            {
                var hit = castHits[i];
                if (hit.collider == null || hit.collider == bodyCollider)
                    continue;

                allowedDistance = Mathf.Min(allowedDistance, Mathf.Max(0f, hit.distance - CollisionSkinWorld));
            }

            if (allowedDistance > 0f)
                body.MovePosition(body.position + direction * allowedDistance);
        }

        private void ApplyPredictedMotion(MotionPrediction prediction)
        {
            if (Mathf.Abs(Mathf.DeltaAngle(body.rotation, prediction.Rotation)) > 0.0001f)
                body.MoveRotation(prediction.Rotation);

            if (Vector2.SqrMagnitude(body.position - prediction.Position) > 0.0000001f)
                body.MovePosition(prediction.Position);
        }

        private float PredictAllowedMoveDistance(Vector2 position, float angleDegrees, Vector2 direction, float requestedDistance, out bool blocked)
        {
            blocked = false;
            if (requestedDistance <= 0f || bodyCollider == null)
                return 0f;

            var hit = Physics2D.BoxCast(position, bodyCollider.size, angleDegrees, direction, requestedDistance + CollisionSkinWorld, wallMask);
            if (hit.collider == null || hit.collider == bodyCollider)
                return requestedDistance;

            blocked = true;
            return Mathf.Min(requestedDistance, Mathf.Max(0f, hit.distance - CollisionSkinWorld));
        }

        private void TryRotate(float rotateInput, float dt)
        {
            var targetAngle = body.rotation - Mathf.Sign(rotateInput) * GameConfig.TankRotationSpeedDeg * dt;
            if (WouldOverlapWall(body.position, targetAngle))
                return;

            body.MoveRotation(targetAngle);
        }

        private bool WouldOverlapWall(Vector2 position, float angleDegrees)
        {
            var size = GetOverlapProbeSize();

            var count = Physics2D.OverlapBox(position, size, angleDegrees, wallFilter, overlapHits);
            for (var i = 0; i < count; i++)
            {
                var hit = overlapHits[i];
                if (hit != null && hit != bodyCollider)
                    return true;
            }

            return false;
        }

        private Vector2 GetOverlapProbeSize()
        {
            var baseSize = bodyCollider != null
                ? bodyCollider.size
                : new Vector2(
                    GameConfig.TankBodyWidth / CoordinateUtil.PixelsPerUnit,
                    GameConfig.TankBodyHeight / CoordinateUtil.PixelsPerUnit);
            var size = baseSize - Vector2.one * (CollisionSkinWorld * 2f);
            size.x = Mathf.Max(size.x, CollisionSkinWorld);
            size.y = Mathf.Max(size.y, CollisionSkinWorld);
            return size;
        }

        private void ResolveWallOverlap()
        {
            if (bodyCollider == null || !WouldOverlapWall(body.position, body.rotation))
                return;

            var start = body.position;
            for (var distance = DepenetrationStepWorld; distance <= MaxDepenetrationWorld + 0.0001f; distance += DepenetrationStepWorld)
            {
                for (var i = 0; i < DepenetrationDirections.Length; i++)
                {
                    var candidate = start + DepenetrationDirections[i] * distance;
                    if (WouldOverlapWall(candidate, body.rotation))
                        continue;

                    body.position = candidate;
                    transform.position = candidate;
                    return;
                }
            }
        }

        private float EstimateWallClearance(Vector2 position, float angleDegrees, float maxDistance)
        {
            if (bodyCollider == null || maxDistance <= 0f)
                return maxDistance;

            var size = GetOverlapProbeSize();

            if (Physics2D.OverlapBox(position, size, angleDegrees, wallMask) != null)
                return 0f;

            var best = maxDistance;
            var rotation = Quaternion.Euler(0f, 0f, angleDegrees);
            best = ProbeClearance(position, size, angleDegrees, (Vector2)(rotation * Vector2.up), maxDistance, best);
            best = ProbeClearance(position, size, angleDegrees, (Vector2)(rotation * Vector2.down), maxDistance, best);
            best = ProbeClearance(position, size, angleDegrees, (Vector2)(rotation * Vector2.right), maxDistance, best);
            best = ProbeClearance(position, size, angleDegrees, (Vector2)(rotation * Vector2.left), maxDistance, best);
            return best;
        }

        private float ProbeClearance(Vector2 position, Vector2 size, float angleDegrees, Vector2 direction, float maxDistance, float currentBest)
        {
            var hit = Physics2D.BoxCast(position, size, angleDegrees, direction, maxDistance, wallMask);
            return hit.collider != null ? Mathf.Min(currentBest, hit.distance) : currentBest;
        }

        private bool TryGetSafeBulletSpawn(Vector2 direction, out Vector2 spawnPosition)
        {
            spawnPosition = BarrelTipWorld;
            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector2.up;

            direction.Normalize();
            var radius = GameConfig.BulletRadius / CoordinateUtil.PixelsPerUnit;
            var minimumDistance = (GameConfig.TankBodyHeight * 0.5f + GameConfig.BulletRadius + 2f) / CoordinateUtil.PixelsPerUnit;
            var minimumSpawn = body.position + direction * minimumDistance;

            if (Physics2D.OverlapCircle(spawnPosition, radius, wallMask) == null)
                return true;

            if (Physics2D.OverlapCircle(minimumSpawn, radius, wallMask) == null)
            {
                spawnPosition = minimumSpawn;
                return true;
            }

            return false;
        }
    }
}
