using NUnit.Framework;
using UnityEngine;
using TankTrouble.Config;
using TankTrouble.Core;

namespace TankTrouble.Tests
{
    public sealed class CoordinateRuleTests
    {
        [Test]
        public void PixelWorldConversionRoundTrips()
        {
            var pixel = new Vector2(320f, 232f);
            var world = CoordinateUtil.PixelToWorld(pixel);
            var roundTrip = CoordinateUtil.WorldToPixel(world);

            Assert.AreEqual(pixel.x, roundTrip.x, 0.0001f);
            Assert.AreEqual(pixel.y, roundTrip.y, 0.0001f);
        }

        [Test]
        public void CellCenterIncludesTopUiOffset()
        {
            var center = CoordinateUtil.CellToPixel(0, 0);

            Assert.AreEqual(GameConfig.CellSize * 0.5f, center.x);
            Assert.AreEqual(GameConfig.GridOffsetY + GameConfig.CellSize * 0.5f, center.y);
        }

        [Test]
        public void CellToPixelAndBackReturnsSameCell()
        {
            for (var row = 0; row < GameConfig.GridRows; row++)
            for (var col = 0; col < GameConfig.GridCols; col++)
            {
                var pixel = CoordinateUtil.CellToPixel(col, row);
                var cell = CoordinateUtil.PixelToCell(pixel);

                Assert.AreEqual(col, cell.x);
                Assert.AreEqual(row, cell.y);
            }
        }
    }
}
