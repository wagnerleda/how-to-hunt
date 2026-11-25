using HowToHunt.Core;
using HowToHunt.Foundation;
using UnityEngine;

namespace HowToHunt.Player
{
    /// <summary>
    /// The cry you hear when your hunter gets hurt (owner's clips in Assets/Audio/SFX/player_damage): one of the
    /// pl_damage_N at random, and once in cl_hurt_rare_odds hits one of the rare_pl_damage_N instead (owner,
    /// 2026-11-25: 1 in 10,000). Played 2D, only for this machine's own hunter.
    /// </summary>
    public static class HurtSounds
    {
        public const string Family = "pl_damage";
        public const string RareFamily = "rare_pl_damage";

        public static readonly FloatCvar Volume = new FloatCvar("cl_hurt_volume", 0.8f, "Volume of your own hurt cry (0 = silent).", CvarFlags.Archive, 0f, 1f);
        public static readonly IntCvar RareOdds = new IntCvar("cl_hurt_rare_odds", 10000, "One hurt cry in this many is a rare one.", CvarFlags.Cheat, 1, 1000000);
        private const float PitchJitter = 0.05f;

        public static void Register(ConsoleSystem console)
        {
            console.Register(Volume);
            console.Register(RareOdds);
        }

        /// <summary>Is this hit the rare one? A roll in [0, odds): 0 wins.</summary>
        public static bool RollRare(int odds) => odds <= 1 || Random.Range(0, odds) == 0;

        public static void Play(float amount)
        {
            if (Volume.Value <= 0f) return;
            AudioClip clip = RollRare(RareOdds.Value) ? Sfx.Pick(RareFamily) : null;
            if (clip == null) clip = Sfx.Pick(Family);
            Sfx.Play2D(clip, Volume.Value, Random.Range(1f - PitchJitter, 1f + PitchJitter));
        }
    }
}
