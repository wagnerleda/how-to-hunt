using System;
using System.Collections.Generic;
using HowToHunt.Hunting;
using HowToHunt.Sim.Economy;
using HowToHunt.Sim.Hunting;

namespace HowToHunt.Economy
{
    /// <summary>
    /// Headless pacing check: a hunter who always takes the best-paying catch their tools allow, sells when the bag
    /// is full and buys every upgrade they can afford. Reports when each upgrade is bought, so tuning changes can be
    /// judged against the target (all Plains upgrades within a few minutes for the M3 demo). Used by tests and econ_sim.
    /// </summary>
    public static class EconomySimulator
    {
        public struct Prey
        {
            public PreyDefinition Def;
            /// <summary>Seconds to find and reach one (plus one more swing per extra hit needed).</summary>
            public float SecondsToReach;
        }

        public struct Unlock
        {
            public UpgradeDefinition Upgrade;
            public double Seconds;
        }

        public sealed class Result
        {
            public readonly List<Unlock> Unlocks = new List<Unlock>();
            public double Seconds;
            public BigDouble Coins;
            public int Catches;
            public bool AllOwned;
        }

        public static Result Run(IReadOnlyList<Prey> prey, IReadOnlyList<ToolDefinition> tools, IReadOnlyList<UpgradeDefinition> upgrades,
            int bagCapacity, float secondsPerSellTrip, float secondsPerSwing, double maxSeconds = 3600)
        {
            var result = new Result();
            var profile = new EconomyProfile();
            var loadout = new Loadout();
            var scratch = new ModifierStack();
            EconomyRules rules = EconomyRules.Default;
            double t = 0;

            while (t < maxSeconds)
            {
                // Best coins per second among what this loadout can take down.
                double bestRate = 0;
                BagItem bestItem = default;
                double bestTime = 0;
                foreach (Prey p in prey)
                {
                    foreach (ToolDefinition tool in tools)
                    {
                        if (!Owns(loadout, tool, upgrades)) continue;
                        ToolStats stats = StatsFor(loadout, tool, upgrades);
                        if (!TryTakeDown(p.Def, stats, out CarcassQuality quality, out int hits)) continue;
                        bool captured = tool.Mode == ToolMode.Capture;
                        CatchReward r = Rewards.Compute(p.Def.Economy, rules, captured, quality, profile.ResearchOf(p.Def.Id.Value), scratch);
                        double seconds = p.SecondsToReach + Math.Max(tool.Cooldown, secondsPerSwing) * hits;
                        double rate = r.Coins.ToDouble() / seconds;
                        if (rate > bestRate)
                        {
                            bestRate = rate;
                            bestTime = seconds;
                            bestItem = new BagItem { Prey = p.Def.Id.Value, Captured = captured, Quality = quality };
                        }
                    }
                }
                if (bestRate <= 0) break;

                t += bestTime;
                profile.Bag.Add(bestItem);
                result.Catches++;
                if (bestItem.Captured) profile.Research[bestItem.Prey] = profile.ResearchOf(bestItem.Prey) + 1;

                if (profile.Bag.Count >= bagCapacity)
                {
                    t += secondsPerSellTrip;
                    profile.Coins += profile.BagValue(rules, scratch).Coins;
                    profile.Bag.Clear();
                    bool bought;
                    do
                    {
                        bought = false;
                        foreach (UpgradeDefinition u in upgrades)
                        {
                            if (EconomyNet.CanBuy(profile, u) != EconomyRefusal.None) continue;
                            profile.Coins -= u.CostCoins;
                            profile.Upgrades.Add(u.Id.Value);
                            loadout.AddUpgrade(u.Id.Value);
                            result.Unlocks.Add(new Unlock { Upgrade = u, Seconds = t });
                            bought = true;
                        }
                    } while (bought);
                    if (profile.Upgrades.Count == upgrades.Count) { result.AllOwned = true; break; }
                }
            }
            result.Seconds = t;
            result.Coins = profile.Coins;
            return result;
        }

        private static bool Owns(Loadout loadout, ToolDefinition tool, IReadOnlyList<UpgradeDefinition> upgrades)
        {
            if (tool.StartOwned) return true;
            foreach (UpgradeDefinition u in upgrades)
                if (u.Tool == tool && u.Effect == UpgradeEffect.UnlockTool && loadout.HasUpgrade(u)) return true;
            return false;
        }

        private static ToolStats StatsFor(Loadout loadout, ToolDefinition tool, IReadOnlyList<UpgradeDefinition> upgrades)
        {
            var s = new ToolStats
            {
                Archetype = tool.Archetype, Mode = tool.Mode, Tier = tool.Tier, Damage = tool.Damage,
                CaptureStrength = tool.CaptureStrength, HeadshotMultiplier = tool.HeadshotMultiplier,
            };
            foreach (UpgradeDefinition u in upgrades)
            {
                if (u.Tool != tool || !loadout.HasUpgrade(u)) continue;
                if (u.Effect == UpgradeEffect.CaptureStrength) s.CaptureStrength += u.Value;
                else if (u.Effect == UpgradeEffect.DamageMultiplier) s.Damage *= u.Value;
            }
            return s;
        }

        /// <summary>Body hits until it's down (no weak-point hits: a fair, slightly pessimistic hunter).</summary>
        private static bool TryTakeDown(PreyDefinition prey, in ToolStats tool, out CarcassQuality quality, out int hits)
        {
            quality = CarcassQuality.Intact;
            hits = 0;
            float health = prey.MaxHealth;
            PreyStats ps = prey.Stats;
            for (int i = 0; i < 20; i++)
            {
                TakedownResult r = Takedown.Resolve(ps, health, tool, false);
                hits++;
                if (r.Outcome == TakedownOutcome.Rejected) return false;
                if (r.Outcome == TakedownOutcome.Captured || r.Outcome == TakedownOutcome.Killed)
                {
                    quality = r.Quality;
                    return true;
                }
                health = r.HealthAfter;
            }
            return false;
        }
    }
}
