using UnityEngine;
using TankTrouble.Map;

namespace TankTrouble.AI
{
    public enum AIGridDirection
    {
        Up = 0,
        Right = 1,
        Down = 2,
        Left = 3
    }

    public static class AIGridDirections
    {
        public static AIGridDirection FromForward(Vector2 forward)
        {
            if (Mathf.Abs(forward.x) >= Mathf.Abs(forward.y))
                return forward.x >= 0f ? AIGridDirection.Right : AIGridDirection.Left;

            return forward.y >= 0f ? AIGridDirection.Up : AIGridDirection.Down;
        }

        public static AIGridDirection FromDelta(Vector2Int delta)
        {
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
                return delta.x >= 0 ? AIGridDirection.Right : AIGridDirection.Left;

            return delta.y <= 0 ? AIGridDirection.Up : AIGridDirection.Down;
        }

        public static AIGridDirection TurnLeft(AIGridDirection direction)
        {
            return (AIGridDirection)(((int)direction + 3) & 3);
        }

        public static AIGridDirection TurnRight(AIGridDirection direction)
        {
            return (AIGridDirection)(((int)direction + 1) & 3);
        }

        public static Vector2Int ToDelta(AIGridDirection direction)
        {
            switch (direction)
            {
                case AIGridDirection.Up:
                    return new Vector2Int(0, -1);
                case AIGridDirection.Right:
                    return new Vector2Int(1, 0);
                case AIGridDirection.Down:
                    return new Vector2Int(0, 1);
                default:
                    return new Vector2Int(-1, 0);
            }
        }

        public static Vector2 ToWorld(AIGridDirection direction)
        {
            switch (direction)
            {
                case AIGridDirection.Up:
                    return Vector2.up;
                case AIGridDirection.Right:
                    return Vector2.right;
                case AIGridDirection.Down:
                    return Vector2.down;
                default:
                    return Vector2.left;
            }
        }

        public static WallDirection ToWallDirection(AIGridDirection direction)
        {
            switch (direction)
            {
                case AIGridDirection.Up:
                    return WallDirection.Top;
                case AIGridDirection.Right:
                    return WallDirection.Right;
                case AIGridDirection.Down:
                    return WallDirection.Bottom;
                default:
                    return WallDirection.Left;
            }
        }
    }
}
