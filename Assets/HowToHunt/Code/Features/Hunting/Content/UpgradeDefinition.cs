using System;
using System.Collections.Generic;
using HowToHunt.Core;
using HowToHunt.Sim.Combat;
using HowToHunt.Sim.Hunting;
using UnityEngine;

namespace HowToHunt.Hunting
{
    public enum UpgradeEffect
    {
        /// <summary>+Value capture strength on the target tool.</summary>
        CaptureStrength,
        /// <summary>×Value damage on the target tool.</summary>
        DamageMultiplier,
        /// <summary>+Value range on the target tool.</summary>
        Range,
        /// <summary>Unlocks the target tool.</summary>
        UnlockTool,
        /// <summary>+Value inventory slots (no tool).</summary>
        InventorySlots,
        /// <summary>A bait, used up when bought: wakes <see cref="UpgradeDefinition.Summons"/> (a boss) in its den. Sold at bait stalls.</summary>
        SummonBoss,
    }

    /// <summary>Something to buy at camp. Effects stack in the order bought; the shop explains them.</summary>
    [CreateAssetMenu(menuName = "How to Hunt/Upgrade", fileName = "Upgrade_")]
    public sealed class UpgradeDefinition : ContentDefinition
    {
        public override string KeyPrefix => "upgrade.";

        public ToolDefinition Tool;
        public UpgradeEffect Effect;
        public float Value;
        public double CostCoins = 50;
        public double CostTrophies;
        [Tooltip("Must own this upgrade first.")]
        public UpgradeDefinition Requires;
        public int SortOrder;
        [Tooltip("Not sold yet: unlocked later in the game (later areas, quests). Hidden from the shop.")]
        public bool Unlisted;
        [Tooltip("Sold at the Gunsmith (guns) instead of the Outfitter.")]
        public bool SoldByGunsmith;
        [Tooltip("Sold by Gunsmith: also at the camp gunsmiths (the Plains). Without it, only the full armory (the movement lab) sells it.")]
        public bool CampGunsmith;
        [Tooltip("SummonBoss: the boss this bait wakes (it must have a summoned spawner in the level).")]
        public PreyDefinition Summons;

        /// <summary>A bait: bought again and again, never owned, sold at bait stalls only.</summary>
        public bool IsBait => Effect == UpgradeEffect.SummonBoss;

        public override void Validate(List<string> errors)
        {
            base.Validate(errors);
            if (Tool == null && Effect != UpgradeEffect.InventorySlots && Effect != UpgradeEffect.SummonBoss) errors.Add("Tool missing.");
            if (Effect == UpgradeEffect.SummonBoss && (Summons == null || !Summons.IsBoss)) errors.Add("A bait must summon a boss.");
            if (CampGunsmith && !SoldByGunsmith) errors.Add("Camp Gunsmith needs Sold By Gunsmith.");
            if (Effect == UpgradeEffect.SummonBoss && SoldByGunsmith) errors.Add("Baits are sold at bait stalls, not the Gunsmith.");
            if (CostCoins < 0 || CostTrophies < 0) errors.Add("Costs must be >= 0.");
            if (Requires == this) errors.Add("An upgrade can't require itself.");
        }
    }
}
