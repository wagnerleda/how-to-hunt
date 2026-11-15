#nullable enable
using System;
using HowToHunt.Sim.Economy;

namespace HowToHunt.Sim.Hunting
{
    /// <summary>How a prey may be taken. Default is Either: the player always chooses.</summary>
    [Flags]
    public enum TakedownMethods : byte
    {
        Capture = 1,
        Kill = 2,
        Either = Capture | Kill,
    }

    public enum ToolMode : byte { Capture, Kill }

    public enum ToolArchetype : byte { Net, Blade, Firearm, Bow, Trap }

    public enum TakedownOutcome : byte
    {
        Missed,
        Damaged,
        Killed,
        Captured,
        /// <summary>The tool can't do that to this prey; see <see cref="RejectReason"/>.</summary>
        Rejected,
    }

    public enum RejectReason : byte
    {
        None,
        /// <summary>This prey can't be captured (or killed), by design.</summary>
        MethodNotAllowed,
        /// <summary>Capture tool not strong enough to hold it ("a net cannot hold a bear").</summary>
        TooWeakToHold,
        /// <summary>Kill tool too weak to hurt it ("that pellet gun barely scratches a boar").</summary>
        TooWeakToHurt,
    }

    public enum CarcassQuality : byte { Intact, Ruined }

    /// <summary>Prey numbers from content (PreyDefinition).</summary>
    public struct PreyStats
    {
        public TakedownMethods Allowed;
        public float MaxHealth;
        /// <summary>Capture strength needed to hold it.</summary>
        public float CaptureDifficulty;
        /// <summary>Kill tools below this tier only scratch it.</summary>
        public int MinKillTier;
        /// <summary>A single hit dealing more than MaxHealth × this ruins the carcass (overkill).</summary>
        public float OverkillRatio;
    }

    /// <summary>Tool numbers from content (ToolDefinition) after upgrades.</summary>
    public struct ToolStats
    {
        public ToolArchetype Archetype;
        public ToolMode Mode;
        public int Tier;
        public float Damage;
        public float CaptureStrength;
        public float HeadshotMultiplier;
    }

    public struct TakedownResult
    {
        public TakedownOutcome Outcome;
        public RejectReason Reason;
        public float Damage;
        public float HealthAfter;
        public CarcassQuality Quality;
    }

    /// <summary>
    /// The one place that decides what a tool does to a prey. Host-authoritative; pure and deterministic.
    /// A wrong or too-weak tool always produces a reason (never silent failure).
    /// </summary>
    public static class Takedown
    {
        public static TakedownResult Resolve(in PreyStats prey, float currentHealth, in ToolStats tool, bool weakPoint)
        {
            var r = new TakedownResult { HealthAfter = currentHealth };
            if (tool.Mode == ToolMode.Capture)
            {
                if ((prey.Allowed & TakedownMethods.Capture) == 0) return Reject(r, RejectReason.MethodNotAllowed);
                if (tool.CaptureStrength < prey.CaptureDifficulty) return Reject(r, RejectReason.TooWeakToHold);
                r.Outcome = TakedownOutcome.Captured;
                r.Quality = CarcassQuality.Intact;
                return r;
            }

            if ((prey.Allowed & TakedownMethods.Kill) == 0) return Reject(r, RejectReason.MethodNotAllowed);
            if (tool.Tier < prey.MinKillTier) return Reject(r, RejectReason.TooWeakToHurt);
            float damage = tool.Damage * (weakPoint ? Math.Max(1f, tool.HeadshotMultiplier) : 1f);
            r.Damage = damage;
            r.HealthAfter = Math.Max(0f, currentHealth - damage);
            if (r.HealthAfter > 0f)
            {
                r.Outcome = TakedownOutcome.Damaged;
                return r;
            }
            r.Outcome = TakedownOutcome.Killed;
            r.Quality = prey.OverkillRatio > 0f && damage > prey.MaxHealth * prey.OverkillRatio ? CarcassQuality.Ruined : CarcassQuality.Intact;
            return r;
        }

        private static TakedownResult Reject(TakedownResult r, RejectReason reason)
        {
            r.Outcome = TakedownOutcome.Rejected;
            r.Reason = reason;
            return r;
        }
    }

    /// <summary>Economy numbers for a prey (PreyDefinition) and the shared reward rules (docs/DESIGN.md).</summary>
    public struct PreyEconomy
    {
        public double BaseValue;
        public double TrophiesOnKill;
    }

    public struct EconomyRules
    {
        public double CaptureMultiplier;      // 1.5
        public double KillMultiplier;         // 1.0
        public double RuinedMultiplier;       // 0.3
        public double MasteryBonusPerLevel;   // 0.1
        public int[] MasteryThresholds;       // captures needed for level 1, 2, 3...

        public static EconomyRules Default => new EconomyRules
        {
            CaptureMultiplier = 1.5,
            KillMultiplier = 1.0,
            RuinedMultiplier = 0.3,
            MasteryBonusPerLevel = 0.1,
            MasteryThresholds = new[] { 3, 10, 25, 60 },
        };

        public int MasteryLevel(int research)
        {
            int level = 0;
            if (MasteryThresholds == null) return 0;
            while (level < MasteryThresholds.Length && research >= MasteryThresholds[level]) level++;
            return level;
        }
    }

    public struct CatchReward
    {
        public BigDouble Coins;
        public double Trophies;
        public int Research;
    }

    public static class Rewards
    {
        /// <summary>
        /// Sell value of one catch. Kill: 1.0× + trophies; capture: 1.5× + 1 research (mastery raises future value);
        /// ruined (overkill) carcasses lose most of their value. All terms land in <paramref name="explain"/>.
        /// </summary>
        public static CatchReward Compute(in PreyEconomy prey, in EconomyRules rules, bool captured, CarcassQuality quality,
            int researchSoFar, ModifierStack explain)
        {
            explain.Clear();
            explain.Multiply(captured ? "captured alive" : "killed", captured ? rules.CaptureMultiplier : rules.KillMultiplier);
            if (!captured && quality == CarcassQuality.Ruined) explain.Multiply("ruined (overkill)", rules.RuinedMultiplier);
            int mastery = rules.MasteryLevel(researchSoFar);
            if (mastery > 0) explain.Multiply($"mastery {mastery}", 1.0 + mastery * rules.MasteryBonusPerLevel);
            return new CatchReward
            {
                Coins = explain.Evaluate(prey.BaseValue).Floor(),
                Trophies = captured ? 0 : prey.TrophiesOnKill,
                Research = captured ? 1 : 0,
            };
        }
    }
}
