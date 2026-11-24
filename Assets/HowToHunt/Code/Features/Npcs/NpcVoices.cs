using HowToHunt.Core;
using HowToHunt.Economy;
using UnityEngine;

namespace HowToHunt.Npcs
{
    /// <summary>Who sounds like what: a character's own VoiceClip, else the shared synthesized blip (made once).</summary>
    public static class NpcVoices
    {
        private static AudioClip _blip;

        public static AudioClip For(NpcDefinition npc)
        {
            if (npc != null && npc.VoiceClip != null) return npc.VoiceClip;
            return _blip != null ? _blip : _blip = ProceduralSounds.Blip();
        }
    }
}
