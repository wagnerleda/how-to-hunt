using HowToHunt.Core;
using UnityEngine;

namespace HowToHunt.Hunting
{
    /// <summary>Synthesized blade sounds (bright and metallic, owner feedback), made once on first use.</summary>
    public static class KnifeSounds
    {
        private static AudioClip _swish, _shing, _hit;

        public static AudioClip Swish => _swish != null ? _swish : _swish = ProceduralSounds.KnifeSwish();
        public static AudioClip Shing => _shing != null ? _shing : _shing = ProceduralSounds.KnifeShing();
        public static AudioClip Hit => _hit != null ? _hit : _hit = ProceduralSounds.BladeHit();
    }
}
