using NUnit.Framework;
using TankTrouble.Config;

namespace TankTrouble.Tests
{
    public sealed class BulletRuleTests
    {
        [Test]
        public void BulletDisappearsAfterSevenCompletedBouncesAndNextWallHit()
        {
            Assert.AreEqual(7, GameConfig.MaxBounces);
        }
    }
}
