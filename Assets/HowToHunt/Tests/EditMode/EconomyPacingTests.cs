using System.Collections.Generic;
using HowToHunt.Core;
using HowToHunt.Economy;
using HowToHunt.Hunting;
using NUnit.Framework;

namespace HowToHunt.Tests
{
    /// <summary>
    /// The Plains economy must fit the M3 demo: a hunter who sells whenever the bag is full buys a first upgrade in about five minutes and everything in about 15, carrying three slots (net, knife, one catch). Phase 3.5; was 20-30, and before that
    /// three and ten in the M3 demo). Breaks when content numbers drift (docs/DESIGN.md, pacing).
    /// </summary>
    public sealed class EconomyPacingTests
    {
        [Test]
        public void PlainsUpgrades_UnlockAtDemoPace()
        {
            ContentRegistry.Reset();
            var prey = new List<EconomySimulator.Prey>
            {
                new EconomySimulator.Prey { Def = ContentRegistry.Get<PreyDefinition>("prey.butterfly"), SecondsToReach = 7f, Group = 1.6f },
                new EconomySimulator.Prey { Def = ContentRegistry.Get<PreyDefinition>("prey.rabbit"), SecondsToReach = 12f },
            };
            Assert.That(prey[0].Def, Is.Not.Null);
            Assert.That(prey[1].Def, Is.Not.Null);

            EconomySimulator.Result r = EconomySimulator.Run(prey, Loadouts.AllTools, Loadouts.AllUpgrades,
                catchesPerTrip: 12, secondsPerSellTrip: 20f, secondsPerSwing: 0.5f, freeSlots: 1);

            Assert.That(r.AllOwned, Is.True, "Every Plains upgrade should be reachable.");
            Assert.That(r.Unlocks[0].Seconds, Is.InRange(210.0, 390.0), "First upgrade should take a few minutes of hunting (about 4-6).");
            Assert.That(r.Seconds, Is.InRange(780.0, 1080.0), "All Plains upgrades in about 15 minutes (owner, Phase 3.5).");
        }

        [Test]
        public void CashBurst_ScalesWithTheAreasExpectedSale()
        {
            Assert.That(CashBurst.Bills(5, 50), Is.EqualTo(0), "small sale for the area: no burst");
            int normal = CashBurst.Bills(50, 50), big = CashBurst.Bills(150, 50);
            Assert.That(normal, Is.GreaterThan(5));
            Assert.That(big, Is.GreaterThan(normal * 2));
            Assert.That(CashBurst.Bills(1e9, 50), Is.EqualTo(CashBurst.MaxBills.Value), "capped");
            Assert.That(CashBurst.Bills(50, 5000), Is.EqualTo(0), "the same sale in a richer area is small");
        }

        [Test]
        public void StarterNet_CannotHoldRabbit_SturdyNetCan()
        {
            ContentRegistry.Reset();
            Loadouts.Reset();
            PreyDefinition rabbit = ContentRegistry.Get<PreyDefinition>("prey.rabbit");
            ToolDefinition net = ContentRegistry.Get<ToolDefinition>("tool.net");
            UpgradeDefinition sturdy = ContentRegistry.Get<UpgradeDefinition>("upgrade.net_sturdy");
            var loadout = new Loadout();
            Sim.Hunting.TakedownResult before = Sim.Hunting.Takedown.Resolve(rabbit.Stats, rabbit.MaxHealth, loadout.StatsFor(net, out _), false);
            Assert.That(before.Reason, Is.EqualTo(Sim.Hunting.RejectReason.TooWeakToHold));
            loadout.AddUpgrade(sturdy.Id.Value);
            Sim.Hunting.TakedownResult after = Sim.Hunting.Takedown.Resolve(rabbit.Stats, rabbit.MaxHealth, loadout.StatsFor(net, out _), false);
            Assert.That(after.Outcome, Is.EqualTo(Sim.Hunting.TakedownOutcome.Captured));
        }
    }
}
