using System;
using UnityEngine;

namespace TankTrouble.AI
{
    public readonly struct AIGridEdge : IEquatable<AIGridEdge>
    {
        public readonly Vector2Int From;
        public readonly Vector2Int To;

        public AIGridEdge(Vector2Int from, Vector2Int to)
        {
            From = from;
            To = to;
        }

        public bool IsValid => From.x >= 0 && From.y >= 0 && To.x >= 0 && To.y >= 0;

        public bool Equals(AIGridEdge other)
        {
            return From == other.From && To == other.To;
        }

        public override bool Equals(object obj)
        {
            return obj is AIGridEdge other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + From.x;
                hash = hash * 31 + From.y;
                hash = hash * 31 + To.x;
                hash = hash * 31 + To.y;
                return hash;
            }
        }
    }
}
