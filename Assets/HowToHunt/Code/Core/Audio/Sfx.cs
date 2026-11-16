using System.Collections.Generic;
using UnityEngine;

namespace HowToHunt.Core
{
    /// <summary>
    /// One-shot sounds from Resources/Audio (Kenney CC0 packs). Pooled 2D and 3D sources; clips cached by family.
    /// A family ("Kenney/footstep_grass") picks a random variant among footstep_grass_000.._009, footstep_grass1..9,
    /// or the single clip of that exact name.
    /// </summary>
    public static class Sfx
    {
        private const int Pool2D = 8;
        private const int Pool3D = 24;
        private static readonly Dictionary<string, AudioClip[]> Families = new Dictionary<string, AudioClip[]>();
        private static AudioSource[] _sources2D;
        private static AudioSource[] _sources3D;
        private static int _next2D, _next3D;

        private static void Ensure()
        {
            if (_sources2D != null && _sources2D[0] != null) return;
            var root = new GameObject("[Sfx]");
            Object.DontDestroyOnLoad(root);
            _sources2D = new AudioSource[Pool2D];
            for (int i = 0; i < Pool2D; i++) _sources2D[i] = MakeSource(root.transform, $"Sfx2D {i}", 0f);
            _sources3D = new AudioSource[Pool3D];
            for (int i = 0; i < Pool3D; i++) _sources3D[i] = MakeSource(root.transform, $"Sfx3D {i}", 1f);
        }

        private static AudioSource MakeSource(Transform parent, string name, float spatial)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            AudioSource s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = spatial;
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.minDistance = 2f;
            s.maxDistance = 70f;
            s.dopplerLevel = 0f;
            return s;
        }

        public static AudioClip Pick(string family)
        {
            if (!Families.TryGetValue(family, out AudioClip[] clips))
            {
                var list = new List<AudioClip>();
                AudioClip single = Resources.Load<AudioClip>("Audio/" + family);
                if (single != null) list.Add(single);
                for (int i = 0; i < 10; i++)
                {
                    AudioClip a = Resources.Load<AudioClip>($"Audio/{family}_{i:000}");
                    if (a != null) list.Add(a);
                    AudioClip b = Resources.Load<AudioClip>($"Audio/{family}{i}");
                    if (b != null) list.Add(b);
                }
                clips = list.ToArray();
                Families[family] = clips;
                if (clips.Length == 0) Debug.LogWarning($"[Sfx] No clips for '{family}'.");
            }
            return clips.Length == 0 ? null : clips[Random.Range(0, clips.Length)];
        }

        public static void Play2D(string family, float volume = 1f, float pitch = 1f) => Play2D(Pick(family), volume, pitch);

        /// <summary>Each 2D sound gets its own pooled source, so overlapping sounds keep their own pitch.</summary>
        public static void Play2D(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;
            Ensure();
            AudioSource s = _sources2D[_next2D];
            _next2D = (_next2D + 1) % Pool2D;
            Start(s, clip, volume, pitch);
        }

        public static void PlayAt(string family, Vector3 position, float volume = 1f, float pitch = 1f) => PlayAt(Pick(family), position, volume, pitch);

        public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;
            Ensure();
            AudioSource s = _sources3D[_next3D];
            _next3D = (_next3D + 1) % Pool3D;
            s.transform.position = position;
            Start(s, clip, volume, pitch);
        }

        private static void Start(AudioSource s, AudioClip clip, float volume, float pitch)
        {
            s.pitch = pitch;
            s.volume = volume;
            s.clip = clip;
            s.Play();
        }
    }
}
