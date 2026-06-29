using NUnit.Framework;
using TankTrouble.Audio;
using TankTrouble.Effects;

namespace TankTrouble.Tests
{
    public sealed class AudioRuleTests
    {
        [Test]
        public void ProceduralSoundDurationsMatchPygameSource()
        {
            Assert.AreEqual(0.1f, AudioManager.ShootClipDuration);
            Assert.AreEqual(0.2f, AudioManager.ExplosionClipDuration);
            Assert.AreEqual(0.2f, ImpactEffect.DefaultDuration);
            Assert.AreEqual(0.45f, ImpactEffect.DefeatDuration);
        }
    }
}
