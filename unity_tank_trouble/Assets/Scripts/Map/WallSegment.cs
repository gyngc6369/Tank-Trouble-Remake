using UnityEngine;
using TankTrouble.Config;
using TankTrouble.Core;

namespace TankTrouble.Map
{
    public readonly struct WallSegment
    {
        public readonly Vector2 StartPixel;
        public readonly Vector2 EndPixel;

        public WallSegment(Vector2 startPixel, Vector2 endPixel)
        {
            StartPixel = startPixel;
            EndPixel = endPixel;
        }

        public bool IsHorizontal => Mathf.Approximately(StartPixel.y, EndPixel.y);
        public bool IsVertical => Mathf.Approximately(StartPixel.x, EndPixel.x);
        public Vector2 CenterPixel => (StartPixel + EndPixel) * 0.5f;
        public float LengthPixel => IsHorizontal
            ? Mathf.Abs(EndPixel.x - StartPixel.x)
            : Mathf.Abs(EndPixel.y - StartPixel.y);

        public Vector2 WorldCenter => CoordinateUtil.PixelToWorld(CenterPixel);

        public Vector2 WorldSize
        {
            get
            {
                var length = Mathf.Max(LengthPixel, GameConfig.WallThickness) / CoordinateUtil.PixelsPerUnit;
                var thickness = GameConfig.WallThickness / CoordinateUtil.PixelsPerUnit;
                return IsHorizontal ? new Vector2(length, thickness) : new Vector2(thickness, length);
            }
        }
    }
}
