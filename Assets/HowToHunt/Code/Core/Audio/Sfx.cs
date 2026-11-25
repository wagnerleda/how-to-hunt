using System.Collections.Generic;
using UnityEngine;

namespace HowToHunt.Core
{
    /// <summary>
    /// One-shot sounds from Assets/Audio/SFX, found through the generated <see cref="SoundLibrary"/> by file name.
    /// Pooled 2D and 3D sources; clips cached by family. A family ("footstep_grass") picks a random variant among
    /// footstep_grass_000.._009, footstep_grass_0..9, footstep_grass0..9, or the single clip of that exact name.
    /// A folder in front ("UI/tick_001") is ignored: names are unique across the SFX folder.
    /// </summary>
    public static class Sfx
    {
        private const int Pool2D = 8;
        private const int Pool3D = 24;
        private const int MaxVariants = 10;
        private static readonly Dictionary<string, AudioClip[]> Families = new Dictionary<string, AudioClip[]>();
        private static Dictionary<string, AudioClip> _clips;
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

        /// <summary>The clip with exactly this file name (no extension), or null.</summary>
        public static AudioClip Clip(string name)
        {
            if (_clips == null)
            {
                _clips = new Dictionary<string, AudioClip>();
                SoundLibrary lib = Resources.Load<SoundLibrary>(SoundLibrary.ResourcePath);
                if (lib == null) Debug.LogWarning("[Sfx] No SoundLibrary in Resources (rebuild generated assets).");
                else
                    foreach (SoundLibrary.Entry e in lib.Entries)
                        if (e.Clip != null && !string.IsNullOrEmpty(e.Key)) _clips[e.Key] = e.Clip;
            }
            int slash = name.LastIndexOf('/');
            if (slash >= 0) name = name.Substring(slash + 1);
            return _clips.TryGetValue(name, out AudioClip clip) ? clip : null;
        }

        /// <summary>Every variant of a family (see the class summary); empty if none. Cached.</summary>
        public static AudioClip[] Family(string family)
        {
            if (Families.TryGetValue(family, out AudioClip[] clips)) return clips;
            var list = new List<AudioClip>();
            AudioClip single = Clip(family);
            if (single != null) list.Add(single);
            for (int i = 0; i < MaxVariants; i++)
            {
                Add(list, Clip($"{family}_{i:000}"));
                Add(list, Clip($"{family}_{i}"));
                Add(list, Clip($"{family}{i}"));
            }
            clips = list.ToArray();
            Families[family] = clips;
            if (clips.Length == 0) Debug.LogWarning($"[Sfx] No clips for '{family}'.");
            return clips;
        }

        private static void Add(List<AudioClip> list, AudioClip clip)
        {
            if (clip != null && !list.Contains(clip)) list.Add(clip);
        }

        public static AudioClip Pick(string family)
        {
            AudioClip[] clips = Family(family);
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
