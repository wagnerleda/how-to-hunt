using System.Collections.Generic;
using HowToHunt.Core;
using HowToHunt.Economy;
using HowToHunt.Hunting;
using NUnit.Framework;

namespace HowToHunt.Tests
{
    /// <summary>
    /// The Plains economy must fit the M3 demo: a hunter who sells whenever the bag is full buys a first upgrade in
    /// about three minutes and everything within ten. Breaks when content numbers drift (docs/DESIGN.md, pacing).
    /// </summary>
    public sealed class EconomyPacingTests
    {
        [Test]
        public void PlainsUpgrades_UnlockAtDemoPace()
        {
            ContentRegistry.Reset();
            var prey = new List<EconomySimulator.Prey>
            {
                new EconomySimulator.Prey { Def = ContentRegistry.Get<PreyDefinition>("prey.butterfly"), SecondsToReach = 7f },
                new EconomySimulator.Prey { Def = ContentRegistry.Get<PreyDefinition>("prey.rabbit"), SecondsToReach = 12f },
            };
            Assert.That(prey[0].Def, Is.Not.Null);
            Assert.That(prey[1].Def, Is.Not.Null);

            EconomySimulator.Result r = EconomySimulator.Run(prey, Loadouts.AllTools, Loadouts.AllUpgrades,
                catchesPerTrip: 12, secondsPerSellTrip: 20f, secondsPerSwing: 0.5f);

            Assert.That(r.AllOwned, Is.True, "Every Plains upgrade should be reachable.");
            Assert.That(r.Unlocks[0].Seconds, Is.InRange(60.0, 210.0), "First upgrade too early or too late for the demo.");
            Assert.That(r.Seconds, Is.LessThanOrEqualTo(600.0), "All Plains upgrades should be bought within ten minutes.");
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
