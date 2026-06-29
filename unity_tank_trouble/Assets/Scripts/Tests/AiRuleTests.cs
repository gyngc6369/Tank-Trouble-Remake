using NUnit.Framework;
using TankTrouble.AI;
using TankTrouble.Core;

namespace TankTrouble.Tests
{
    public sealed class AiRuleTests
    {
        [Test]
        public void BallisticsSupportsAtLeastFourBounces()
        {
            Assert.GreaterOrEqual(AIBallistics.MaxAimBounces, 4);
        }

        [Test]
        public void DifficultySettingsMatchPygameBehavior()
        {
            var normal = AiDifficultySettings.FromDifficulty(AiDifficulty.Normal);
            var hard = AiDifficultySettings.FromDifficulty(AiDifficulty.Hard);

            Assert.AreEqual(0.55f, normal.BallisticsInterval);
            Assert.AreEqual(0.32f, normal.PathfindInterval);
            Assert.AreEqual(2, normal.BurstCount);

            Assert.AreEqual(0.18f, hard.BallisticsInterval);
            Assert.AreEqual(0.12f, hard.PathfindInterval);
            Assert.AreEqual(3, hard.BurstCount);
        }
    }
}
