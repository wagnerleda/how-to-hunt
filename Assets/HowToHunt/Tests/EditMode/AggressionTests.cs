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
                bool struck = PreyBrain.StepAggressive(ref s, in b, hunter, true, true, world, time, Dt) != PreyStrike.None;
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
                Assert.AreEqual(PreyStrike.None, PreyBrain.StepAggressive(ref s, in b, hunter, true, true, world, i * Dt, Dt));
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
            for (int i = 0; i < 64 * 6 && !struck; i++, time += Dt) struck = PreyBrain.StepAggressive(ref s, in b, hunter, true, true, world, time, Dt) != PreyStrike.None;
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

        private static PreyBehavior Boss()
        {
            PreyBehavior b = Bear();
            b.MaxHealth = 3000f;
            b.SlamEvery = 3; b.SlamRadius = 5.5f; b.SlamWindup = 1f; b.SlamDodgeHeight = 0.5f;
            b.PounceRange = 13f; b.PounceSpeed = 16f; b.PounceWindup = 0.55f; b.PounceCooldown = 5f;
            b.EnrageBelow = 0.4f; b.EnrageTempo = 0.72f; b.EnrageSpeed = 1.1f;
            return b;
        }

        [Test]
        public void Boss_EveryThirdStrike_IsASlam()
        {
            BrushWorld world = Floor();
            PreyBehavior b = Boss();
            b.PounceRange = 0f;
            PreyState s = At(Vector3.Zero);
            s.Health = b.MaxHealth;
            var hunter = new Vector3(0f, 0.9f, 2f); // in reach: it never needs to move
            var strikes = new System.Collections.Generic.List<PreyStrike>();
            float time = 0f;
            for (int i = 0; i < 64 * 20 && strikes.Count < 6; i++, time += Dt)
            {
                PreyStrike k = PreyBrain.StepAggressive(ref s, in b, hunter, true, true, world, time, Dt);
                if (k != PreyStrike.None) strikes.Add(k);
            }
            CollectionAssert.AreEqual(new[] { PreyStrike.Swipe, PreyStrike.Swipe, PreyStrike.Slam, PreyStrike.Swipe, PreyStrike.Swipe, PreyStrike.Slam }, strikes);
        }

        [Test]
        public void Slam_HitsAllAround_ButNotAJumpingHunter()
        {
            PreyBehavior b = Boss();
            var s = new PreyState { Position = Vector3.Zero, Yaw = 0f };
            Assert.IsTrue(PreyBrain.SlamReaches(in s, in b, new Vector3(0f, 0f, -4f), 0.5f, 2f), "behind it too");
            Assert.IsTrue(PreyBrain.SlamReaches(in s, in b, new Vector3(5f, 0f, 0f), 0.5f, 2f), "beside it");
            Assert.IsFalse(PreyBrain.SlamReaches(in s, in b, new Vector3(0f, 0.8f, 3f), 0.5f, 2f), "a hunter in the air clears it");
            Assert.IsFalse(PreyBrain.SlamReaches(in s, in b, new Vector3(0f, 0f, 7f), 0.5f, 2f), "outside the circle");
        }

        [Test]
        public void Boss_PouncesAtAHunterKitingAtMidRange()
        {
            BrushWorld world = Floor();
            PreyBehavior b = Boss();
            PreyState s = At(Vector3.Zero);
            s.Health = b.MaxHealth;
            var hunter = new Vector3(0f, 0.9f, 11f);
            float time = 0f, crouched = -1f, struck = -1f;
            for (int i = 0; i < 64 * 4 && struck < 0f; i++, time += Dt)
            {
                PreyStrike k = PreyBrain.StepAggressive(ref s, in b, hunter, true, true, world, time, Dt);
                if (s.Ai == PreyAiState.Pounce && crouched < 0f) crouched = time;
                if (k != PreyStrike.None) struck = time;
            }
            Assert.GreaterOrEqual(crouched, 0f, "it crouches to pounce");
            Assert.Greater(struck, 0f, "and lands a blow");
            Assert.Less(struck - crouched, b.PounceWindup + 1f, "the leap covers the gap fast");
            Assert.IsTrue(PreyBrain.StrikeReaches(in s, in b, new Vector3(0f, 0f, 11f), 0.6f, 2.2f), "it landed on the hunter");
        }

        [Test]
        public void Boss_Enraged_WindsUpFaster()
        {
            PreyBehavior b = Boss();
            float Windup(float health)
            {
                BrushWorld world = Floor();
                PreyState s = At(Vector3.Zero);
                s.Health = health;
                var hunter = new Vector3(0f, 0.9f, 2f);
                float time = 0f, start = -1f;
                for (int i = 0; i < 64 * 4; i++, time += Dt)
                {
                    PreyStrike k = PreyBrain.StepAggressive(ref s, in b, hunter, true, true, world, time, Dt);
                    if (s.Ai == PreyAiState.Attack && start < 0f) start = time;
                    if (k != PreyStrike.None) return time - start;
                }
                return float.MaxValue;
            }
            Assert.Less(Windup(b.MaxHealth * 0.2f), Windup(b.MaxHealth) * 0.85f);
            Assert.IsTrue(PreyBrain.IsEnraged(new PreyState { Health = 1000f }, in b));
            Assert.IsFalse(PreyBrain.IsEnraged(new PreyState { Health = 2000f }, in b));
        }

        [Test]
        public void StepAggressive_DoesNotAllocate()
        {
            BrushWorld world = Floor();
            PreyBehavior b = Boss();
            PreyState s = At(Vector3.Zero);
            s.Health = 1000f; // enraged, slams and pounces included
            var hunter = new Vector3(0f, 0.9f, 8f);
            PreyBrain.StepAggressive(ref s, in b, hunter, true, true, world, 0f, Dt);
            AllocationAssert.None(() =>
            {
                for (int i = 0; i < 2000; i++) PreyBrain.StepAggressive(ref s, in b, hunter, (i & 64) == 0, true, world, i * Dt, Dt);
            });
        }
    }
}
