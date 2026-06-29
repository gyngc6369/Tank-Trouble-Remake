using NUnit.Framework;
using TankTrouble.Config;

namespace TankTrouble.Tests
{
    public sealed class MenuConfigTests
    {
        [Test]
        public void WinScoreOptionsMatchPygameSource()
        {
            CollectionAssert.AreEqual(new[] { 1, 3, 5, 7, 10 }, GameConfig.WinScoreOptions);
        }
    }
}
