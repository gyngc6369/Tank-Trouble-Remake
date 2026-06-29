using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using TankTrouble.AI;
using TankTrouble.Entities;
using TankTrouble.Map;

namespace TankTrouble.Tests
{
    public sealed class AIBehaviorTests
    {
        [Test]
        public void PathFollowerUsesOpenNeighborInsteadOfDirectBlockedDirection()
        {
            var map = new GridMap(3, 3);
            map.FillAllWalls(true);
            map.RemoveWall(0, 1, WallDirection.Top);

            var tankObject = new GameObject("AI Path Tank");
            var enemyObject = new GameObject("Enemy");
            try
            {
                var tank = tankObject.AddComponent<Rigidbody2D>();
                tankObject.AddComponent<BoxCollider2D>();
                var tankController = tankObject.AddComponent<TankController>();
                tankObject.transform.position = TankTrouble.Core.CoordinateUtil.CellToWorld(0, 1);

                enemyObject.AddComponent<Rigidbody2D>();
                enemyObject.AddComponent<BoxCollider2D>();
                var enemy = enemyObject.AddComponent<TankController>();
                enemyObject.transform.position = TankTrouble.Core.CoordinateUtil.CellToWorld(2, 1);

                var direction = AIPathFollower.GetBestOpenNeighborDirection(
                    map,
                    tankController,
                    new List<TankController> { enemy },
                    new HashSet<Vector2Int>());

                Assert.Greater(direction.sqrMagnitude, 0.01f);
                Assert.Greater(direction.y, 0.25f);
                Assert.Less(Mathf.Abs(direction.x), 0.4f);
                Assert.IsNotNull(tank);
            }
            finally
            {
                Object.DestroyImmediate(tankObject);
                Object.DestroyImmediate(enemyObject);
            }
        }

        [Test]
        public void EvadeControllerChoosesMovingCommandWhenTrajectoryCrossesTank()
        {
            var tankObject = new GameObject("AI Evade Tank");
            try
            {
                tankObject.AddComponent<Rigidbody2D>();
                tankObject.AddComponent<BoxCollider2D>();
                var tank = tankObject.AddComponent<TankController>();
                tankObject.transform.position = Vector2.zero;

                var field = new DangerField();
                field.AddPredictedShot(new Vector2(-1f, 0f), Vector2.right, LayerMask.GetMask("Wall"), 1.1f);

                var command = AIEvadeController.BuildCommand(tank, field, null, null);

                Assert.Greater(Mathf.Abs(command.Move), 0.1f);
            }
            finally
            {
                Object.DestroyImmediate(tankObject);
            }
        }
        [Test]
        public void DriverLocksTurnDirectionForLargeAngleChanges()
        {
            var tankObject = new GameObject("AI Driver Tank");
            try
            {
                tankObject.AddComponent<Rigidbody2D>();
                tankObject.AddComponent<BoxCollider2D>();
                var tank = tankObject.AddComponent<TankController>();
                var memory = new AITankDriver.Memory();

                var first = AITankDriver.DriveToDirection(tank, Vector2.left, false, ref memory, 0.016f);
                var second = AITankDriver.DriveToDirection(tank, Vector2.right, false, ref memory, 0.016f);

                Assert.AreNotEqual(0f, first.Rotate);
                Assert.AreEqual(first.Rotate, second.Rotate);
                Assert.AreEqual(0f, first.Move);
            }
            finally
            {
                Object.DestroyImmediate(tankObject);
            }
        }

        [Test]
        public void PursueGoalStaysNearReachableEnemyArea()
        {
            var map = new GridMap(5, 3);
            map.FillAllWalls(false);
            var enemyObject = new GameObject("Pursue Enemy");
            try
            {
                enemyObject.AddComponent<Rigidbody2D>();
                enemyObject.AddComponent<BoxCollider2D>();
                var enemy = enemyObject.AddComponent<TankController>();
                enemyObject.transform.position = TankTrouble.Core.CoordinateUtil.CellToWorld(4, 1);

                var found = AIGoalPlanner.TryFindPursueGoal(
                    map,
                    new Vector2Int(0, 1),
                    new List<TankController> { enemy },
                    new HashSet<Vector2Int>(),
                    out var goal);

                var distanceToEnemy = Mathf.Abs(goal.x - 4) + Mathf.Abs(goal.y - 1);
                Assert.IsTrue(found);
                Assert.GreaterOrEqual(distanceToEnemy, 2);
                Assert.LessOrEqual(distanceToEnemy, 5);
            }
            finally
            {
                Object.DestroyImmediate(enemyObject);
            }
        }

        [Test]
        public void CenterlineFollowerRecentersToCorridorAxisBeforeMovingAcrossCell()
        {
            var map = new GridMap(3, 3);
            map.FillAllWalls(false);
            var tankObject = new GameObject("AI Centerline Axis Tank");
            try
            {
                tankObject.AddComponent<Rigidbody2D>();
                tankObject.AddComponent<BoxCollider2D>();
                var tank = tankObject.AddComponent<TankController>();
                var center = (Vector2)TankTrouble.Core.CoordinateUtil.CellToWorld(1, 1);
                tankObject.transform.position = center + new Vector2(0f, 0.12f);
                var path = new List<Vector2Int> { new Vector2Int(2, 1) };

                var direction = AICenterlineFollower.GetPathDirection(path, tank, map, 0.16f);
                var recenterDirection = (center - (Vector2)tankObject.transform.position).normalized;

                Assert.Greater(Vector2.Dot(direction, recenterDirection), 0.85f);
            }
            finally
            {
                Object.DestroyImmediate(tankObject);
            }
        }

        [Test]
        public void CenterlineFollowerReturnsToCellCenterBeforeTurn()
        {
            var map = new GridMap(3, 3);
            map.FillAllWalls(false);
            var tankObject = new GameObject("AI Centerline Turn Tank");
            try
            {
                tankObject.AddComponent<Rigidbody2D>();
                tankObject.AddComponent<BoxCollider2D>();
                var tank = tankObject.AddComponent<TankController>();
                var center = (Vector2)TankTrouble.Core.CoordinateUtil.CellToWorld(1, 1);
                tankObject.transform.position = center + new Vector2(0.12f, 0f);
                var path = new List<Vector2Int>
                {
                    new Vector2Int(2, 1),
                    new Vector2Int(2, 0)
                };

                var direction = AICenterlineFollower.GetPathDirection(path, tank, map, 0.16f);
                var recenterDirection = (center - (Vector2)tankObject.transform.position).normalized;

                Assert.Greater(Vector2.Dot(direction, recenterDirection), 0.85f);
            }
            finally
            {
                Object.DestroyImmediate(tankObject);
            }
        }

        [Test]
        public void CenterlineFollowerKeepsCurrentWaypointUntilTankIsCentered()
        {
            var map = new GridMap(3, 3);
            map.FillAllWalls(false);
            var tankObject = new GameObject("AI Centerline Current Cell Tank");
            try
            {
                tankObject.AddComponent<Rigidbody2D>();
                tankObject.AddComponent<BoxCollider2D>();
                var tank = tankObject.AddComponent<TankController>();
                var center = (Vector2)TankTrouble.Core.CoordinateUtil.CellToWorld(1, 1);
                tankObject.transform.position = center + new Vector2(0.2f, 0f);
                var path = new List<Vector2Int>
                {
                    new Vector2Int(1, 1),
                    new Vector2Int(2, 1)
                };

                var direction = AICenterlineFollower.GetPathDirection(path, tank, map, 0.16f);
                var recenterDirection = (center - (Vector2)tankObject.transform.position).normalized;

                Assert.AreEqual(2, path.Count);
                Assert.Greater(Vector2.Dot(direction, recenterDirection), 0.85f);
            }
            finally
            {
                Object.DestroyImmediate(tankObject);
            }
        }

        [Test]
        public void PathExecutorRecentersBeforeCommittedCorner()
        {
            var map = new GridMap(3, 3);
            map.FillAllWalls(false);
            var tankObject = new GameObject("AI Executor Corner Tank");
            try
            {
                tankObject.AddComponent<Rigidbody2D>();
                tankObject.AddComponent<BoxCollider2D>();
                var tank = tankObject.AddComponent<TankController>();
                var center = (Vector2)TankTrouble.Core.CoordinateUtil.CellToWorld(1, 1);
                tankObject.transform.position = center + new Vector2(0.12f, 0f);
                var executor = new AIPathExecutor();
                executor.SetPath(
                    new List<Vector2Int>
                    {
                        new Vector2Int(2, 1),
                        new Vector2Int(2, 0)
                    },
                    new Vector2Int(1, 1),
                    new Vector2Int(2, 0));

                var direction = executor.GetDirection(tank, map, 0.16f, 0.016f);
                var recenterDirection = (center - (Vector2)tankObject.transform.position).normalized;

                Assert.Greater(Vector2.Dot(direction, recenterDirection), 0.85f);
                Assert.AreEqual(2, executor.RemainingWaypoints);
            }
            finally
            {
                Object.DestroyImmediate(tankObject);
            }
        }

        [Test]
        public void PathExecutorOffersLocalCorrectionWithoutDroppingPath()
        {
            var map = new GridMap(3, 3);
            map.FillAllWalls(false);
            var tankObject = new GameObject("AI Executor Correction Tank");
            try
            {
                tankObject.AddComponent<Rigidbody2D>();
                tankObject.AddComponent<BoxCollider2D>();
                var tank = tankObject.AddComponent<TankController>();
                var center = (Vector2)TankTrouble.Core.CoordinateUtil.CellToWorld(1, 1);
                tankObject.transform.position = center + new Vector2(0.15f, 0f);
                var executor = new AIPathExecutor();
                executor.SetPath(
                    new List<Vector2Int> { new Vector2Int(2, 1) },
                    new Vector2Int(1, 1),
                    new Vector2Int(2, 1));

                var hasCorrection = executor.TryGetLocalCorrectionDirection(tank, map, out var direction);
                var recenterDirection = (center - (Vector2)tankObject.transform.position).normalized;

                Assert.IsTrue(hasCorrection);
                Assert.Greater(Vector2.Dot(direction, recenterDirection), 0.85f);
                Assert.AreEqual(1, executor.RemainingWaypoints);
            }
            finally
            {
                Object.DestroyImmediate(tankObject);
            }
        }

        [Test]
        public void SegmentFollowerTurnsInPlaceBeforeLeavingCenteredCell()
        {
            var map = new GridMap(3, 3);
            map.FillAllWalls(false);
            var tankObject = new GameObject("AI Segment Turn Tank");
            try
            {
                tankObject.AddComponent<Rigidbody2D>();
                tankObject.AddComponent<BoxCollider2D>();
                var tank = tankObject.AddComponent<TankController>();
                tankObject.transform.position = TankTrouble.Core.CoordinateUtil.CellToWorld(1, 1);
                var follower = new AIPathSegmentFollower();
                follower.SetPath(
                    new List<Vector2Int>
                    {
                        new Vector2Int(2, 1),
                        new Vector2Int(2, 0)
                    },
                    new Vector2Int(1, 1),
                    new Vector2Int(2, 0));

                var command = follower.BuildCommand(tank, map, 0.016f);

                Assert.AreEqual(0f, command.Move);
                Assert.AreNotEqual(0f, command.Rotate);
                Assert.AreEqual(2, follower.RemainingWaypoints);
            }
            finally
            {
                Object.DestroyImmediate(tankObject);
            }
        }

        [Test]
        public void SegmentFollowerMovesForwardWhenAlignedToSegment()
        {
            var map = new GridMap(3, 3);
            map.FillAllWalls(false);
            var tankObject = new GameObject("AI Segment Move Tank");
            try
            {
                tankObject.AddComponent<Rigidbody2D>();
                tankObject.AddComponent<BoxCollider2D>();
                var tank = tankObject.AddComponent<TankController>();
                tankObject.transform.SetPositionAndRotation(
                    TankTrouble.Core.CoordinateUtil.CellToWorld(1, 1),
                    Quaternion.Euler(0f, 0f, -90f));
                var follower = new AIPathSegmentFollower();
                follower.SetPath(
                    new List<Vector2Int> { new Vector2Int(2, 1) },
                    new Vector2Int(1, 1),
                    new Vector2Int(2, 1));

                var command = follower.BuildCommand(tank, map, 0.016f);

                Assert.Greater(command.Move, 0.5f);
                Assert.AreEqual(0f, command.Rotate);
            }
            finally
            {
                Object.DestroyImmediate(tankObject);
            }
        }

        [Test]
        public void SegmentFollowerKeepsCornerWaypointUntilTankReachesCenter()
        {
            var map = new GridMap(3, 3);
            map.FillAllWalls(false);
            var tankObject = new GameObject("AI Segment Corner Tank");
            try
            {
                tankObject.AddComponent<Rigidbody2D>();
                tankObject.AddComponent<BoxCollider2D>();
                var tank = tankObject.AddComponent<TankController>();
                var center = (Vector2)TankTrouble.Core.CoordinateUtil.CellToWorld(2, 1);
                tankObject.transform.position = center + new Vector2(0.11f, 0f);
                var follower = new AIPathSegmentFollower();
                follower.SetPath(
                    new List<Vector2Int>
                    {
                        new Vector2Int(2, 1),
                        new Vector2Int(2, 0)
                    },
                    new Vector2Int(1, 1),
                    new Vector2Int(2, 0));

                follower.BuildCommand(tank, map, 0.016f);
                var recenterDirection = (center - (Vector2)tankObject.transform.position).normalized;

                Assert.AreEqual(2, follower.RemainingWaypoints);
                Assert.Greater(Vector2.Dot(follower.LastDesiredDirection, recenterDirection), 0.85f);
            }
            finally
            {
                Object.DestroyImmediate(tankObject);
            }
        }

        [Test]
        public void SegmentFollowerLocksExitWhenEnteringJunction()
        {
            var map = CreateTJunctionMap();
            var tankObject = new GameObject("AI Junction Lock Tank");
            try
            {
                tankObject.AddComponent<Rigidbody2D>();
                tankObject.AddComponent<BoxCollider2D>();
                var tank = tankObject.AddComponent<TankController>();
                tankObject.transform.position = TankTrouble.Core.CoordinateUtil.CellToWorld(1, 1);
                var follower = new AIPathSegmentFollower();
                follower.SetPath(
                    new List<Vector2Int> { new Vector2Int(2, 1) },
                    new Vector2Int(1, 1),
                    new Vector2Int(2, 1));

                follower.BuildCommand(tank, map, 0.016f);

                Assert.IsTrue(follower.HasLockedJunctionExit);
                Assert.AreEqual(new Vector2Int(2, 1), follower.LockedExitCell);
            }
            finally
            {
                Object.DestroyImmediate(tankObject);
            }
        }

        [Test]
        public void SegmentFollowerLocksThroughJunctionClusterToFirstNormalCell()
        {
            var map = CreateJunctionClusterMap();
            var tankObject = new GameObject("AI Junction Cluster Tank");
            try
            {
                tankObject.AddComponent<Rigidbody2D>();
                tankObject.AddComponent<BoxCollider2D>();
                var tank = tankObject.AddComponent<TankController>();
                tankObject.transform.position = TankTrouble.Core.CoordinateUtil.CellToWorld(1, 1);
                var follower = new AIPathSegmentFollower();
                follower.SetPath(
                    new List<Vector2Int>
                    {
                        new Vector2Int(2, 1),
                        new Vector2Int(3, 1)
                    },
                    new Vector2Int(1, 1),
                    new Vector2Int(3, 1));

                follower.BuildCommand(tank, map, 0.016f);

                Assert.IsTrue(follower.HasLockedJunctionExit);
                Assert.AreEqual(new Vector2Int(3, 1), follower.LockedExitCell);

                tankObject.transform.position = TankTrouble.Core.CoordinateUtil.CellToWorld(2, 1);
                follower.BuildCommand(tank, map, 0.016f);

                Assert.IsTrue(follower.HasLockedJunctionExit);
                Assert.AreEqual(new Vector2Int(3, 1), follower.LockedExitCell);
            }
            finally
            {
                Object.DestroyImmediate(tankObject);
            }
        }

        [Test]
        public void SegmentFollowerReleasesJunctionLockAtExitCenter()
        {
            var map = CreateJunctionClusterMap();
            var tankObject = new GameObject("AI Junction Release Tank");
            try
            {
                tankObject.AddComponent<Rigidbody2D>();
                tankObject.AddComponent<BoxCollider2D>();
                var tank = tankObject.AddComponent<TankController>();
                tankObject.transform.position = TankTrouble.Core.CoordinateUtil.CellToWorld(1, 1);
                var follower = new AIPathSegmentFollower();
                follower.SetPath(
                    new List<Vector2Int>
                    {
                        new Vector2Int(2, 1),
                        new Vector2Int(3, 1)
                    },
                    new Vector2Int(1, 1),
                    new Vector2Int(3, 1));

                follower.BuildCommand(tank, map, 0.016f);
                tankObject.transform.position = TankTrouble.Core.CoordinateUtil.CellToWorld(2, 1);
                follower.BuildCommand(tank, map, 0.016f);
                tankObject.transform.position = TankTrouble.Core.CoordinateUtil.CellToWorld(3, 1);
                follower.BuildCommand(tank, map, 0.016f);

                Assert.IsFalse(follower.HasLockedJunctionExit);
                Assert.AreEqual(0, follower.RemainingWaypoints);
            }
            finally
            {
                Object.DestroyImmediate(tankObject);
            }
        }

        [Test]
        public void CenterTaskFollowerRecentersToAxisBeforeDriving()
        {
            var map = new GridMap(3, 3);
            map.FillAllWalls(false);
            var tankObject = new GameObject("AI Center Task Axis Tank");
            try
            {
                tankObject.AddComponent<Rigidbody2D>();
                tankObject.AddComponent<BoxCollider2D>();
                var tank = tankObject.AddComponent<TankController>();
                var center = (Vector2)TankTrouble.Core.CoordinateUtil.CellToWorld(1, 1);
                tankObject.transform.position = center + new Vector2(0f, 0.12f);
                var follower = new AICenterTaskFollower();
                follower.SetPath(
                    new List<Vector2Int> { new Vector2Int(2, 1) },
                    new Vector2Int(1, 1),
                    new Vector2Int(2, 1));

                follower.BuildCommand(tank, map, 0.016f);
                var recenterDirection = (center - (Vector2)tankObject.transform.position).normalized;

                Assert.Greater(Vector2.Dot(follower.LastDesiredDirection, recenterDirection), 0.85f);
                Assert.AreEqual(1, follower.RemainingWaypoints);
            }
            finally
            {
                Object.DestroyImmediate(tankObject);
            }
        }

        [Test]
        public void CenterTaskFollowerMovesToCenterWhenFarFromAxis()
        {
            var map = new GridMap(3, 3);
            map.FillAllWalls(false);
            var tankObject = new GameObject("AI Center Task Far Tank");
            try
            {
                tankObject.AddComponent<Rigidbody2D>();
                tankObject.AddComponent<BoxCollider2D>();
                var tank = tankObject.AddComponent<TankController>();
                var center = (Vector2)TankTrouble.Core.CoordinateUtil.CellToWorld(1, 1);
                tankObject.transform.position = center + new Vector2(0.16f, 0.16f);
                var follower = new AICenterTaskFollower();
                follower.SetPath(
                    new List<Vector2Int> { new Vector2Int(2, 1) },
                    new Vector2Int(1, 1),
                    new Vector2Int(2, 1));

                follower.BuildCommand(tank, map, 0.016f);
                var recenterDirection = (center - (Vector2)tankObject.transform.position).normalized;

                Assert.Greater(Vector2.Dot(follower.LastDesiredDirection, recenterDirection), 0.85f);
                Assert.AreEqual(1, follower.RemainingWaypoints);
            }
            finally
            {
                Object.DestroyImmediate(tankObject);
            }
        }

        [Test]
        public void CenterTaskFollowerTurnsBeforeLeavingCenteredCell()
        {
            var map = new GridMap(3, 3);
            map.FillAllWalls(false);
            var tankObject = new GameObject("AI Center Task Turn Tank");
            try
            {
                tankObject.AddComponent<Rigidbody2D>();
                tankObject.AddComponent<BoxCollider2D>();
                var tank = tankObject.AddComponent<TankController>();
                tankObject.transform.position = TankTrouble.Core.CoordinateUtil.CellToWorld(1, 1);
                var follower = new AICenterTaskFollower();
                follower.SetPath(
                    new List<Vector2Int> { new Vector2Int(2, 1) },
                    new Vector2Int(1, 1),
                    new Vector2Int(2, 1));

                var command = follower.BuildCommand(tank, map, 0.016f);

                Assert.AreEqual(0f, command.Move);
                Assert.AreNotEqual(0f, command.Rotate);
                Assert.IsTrue(follower.CanReplan);
            }
            finally
            {
                Object.DestroyImmediate(tankObject);
            }
        }

        [Test]
        public void CenterTaskFollowerDrivesForwardWhenAligned()
        {
            var map = new GridMap(3, 3);
            map.FillAllWalls(false);
            var tankObject = new GameObject("AI Center Task Drive Tank");
            try
            {
                tankObject.AddComponent<Rigidbody2D>();
                tankObject.AddComponent<BoxCollider2D>();
                var tank = tankObject.AddComponent<TankController>();
                tankObject.transform.SetPositionAndRotation(
                    TankTrouble.Core.CoordinateUtil.CellToWorld(1, 1),
                    Quaternion.Euler(0f, 0f, -90f));
                var follower = new AICenterTaskFollower();
                follower.SetPath(
                    new List<Vector2Int> { new Vector2Int(2, 1) },
                    new Vector2Int(1, 1),
                    new Vector2Int(2, 1));

                var command = follower.BuildCommand(tank, map, 0.016f);

                Assert.Greater(command.Move, 0.5f);
                Assert.AreEqual(0f, command.Rotate);
                Assert.IsFalse(follower.CanReplan);
            }
            finally
            {
                Object.DestroyImmediate(tankObject);
            }
        }

        [Test]
        public void CenterTaskFollowerDisallowsReplanDuringCenterToCenterDrive()
        {
            var map = new GridMap(3, 3);
            map.FillAllWalls(false);
            var tankObject = new GameObject("AI Center Task Mid Segment Tank");
            try
            {
                tankObject.AddComponent<Rigidbody2D>();
                tankObject.AddComponent<BoxCollider2D>();
                var tank = tankObject.AddComponent<TankController>();
                var center = (Vector2)TankTrouble.Core.CoordinateUtil.CellToWorld(1, 1);
                tankObject.transform.SetPositionAndRotation(
                    center,
                    Quaternion.Euler(0f, 0f, -90f));
                var follower = new AICenterTaskFollower();
                follower.SetPath(
                    new List<Vector2Int> { new Vector2Int(2, 1) },
                    new Vector2Int(1, 1),
                    new Vector2Int(2, 1));

                follower.BuildCommand(tank, map, 0.016f);
                tankObject.transform.position = center + new Vector2(0.2f, 0f);
                follower.BuildCommand(tank, map, 0.016f);

                Assert.IsFalse(follower.CanReplan);
                Assert.AreEqual(1, follower.RemainingWaypoints);
            }
            finally
            {
                Object.DestroyImmediate(tankObject);
            }
        }

        [Test]
        public void CenterTaskFollowerConsumesWaypointAtTargetCenter()
        {
            var map = new GridMap(3, 3);
            map.FillAllWalls(false);
            var tankObject = new GameObject("AI Center Task Target Tank");
            try
            {
                tankObject.AddComponent<Rigidbody2D>();
                tankObject.AddComponent<BoxCollider2D>();
                var tank = tankObject.AddComponent<TankController>();
                tankObject.transform.position = TankTrouble.Core.CoordinateUtil.CellToWorld(2, 1);
                var follower = new AICenterTaskFollower();
                follower.SetPath(
                    new List<Vector2Int> { new Vector2Int(2, 1) },
                    new Vector2Int(1, 1),
                    new Vector2Int(2, 1));

                var command = follower.BuildCommand(tank, map, 0.016f);

                Assert.AreEqual(0, follower.RemainingWaypoints);
                Assert.AreEqual(0f, command.Move);
                Assert.IsTrue(follower.CanReplan);
            }
            finally
            {
                Object.DestroyImmediate(tankObject);
            }
        }

        [Test]
        public void TankBlockedCommandDetectsRotationOnlyWallOverlap()
        {
            var tankObject = new GameObject("Rotation Block Tank");
            var wallObject = new GameObject("Rotation Block Wall");
            try
            {
                tankObject.AddComponent<Rigidbody2D>();
                tankObject.AddComponent<BoxCollider2D>();
                var tank = tankObject.AddComponent<TankController>();
                tankObject.transform.position = Vector2.zero;

                wallObject.layer = LayerMask.NameToLayer("Wall");
                wallObject.transform.position = Vector2.zero;
                var wall = wallObject.AddComponent<BoxCollider2D>();
                wall.size = Vector2.one * 0.2f;
                Physics2D.SyncTransforms();

                var blocked = tank.IsCommandBlocked(new TankInputCommand(0f, 1f, false), 0.2f);

                Assert.IsTrue(blocked);
            }
            finally
            {
                Object.DestroyImmediate(tankObject);
                Object.DestroyImmediate(wallObject);
            }
        }


        private static GridMap CreateTJunctionMap()
        {
            var map = new GridMap(3, 3);
            map.FillAllWalls(true);
            map.RemoveWall(1, 1, WallDirection.Left);
            map.RemoveWall(1, 1, WallDirection.Right);
            map.RemoveWall(1, 1, WallDirection.Top);
            return map;
        }

        private static GridMap CreateJunctionClusterMap()
        {
            var map = new GridMap(5, 3);
            map.FillAllWalls(true);
            map.RemoveWall(1, 1, WallDirection.Left);
            map.RemoveWall(1, 1, WallDirection.Right);
            map.RemoveWall(1, 1, WallDirection.Top);
            map.RemoveWall(2, 1, WallDirection.Right);
            map.RemoveWall(2, 1, WallDirection.Top);
            return map;
        }
    }
}
