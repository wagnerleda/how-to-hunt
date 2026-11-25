using System;
using System.Collections.Generic;
using System.IO;
using HowToHunt.Core;
using UnityEditor;
using UnityEngine;

namespace HowToHunt.Editor
{
    /// <summary>
    /// All the game's audio lives in Assets/Audio (owner, 2026-11-25): Music, and SFX by kind (UI, Foley, Footsteps,
    /// Impacts, player_damage, Weapons/&lt;gun folder&gt; with the generated _Trimmed copies). This writes the
    /// <see cref="SoundLibrary"/> that Sfx finds clips in (every SFX clip but the guns', keyed by file name) and runs
    /// again by itself whenever a file under Assets/Audio/SFX is added, moved or deleted. File names must be unique.
    /// </summary>
    public sealed class SoundLibraryBuilder : AssetPostprocessor
    {
        public const string AudioRoot = "Assets/Audio";
        public const string SfxRoot = AudioRoot + "/SFX";
        public const string WeaponsRoot = SfxRoot + "/Weapons/";
        public const string LibraryPath = GeneratedAssets.ResourcesDir + "/" + SoundLibrary.ResourcePath + ".asset";

        private static bool _queued;

        [MenuItem("How to Hunt/Rebuild Sound Library")]
        public static void Build()
        {
            var entries = new List<SoundLibrary.Entry>();
            var seen = new Dictionary<string, string>();
            var paths = new List<string>();
            if (AssetDatabase.IsValidFolder(SfxRoot))
                foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { SfxRoot })) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            paths.Sort(StringComparer.Ordinal);
            foreach (string path in paths)
            {
                if (path.StartsWith(WeaponsRoot, StringComparison.Ordinal)) continue;
                string key = Path.GetFileNameWithoutExtension(path);
                if (seen.TryGetValue(key, out string other))
                {
                    Debug.LogWarning($"[HowToHunt] Two sounds named '{key}': {other} and {path}. Only the first is played.");
                    continue;
                }
                seen[key] = path;
                entries.Add(new SoundLibrary.Entry { Key = key, Clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path) });
            }

            var lib = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<SoundLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }
            lib.Entries = entries.ToArray();
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Short sounds heard at once (the damage cries): decompressed on load, played 2D.</summary>
        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(SfxRoot + "/player_damage/", StringComparison.Ordinal)) return;
            var importer = (AudioImporter)assetImporter;
            AudioImporterSampleSettings s = importer.defaultSampleSettings;
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.7f;
            s.preloadAudioData = true;
            importer.defaultSampleSettings = s;
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (_queued || !(Touches(imported) || Touches(deleted) || Touches(moved) || Touches(movedFrom))) return;
            _queued = true;
            EditorApplication.delayCall += () => { _queued = false; Build(); };
        }

        private static bool Touches(string[] paths)
        {
            foreach (string p in paths)
                if (p.StartsWith(SfxRoot + "/", StringComparison.Ordinal) && !p.EndsWith(".meta", StringComparison.Ordinal) && !p.EndsWith(".txt", StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
