using NUnit.Framework;
using TankTrouble.Map;

namespace TankTrouble.Tests
{
    public sealed class MapGenerationTests
    {
        [Test]
        public void PresetMapsAreReachable()
        {
            for (var i = 0; i < 5; i++)
            {
                var map = PresetMaps.Build(i);
                Assert.IsTrue(map.CheckAllReachable(), $"Preset map {i} should be fully reachable.");
            }
        }

        [Test]
        public void RandomMapIsReachable()
        {
            var map = RandomMapGenerator.Generate(seed: 12345);
            Assert.IsTrue(map.CheckAllReachable());
        }

        [Test]
        public void MergedWallSegmentsReduceColliderCount()
        {
            var map = RandomMapGenerator.Generate(seed: 12345);
            var merged = map.GetWallSegments(mergeContiguous: true).Count;
            Assert.Less(merged, 330);
        }
    }
}
