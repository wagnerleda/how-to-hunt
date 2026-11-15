using HowToHunt.Sim.Collision;
using HowToHunt.Sim.Economy;
using HowToHunt.Sim.Hunting;
using NUnit.Framework;
using Vector3 = System.Numerics.Vector3;

namespace HowToHunt.Tests
{
    public class EconomyAndHuntingTests
    {
        private static readonly PreyStats Butterfly = new PreyStats { Allowed = TakedownMethods.Either, MaxHealth = 10, CaptureDifficulty = 1, MinKillTier = 0, OverkillRatio = 2f };
        private static readonly PreyStats Rabbit = new PreyStats { Allowed = TakedownMethods.Either, MaxHealth = 40, CaptureDifficulty = 2, MinKillTier = 0, OverkillRatio = 3f };
        private static readonly ToolStats StarterNet = new ToolStats { Archetype = ToolArchetype.Net, Mode = ToolMode.Capture, CaptureStrength = 1 };
        private static readonly ToolStats SturdyNet = new ToolStats { Archetype = ToolArchetype.Net, Mode = ToolMode.Capture, CaptureStrength = 2 };
        private static readonly ToolStats Blade = new ToolStats { Archetype = ToolArchetype.Blade, Mode = ToolMode.Kill, Tier = 1, Damage = 20 };
        private static readonly ToolStats Pistol = new ToolStats { Archetype = ToolArchetype.Firearm, Mode = ToolMode.Kill, Tier = 2, Damage = 35, HeadshotMultiplier = 2 };

        [Test]
        public void BigDouble_ArithmeticCompareAndFormat()
        {
            BigDouble a = 1500, b = 2.5e6;
            Assert.AreEqual(2501500d, (a + b).ToDouble(), 1e-6);
            Assert.AreEqual(3.75e9, (a * b).ToDouble(), 1);
            Assert.IsTrue(b > a);
            Assert.AreEqual("1.5K", a.ToShortString());
            Assert.AreEqual("2.5M", b.ToShortString());
            BigDouble huge = BigDouble.Create(4.2, 500);
            Assert.IsTrue(huge * huge > huge, "beyond double range");
            Assert.AreEqual("4.2e500", huge.ToShortString());
            Assert.AreEqual(huge, BigDouble.Deserialize(huge.Serialize()));
            Assert.AreEqual(0d, (a - a).ToDouble(), 1e-9);
        }

        [Test]
        public void Modifiers_ApplyAddThenMultiply_AndExplain()
        {
            var m = new ModifierStack().Add("lucky charm", 2).Multiply("captured alive", 1.5).Multiply("mastery 1", 1.1);
            Assert.AreEqual(23.1, m.Evaluate(12).ToDouble(), 1e-9);
            StringAssert.Contains("captured alive", m.Explain(12));
            StringAssert.EndsWith("= 23.1", m.Explain(12));
        }

        [Test]
        public void Net_CatchesButterfly_ButStarterNetCantHoldRabbit()
        {
            Assert.AreEqual(TakedownOutcome.Captured, Takedown.Resolve(Butterfly, 10, StarterNet, false).Outcome);
            TakedownResult rabbit = Takedown.Resolve(Rabbit, 40, StarterNet, false);
            Assert.AreEqual(TakedownOutcome.Rejected, rabbit.Outcome);
            Assert.AreEqual(RejectReason.TooWeakToHold, rabbit.Reason);
            Assert.AreEqual(TakedownOutcome.Captured, Takedown.Resolve(Rabbit, 40, SturdyNet, false).Outcome);
        }

        [Test]
        public void Blade_KillsButterflyIntact_PistolRuinsIt()
        {
            TakedownResult blade = Takedown.Resolve(Butterfly, 10, Blade, false);
            Assert.AreEqual(TakedownOutcome.Killed, blade.Outcome);
            Assert.AreEqual(CarcassQuality.Intact, blade.Quality);
            TakedownResult pistol = Takedown.Resolve(Butterfly, 10, Pistol, false);
            Assert.AreEqual(TakedownOutcome.Killed, pistol.Outcome);
            Assert.AreEqual(CarcassQuality.Ruined, pistol.Quality, "35 damage on a 10 HP butterfly is overkill");
        }

        [Test]
        public void Rabbit_TakesTwoBladeHits_OneHeadshot()
        {
            TakedownResult first = Takedown.Resolve(Rabbit, 40, Blade, false);
            Assert.AreEqual(TakedownOutcome.Damaged, first.Outcome);
            Assert.AreEqual(TakedownOutcome.Killed, Takedown.Resolve(Rabbit, first.HealthAfter, Blade, false).Outcome);
            Assert.AreEqual(TakedownOutcome.Killed, Takedown.Resolve(Rabbit, 40, Pistol, weakPoint: true).Outcome);
        }

        [Test]
        public void WeakTools_AndForbiddenMethods_ExplainWhy()
        {
            var armored = new PreyStats { Allowed = TakedownMethods.Kill, MaxHealth = 300, CaptureDifficulty = 9, MinKillTier = 3 };
            Assert.AreEqual(RejectReason.TooWeakToHurt, Takedown.Resolve(armored, 300, Pistol, false).Reason);
            Assert.AreEqual(RejectReason.MethodNotAllowed, Takedown.Resolve(armored, 300, SturdyNet, false).Reason);
        }

        [Test]
        public void Rewards_CaptureBeatsKill_RuinedLosesValue_MasteryAdds()
        {
            var econ = new PreyEconomy { BaseValue = 20, TrophiesOnKill = 1 };
            var rules = EconomyRules.Default;
            var explain = new ModifierStack();
            CatchReward kill = Rewards.Compute(econ, rules, captured: false, CarcassQuality.Intact, 0, explain);
            CatchReward capture = Rewards.Compute(econ, rules, captured: true, CarcassQuality.Intact, 0, explain);
            CatchReward ruined = Rewards.Compute(econ, rules, captured: false, CarcassQuality.Ruined, 0, explain);
            CatchReward mastered = Rewards.Compute(econ, rules, captured: true, CarcassQuality.Intact, 10, explain);
            Assert.AreEqual(20d, kill.Coins.ToDouble());
            Assert.AreEqual(1d, kill.Trophies);
            Assert.AreEqual(30d, capture.Coins.ToDouble());
            Assert.AreEqual(1, capture.Research);
            Assert.AreEqual(6d, ruined.Coins.ToDouble());
            Assert.AreEqual(36d, mastered.Coins.ToDouble(), "mastery 2 = +20%");
        }

        [Test]
        public void PreyBrain_FleesFromThreat_AndStaysOnTheGround()
        {
            var world = new BrushWorld();
            world.Add(Brush.Box(new Vector3(0, -0.5f, 0), new Vector3(100, 0.5f, 100)));
            var behavior = new PreyBehavior { WalkSpeed = 1.2f, FleeSpeed = 6f, FleeRadius = 6f, CalmDownSeconds = 2f, HomeRadius = 15f, StunSeconds = 1f, HalfExtents = new Vector3(0.2f, 0.2f, 0.2f) };
            var s = new PreyState { Position = new Vector3(0, 0, 0), Home = Vector3.Zero, Rng = 1234 };
            var threat = new Vector3(0, 0, -3);
            for (int i = 0; i < 64; i++) PreyBrain.Step(ref s, behavior, threat, true, world, i / 64f, 1f / 64f);
            Assert.AreEqual(PreyAiState.Flee, s.Ai);
            Assert.Greater(s.Position.Z, 3f, "ran away from the threat");
            Assert.AreEqual(0f, s.Position.Y, 0.05f, "on the ground");
        }

        [Test]
        public void ButterflyFlight_IsDeterministicAndSmooth()
        {
            var home = new Vector3(5, 0, 5);
            var a = ButterflyFlight.PoseAt(42, home, 6f, 1000, 64);
            var b = ButterflyFlight.PoseAt(42, home, 6f, 1000, 64);
            var next = ButterflyFlight.PoseAt(42, home, 6f, 1001, 64);
            Assert.AreEqual(a.Position, b.Position);
            Assert.Less(Vector3.Distance(a.Position, next.Position), 0.2f, "moves a little per tick");
            Assert.Greater(a.Position.Y, 0.3f);
        }
    }
}
