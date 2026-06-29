using System;
using System.Collections.Generic;
using UnityEngine;
using TankTrouble.Entities;

namespace TankTrouble.Core
{
    public sealed class ScoreManager : MonoBehaviour
    {
        private readonly Dictionary<TankController, int> scores = new Dictionary<TankController, int>();

        public event Action<TankController, int> ScoreChanged;

        public void RegisterTanks(IReadOnlyList<TankController> tanks)
        {
            scores.Clear();
            for (var i = 0; i < tanks.Count; i++)
            {
                var tank = tanks[i];
                if (tank != null && !scores.ContainsKey(tank))
                    scores.Add(tank, 0);
            }
        }

        public void ResetScores()
        {
            var keys = new List<TankController>(scores.Keys);
            for (var i = 0; i < keys.Count; i++)
            {
                scores[keys[i]] = 0;
                ScoreChanged?.Invoke(keys[i], 0);
            }
        }

        public int GetScore(TankController tank)
        {
            if (tank == null)
                return 0;
            return scores.TryGetValue(tank, out var score) ? score : 0;
        }

        public int AddScore(TankController tank, int amount = 1)
        {
            if (tank == null)
                return 0;

            if (!scores.ContainsKey(tank))
                scores.Add(tank, 0);

            scores[tank] += amount;
            ScoreChanged?.Invoke(tank, scores[tank]);
            return scores[tank];
        }

        public TankController ApplySurvivorScore(IReadOnlyList<TankController> tanks)
        {
            var survivor = RoundRules.GetSoleSurvivor(tanks);
            if (survivor != null)
                AddScore(survivor);
            return survivor;
        }

        public bool TryGetWinner(int winScore, out TankController winner)
        {
            winner = null;
            var bestScore = int.MinValue;

            foreach (var pair in scores)
            {
                if (pair.Value < winScore || pair.Value <= bestScore)
                    continue;

                bestScore = pair.Value;
                winner = pair.Key;
            }

            return winner != null;
        }
    }
}
