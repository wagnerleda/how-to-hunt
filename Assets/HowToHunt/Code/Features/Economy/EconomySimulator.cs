using System;
using System.Collections.Generic;
using HowToHunt.Hunting;
using HowToHunt.Sim.Economy;
using HowToHunt.Sim.Hunting;

namespace HowToHunt.Economy
{
    /// <summary>
    /// Headless pacing check: a hunter who always takes the best-paying catch their tools allow, sells every few catches and buys every upgrade they can afford. Reports when each upgrade is bought, so tuning changes can be
    /// judged against the target (all Plains upgrades within a few minutes for the M3 demo). Used by tests and econ_sim.
    /// </summary>
    public static class EconomySimulator
    {
        public struct Prey
        {
            public PreyDefinition Def;
            /// <summary>Seconds to find and reach one (plus one more swing per extra hit needed).</summary>
            public float SecondsToReach;
            /// <summary>How many usually sit close enough together for one sweep (butterflies drift in groups). Default 1.</summary>
            public float Group;
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
            int catchesPerTrip, float secondsPerSellTrip, float secondsPerSwing, double maxSeconds = 3600, int freeSlots = 0)
        {
            // freeSlots > 0: trips end when the bag is full by real slot and stack rules (tools take the other slots).
            int listed = 0;
            foreach (UpgradeDefinition u in upgrades) if (!u.Unlisted && !u.SoldByGunsmith && !u.IsBait) listed++;
            var result = new Result();
            var profile = new EconomyProfile();
            var loadout = new Loadout();
            var scratch = new ModifierStack();
            EconomyRules rules = EconomyRules.Default;
            double t = 0;
            var carried = new List<ItemStack>();

            while (t < maxSeconds)
            {
                // Best coins per second among what this loadout can take down.
                double bestRate = 0;
                ItemStack bestItem = default;
                double bestTime = 0;
                int bestCount = 1;
                foreach (Prey p in prey)
                {
                    foreach (ToolDefinition tool in tools)
                    {
                        if (!Owns(loadout, tool, upgrades)) continue;
                        ToolStats stats = StatsFor(loadout, tool, upgrades);
                        if (!TryTakeDown(p.Def, stats, out CarcassQuality quality, out int hits)) continue;
                        bool captured = tool.Mode == ToolMode.Capture;
                        CatchReward r = Rewards.Compute(p.Def.Economy, rules, captured, quality, profile.ResearchOf(p.Def.Id.Value), scratch);
                        // Kills drop a carcass that has to be fetched (owner rule): a few seconds more than a catch.
                        double seconds = p.SecondsToReach + Math.Max(tool.Cooldown, secondsPerSwing) * hits + (captured ? 0.0 : 3.0);
                        // A net sweep scoops a whole group (up to the net's MaxTargets).
                        int count = captured ? Math.Max(1, Math.Min(tool.MaxTargets, (int)Math.Round(Math.Max(1f, p.Group)))) : 1;
                        double rate = r.Coins.ToDouble() * count / seconds;
                        if (rate > bestRate)
                        {
                            bestRate = rate;
                            bestTime = seconds;
                            bestCount = count;
                            bestItem = ItemStack.Prey(p.Def.Id.Value, captured, quality);
                        }
                    }
                }
                if (bestRate <= 0) break;

                t += bestTime;
                for (int k = 0; k < bestCount; k++)
                {
                    if (freeSlots > 0 && !Fits(carried, bestItem, freeSlots)) break; // the rest of the sweep is left behind
                    carried.Add(bestItem);
                    result.Catches++;
                    if (bestItem.Captured) profile.Research[bestItem.Content] = profile.ResearchOf(bestItem.Content) + 1;
                }

                bool full = freeSlots > 0 ? !Fits(carried, bestItem, freeSlots) : carried.Count >= catchesPerTrip;
                if (full)
                {
                    t += secondsPerSellTrip;
                    foreach (ItemStack c in carried) profile.Coins += profile.ValueOf(c, rules, scratch).Coins;
                    carried.Clear();
                    bool bought;
                    do
                    {
                        bought = false;
                        foreach (UpgradeDefinition u in upgrades)
                        {
                            if (u.SoldByGunsmith || u.IsBait || EconomyNet.CanBuy(profile, u) != EconomyRefusal.None) continue; // baits are used up, not owned
                            profile.Coins -= u.CostCoins;
                            profile.Upgrades.Add(u.Id.Value);
                            loadout.AddUpgrade(u.Id.Value);
                            result.Unlocks.Add(new Unlock { Upgrade = u, Seconds = t });
                            bought = true;
                        }
                    } while (bought);
                    if (profile.Upgrades.Count >= listed) { result.AllOwned = true; break; }
                }
            }
            result.Seconds = t;
            result.Coins = profile.Coins;
            return result;
        }

        /// <summary>Would one more of this item fit in the bag (stacks fill before a new slot is used)?</summary>
        private static bool Fits(List<ItemStack> carried, in ItemStack item, int freeSlots)
        {
            int slots = 0, same = 0;
            // Group by content and catch/kill (they stack separately), counting slots per group.
            for (int i = 0; i < carried.Count; i++)
            {
                bool seen = false;
                for (int j = 0; j < i; j++)
                    if (carried[j].Content == carried[i].Content && carried[j].Captured == carried[i].Captured) { seen = true; break; }
                if (seen) continue;
                int n = 0;
                for (int j = i; j < carried.Count; j++)
                    if (carried[j].Content == carried[i].Content && carried[j].Captured == carried[i].Captured) n++;
                int stack = StackOf(carried[i].Content);
                slots += (n + stack - 1) / stack;
                if (carried[i].Content == item.Content && carried[i].Captured == item.Captured) same = n;
            }
            int itemStack = StackOf(item.Content);
            return same % itemStack != 0 || slots < freeSlots;
        }

        private static int StackOf(uint content)
        {
            PreyDefinition d = HowToHunt.Core.ContentRegistry.Get<PreyDefinition>(new HowToHunt.Foundation.ContentId(content));
            return d != null ? Math.Max(1, d.StackSize) : 1;
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
