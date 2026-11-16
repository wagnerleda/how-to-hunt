using System.Numerics;
using HowToHunt.Sim.Collision;
using HowToHunt.Sim.Hunting;
using NUnit.Framework;

namespace HowToHunt.Tests
{
    /// <summary>Prey flee from as far as they can see you, hide behind cover, and bolt at gunshots.</summary>
    public sealed class PreySensesTests
    {
        private const float Dt = 1f / 64f;

        private static PreyBehavior Rabbit(float wariness = 1f) => new PreyBehavior
        {
            WalkSpeed = 1.2f, FleeSpeed = 8.5f, FleeRadius = 8f * wariness, SightRange = 22f * wariness, HearingRange = 45f,
            CalmDownSeconds = 3f, HomeRadius = 30f, StunSeconds = 0.15f, HalfExtents = new Vector3(0.15f, 0.15f, 0.2f),
            StaminaSeconds = 3f, TiredSpeed = 4.8f,
        };

        private static BrushWorld Flat()
        {
            var world = new BrushWorld();
            world.Add(Brush.Box(new Vector3(0f, -0.5f, 0f), new Vector3(200f, 0.5f, 200f)));
            return world;
        }

        private static readonly Vector3 PlayerEye = new Vector3(0f, 1.6f, -18f);

        [Test]
        public void SeesAPlayerInTheOpen_FarBeyondTheCloseFleeRadius()
        {
            BrushWorld world = Flat();
            PreyBehavior b = Rabbit();
            var s = new PreyState { Rng = 3, Stamina = 3f };
            Assert.IsTrue(PreyBrain.CanSee(in s, in b, PlayerEye, 1f, world));
            for (int i = 0; i < 16; i++) PreyBrain.Step(ref s, in b, new Vector3(0f, 0f, -18f), true, true, world, i * Dt, Dt);
            Assert.AreEqual(PreyAiState.Flee, s.Ai);
            Assert.Greater(s.Position.Z, 0.5f, "runs away from the player");
        }

        [Test]
        public void CoverHidesThePlayer_AndCrouchingHalvesTheRange()
        {
            BrushWorld world = Flat();
            PreyBehavior b = Rabbit();
            var s = new PreyState();
            Assert.IsFalse(PreyBrain.CanSee(in s, in b, PlayerEye, 0.5f, world), "crouched at 18 m: 11 m sight");
            world.Add(Brush.Box(new Vector3(0f, 1.5f, -9f), new Vector3(3f, 1.5f, 0.5f)));
            Assert.IsFalse(PreyBrain.CanSee(in s, in b, PlayerEye, 1f, world), "a rock in between");
        }

        [Test]
        public void RarePreyAreWarier()
        {
            BrushWorld world = Flat();
            var s = new PreyState();
            var far = new Vector3(0f, 1.6f, -30f);
            PreyBehavior common = Rabbit(), rare = Rabbit(1.5f);
            Assert.IsFalse(PreyBrain.CanSee(in s, in common, far, 1f, world));
            Assert.IsTrue(PreyBrain.CanSee(in s, in rare, far, 1f, world));
        }

        [Test]
        public void GunshotsStartleHarmlessPrey_NotDeafOnes()
        {
            BrushWorld world = Flat();
            PreyBehavior b = Rabbit();
            var s = new PreyState { Rng = 9, Stamina = 3f };
            var shot = new Vector3(40f, 1.6f, 0f);
            Assert.IsTrue(PreyBrain.Startle(ref s, in b, shot, 0f));
            for (int i = 0; i < 32; i++) PreyBrain.Step(ref s, in b, default, false, world, i * Dt, Dt);
            Assert.AreEqual(PreyAiState.Flee, s.Ai);
            Assert.Less(s.Position.X, -1f, "bolts away from the shot");

            var far = new PreyState();
            Assert.IsFalse(PreyBrain.Startle(ref far, in b, new Vector3(60f, 0f, 0f), 0f), "out of earshot");
            PreyBehavior deaf = b;
            deaf.HearingRange = 0f;
            var bear = new PreyState();
            Assert.IsFalse(PreyBrain.Startle(ref bear, in deaf, shot, 0f), "predators aren't scared of guns");
        }
    }
}
