using System;
using System.Collections.Generic;
using UnityEngine;
using TankTrouble.Config;
using TankTrouble.Entities;
using TankTrouble.Map;

namespace TankTrouble.Core
{
    public sealed class RoundManager : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private MapRenderer mapRenderer;
        [SerializeField] private BulletPool bulletPool;
        [SerializeField] private TankController[] tanks;

        [Header("Map")]
        [SerializeField] private MapKind mapKind = MapKind.Random;
        [SerializeField] private bool useRandomSeed;
        [SerializeField] private int randomSeed;

        private readonly List<TankController> activeTanks = new List<TankController>(3);
        private ScoreManager scoreManager;
        private GridMap currentMap;
        private GameMode mode;
        private AiDifficulty aiDifficulty = AiDifficulty.Hard;
        private int winScore;
        private float timer;

        public event Action<int> RoundStarted;
        public event Action<TankController> RoundEnded;
        public event Action<TankController> MatchEnded;
        public event Action<TankController, TankController> TankDied;

        public RoundPhase Phase { get; private set; } = RoundPhase.Inactive;
        public int RoundNumber { get; private set; }
        public float CountdownRemaining => Phase == RoundPhase.Countdown ? timer : 0f;
        public float RoundEndRemaining => Phase == RoundPhase.RoundEnd ? timer : 0f;
        public IReadOnlyList<TankController> ActiveTanks => activeTanks;
        public GridMap CurrentMap => currentMap;
        public ScoreManager ScoreManager => scoreManager;
        public int TargetWinScore => winScore;
        public TankController LastRoundWinner { get; private set; }
        public TankController MatchWinner { get; private set; }

        private void Update()
        {
            if (Phase == RoundPhase.Countdown)
            {
                timer -= Time.deltaTime;
                if (timer <= 0f)
                    BeginPlaying();
            }
            else if (Phase == RoundPhase.RoundEnd)
            {
                timer -= Time.deltaTime;
                if (timer <= 0f)
                    StartRound();
            }
        }

        public void SetMapKind(MapKind newMapKind)
        {
            mapKind = newMapKind;
        }

        public void SetAiDifficulty(AiDifficulty newDifficulty)
        {
            aiDifficulty = newDifficulty;
        }

        public void SetRandomSeed(int seed, bool enabled = true)
        {
            randomSeed = seed;
            useRandomSeed = enabled;
        }

        public void BeginMatch(GameMode gameMode, ScoreManager scores, int targetWinScore)
        {
            mode = gameMode;
            scoreManager = scores;
            winScore = targetWinScore;
            RoundNumber = 0;
            LastRoundWinner = null;
            MatchWinner = null;

            ConfigureActiveTanks();
            scoreManager.RegisterTanks(activeTanks);
            scoreManager.ResetScores();
            BuildMap();
            StartRound();
        }

        public void StopMatch()
        {
            Phase = RoundPhase.Inactive;
            timer = 0f;
            if (bulletPool != null)
                bulletPool.ClearActive();
            SetInputEnabled(false);
            SetBrainInputEnabled(false);
            UnsubscribeTankEvents();
        }

        private void ConfigureActiveTanks()
        {
            activeTanks.Clear();
            var desiredCount = mode == GameMode.PvpAi ? 3 : 2;
            for (var i = 0; i < tanks.Length; i++)
            {
                var tank = tanks[i];
                if (tank == null)
                    continue;

                var active = activeTanks.Count < desiredCount;
                tank.gameObject.SetActive(active);
                var isHuman = active && IsHumanPlayerIndex(activeTanks.Count);
                SetInputComponentEnabled(tank, isHuman);
                SetBrainEnabled(tank, active && !isHuman);

                if (active)
                    activeTanks.Add(tank);
            }
        }

        private bool IsHumanPlayerIndex(int index)
        {
            if (mode == GameMode.PvAi)
                return index == 0;
            if (mode == GameMode.PvpAi)
                return index < 2;
            return index < 2;
        }

        private void BuildMap()
        {
            var seed = useRandomSeed ? randomSeed : (int?)null;
            currentMap = MapBuilder.Build(mapKind, seed);
            if (mapRenderer != null)
                mapRenderer.Render(currentMap);
        }

        private void StartRound()
        {
            RoundNumber++;
            LastRoundWinner = null;
            Phase = RoundPhase.Countdown;
            timer = GameConfig.RoundCountdownDuration;

            if (bulletPool != null)
                bulletPool.ClearActive();

            UnsubscribeTankEvents();
            var spawnPoints = currentMap.PickSpawnPoints(activeTanks.Count);
            for (var i = 0; i < activeTanks.Count; i++)
            {
                var tank = activeTanks[i];
                tank.ResetTank(CoordinateUtil.CellToPixel(spawnPoints[i].x, spawnPoints[i].y));
                tank.Died += HandleTankDied;
                tank.SetCommand(TankInputCommand.None);
            }

            SetInputEnabled(false);
            SetBrainInputEnabled(false);
            RoundStarted?.Invoke(RoundNumber);
        }

        private void BeginPlaying()
        {
            Phase = RoundPhase.Playing;
            timer = 0f;
            SetInputEnabled(true);
            SetBrainInputEnabled(true);
        }

        private void HandleTankDied(TankController deadTank, TankController killerTank)
        {
            if (Phase != RoundPhase.Playing)
                return;

            TankDied?.Invoke(deadTank, killerTank);
            if (!RoundRules.ShouldEndRound(CountAliveTanks()))
                return;

            ResolveRound();
        }

        private void ResolveRound()
        {
            SetInputEnabled(false);
            SetBrainInputEnabled(false);
            if (bulletPool != null)
                bulletPool.ClearActive();

            LastRoundWinner = scoreManager.ApplySurvivorScore(activeTanks);
            RoundEnded?.Invoke(LastRoundWinner);

            if (scoreManager.TryGetWinner(winScore, out var winner))
            {
                MatchWinner = winner;
                Phase = RoundPhase.MatchOver;
                MatchEnded?.Invoke(winner);
                return;
            }

            Phase = RoundPhase.RoundEnd;
            timer = GameConfig.RoundEndDuration;
        }

        private int CountAliveTanks()
        {
            var count = 0;
            for (var i = 0; i < activeTanks.Count; i++)
            {
                if (activeTanks[i] != null && activeTanks[i].Alive)
                    count++;
            }

            return count;
        }

        private void SetInputEnabled(bool enabled)
        {
            for (var i = 0; i < activeTanks.Count; i++)
                SetInputComponentEnabled(activeTanks[i], enabled && IsHumanPlayerIndex(i));
        }

        private void SetBrainInputEnabled(bool enabled)
        {
            for (var i = 0; i < activeTanks.Count; i++)
                SetBrainEnabled(activeTanks[i], enabled && !IsHumanPlayerIndex(i));
        }

        private static void SetInputComponentEnabled(TankController tank, bool enabled)
        {
            if (tank != null && tank.TryGetComponent<PlayerTankInput>(out var input))
            {
                input.enabled = enabled;
                if (!enabled)
                    tank.SetCommand(TankInputCommand.None);
            }
        }

        private void SetBrainEnabled(TankController tank, bool enabled)
        {
            if (tank != null && tank.TryGetComponent<TankTrouble.AI.AIController>(out var ai))
            {
                ai.SetDifficulty(aiDifficulty);
                ai.enabled = enabled;
                if (!enabled)
                    tank.SetCommand(TankInputCommand.None);
            }
        }

        private void UnsubscribeTankEvents()
        {
            for (var i = 0; i < activeTanks.Count; i++)
            {
                if (activeTanks[i] != null)
                    activeTanks[i].Died -= HandleTankDied;
            }
        }
    }
}
