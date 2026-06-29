using System.Collections.Generic;
using UnityEngine;

namespace TankTrouble.AI
{
    public sealed class AIBlockedEdgeSet
    {
        private readonly Dictionary<AIGridEdge, float> blockedEdges = new Dictionary<AIGridEdge, float>();
        private readonly List<AIGridEdge> expired = new List<AIGridEdge>(8);

        public int Count => blockedEdges.Count;

        public void Add(AIGridEdge edge, float duration)
        {
            if (!edge.IsValid || duration <= 0f)
                return;

            blockedEdges[edge] = Mathf.Max(duration, blockedEdges.TryGetValue(edge, out var existing) ? existing : 0f);
        }

        public bool Contains(Vector2Int from, Vector2Int to)
        {
            return blockedEdges.ContainsKey(new AIGridEdge(from, to));
        }

        public void Tick(float dt)
        {
            if (blockedEdges.Count == 0)
                return;

            expired.Clear();
            var keys = ListPool;
            keys.Clear();
            foreach (var pair in blockedEdges)
                keys.Add(pair.Key);

            for (var i = 0; i < keys.Count; i++)
            {
                var edge = keys[i];
                var remaining = blockedEdges[edge] - Mathf.Max(0f, dt);
                if (remaining <= 0f)
                    expired.Add(edge);
                else
                    blockedEdges[edge] = remaining;
            }

            for (var i = 0; i < expired.Count; i++)
                blockedEdges.Remove(expired[i]);
        }

        public void Clear()
        {
            blockedEdges.Clear();
            expired.Clear();
            ListPool.Clear();
        }

        private static readonly List<AIGridEdge> ListPool = new List<AIGridEdge>(16);
    }
}
