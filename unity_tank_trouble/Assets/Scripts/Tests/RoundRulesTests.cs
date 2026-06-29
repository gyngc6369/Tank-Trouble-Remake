using NUnit.Framework;
using TankTrouble.Core;

namespace TankTrouble.Tests
{
    public sealed class RoundRulesTests
    {
        [TestCase(3, false)]
        [TestCase(2, false)]
        [TestCase(1, true)]
        [TestCase(0, true)]
        public void RoundEndsOnlyWhenAtMostOneTankIsAlive(int aliveCount, bool expected)
        {
            Assert.AreEqual(expected, RoundRules.ShouldEndRound(aliveCount));
        }
    }
}
