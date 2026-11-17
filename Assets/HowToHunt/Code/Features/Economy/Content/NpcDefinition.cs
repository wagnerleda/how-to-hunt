using System.Collections.Generic;
using HowToHunt.Core;
using UnityEngine;

namespace HowToHunt.Economy
{
    /// <summary>
    /// A camp character (key "npc.trader"): who stands at a stall, what they say, and the mask they wear (the same
    /// painted tribe masks the hunters wear). Quests name the character who gives them.
    /// </summary>
    [CreateAssetMenu(menuName = "How to Hunt/NPC", fileName = "Npc_New")]
    public sealed class NpcDefinition : ContentDefinition
    {
        public override string KeyPrefix => "npc.";

        [Tooltip("What they do at camp, shown under their name (\"Trader\").")]
        public string Role = "Trader";
        [Tooltip("Lines said when you walk up (one at random).")]
        [TextArea] public List<string> Greetings = new List<string>();
        [Tooltip("Said when they have nothing for you right now.")]
        [TextArea] public string Idle = "";
        [Tooltip("The stall they run: its action is offered in the conversation (sell, browse, attachments).")]
        public CampStationKind Station;
        public bool HasStation = true;
        [Tooltip("Their mask, as a cl_mask string (empty: a mask is made from the key).")]
        [TextArea] public string Mask = "";

        public override void Validate(List<string> errors)
        {
            base.Validate(errors);
            if (Greetings.Count == 0) errors.Add("An NPC needs at least one greeting.");
        }
    }
}
