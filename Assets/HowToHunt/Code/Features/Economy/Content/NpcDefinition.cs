using System.Collections.Generic;
using HowToHunt.Core;
using UnityEngine;

namespace HowToHunt.Economy
{
    /// <summary>
    /// A camp character (key "npc.trader"): who stands at a stall, what they say, and the mask they wear (the same
    /// painted tribe masks the hunters wear). Quests name the character who gives them.
    /// </summary>
    /// <summary>What a character is doing in the world (NpcBody poses them): behind a stall, or out at camp.</summary>
    public enum NpcActivity : byte { Stall, Campfire, ChopWood, Whittle }

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
        [Tooltip("Stall: stands behind its counter. The others are characters about camp (no stall): sitting at the fire, chopping wood, whittling.")]
        public NpcActivity Activity = NpcActivity.Stall;
        [Tooltip("Their voice while they talk: pitch of the blips (1 = middle; lower = deeper).")]
        [Range(0.5f, 2f)] public float VoicePitch = 1f;
        [Tooltip("Optional: their own voice sample (one syllable). Empty: the shared synthesized blip.")]
        public AudioClip VoiceClip;
        [Tooltip("Short lines they call out when you walk past (no conversation). Bubble markup works: [shout] Hey!")]
        [TextArea] public List<string> Barks = new List<string>();
        [Tooltip("Their mask, as a cl_mask string (empty: a mask is made from the key).")]
        [TextArea] public string Mask = "";

        public override void Validate(List<string> errors)
        {
            base.Validate(errors);
            if (Greetings.Count == 0) errors.Add("An NPC needs at least one greeting.");
            if (HasStation != (Activity == NpcActivity.Stall)) errors.Add("Stall characters have a station; the others (fire, wood, whittling) don't.");
        }
    }
}
