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

        public override void Validate(List<string> errors)
        {
            base.Validate(errors);
            if (Tool == null && Effect != UpgradeEffect.InventorySlots) errors.Add("Tool missing.");
            if (CostCoins < 0 || CostTrophies < 0) errors.Add("Costs must be >= 0.");
            if (Requires == this) errors.Add("An upgrade can't require itself.");
        }
    }
}
