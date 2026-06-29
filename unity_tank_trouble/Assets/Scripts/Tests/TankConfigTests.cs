using NUnit.Framework;
using TankTrouble.Config;

namespace TankTrouble.Tests
{
    public sealed class TankConfigTests
    {
        [Test]
        public void TankAndBulletParametersMatchPygameSource()
        {
            Assert.AreEqual(20f, GameConfig.TankBodyWidth);
            Assert.AreEqual(28f, GameConfig.TankBodyHeight);
            Assert.AreEqual(110f, GameConfig.TankSpeed);
            Assert.AreEqual(150f, GameConfig.TankRotationSpeedDeg);
            Assert.AreEqual(165f, GameConfig.BulletSpeed);
            Assert.AreEqual(2f, GameConfig.BulletRadius);
            Assert.AreEqual(1.0f, GameConfig.AmmoRegenTime);
        }
    }
}
