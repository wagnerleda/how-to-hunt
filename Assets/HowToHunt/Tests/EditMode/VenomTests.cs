using HowToHunt.Core;
using HowToHunt.Hunting;
using NUnit.Framework;

namespace HowToHunt.Tests
{
    /// <summary>The desert's venomous animals (owner, 2026-11-24): their strikes poison, the others' don't.</summary>
    public sealed class VenomTests
    {
        [Test]
        public void SnakesAndScorpionsPoison_TheOthersDont()
        {
            ContentRegistry.Reset();
            foreach (string key in new[] { "prey.rattlesnake", "prey.coral_snake", "prey.scorpion" })
            {
                PreyDefinition p = ContentRegistry.Get<PreyDefinition>(key);
                Assert.That(p, Is.Not.Null, key);
                Assert.Greater(p.PoisonDamagePerSecond, 0f, key + " is venomous");
                Assert.Greater(p.PoisonSeconds, 0f, key);
            }
            PreyDefinition coral = ContentRegistry.Get<PreyDefinition>("prey.coral_snake");
            PreyDefinition rattler = ContentRegistry.Get<PreyDefinition>("prey.rattlesnake");
            Assert.Greater(coral.PoisonDamagePerSecond * coral.PoisonSeconds, rattler.PoisonDamagePerSecond * rattler.PoisonSeconds, "the coral snake's venom is the worst");
            foreach (string key in new[] { "prey.boar", "prey.coyote", "prey.old_bear" })
                Assert.AreEqual(0f, ContentRegistry.Get<PreyDefinition>(key).PoisonDamagePerSecond, key + " bites, no venom");
        }
    }
}
