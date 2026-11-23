using HowToHunt.Sim.Collision;
using HowToHunt.Sim.Hunting;
using NUnit.Framework;
using Vector3 = System.Numerics.Vector3;

namespace HowToHunt.Tests
{
    /// <summary>Animals that fight back (boars, bosses): PreyBrain.StepAggressive. ADR 0014.</summary>
    public sealed class AggressionTests
    {
        private const float Dt = 1f / 64f;

        private static BrushWorld Floor()
        {
            var world = new BrushWorld();
            world.Add(Brush.Box(new Vector3(0, -0.5f, 0), new Vector3(200, 0.5f, 200)));
            return world;
        }

        private static PreyBehavior Bear(PreyAggression mode = PreyAggression.Always) => new PreyBehavior
        {
            WalkSpeed = 1.2f, FleeSpeed = 6f, FleeRadius = 8f, SightRange = 20f, CalmDownSeconds = 3f, HomeRadius = 5f, StunSeconds = 0.08f,
            HalfExtents = new Vector3(0.5f, 0.7f, 0.9f),
            Aggression = mode, AggroRange = 20f, LeashRadius = 40f, AttackRange = 2.5f, AttackWindup = 0.6f, AttackCooldown = 1.2f,
            ChargeSpeed = 7f, ProvokeSeconds = 8f,
        };

        private static PreyState At(Vector3 p) => new PreyState { Position = p, Home = p, Rng = 7, Ai = PreyAiState.Idle, Timer = 0.5f };

        [Test]
        public void Boss_ChargesAHunterInRange_WindsUp_ThenStrikes()
        {
            BrushWorld world = Floor();
            PreyBehavior b = Bear();
            PreyState s = At(Vector3.Zero);
            var hunter = new Vector3(0f, 0.9f, 12f);
            float time = 0f, windupStarted = -1f, struckAt = -1f;
            for (int i = 0; i < 64 * 6 && struckAt < 0f; i++, time += Dt)
            {
                bool struck = PreyBrain.StepAggressive(ref s, in b, hunter, true, true, world, time, Dt);
                if (s.Ai == PreyAiState.Attack && windupStarted < 0f) windupStarted = time;
                if (struck) struckAt = time;
            }
            Assert.Greater(windupStarted, 0f, "it should wind up once in reach");
            Assert.Greater(struckAt, 0f, "it should strike");
            Assert.GreaterOrEqual(struckAt - windupStarted, b.AttackWindup - Dt * 1.5f, "the wind-up gives time to dodge");
            Assert.LessOrEqual(Vector3.Distance(new Vector3(s.Position.X, 0f, s.Position.Z), new Vector3(0f, 0f, 12f)), b.AttackRange + 0.1f, "it closed in");
            Assert.IsTrue(PreyBrain.StrikeReaches(in s, in b, new Vector3(0f, 0f, 12f), 0.6f, 2.2f), "the strike reaches the hunter it faced");
        }

        [Test]
        public void Boss_IgnoresHuntersBeyondAggroRange_AndNeverFlees()
        {
            BrushWorld world = Floor();
            PreyBehavior b = Bear();
            PreyState s = At(Vector3.Zero);
            var hunter = new Vector3(0f, 0.9f, 30f); // inside the leash, outside the 20 m aggro range
            for (int i = 0; i < 64 * 5; i++)
            {
                Assert.IsFalse(PreyBrain.StepAggressive(ref s, in b, hunter, true, true, world, i * Dt, Dt));
                Assert.AreNotEqual(PreyAiState.Flee, s.Ai);
                Assert.AreNotEqual(PreyAiState.Chase, s.Ai);
            }
            Assert.Less(new Vector3(s.Position.X, 0f, s.Position.Z).Length(), b.HomeRadius + 2f, "it stays by its den");
        }

        [Test]
        public void Boss_GivesUpPastItsLeash_AndWalksHome()
        {
            BrushWorld world = Floor();
            PreyBehavior b = Bear();
            PreyState s = At(Vector3.Zero);
            var near = new Vector3(0f, 0.9f, 15f);
            float time = 0f;
            for (int i = 0; i < 64; i++, time += Dt) PreyBrain.StepAggressive(ref s, in b, near, true, true, world, time, Dt);
            Assert.AreEqual(PreyAiState.Chase, s.Ai);
            Assert.Greater(s.Position.Z, 3f, "it started the charge");
            var gone = new Vector3(0f, 0.9f, 60f); // the hunter ran past the 40 m leash
            for (int i = 0; i < 64 * 30; i++, time += Dt) PreyBrain.StepAggressive(ref s, in b, gone, true, true, world, time, Dt);
            Assert.Less(new Vector3(s.Position.X, 0f, s.Position.Z).Length(), b.HomeRadius + 2.5f, "back at its den");
            Assert.AreNotEqual(PreyAiState.Chase, s.Ai);
        }

        [Test]
        public void Boar_FleesUntilHurt_ThenCharges()
        {
            BrushWorld world = Floor();
            PreyBehavior b = Bear(PreyAggression.WhenHurt);
            b.ChargeSpeed = 7.4f;
            b.FleeSpeed = 7f;
            PreyState s = At(Vector3.Zero);
            var hunter = new Vector3(0f, 0.9f, 6f);
            float time = 0f;
            for (int i = 0; i < 32; i++, time += Dt) PreyBrain.StepAggressive(ref s, in b, hunter, true, true, world, time, Dt);
            Assert.AreEqual(PreyAiState.Flee, s.Ai, "unhurt, a boar runs from you");
            Assert.Less(s.Position.Z, 0f, "away from the hunter");

            PreyBrain.Provoke(ref s, in b, time);
            bool struck = false;
            for (int i = 0; i < 64 * 6 && !struck; i++, time += Dt) struck = PreyBrain.StepAggressive(ref s, in b, hunter, true, true, world, time, Dt);
            Assert.IsTrue(struck, "hurt, it turns and charges");
        }

        [Test]
        public void Strike_MissesBehindAboveAndOutOfReach()
        {
            PreyBehavior b = Bear();
            var s = new PreyState { Position = Vector3.Zero, Yaw = 0f };
            Assert.IsTrue(PreyBrain.StrikeReaches(in s, in b, new Vector3(0f, 0f, 2f), 0.5f, 2f), "in front");
            Assert.IsFalse(PreyBrain.StrikeReaches(in s, in b, new Vector3(0f, 0f, -2f), 0.5f, 2f), "behind");
            Assert.IsFalse(PreyBrain.StrikeReaches(in s, in b, new Vector3(0f, 3f, 2f), 0.5f, 2f), "a hunter jumping over it");
            Assert.IsFalse(PreyBrain.StrikeReaches(in s, in b, new Vector3(0f, 0f, 4f), 0.5f, 2f), "out of reach");
        }

        [Test]
        public void StepAggressive_DoesNotAllocate()
        {
            BrushWorld world = Floor();
            PreyBehavior b = Bear();
            PreyState s = At(Vector3.Zero);
            var hunter = new Vector3(0f, 0.9f, 8f);
            PreyBrain.StepAggressive(ref s, in b, hunter, true, true, world, 0f, Dt);
            AllocationAssert.None(() =>
            {
                for (int i = 0; i < 2000; i++) PreyBrain.StepAggressive(ref s, in b, hunter, (i & 64) == 0, true, world, i * Dt, Dt);
            });
        }
    }
}
