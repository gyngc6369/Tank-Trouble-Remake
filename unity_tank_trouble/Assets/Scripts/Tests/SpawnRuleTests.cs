using NUnit.Framework;
using UnityEngine;
using TankTrouble.Config;
using TankTrouble.Map;

namespace TankTrouble.Tests
{
    public sealed class SpawnRuleTests
    {
        [Test]
        public void ThreeTankSpawnPointsKeepMinimumDistanceOnRandomMap()
        {
            var map = RandomMapGenerator.Generate(seed: 20260628);
            var spawns = map.PickSpawnPoints(3, GameConfig.SpawnMinDistance, new System.Random(11));

            Assert.AreEqual(3, spawns.Count);
            for (var i = 0; i < spawns.Count; i++)
            {
                Assert.IsTrue(map.IsInside(spawns[i].x, spawns[i].y));
                for (var j = i + 1; j < spawns.Count; j++)
                    Assert.GreaterOrEqual(Manhattan(spawns[i], spawns[j]), GameConfig.SpawnMinDistance);
            }
        }

        [Test]
        public void PresetMapsCanPickThreeSpawns()
        {
            for (var i = 0; i < 5; i++)
            {
                var map = PresetMaps.Build(i);
                var spawns = map.PickSpawnPoints(3, GameConfig.SpawnMinDistance, new System.Random(i));

                Assert.AreEqual(3, spawns.Count, $"Preset map {i} should support three tanks.");
            }
        }

        private static int Manhattan(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }
    }
}
