using HowToHunt.Core;
using UnityEngine;

namespace HowToHunt.Hunting
{
    /// <summary>Hit sounds per prey voice (synthesized once, cached), played where the hit lands.</summary>
    public static class PreySounds
    {
        private static AudioClip _squeak, _flutter, _grunt, _roar;

        public static AudioClip For(PreyVoice voice)
        {
            switch (voice)
            {
                case PreyVoice.Flutter: return _flutter != null ? _flutter : _flutter = ProceduralSounds.Flutter();
                case PreyVoice.Grunt: return _grunt != null ? _grunt : _grunt = ProceduralSounds.Grunt();
                case PreyVoice.Roar: return _roar != null ? _roar : _roar = ProceduralSounds.Roar();
                default: return _squeak != null ? _squeak : _squeak = ProceduralSounds.Squeak();
            }
        }

        /// <summary>An aggressive animal winds up a strike: its cry, louder and lower than a hit, heard from further.</summary>
        public static void PlayWindup(PreyDefinition prey, Vector3 at, bool heavy = false)
        {
            if (prey == null) return;
            // A slam's roar is deeper: the cue that this one hits all around.
            Sfx.PlayAt(For(prey.Voice), at, prey.IsBoss ? 1f : 0.8f, Random.Range(0.8f, 0.9f) * (prey.IsBoss ? 0.8f : 1f) * (heavy ? 0.75f : 1f));
        }

        /// <summary>A ground slam lands: a heavy thud.</summary>
        public static void PlaySlam(Vector3 at)
        {
            Sfx.PlayAt("impactPunch_heavy", at, 1f, Random.Range(0.5f, 0.6f));
            Sfx.PlayAt("impactSoft_heavy", at, 1f, Random.Range(0.6f, 0.7f));
        }

        /// <summary>A hit (killed = lower and louder, the last cry).</summary>
        public static void PlayHit(PreyDefinition prey, Vector3 at, bool killed)
        {
            if (prey == null) return;
            float pitch = Random.Range(0.92f, 1.1f) * (killed ? 0.85f : 1f);
            Sfx.PlayAt(For(prey.Voice), at, killed ? 0.75f : 0.55f, pitch);
        }
    }
}
