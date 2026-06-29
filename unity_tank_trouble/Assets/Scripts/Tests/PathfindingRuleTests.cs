using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using TankTrouble.AI;
using TankTrouble.Map;

namespace TankTrouble.Tests
{
    public sealed class PathfindingRuleTests
    {
        [Test]
        public void PathfindingAvoidsDangerCellsWhenAlternativeExists()
        {
            var map = new GridMap(3, 3);
            map.FillAllWalls(false);
            var danger = new HashSet<Vector2Int> { new Vector2Int(1, 0) };
            var path = new List<Vector2Int>();

            var found = AIPathfinding.TryFindPath(map, new Vector2Int(0, 0), new Vector2Int(2, 0), danger, path);

            Assert.IsTrue(found);
            CollectionAssert.DoesNotContain(path, new Vector2Int(1, 0));
            Assert.AreEqual(new Vector2Int(2, 0), path[path.Count - 1]);
        }

        [Test]
        public void NearestSafeCellReturnsStartWhenStartIsAlreadySafe()
        {
            var map = new GridMap(3, 3);
            map.FillAllWalls(false);
            var danger = new HashSet<Vector2Int> { new Vector2Int(2, 2) };

            var found = AIPathfinding.TryFindNearestSafeCell(map, new Vector2Int(1, 1), danger, out var safeCell);

            Assert.IsTrue(found);
            Assert.AreEqual(new Vector2Int(1, 1), safeCell);
        }
        [Test]
        public void ComfortPathPrefersCenterRouteOverOuterRing()
        {
            var map = new GridMap(3, 3);
            map.FillAllWalls(false);
            var path = new List<Vector2Int>();

            var found = AIPathfinding.TryFindComfortPath(map, new Vector2Int(0, 0), new Vector2Int(2, 2), null, path);

            Assert.IsTrue(found);
            CollectionAssert.Contains(path, new Vector2Int(1, 1));
            Assert.AreEqual(new Vector2Int(2, 2), path[path.Count - 1]);
        }

        [Test]
        public void ComfortPathStillUsesRequiredNarrowCorridor()
        {
            var map = new GridMap(3, 1);
            map.FillAllWalls(false);
            var path = new List<Vector2Int>();

            var found = AIPathfinding.TryFindComfortPath(map, new Vector2Int(0, 0), new Vector2Int(2, 0), null, path);

            Assert.IsTrue(found);
            Assert.AreEqual(new Vector2Int(1, 0), path[0]);
            Assert.AreEqual(new Vector2Int(2, 0), path[path.Count - 1]);
        }

        [Test]
        public void ComfortPathAvoidsDangerBeforeComfort()
        {
            var map = new GridMap(3, 3);
            map.FillAllWalls(false);
            var danger = new HashSet<Vector2Int> { new Vector2Int(1, 1) };
            var path = new List<Vector2Int>();

            var found = AIPathfinding.TryFindComfortPath(map, new Vector2Int(0, 0), new Vector2Int(2, 2), danger, path);

            Assert.IsTrue(found);
            CollectionAssert.DoesNotContain(path, new Vector2Int(1, 1));
            Assert.AreEqual(new Vector2Int(2, 2), path[path.Count - 1]);
        }

    }
}
