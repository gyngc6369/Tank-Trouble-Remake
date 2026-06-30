using System.Collections.Generic;
using UnityEngine;
using TankTrouble.Core;
using TankTrouble.Entities;
using TankTrouble.Map;

namespace TankTrouble.AI
{
    [RequireComponent(typeof(TankController))]
    public sealed class AIController : MonoBehaviour
    {
        private const float DangerInterruptRisk = 0.42f;
        private const float ShotBlockRisk = 0.32f;
        private const float ShotBlockRiskCloseRange = 0.65f;
        private const float MaxAimEngageAngle = 95f;
        private const float MaxAimEngageAngleCloseRange = 180f;
        private const float CloseRangeCells = 3f;
        private const float FanSpreadDegrees = 3f;
        private const int FanShotCount = 3;
        private const int AmmoFanThreshold = 5;
        private const int AmmoBurstThreshold = 3;
        private const float EvadeLargeAngleThreshold = 58f;
        private const float EvadeSmallAngleThreshold = 20f;
        private const float EvadeSlowSpeed = 0.5f;

        public enum FireMode { Single, Burst, Fan }

        [SerializeField] private AiDifficulty difficulty = AiDifficulty.Hard;
        [SerializeField] private RoundManager roundManager;
        [SerializeField] private BulletPool bulletPool;
        [SerializeField] private LayerMask wallMask;
        [SerializeField] private LayerMask tankMask;

        private readonly List<TankController> enemies = new List<TankController>(3);
        private readonly DangerField dangerField = new DangerField();
        private readonly AISimpleStateMachine simpleAi = new AISimpleStateMachine();
        private readonly AIAdvantageGoalPlanner advantagePlanner = new AIAdvantageGoalPlanner();
        private readonly HashSet<Vector2Int> dangerCells = new HashSet<Vector2Int>();
        private readonly List<Vector2> fanDirections = new List<Vector2>(5);

        private TankController tank;
        private AiDifficultySettings settings;
        private DangerQuery incomingDanger;
        private BulletThreat immediateThreat;
        private AIAdvantageGoal advantageGoal;
        private HitSolution currentShot;
        private bool hasAdvantageGoal;
        private bool hasShot;
        private float ballisticsTimer;
        private float advantageTimer;
        private float shootTimer;

        // Burst / fan fire state
        private int burstRemaining;
        private int fanIndex;
        private Vector2 burstBaseDirection;
        private FireMode activeFireMode;

        public AISimpleState DebugState => simpleAi.State;
        public int DebugRemainingWaypoints => simpleAi.RemainingWaypoints;
        public int DebugBlacklistedEdges => simpleAi.BlacklistedEdgeCount;
        public bool DebugHasAdvantageGoal => hasAdvantageGoal;
        public bool DebugHasShot => hasShot;
        public int DebugBurstRemaining => burstRemaining;
        public FireMode DebugFireMode => activeFireMode;

        private void Awake()
        {
            tank = GetComponent<TankController>();
            settings = AiDifficultySettings.FromDifficulty(difficulty);
            if (roundManager == null)
                roundManager = FindObjectOfType<RoundManager>();
            if (bulletPool == null)
                bulletPool = FindObjectOfType<BulletPool>();
            if (wallMask.value == 0)
                wallMask = LayerMask.GetMask("Wall");
            if (tankMask.value == 0)
                tankMask = LayerMask.GetMask("Tank");
        }

        private void OnEnable()
        {
            settings = AiDifficultySettings.FromDifficulty(difficulty);
            enemies.Clear();
            dangerField.Clear();
            dangerCells.Clear();
            fanDirections.Clear();
            incomingDanger = DangerQuery.None;
            immediateThreat = BulletThreat.None;
            advantageGoal = AIAdvantageGoal.None;
            currentShot = default;
            hasAdvantageGoal = false;
            hasShot = false;
            ballisticsTimer = settings.BallisticsInterval;
            advantageTimer = settings.PathfindInterval;
            shootTimer = settings.ShootCooldown;
            burstRemaining = 0;
            fanIndex = 0;
            burstBaseDirection = default;
            activeFireMode = FireMode.Single;
            simpleAi.Reset();
        }

        private void OnDisable()
        {
            if (tank != null)
                tank.SetCommand(TankInputCommand.None);
        }

        private void Update()
        {
            if (roundManager == null || tank == null || !tank.Alive || roundManager.Phase != RoundPhase.Playing)
            {
                if (tank != null)
                    tank.SetCommand(TankInputCommand.None);
                return;
            }

            BuildEnemyList();
            if (enemies.Count == 0)
            {
                simpleAi.Reset();
                hasAdvantageGoal = false;
                hasShot = false;
                burstRemaining = 0;
                tank.SetCommand(TankInputCommand.None);
                return;
            }

            var dt = Time.deltaTime;
            ballisticsTimer += dt;
            advantageTimer += dt;
            shootTimer += dt;

            UpdateSimpleDanger();
            UpdateBallistics();
            UpdateAdvantageGoal();

            // Always compute navigation (danger is handled at this level, not inside simpleAi)
            var navCommand = simpleAi.Tick(
                tank,
                roundManager.CurrentMap,
                enemies,
                false,
                hasAdvantageGoal,
                hasAdvantageGoal ? advantageGoal.Cell : default,
                dt);

            // Compute attack overlay independently (aim rotation + fire decision)
            var (aimRotate, shouldFire) = ComputeAttackOverlay();

            // Decide final movement: evade > navigate, aim rotation overrides nav rotation when shot is usable
            float finalMove;
            float finalRotate;

            if (immediateThreat.IsThreat)
            {
                // Use physics-simulated evasion via AIEvadeController
                var evadeCommand = AIEvadeController.BuildCommand(tank, dangerField, roundManager.CurrentMap, dangerCells);
                if (evadeCommand.Move != 0f || evadeCommand.Rotate != 0f)
                {
                    finalMove = evadeCommand.Move;
                    finalRotate = evadeCommand.Rotate;
                }
                else
                {
                    // Fallback: simple steering toward escape direction
                    var evadeDir = dangerField.GetEscapeDirection(tank.transform.position);
                    if (evadeDir.sqrMagnitude > 0.0001f)
                        (finalMove, finalRotate) = SteerTowardDirection(tank, evadeDir);
                    else
                        (finalMove, finalRotate) = (navCommand.Move, navCommand.Rotate);
                }
            }
            else
            {
                finalMove = navCommand.Move;
                // When we have a usable shot or are bursting, prioritize aiming over path-follow rotation
                if (HasUsableShot() || burstRemaining > 0)
                {
                    finalRotate = aimRotate;
                }
                else
                {
                    // No shot solution: face the nearest enemy instead of following path rotation blindly
                    var faceEnemyRotate = GetFaceEnemyRotate();
                    finalRotate = faceEnemyRotate != 0f ? faceEnemyRotate : navCommand.Rotate;
                }
            }

            tank.SetCommand(new TankInputCommand(finalMove, finalRotate, shouldFire));
        }

        public void SetDifficulty(AiDifficulty newDifficulty)
        {
            difficulty = newDifficulty;
            settings = AiDifficultySettings.FromDifficulty(difficulty);
        }

        private void BuildEnemyList()
        {
            enemies.Clear();
            var tanks = roundManager.ActiveTanks;
            for (var i = 0; i < tanks.Count; i++)
            {
                var other = tanks[i];
                if (other != null && other != tank && other.Alive)
                    enemies.Add(other);
            }
        }

        private void UpdateSimpleDanger()
        {
            var bullets = bulletPool != null ? bulletPool.ActiveBullets : null;
            dangerField.Clear();
            if (bullets != null)
                dangerField.Build(bullets, wallMask, settings.DangerPredictTime);

            incomingDanger = dangerField.QueryIncoming(tank.transform.position);
            immediateThreat = BulletThreatEvaluator.Evaluate(tank, bullets, wallMask);

            // Build discrete danger cells for AIEvadeController
            dangerCells.Clear();
            if (bullets != null && roundManager != null && roundManager.CurrentMap != null)
                DangerMap.CollectDangerCells(roundManager.CurrentMap, bullets, wallMask, settings.DangerPredictTime, dangerCells);
        }

        private void UpdateBallistics()
        {
            if (ballisticsTimer < settings.BallisticsInterval)
                return;

            ballisticsTimer = 0f;

            // Skip refresh only when bursting with ammo — keep the burst going
            if (burstRemaining > 0 && tank.Ammo > 0)
                return;

            // Invalidate shot if target is dead or gone
            if (hasShot && (currentShot.Target == null || !currentShot.Target.Alive))
                hasShot = false;

            // Don't search if we're out of ammo or in mortal danger
            if (tank.Ammo <= 0 || IsTooDangerousToShoot())
            {
                // Keep old shot if still valid — it's better than nothing
                if (hasShot)
                    return;
                hasShot = false;
                return;
            }

            if (AIBallistics.TryFindBestShot(tank, enemies, wallMask, tankMask, out var solution))
            {
                currentShot = solution;
                hasShot = true;
                DetermineFireMode();
            }
            // If no new solution found, keep the old hasShot/currentShot — it's still a valid aim direction
        }

        private void DetermineFireMode()
        {
            var ammo = tank.Ammo;
            if (ammo >= AmmoFanThreshold)
                activeFireMode = FireMode.Fan;
            else if (ammo >= AmmoBurstThreshold)
                activeFireMode = FireMode.Burst;
            else
                activeFireMode = FireMode.Single;
        }

        private void UpdateAdvantageGoal()
        {
            var grid = roundManager != null ? roundManager.CurrentMap : null;
            if (grid == null)
            {
                hasAdvantageGoal = false;
                advantageGoal = AIAdvantageGoal.None;
                return;
            }

            if (advantageTimer < settings.PathfindInterval && hasAdvantageGoal && IsAdvantageGoalStillSafe())
                return;

            advantageTimer = 0f;
            hasAdvantageGoal = advantagePlanner.TryFindGoal(
                tank,
                grid,
                enemies,
                dangerField,
                wallMask,
                tankMask,
                out advantageGoal);
        }

        private bool HasUsableShot()
        {
            if (!hasShot
                || currentShot.Target == null
                || !currentShot.Target.Alive
                || tank.Ammo <= 0
                || IsTooDangerousToShoot())
            {
                return false;
            }

            var angleToShot = Mathf.Abs(Vector2.SignedAngle(tank.VelocityForward, currentShot.Direction));
            var maxAngle = IsCloseRange() ? MaxAimEngageAngleCloseRange : MaxAimEngageAngle;
            return angleToShot <= maxAngle;
        }

        /// <summary>
        /// Computes a rotate input to face the nearest enemy when no shot solution is available.
        /// Returns 0 if no enemy is found or we're already facing them.
        /// </summary>
        private float GetFaceEnemyRotate()
        {
            if (enemies.Count == 0)
                return 0f;

            // Find the nearest enemy
            var myPos = (Vector2)tank.transform.position;
            var nearestDist = float.MaxValue;
            Vector2 nearestDir = default;
            for (var i = 0; i < enemies.Count; i++)
            {
                var dir = (Vector2)enemies[i].transform.position - myPos;
                var dist = dir.sqrMagnitude;
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestDir = dir;
                }
            }

            if (nearestDir.sqrMagnitude < 0.0001f)
                return 0f;

            var signedAngle = Vector2.SignedAngle(tank.VelocityForward, nearestDir.normalized);
            var absAngle = Mathf.Abs(signedAngle);

            // Only rotate if we're more than 5° off
            if (absAngle <= 5f)
                return 0f;

            return AIGeometryUtils.RotateInputForAngle(signedAngle);
        }

        /// <summary>
        /// Returns true if the nearest enemy is within CloseRangeCells (Manhattan distance).
        /// </summary>
        private bool IsCloseRange()
        {
            if (enemies.Count == 0)
                return false;
            var myCell = CoordinateUtil.PixelToCell(CoordinateUtil.WorldToPixel(tank.transform.position));
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemyCell = CoordinateUtil.PixelToCell(CoordinateUtil.WorldToPixel(enemies[i].transform.position));
                if (AIGeometryUtils.Manhattan(myCell, enemyCell) <= CloseRangeCells)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Computes the aiming rotation and fire decision independently of movement.
        /// Supports burst fire (rapid shots at same aim) and fan fire (spread shots across ±FanSpreadDegrees).
        /// </summary>
        private (float rotate, bool fire) ComputeAttackOverlay()
        {
            // ---- Burst / fan continuation ----
            if (burstRemaining > 0)
            {
                var burstDesired = GetBurstDirection();
                var burstSignedAngle = Vector2.SignedAngle(tank.VelocityForward, burstDesired);
                var burstAngleToShot = Mathf.Abs(burstSignedAngle);

                // During burst, use tank-level cooldown (0.15s) for rapid fire, not AI cooldown
                var burstReady = burstAngleToShot <= settings.AimToleranceDegrees
                    && tank.Ammo > 0
                    && tank.ShootCooldownRemaining <= 0f;

                var burstCanShoot = burstReady
                    && AIBallistics.TryValidateShot(tank, enemies, burstDesired, wallMask, tankMask, out _);

                if (burstCanShoot)
                {
                    burstRemaining--;
                    fanIndex++;
                    if (burstRemaining == 0)
                    {
                        hasShot = false;
                        activeFireMode = FireMode.Single;
                        shootTimer = 0f;
                    }
                }

                var burstRotate = burstAngleToShot > 2f
                    ? AIGeometryUtils.RotateInputForAngle(burstSignedAngle)
                    : 0f;
                return (burstRotate, burstCanShoot);
            }

            // ---- New shot acquisition ----
            if (!hasShot)
                return (0f, false);

            var desired = currentShot.Direction.sqrMagnitude > 0.0001f
                ? currentShot.Direction.normalized
                : tank.VelocityForward;

            var signedAngle = Vector2.SignedAngle(tank.VelocityForward, desired);
            var angleToShot = Mathf.Abs(signedAngle);

            var readyToValidate = angleToShot <= settings.AimToleranceDegrees
                && shootTimer >= settings.ShootCooldown
                && tank.Ammo > 0
                && tank.ShootCooldownRemaining <= 0f;

            var canShoot = readyToValidate
                && AIBallistics.TryValidateShot(tank, enemies, tank.VelocityForward, wallMask, tankMask, out _);

            if (canShoot)
            {
                shootTimer = 0f;
                StartBurst();
            }
            else if (readyToValidate)
            {
                hasShot = false;
            }

            var rotate = angleToShot > 2f ? AIGeometryUtils.RotateInputForAngle(signedAngle) : 0f;
            return (rotate, canShoot);
        }

        /// <summary>
        /// Begins a burst-fire sequence based on the current fire mode.
        /// Fan mode: spread shots across ±FanSpreadDegrees.
        /// Burst mode: rapid shots at the best aim direction.
        /// Single mode: one shot only.
        /// </summary>
        private void StartBurst()
        {
            var totalShots = activeFireMode == FireMode.Fan ? FanShotCount
                : activeFireMode == FireMode.Burst ? settings.BurstCount
                : 1;

            burstRemaining = totalShots - 1; // first shot already fired
            fanIndex = 0;
            burstBaseDirection = currentShot.Direction;

            if (activeFireMode == FireMode.Fan)
            {
                BuildFanDirections();
            }

            if (burstRemaining == 0)
            {
                hasShot = false;
                activeFireMode = FireMode.Single;
            }
        }

        /// <summary>
        /// Pre-computes fan fire directions around the best shot direction.
        /// </summary>
        private void BuildFanDirections()
        {
            fanDirections.Clear();
            fanDirections.Add(burstBaseDirection); // center

            // Add ±FanSpreadDegrees offsets
            var halfAngleRad = FanSpreadDegrees * Mathf.Deg2Rad;
            var baseAngle = Mathf.Atan2(burstBaseDirection.y, burstBaseDirection.x);

            for (var i = 1; i <= (FanShotCount - 1) / 2; i++)
            {
                var offsetAngle = i * halfAngleRad;
                var dir1 = new Vector2(Mathf.Cos(baseAngle + offsetAngle), Mathf.Sin(baseAngle + offsetAngle));
                var dir2 = new Vector2(Mathf.Cos(baseAngle - offsetAngle), Mathf.Sin(baseAngle - offsetAngle));
                fanDirections.Add(dir1);
                fanDirections.Add(dir2);
            }
        }

        /// <summary>
        /// Returns the current burst/fan shot direction, cycling through fan directions if applicable.
        /// </summary>
        private Vector2 GetBurstDirection()
        {
            if (activeFireMode == FireMode.Fan && fanDirections.Count > 0)
                return fanDirections[fanIndex % fanDirections.Count];

            return burstBaseDirection;
        }

        /// <summary>
        /// Converts a desired world-space direction into tank move/rotate commands.
        /// Large angles → rotate in place. Medium angles → slow forward + turn. Small angles → full speed.
        /// </summary>
        private static (float move, float rotate) SteerTowardDirection(TankController tk, Vector2 desiredDirection)
        {
            var signedAngle = Vector2.SignedAngle(tk.VelocityForward, desiredDirection);
            var absAngle = Mathf.Abs(signedAngle);

            if (absAngle > EvadeLargeAngleThreshold)
                return (0f, signedAngle > 0f ? -1f : 1f);

            if (absAngle > EvadeSmallAngleThreshold)
                return (EvadeSlowSpeed, signedAngle > 0f ? -1f : 1f);

            return (1f, 0f);
        }

        private bool IsTooDangerousToShoot()
        {
            // At close range, be more aggressive — only block shooting for truly imminent threats
            if (IsCloseRange())
            {
                // Only block if an immediate bullet threat exists, ignore moderate danger
                if (immediateThreat.IsThreat)
                    return true;
                return incomingDanger.HasRisk && incomingDanger.Risk >= ShotBlockRiskCloseRange;
            }

            return immediateThreat.IsThreat || (incomingDanger.HasRisk && incomingDanger.Risk >= ShotBlockRisk);
        }

        private bool IsAdvantageGoalStillSafe()
        {
            if (!hasAdvantageGoal || !advantageGoal.IsValid || advantageGoal.Target == null || !advantageGoal.Target.Alive)
                return false;

            var center = CoordinateUtil.CellToWorld(advantageGoal.Cell.x, advantageGoal.Cell.y);
            if (dangerField == null || !dangerField.HasDanger)
                return true;

            // Estimate arrival time based on path distance
            var currentCell = CoordinateUtil.PixelToCell(CoordinateUtil.WorldToPixel(tank.transform.position));
            var pathDistance = Mathf.Abs(advantageGoal.Cell.x - currentCell.x) + Mathf.Abs(advantageGoal.Cell.y - currentCell.y);
            var arrivalTime = Mathf.Clamp(pathDistance * 0.34f + 0.2f, 0f, 2.2f);

            return dangerField.GetRisk(center, arrivalTime) < ShotBlockRisk;
        }

        private bool ShouldInterruptForDanger()
        {
            return immediateThreat.IsThreat
                || (incomingDanger.HasRisk && incomingDanger.Risk >= DangerInterruptRisk);
        }
    }
}
