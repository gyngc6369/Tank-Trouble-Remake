using System.Collections.Generic;
using UnityEngine;
using TankTrouble.Config;
using TankTrouble.Core;
using TankTrouble.Entities;
using TankTrouble.Map;

namespace TankTrouble.AI
{
    [RequireComponent(typeof(TankController))]
    public sealed class AIController : MonoBehaviour
    {
        private const float StuckMovementThreshold = 0.002f;
        private const float StuckDuration = 1.05f;
        private const float RepositionDuration = 0.85f;
        private const float IncomingDangerInterruptRisk = 0.34f;
        private const float PlanDangerInterruptRisk = 0.24f;
        private const float PlanDangerRebuildRisk = 0.22f;
        private const float EvadeHoldDuration = 0.24f;
        private const float RecoverDuration = 0.22f;
        private const float RecoverProbeTime = 0.2f;
        private const float RecenterTolerance = 0.06f;
        private const float StuckRotationThresholdDegrees = 0.35f;

        private enum AIState
        {
            Cruise,
            Aim,
            Shoot,
            Evade,
            Recenter,
            Recover
        }

        [SerializeField] private AiDifficulty difficulty = AiDifficulty.Hard;
        [SerializeField] private RoundManager roundManager;
        [SerializeField] private BulletPool bulletPool;
        [SerializeField] private LayerMask wallMask;
        [SerializeField] private LayerMask tankMask;

        private readonly List<Vector2Int> currentPath = new List<Vector2Int>(32);
        private readonly HashSet<Vector2Int> dangerCells = new HashSet<Vector2Int>();
        private readonly List<TankController> enemies = new List<TankController>(3);
        private readonly DangerField dangerField = new DangerField();
        private readonly AICenterTaskFollower centerTaskFollower = new AICenterTaskFollower();

        private TankController tank;
        private AiDifficultySettings settings;
        private HitSolution currentShot;
        private bool hasShot;
        private bool currentCellDangerous;
        private BulletThreat immediateThreat;
        private DangerQuery incomingDanger;
        private float ballisticsTimer;
        private float pathfindTimer;
        private float shootTimer;
        private float stuckTimer;
        private float repositionTimer;
        private float evadeHoldTimer;
        private float recoverTimer;
        private Vector2Int pursueGoal;
        private Vector2Int committedEnemyCell;
        private bool hasPursueGoal;
        private bool hasCommittedEnemyCell;
        private AITankDriver.Memory driverMemory;
        private Vector2 lastPosition;
        private float lastRotation;
        private Vector2 predictedShotOrigin;
        private Vector2 predictedShotDirection;
        private bool hasPredictedShot;
        private bool needsRecenter;
        private AIState activeState;
        private TankInputCommand previousCommand;
        private TankInputCommand recoverCommand;
        private AIMotionPlan motionPlan;

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
            currentPath.Clear();
            centerTaskFollower.Clear();
            dangerCells.Clear();
            dangerField.Clear();
            enemies.Clear();
            hasShot = false;
            incomingDanger = DangerQuery.None;
            ballisticsTimer = settings.BallisticsInterval;
            pathfindTimer = settings.PathfindInterval;
            shootTimer = settings.ShootCooldown;
            stuckTimer = 0f;
            repositionTimer = 0f;
            evadeHoldTimer = 0f;
            recoverTimer = 0f;
            hasPursueGoal = false;
            hasCommittedEnemyCell = false;
            driverMemory.Reset();
            hasPredictedShot = false;
            needsRecenter = false;
            activeState = AIState.Cruise;
            previousCommand = TankInputCommand.None;
            recoverCommand = TankInputCommand.None;
            motionPlan = AIMotionPlan.Invalid;
            lastPosition = transform.position;
            lastRotation = transform.eulerAngles.z;
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

            var dt = Time.deltaTime;
            ballisticsTimer += dt;
            pathfindTimer += dt;
            shootTimer += dt;
            if (evadeHoldTimer > 0f)
                evadeHoldTimer = Mathf.Max(0f, evadeHoldTimer - dt);
            if (recoverTimer > 0f)
                recoverTimer = Mathf.Max(0f, recoverTimer - dt);
            if (repositionTimer > 0f)
            {
                repositionTimer = Mathf.Max(0f, repositionTimer - dt);
                if (repositionTimer <= 0f)
                    hasPredictedShot = false;
            }

            UpdateStuckDetection(dt);

            BuildEnemyList();
            if (enemies.Count == 0)
            {
                ApplyCommand(TankInputCommand.None);
                return;
            }

            UpdateImmediateThreat();
            UpdateBallistics();
            UpdatePathfinding(ShouldRefreshTacticalMap());
            ApplyCommand(BuildCommand());
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

        private void UpdateBallistics()
        {
            if (ballisticsTimer < settings.BallisticsInterval)
                return;

            ballisticsTimer = 0f;
            hasShot = false;
            if (tank.Ammo <= 0)
                return;

            if (AIBallistics.TryFindBestShot(tank, enemies, wallMask, tankMask, out var solution))
            {
                currentShot = solution;
                hasShot = true;
            }
        }

        private void UpdateImmediateThreat()
        {
            var bullets = bulletPool != null ? bulletPool.ActiveBullets : null;
            dangerField.Clear();
            if (bullets != null)
                dangerField.Build(bullets, wallMask, settings.DangerPredictTime);
            if (hasPredictedShot)
            {
                var ignoreDistance = (GameConfig.TankBodyHeight + GameConfig.BarrelLength) / CoordinateUtil.PixelsPerUnit;
                dangerField.AddPredictedShot(predictedShotOrigin, predictedShotDirection, wallMask, settings.DangerPredictTime, ignoreDistance);
            }

            incomingDanger = dangerField.QueryIncoming(tank.transform.position);
            immediateThreat = BulletThreatEvaluator.Evaluate(tank, bullets, wallMask);
            if (!immediateThreat.IsThreat && incomingDanger.HasRisk && incomingDanger.Risk >= IncomingDangerInterruptRisk)
            {
                immediateThreat = new BulletThreat(true, incomingDanger.EscapeDirection, incomingDanger.TimeToImpact, incomingDanger.Distance);
            }

            if ((bullets == null || bullets.Count == 0) && !hasPredictedShot)
            {
                dangerCells.Clear();
                currentCellDangerous = false;
            }
        }

        private void UpdatePathfinding(bool force)
        {
            if (!force && pathfindTimer < settings.PathfindInterval)
                return;

            pathfindTimer = 0f;
            var grid = roundManager.CurrentMap;
            if (grid == null)
                return;

            var bullets = bulletPool != null ? bulletPool.ActiveBullets : null;
            DangerMap.CollectDangerCells(grid, bullets, wallMask, settings.DangerPredictTime, dangerCells);
            if (hasPredictedShot)
            {
                var ignoreDistance = (GameConfig.TankBodyHeight + GameConfig.BarrelLength) / CoordinateUtil.PixelsPerUnit;
                DangerMap.AddPredictedShot(grid, predictedShotOrigin, predictedShotDirection, wallMask, settings.DangerPredictTime, dangerCells, ignoreDistance);
            }

            var currentCell = CoordinateUtil.PixelToCell(CoordinateUtil.WorldToPixel(tank.transform.position));
            currentCellDangerous = dangerCells.Contains(currentCell) || immediateThreat.IsThreat || IsIncomingDangerHigh();
            if (currentCellDangerous)
            {
                hasPursueGoal = false;
                hasCommittedEnemyCell = false;
                if (AIGoalPlanner.TryFindEvadeGoal(grid, currentCell, dangerField, dangerCells, out var evadeGoal))
                    TryFindPath(grid, currentCell, evadeGoal, avoidDanger: true, preferComfort: false);
                else if (AIPathfinding.TryFindNearestSafeCell(grid, currentCell, dangerCells, out var safeCell))
                    TryFindPath(grid, currentCell, safeCell, avoidDanger: false, preferComfort: false);
                else
                {
                    currentPath.Clear();
                    centerTaskFollower.Clear();
                }
                return;
            }

            if (repositionTimer > 0f)
            {
                hasPursueGoal = false;
                hasCommittedEnemyCell = false;
                if (AIPathfinding.TryFindBestRepositionCell(grid, currentCell, enemies, dangerCells, out var repositionCell))
                {
                    TryFindPath(grid, currentCell, repositionCell, avoidDanger: true);
                }
                else
                {
                    currentPath.Clear();
                    centerTaskFollower.Clear();
                }
                return;
            }


            if (HasUsableShot())
            {
                currentPath.Clear();
                centerTaskFollower.Clear();
                hasPursueGoal = false;
                hasCommittedEnemyCell = false;
                return;
            }

            if (ShouldKeepCommittedPursuePath(grid, currentCell))
                return;

            UpdatePursuePath(grid, currentCell);
        }

        private bool TryFindPath(GridMap grid, Vector2Int currentCell, Vector2Int goalCell, bool avoidDanger, bool preferComfort = true)
        {
            var blockers = avoidDanger ? dangerCells : null;
            if (preferComfort && AIPathfinding.TryFindComfortPath(grid, currentCell, goalCell, blockers, currentPath))
            {
                centerTaskFollower.SetPath(currentPath, currentCell, goalCell);
                return true;
            }
            if (AIPathfinding.TryFindPath(grid, currentCell, goalCell, blockers, currentPath))
            {
                centerTaskFollower.SetPath(currentPath, currentCell, goalCell);
                return true;
            }

            if (avoidDanger)
            {
                if (preferComfort && AIPathfinding.TryFindComfortPath(grid, currentCell, goalCell, null, currentPath))
                {
                    centerTaskFollower.SetPath(currentPath, currentCell, goalCell);
                    return true;
                }
                if (AIPathfinding.TryFindPath(grid, currentCell, goalCell, null, currentPath))
                {
                    centerTaskFollower.SetPath(currentPath, currentCell, goalCell);
                    return true;
                }
            }

            currentPath.Clear();
            centerTaskFollower.Clear();
            return false;
        }


        private bool ShouldKeepCommittedPursuePath(GridMap grid, Vector2Int currentCell)
        {
            if (!centerTaskFollower.HasPath
                || centerTaskFollower.IsBlocked
                || !hasPursueGoal
                || centerTaskFollower.GoalCell != pursueGoal)
            {
                return false;
            }

            if (!hasCommittedEnemyCell)
                return true;

            if (!centerTaskFollower.CanReplan)
                return true;

            var enemyCell = AIPathfinding.FindNearestEnemyCell(grid, currentCell, enemies);
            return enemyCell == committedEnemyCell;
        }

        private void UpdatePursuePath(GridMap grid, Vector2Int currentCell)
        {
            var enemyCell = AIPathfinding.FindNearestEnemyCell(grid, currentCell, enemies);

            if (AIGoalPlanner.TryFindPursueGoal(grid, currentCell, enemies, dangerCells, out var goal))
            {
                pursueGoal = goal;
                hasPursueGoal = true;
                committedEnemyCell = enemyCell;
                hasCommittedEnemyCell = true;
                if (TryFindPath(grid, currentCell, pursueGoal, avoidDanger: dangerCells.Count > 0))
                    return;
            }

            pursueGoal = enemyCell;
            committedEnemyCell = enemyCell;
            hasPursueGoal = true;
            hasCommittedEnemyCell = true;
            TryFindPath(grid, currentCell, pursueGoal, avoidDanger: false);
        }

        private TankInputCommand BuildCommand()
        {
            if (ShouldUseEvadeController())
            {
                activeState = AIState.Evade;
                needsRecenter = true;
                recoverTimer = 0f;
                motionPlan = AIMotionPlan.Invalid;
                return AIEvadeController.BuildCommand(
                    tank,
                    dangerField,
                    roundManager != null ? roundManager.CurrentMap : null,
                    dangerCells);
            }

            if (recoverTimer > 0f)
            {
                activeState = AIState.Recover;
                return recoverCommand;
            }

            if (repositionTimer > 0f && !ShouldBreakRepositionForCounterShot())
            {
                activeState = AIState.Recenter;
                var repositionDirection = GetPathDirection();
                return BuildDrivenCommand(repositionDirection, allowReverse: true);
            }

            if (HasUsableShot())
            {
                var desiredDirection = currentShot.Direction;
                var angleToShot = Mathf.Abs(Vector2.SignedAngle(tank.VelocityForward, desiredDirection));
                var canShoot = angleToShot <= settings.AimToleranceDegrees
                    && shootTimer >= settings.ShootCooldown
                    && tank.Ammo > 0
                    && tank.ShootCooldownRemaining <= 0f
                    && AIBallistics.TryValidateShot(tank, enemies, tank.VelocityForward, wallMask, tankMask, out _);

                activeState = canShoot ? AIState.Shoot : AIState.Aim;
                needsRecenter = true;
                if (canShoot)
                {
                    shootTimer = 0f;
                    BeginReposition(desiredDirection);
                }

                return BuildAttackCommand(desiredDirection, angleToShot, canShoot);
            }

            if (needsRecenter)
            {
                var recenterDirection = GetRecenterDirection();
                if (recenterDirection != Vector2.zero)
                {
                    activeState = AIState.Recenter;
                    return BuildDrivenCommand(recenterDirection, allowReverse: false);
                }

                needsRecenter = false;
            }

            activeState = AIState.Cruise;
            var pathCommand = BuildPathFollowerCommand();
            if (centerTaskFollower.IsBlocked)
            {
                var recoverDirection = centerTaskFollower.RecoverDirection;
                if (recoverDirection == Vector2.zero)
                    recoverDirection = PeekPathDirection();
                StartRecover(recoverDirection, ShouldPreferReverse(), invalidatePath: true);
                return recoverCommand;
            }

            if (HasCommandInput(pathCommand))
                return pathCommand;

            if (!centerTaskFollower.HasPath)
            {
                EnsurePressurePath();
                pathCommand = BuildPathFollowerCommand();
            }

            if (HasCommandInput(pathCommand))
                return pathCommand;

            var pathDirection = AIPathFollower.GetBestOpenNeighborDirection(
                roundManager != null ? roundManager.CurrentMap : null,
                tank,
                enemies,
                dangerCells);
            if (pathDirection == Vector2.zero)
            {
                var directDirection = GetNearestEnemyDirection();
                if (CanDriveToward(directDirection, allowReverse: false))
                    pathDirection = directDirection;
            }

            return BuildDrivenCommand(pathDirection, allowReverse: false);
        }


        private TankInputCommand BuildDrivenCommand(Vector2 desiredDirection, bool allowReverse, bool urgentEvade = false)
        {
            if (desiredDirection.sqrMagnitude < 0.0001f)
                return TankInputCommand.None;

            var command = AITankDriver.DriveToDirection(tank, desiredDirection, allowReverse, ref driverMemory, Time.deltaTime);
            if (!driverMemory.NeedsRepath && !ShouldRecoverFromBlockedCommand(command))
                return command;

            if (urgentEvade)
            {
                var evade = AIEvadeController.BuildCommand(
                    tank,
                    dangerField,
                    roundManager != null ? roundManager.CurrentMap : null,
                    dangerCells);
                return HasCommandInput(evade) ? evade : command;
            }

            if (TryBuildPathCorrectionCommand(out var correctionCommand))
                return correctionCommand;

            var preserveCommittedPath = centerTaskFollower.HasPath && !centerTaskFollower.IsBlocked;
            StartRecover(
                desiredDirection,
                !preserveCommittedPath && (allowReverse || ShouldPreferReverse()),
                invalidatePath: !preserveCommittedPath);
            return recoverCommand;
        }

        private bool TryBuildPathCorrectionCommand(out TankInputCommand correctionCommand)
        {
            correctionCommand = TankInputCommand.None;
            var grid = roundManager != null ? roundManager.CurrentMap : null;
            if (!centerTaskFollower.HasPath || grid == null)
                return false;

            if (!centerTaskFollower.TryGetLocalCorrectionDirection(tank, grid, out var correctionDirection))
                return false;

            var memory = new AITankDriver.Memory();
            correctionCommand = AITankDriver.DriveToDirection(
                tank,
                correctionDirection,
                allowReverse: false,
                ref memory,
                Time.deltaTime);

            if (!HasCommandInput(correctionCommand) || tank.IsCommandBlocked(correctionCommand, RecoverProbeTime))
                return false;

            driverMemory = memory;
            return true;
        }

        private void StartRecover(Vector2 desiredDirection, bool preferReverse, bool invalidatePath)
        {
            if (desiredDirection.sqrMagnitude < 0.0001f)
                desiredDirection = GetNearestEnemyDirection();

            recoverCommand = AIMovementEvaluator.ChooseMovement(
                tank,
                desiredDirection,
                roundManager != null ? roundManager.CurrentMap : null,
                dangerCells,
                enemies,
                wallMask,
                urgentEvade: false,
                preferReverse: preferReverse,
                stuckAssist: true,
                previousCommand: previousCommand);
            recoverTimer = RecoverDuration;
            activeState = AIState.Recover;
            needsRecenter = true;
            driverMemory.Reset();
            motionPlan = AIMotionPlan.Invalid;

            if (!invalidatePath)
                return;

            currentPath.Clear();
            centerTaskFollower.Clear();
            hasPursueGoal = false;
            hasCommittedEnemyCell = false;
            pathfindTimer = settings.PathfindInterval;
        }

        private static bool HasCommandInput(TankInputCommand command)
        {
            return Mathf.Abs(command.Move) > 0.01f || Mathf.Abs(command.Rotate) > 0.01f || command.FireHeld;
        }

        private bool ShouldRecoverFromBlockedCommand(TankInputCommand command)
        {
            if (tank == null || (Mathf.Abs(command.Move) <= 0.01f && Mathf.Abs(command.Rotate) <= 0.01f))
                return false;

            return tank.IsCommandBlocked(command, RecoverProbeTime);
        }

        private TankInputCommand BuildMotionPlanCommand(Vector2 desiredDirection, AIMotionIntent intent)
        {
            if (desiredDirection.sqrMagnitude < 0.0001f)
                desiredDirection = GetNearestEnemyDirection();
            if (desiredDirection.sqrMagnitude < 0.0001f)
                desiredDirection = tank.VelocityForward;
            desiredDirection.Normalize();

            var shouldRebuild = !motionPlan.Valid || motionPlan.Expired || motionPlan.Intent != intent;

            if (!shouldRebuild && dangerField.HasDanger)
            {
                var currentPlanRisk = AIMotionPlanner.EstimatePlanRisk(tank, motionPlan, dangerField);
                if (currentPlanRisk >= PlanDangerRebuildRisk)
                    shouldRebuild = true;
            }

            if (shouldRebuild)
            {
                motionPlan = AIMotionPlanner.BuildPlan(
                    tank,
                    intent,
                    desiredDirection,
                    roundManager != null ? roundManager.CurrentMap : null,
                    dangerCells,
                    dangerField,
                    enemies);
            }

            var command = motionPlan.Valid ? motionPlan.CurrentCommand : TankInputCommand.None;
            motionPlan.Advance(Time.deltaTime);
            return command;
        }

        private TankInputCommand BuildAttackCommand(Vector2 desiredDirection, float angleToShot, bool fire)
        {
            if (fire || angleToShot <= settings.AimToleranceDegrees * 2.4f)
            {
                motionPlan = AIMotionPlan.Invalid;
                return BuildSteeringCommand(desiredDirection, aiming: true, fire: fire);
            }

            var moveDirection = PeekPathDirection();
            if (moveDirection == Vector2.zero)
                moveDirection = GetNearestEnemyDirection();

            var movement = BuildDrivenCommand(moveDirection, allowReverse: false);
            var signedAngle = Vector2.SignedAngle(tank.VelocityForward, desiredDirection.normalized);
            var rotate = Mathf.Abs(signedAngle) > 3f ? RotateInputForAngle(signedAngle) : 0f;
            return new TankInputCommand(movement.Move, rotate, false);
        }

        private TankInputCommand BuildSteeringCommand(Vector2 desiredDirection, bool aiming, bool fire)
        {
            if (desiredDirection.sqrMagnitude < 0.0001f)
                return new TankInputCommand(0f, 0f, fire);

            var desired = desiredDirection.normalized;
            var forwardAngle = Vector2.SignedAngle(tank.VelocityForward, desired);
            var rotate = Mathf.Abs(forwardAngle) > (aiming ? 2f : 5f) ? RotateInputForAngle(forwardAngle) : 0f;

            if (aiming)
                return new TankInputCommand(0f, rotate, fire);

            var reverseAngle = Vector2.SignedAngle(-tank.VelocityForward, desired);
            var useReverse = Mathf.Abs(reverseAngle) + 18f < Mathf.Abs(forwardAngle);
            var steeringAngle = useReverse ? reverseAngle : forwardAngle;
            rotate = Mathf.Abs(steeringAngle) > 5f ? RotateInputForAngle(steeringAngle) : 0f;

            var move = useReverse ? -0.75f : 1f;
            if (Mathf.Abs(steeringAngle) > 80f)
                move = useReverse ? -0.35f : 0.35f;

            return new TankInputCommand(move, rotate, false);
        }

        private Vector2 GetPathDirection()
        {
            return PeekPathDirection();
        }

        private TankInputCommand BuildPathFollowerCommand()
        {
            return centerTaskFollower.BuildCommand(
                tank,
                roundManager != null ? roundManager.CurrentMap : null,
                Time.deltaTime);
        }

        private Vector2 GetRecenterDirection()
        {
            return AICenterlineFollower.GetRecenterDirection(
                tank,
                roundManager != null ? roundManager.CurrentMap : null,
                RecenterTolerance);
        }

        private Vector2 GetNearestEnemyDirection()
        {
            var bestDistance = float.MaxValue;
            TankController best = null;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.Alive)
                    continue;

                var distance = Vector2.SqrMagnitude(enemy.transform.position - transform.position);
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                best = enemy;
            }

            return best != null ? ((Vector2)best.transform.position - (Vector2)transform.position).normalized : Vector2.zero;
        }

        private void EnsurePressurePath()
        {
            var grid = roundManager != null ? roundManager.CurrentMap : null;
            if (grid == null || enemies.Count == 0)
                return;

            var currentCell = CoordinateUtil.PixelToCell(CoordinateUtil.WorldToPixel(tank.transform.position));
            var approachCell = AIPathfinding.FindBestApproachCell(grid, currentCell, enemies, dangerCells);
            if (TryFindPath(grid, currentCell, approachCell, avoidDanger: dangerCells.Count > 0))
                return;

            var enemyCell = AIPathfinding.FindNearestEnemyCell(grid, currentCell, enemies);
            TryFindPath(grid, currentCell, enemyCell, avoidDanger: false);
        }

        private void UpdateStuckDetection(float dt)
        {
            var position = (Vector2)transform.position;
            var moved = Vector2.Distance(position, lastPosition);
            lastPosition = position;
            var rotation = transform.eulerAngles.z;
            var rotated = Mathf.Abs(Mathf.DeltaAngle(lastRotation, rotation));
            lastRotation = rotation;

            var movementStuck = Mathf.Abs(previousCommand.Move) > 0.1f && moved < StuckMovementThreshold;
            var rotationStuck = Mathf.Abs(previousCommand.Rotate) > 0.1f && rotated < StuckRotationThresholdDegrees;
            if ((movementStuck || rotationStuck) && tank.IsCommandBlocked(previousCommand, RecoverProbeTime))
                stuckTimer += dt;
            else
                stuckTimer = 0f;

            if (stuckTimer < StuckDuration)
                return;

            stuckTimer = 0f;
            var desiredDirection = PeekPathDirection();
            if (desiredDirection == Vector2.zero)
                desiredDirection = GetNearestEnemyDirection();
            StartRecover(desiredDirection, ShouldPreferReverse(), invalidatePath: true);
        }

        private bool HasUsableShot()
        {
            return hasShot
                && currentShot.Target != null
                && currentShot.Target.Alive
                && !currentCellDangerous
                && !immediateThreat.IsThreat
                && !IsIncomingDangerHigh()
                && evadeHoldTimer <= 0f;
        }

        private void BeginReposition(Vector2 shotDirection)
        {
            predictedShotOrigin = tank.BarrelTipWorld;
            predictedShotDirection = shotDirection.normalized;
            hasPredictedShot = true;
            needsRecenter = true;
            repositionTimer = RepositionDuration;
            pathfindTimer = settings.PathfindInterval;
            motionPlan = AIMotionPlan.Invalid;
            hasPursueGoal = false;
            hasCommittedEnemyCell = false;
            driverMemory.Reset();
            recoverTimer = 0f;
            recoverCommand = TankInputCommand.None;
            currentPath.Clear();
            centerTaskFollower.Clear();
        }

        private bool ShouldRefreshTacticalMap()
        {
            if (immediateThreat.IsThreat || IsIncomingDangerHigh() || hasPredictedShot || repositionTimer > 0f)
                return true;

            return bulletPool != null && bulletPool.ActiveBullets != null && bulletPool.ActiveBullets.Count > 0;
        }

        private bool ShouldUseEvadeController()
        {
            var directDanger = immediateThreat.IsThreat || IsIncomingDangerHigh() || currentCellDangerous;
            if (!directDanger && dangerField.HasDanger && motionPlan.Valid)
            {
                var planRisk = AIMotionPlanner.EstimatePlanRisk(tank, motionPlan, dangerField);
                directDanger = planRisk >= PlanDangerInterruptRisk;
            }

            if (directDanger)
                evadeHoldTimer = EvadeHoldDuration;

            return evadeHoldTimer > 0f && dangerField.HasDanger;
        }

        private bool IsIncomingDangerHigh()
        {
            return incomingDanger.HasRisk && incomingDanger.Risk >= IncomingDangerInterruptRisk;
        }

        private bool ShouldBreakRepositionForCounterShot()
        {
            if (!HasUsableShot() || currentShot.SelfRisk > 0.55f)
                return false;

            if (currentShot.Target == null)
                return false;

            var distance = Vector2.Distance(transform.position, currentShot.Target.transform.position);
            var angleToShot = Mathf.Abs(Vector2.SignedAngle(tank.VelocityForward, currentShot.Direction));
            return distance <= 2.4f || angleToShot <= settings.AimToleranceDegrees * 1.5f;
        }

        private bool ShouldPreferReverse()
        {
            var nearestDirection = GetNearestEnemyDirection();
            if (nearestDirection != Vector2.zero)
            {
                var distance = GetNearestEnemyDistance();
                if (distance <= 1.8f && Mathf.Abs(Vector2.SignedAngle(tank.VelocityForward, nearestDirection)) <= 80f)
                    return true;
            }

            var pathDirection = PeekPathDirection();
            if (pathDirection == Vector2.zero)
                return false;

            var reverseAngle = Mathf.Abs(Vector2.SignedAngle(-tank.VelocityForward, pathDirection));
            var forwardAngle = Mathf.Abs(Vector2.SignedAngle(tank.VelocityForward, pathDirection));
            return reverseAngle + 18f < forwardAngle;
        }

        private Vector2 PeekPathDirection()
        {
            var executorDirection = centerTaskFollower.PeekDirection(
                tank,
                roundManager != null ? roundManager.CurrentMap : null);
            if (executorDirection != Vector2.zero)
                return executorDirection;

            if (currentPath.Count == 0)
                return Vector2.zero;

            var targetWorld = CoordinateUtil.CellToWorld(currentPath[0].x, currentPath[0].y);
            return ((Vector2)targetWorld - (Vector2)tank.transform.position).normalized;
        }

        private float GetNearestEnemyDistance()
        {
            var bestDistance = float.MaxValue;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.Alive)
                    continue;

                bestDistance = Mathf.Min(bestDistance, Vector2.Distance(transform.position, enemy.transform.position));
            }

            return bestDistance;
        }

        private bool CanDriveToward(Vector2 direction, bool allowReverse)
        {
            if (direction.sqrMagnitude < 0.0001f)
                return false;

            var memory = new AITankDriver.Memory();
            var command = AITankDriver.DriveToDirection(tank, direction, allowReverse, ref memory, Time.deltaTime);
            return HasCommandInput(command) && !tank.IsCommandBlocked(command, RecoverProbeTime);
        }

        private void ApplyCommand(TankInputCommand command)
        {
            previousCommand = command;
            tank.SetCommand(command);
        }

        private static float RotateInputForAngle(float signedAngle)
        {
            return signedAngle > 0f ? -1f : 1f;
        }
    }
}
