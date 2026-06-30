using UnityEngine;

namespace TankTrouble.AI
{
    /// <summary>
    /// Shared geometry and math utilities used across the AI subsystem.
    /// Consolidates duplicated functions that previously existed in multiple files.
    /// </summary>
    public static class AIGeometryUtils
    {
        /// <summary>
        /// Manhattan distance between two grid cells.
        /// </summary>
        public static int Manhattan(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        /// <summary>
        /// Returns true if two grid cells are adjacent (Manhattan distance == 1).
        /// </summary>
        public static bool AreAdjacent(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1;
        }

        /// <summary>
        /// Returns the tank rotation input needed to turn toward a signed angle.
        /// Positive signedAngle → turn right (rotate = -1 in Unity).
        /// </summary>
        public static float RotateInputForAngle(float signedAngle)
        {
            return signedAngle > 0f ? -1f : 1f;
        }

        /// <summary>
        /// Closest point on a line segment to a given point, with parametric t in [0,1].
        /// </summary>
        public static Vector2 ClosestPointOnSegment(Vector2 point, Vector2 start, Vector2 end, out float t)
        {
            var segment = end - start;
            var lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= 0.000001f)
            {
                t = 0f;
                return start;
            }

            t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
            return start + segment * t;
        }

        /// <summary>
        /// Counts the number of open (no wall) orthogonal neighbors for a grid cell.
        /// </summary>
        public static int CountOpenNeighbors(Map.GridMap grid, Vector2Int cell)
        {
            var count = 0;
            if (!grid.HasWall(cell.x, cell.y, Map.WallDirection.Top)) count++;
            if (!grid.HasWall(cell.x, cell.y, Map.WallDirection.Right)) count++;
            if (!grid.HasWall(cell.x, cell.y, Map.WallDirection.Bottom)) count++;
            if (!grid.HasWall(cell.x, cell.y, Map.WallDirection.Left)) count++;
            return count;
        }
    }
}
