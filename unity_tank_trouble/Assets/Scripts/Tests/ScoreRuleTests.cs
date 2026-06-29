using NUnit.Framework;
using UnityEngine;
using TankTrouble.Core;
using TankTrouble.Entities;

namespace TankTrouble.Tests
{
    public sealed class ScoreRuleTests
    {
        [Test]
        public void SurvivorScoreOnlyAwardsTheSoleAliveTank()
        {
            var scoreObject = new GameObject("ScoreManagerTest");
            var aObject = new GameObject("TankA");
            var bObject = new GameObject("TankB");
            var cObject = new GameObject("TankC");

            try
            {
                var score = scoreObject.AddComponent<ScoreManager>();
                var a = aObject.AddComponent<TankController>();
                var b = bObject.AddComponent<TankController>();
                var c = cObject.AddComponent<TankController>();

                score.RegisterTanks(new[] { a, b, c });
                b.ApplyHit(a);
                c.ApplyHit(a);

                var survivor = score.ApplySurvivorScore(new[] { a, b, c });

                Assert.AreSame(a, survivor);
                Assert.AreEqual(1, score.GetScore(a));
                Assert.AreEqual(0, score.GetScore(b));
                Assert.AreEqual(0, score.GetScore(c));
            }
            finally
            {
                Object.DestroyImmediate(scoreObject);
                Object.DestroyImmediate(aObject);
                Object.DestroyImmediate(bObject);
                Object.DestroyImmediate(cObject);
            }
        }
    }
}
