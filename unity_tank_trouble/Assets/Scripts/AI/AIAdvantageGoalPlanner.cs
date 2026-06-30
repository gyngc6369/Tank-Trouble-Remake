using System.Collections.Generic;
using UnityEngine;
using TankTrouble.Core;
using TankTrouble.Entities;
using TankTrouble.Map;

namespace TankTrouble.AI
{
    public readonly struct AIAdvantageGoal
    {
        public readonly bool IsValid;
        public readonly Vector2Int Cell;
        public readonly TankController Target;
        public readonly HitSolution Shot;
        public readonly float Score;

        public AIAdvantageGoal(Vector2Int cell, TankController target, HitSolution shot, float score)
        {
            IsValid = true;
            Cell = cell;
            Target = target;
            Shot = shot;
            Score = score;
        }

        public static AIAdvantageGoal None => default;
    }

    public sealed class AIAdvantageGoalPlanner
    {
        private const int MinRangeCells = 2;
        private const int MaxRangeCells = 6;
        private const int PreferredRangeCells = 4;
        private const int MaxBallisticCandidates = 6;
        private const float RejectRisk = 0.22f;

        private readonly AIGridDistanceField distanceField = new AIGridDistanceField();
        private readonly List<Vector2Int> scratchNeighbors = new List<Vector2Int>(4);
        private readonly List<Candidate> candidates = new List<Candidate>(MaxBallisticCandidates);

        public bool TryFindGoal(
            TankController tank,
            GridMap grid,
            IReadOnlyList<TankController> enemies,
            DangerField dangerField,
            LayerMask wallMask,
            LayerMask tankMask,
            out AIAdvantageGoal goal)
        {
            goal = AIAdvantageGoal.None;
            candidates.Clear();

            if (tank == null || grid == null || enemies == null || enemies.Count == 0)
                return false;

            var start = CoordinateUtil.PixelToCell(CoordinateUtil.WorldToPixel(tank.transform.position));
            if (!grid.IsInside(start.x, start.y))
                return false;

            var facing = AIGridDirections.FromForward(tank.VelocityForward);
            distanceField.Build(grid, start);
            CollectCheapCandidates(tank, grid, start, facing, enemies, dangerField);
            if (candidates.Count == 0)
                return false;

            candidates.Sort((a, b) => a.CheapScore.CompareTo(b.CheapScore));

            var bestScore = float.PositiveInfinity;
            var found = false;
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                var center = CoordinateUtil.CellToWorld(candidate.Cell.x, candidate.Cell.y);
                var preferredForward = ((Vector2)candidate.NearestEnemy.transform.position - center).normalized;
                if (!AIBallistics.TryFindBestShotFromCenter(tank, center, preferredForward, enemies, wallMask, tankMask, out var shot))
                    continue;

                if (shot.Target == null || !shot.Target.Alive)
                    continue;

                var shotTargetCell = CoordinateUtil.PixelToCell(CoordinateUtil.WorldToPixel(shot.Target.transform.position));
                var shotRange = Manhattan(candidate.Cell, shotTargetCell);
                if (shotRange < MinRangeCells || shotRange > MaxRangeCells)
                    continue;

                var ballisticPenalty = shot.Bounces * 48f + shot.SelfRisk * 420f + shot.AngleError * 1.4f + shot.PathLength * 4f;
                var score = candidate.CheapScore + ballisticPenalty;
                if (score >= bestScore)
                    continue;

                bestScore = score;
                goal = new AIAdvantageGoal(candidate.Cell, shot.Target, shot, score);
                found = true;
            }

            return found;
        }

        private void CollectCheapCandidates(
            TankController tank,
            GridMap grid,
            Vector2Int start,
            AIGridDirection facing,
            IReadOnlyList<TankController> enemies,
            DangerField dangerField)
        {
            for (var enemyIndex = 0; enemyIndex < enemies.Count; enemyIndex++)
            {
                var nearestEnemy = enemies[enemyIndex];
                if (nearestEnemy == null || !nearestEnemy.Alive)
                    continue;

                var enemyCell = CoordinateUtil.PixelToCell(CoordinateUtil.WorldToPixel(nearestEnemy.transform.position));
                for (var row = enemyCell.y - MaxRangeCells; row <= enemyCell.y + MaxRangeCells; row++)
                {
                    for (var col = enemyCell.x - MaxRangeCells; col <= enemyCell.x + MaxRangeCells; col++)
                    {
                        if (!grid.IsInside(col, row))
                            continue;

                        var cell = new Vector2Int(col, row);
                        var range = Manhattan(cell, enemyCell);
                        if (range < MinRangeCells || range > MaxRangeCells)
                            continue;

                        var pathLength = distanceField.GetDistance(cell);
                        if (pathLength < 0)
                            continue;

                        var center = CoordinateUtil.CellToWorld(col, row);
                        var arrivalTime = EstimateArrivalTime(pathLength);
                        var risk = EstimateRisk(dangerField, center, arrivalTime);
                        if (risk >= RejectRisk)
                            continue;

                        var openness = CountOpenNeighbors(grid, cell);
                        var rangePenalty = Mathf.Abs(range - PreferredRangeCells) * 42f;
                        var travelPenalty = pathLength * 16f;
                        var dangerPenalty = risk * 3000f;
                        var deadEndPenalty = (4 - openness) * 18f;
                        var facingPenalty = EstimateFacingPenalty(facing, start, cell);
                        var cheapScore = dangerPenalty + travelPenalty + rangePenalty + deadEndPenalty + facingPenalty;
                        AddCandidate(new Candidate(cell, nearestEnemy, cheapScore));
                    }
                }
            }
        }

        private void AddCandidate(Candidate candidate)
        {
            if (candidates.Count < MaxBallisticCandidates)
            {
                candidates.Add(candidate);
                return;
            }

            var worstIndex = 0;
            var worstScore = candidates[0].CheapScore;
            for (var i = 1; i < candidates.Count; i++)
            {
                if (candidates[i].CheapScore <= worstScore)
                    continue;

                worstScore = candidates[i].CheapScore;
                worstIndex = i;
            }

            if (candidate.CheapScore < worstScore)
                candidates[worstIndex] = candidate;
        }

        private static float EstimateArrivalTime(int pathLength)
        {
            return Mathf.Clamp(pathLength * 0.34f + 0.2f, 0f, 2.2f);
        }

        private static float EstimateRisk(DangerField dangerField, Vector2 center, float arrivalTime)
        {
            if (dangerField == null || !dangerField.HasDanger)
                return 0f;

            return Mathf.Max(
                dangerField.GetRisk(center, 0f),
                dangerField.GetRisk(center, arrivalTime),
                dangerField.GetRisk(center, arrivalTime + 0.45f));
        }

        private static TankController FindNearestEnemy(Vector2 position, IReadOnlyList<TankController> enemies)
        {
            var best = default(TankController);
            var bestDistance = float.PositiveInfinity;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.Alive)
                    continue;

                var distance = Vector2.SqrMagnitude((Vector2)enemy.transform.position - position);
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                best = enemy;
            }

            return best;
        }

        private int CountOpenNeighbors(GridMap grid, Vector2Int cell)
        {
            grid.FillNeighbors(cell.x, cell.y, scratchNeighbors);
            return scratchNeighbors.Count;
        }

        private static float EstimateFacingPenalty(AIGridDirection facing, Vector2Int start, Vector2Int cell)
        {
            var delta = cell - start;
            if (delta == Vector2Int.zero)
                return 0f;

            var preferred = AIGridDirections.FromDelta(delta);
            return preferred == facing ? 0f : 8f;
        }

        private static int Manhattan(Vector2Int a, Vector2Int b) => AIGeometryUtils.Manhattan(a, b);

        private readonly struct Candidate
        {
            public readonly Vector2Int Cell;
            public readonly TankController NearestEnemy;
            public readonly float CheapScore;

            public Candidate(Vector2Int cell, TankController nearestEnemy, float cheapScore)
            {
                Cell = cell;
                NearestEnemy = nearestEnemy;
                CheapScore = cheapScore;
            }
        }
    }
}
