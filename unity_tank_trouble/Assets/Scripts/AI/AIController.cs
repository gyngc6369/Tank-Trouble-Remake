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

        [SerializeField] private AiDifficulty difficulty = AiDifficulty.Hard;
        [SerializeField] private RoundManager roundManager;
        [SerializeField] private BulletPool bulletPool;
        [SerializeField] private LayerMask wallMask;
        [SerializeField] private LayerMask tankMask;

        private readonly List<TankController> enemies = new List<TankController>(3);
        private readonly DangerField dangerField = new DangerField();
        private readonly AISimpleStateMachine simpleAi = new AISimpleStateMachine();

        private TankController tank;
        private AiDifficultySettings settings;
        private DangerQuery incomingDanger;
        private BulletThreat immediateThreat;

        public AISimpleState DebugState => simpleAi.State;
        public int DebugRemainingWaypoints => simpleAi.RemainingWaypoints;
        public int DebugBlacklistedEdges => simpleAi.BlacklistedEdgeCount;

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
            incomingDanger = DangerQuery.None;
            immediateThreat = BulletThreat.None;
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
                tank.SetCommand(TankInputCommand.None);
                return;
            }

            UpdateSimpleDanger();
            var command = simpleAi.Tick(
                tank,
                roundManager.CurrentMap,
                enemies,
                ShouldInterruptForDanger(),
                Time.deltaTime);
            tank.SetCommand(command);
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
        }

        private bool ShouldInterruptForDanger()
        {
            return immediateThreat.IsThreat
                || (incomingDanger.HasRisk && incomingDanger.Risk >= DangerInterruptRisk);
        }
    }
}
