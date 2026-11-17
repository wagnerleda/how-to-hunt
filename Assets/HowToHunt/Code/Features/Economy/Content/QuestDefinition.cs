using System.Collections.Generic;
using HowToHunt.Core;
using HowToHunt.Hunting;
using UnityEngine;

namespace HowToHunt.Economy
{
    /// <summary>What a quest asks for. The host counts it from what the player does.</summary>
    public enum QuestObjective : byte
    {
        Capture,     // catch prey alive (net)
        Kill,        // take prey down dead
        TakeDown,    // either
        Sell,        // sell this many catches
        EarnCoins,   // earn this many coins from sales
        BuyUpgrade,  // buy anything at the outfitter
        BuyAttachment,
    }

    /// <summary>
    /// A quest an NPC gives (key "quest.fresh_stock"): offer, objective, reward, and what it unlocks in the world
    /// (a flag such as "east_clearing" opens the barricade to the next zone for the whole party). Quests chain through
    /// <see cref="Requires"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "How to Hunt/Quest", fileName = "Quest_New")]
    public sealed class QuestDefinition : ContentDefinition
    {
        public override string KeyPrefix => "quest.";

        public NpcDefinition Giver;
        [Tooltip("Done before this one can be offered.")]
        public QuestDefinition Requires;
        public QuestObjective Objective;
        [Tooltip("Capture / Kill / TakeDown: this species only (empty: any).")]
        public PreyDefinition Prey;
        public int Count = 1;
        public double RewardCoins;
        [Tooltip("A world flag set for the party when it's turned in (\"east_clearing\").")]
        public string Unlocks = "";
        [Tooltip("What the giver says when offering it.")]
        [TextArea] public string Offer = "";
        [Tooltip("The short task line on the HUD and in the conversation (\"Sell 3 catches to Tomas\").")]
        public string Task = "";
        [Tooltip("Said when you come back before it's done.")]
        [TextArea] public string Waiting = "";
        [Tooltip("Said when you turn it in.")]
        [TextArea] public string Done = "";
        public int Order;

        public override void Validate(List<string> errors)
        {
            base.Validate(errors);
            if (Giver == null) errors.Add("A quest needs a giver.");
            if (Count <= 0) errors.Add("Count must be > 0.");
            if (string.IsNullOrEmpty(Task)) errors.Add("A quest needs a task line.");
        }

        /// <summary>Does this takedown count?</summary>
        public bool Counts(uint prey, bool captured)
        {
            if (Prey != null && Prey.Id.Value != prey) return false;
            return Objective == QuestObjective.TakeDown || (Objective == QuestObjective.Capture && captured) || (Objective == QuestObjective.Kill && !captured);
        }
    }
}
