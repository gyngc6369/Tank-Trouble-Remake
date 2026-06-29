using UnityEngine;
using TankTrouble.Config;

namespace TankTrouble.Core
{
    public static class CoordinateUtil
    {
        public const float PixelsPerUnit = 100f;

        public static Vector2 PixelToWorld(Vector2 pixel)
        {
            return new Vector2(pixel.x / PixelsPerUnit, -pixel.y / PixelsPerUnit);
        }

        public static Vector2 WorldToPixel(Vector2 world)
        {
            return new Vector2(world.x * PixelsPerUnit, -world.y * PixelsPerUnit);
        }

        public static Vector2 CellToPixel(int col, int row)
        {
            var x = col * GameConfig.CellSize + GameConfig.CellSize * 0.5f;
            var y = row * GameConfig.CellSize + GameConfig.CellSize * 0.5f + GameConfig.GridOffsetY;
            return new Vector2(x, y);
        }

        public static Vector2 CellToWorld(int col, int row)
        {
            return PixelToWorld(CellToPixel(col, row));
        }

        public static Vector2Int PixelToCell(Vector2 pixel)
        {
            var col = Mathf.FloorToInt(pixel.x / GameConfig.CellSize);
            var row = Mathf.FloorToInt((pixel.y - GameConfig.GridOffsetY) / GameConfig.CellSize);
            return new Vector2Int(
                Mathf.Clamp(col, 0, GameConfig.GridCols - 1),
                Mathf.Clamp(row, 0, GameConfig.GridRows - 1));
        }
    }
}
