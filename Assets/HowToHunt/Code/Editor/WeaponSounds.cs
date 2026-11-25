using System;
using System.Collections.Generic;
using System.IO;
using HowToHunt.Hunting;
using UnityEditor;
using UnityEngine;

namespace HowToHunt.Editor
{
    /// <summary>
    /// Links a weapon's sounds by convention, so new audio only needs dropping in the right folder:
    /// <c>Assets/Audio/SFX/Weapons/&lt;tool name&gt;/</c> (the tool key without "tool.", e.g. revolver, scout),
    /// files named by what they are: <c>fire*</c> (several = picked at random), <c>reload*</c>, <c>dry*</c> (empty
    /// click), <c>equip*</c>, <c>inspect*</c>. The model, in the same-named folder under Art/Weapons, is fitted by hand once (orientation, grip,
    /// sights), then kept as the tool's viewmodel. See docs/ASSETS.md, "Weapon drop-in".
    /// </summary>
    public static class WeaponSounds
    {
        public const string Root = SoundLibraryBuilder.WeaponsRoot;

        /// <summary>Folders named differently from the tool key (the owner's drop-ins).</summary>
        private static readonly System.Collections.Generic.Dictionary<string, string> Folders = new System.Collections.Generic.Dictionary<string, string>
        {
            { "tool.glock", "Glock" }, { "tool.uzi", "Uzi" }, { "tool.sawedoff", "Sawed-Off" }, { "tool.handcannon", "Handcannon" }, { "tool.revolver", "old revolver" },
            { "tool.scout", "ScoutSniper" }, { "tool.heavyrifle", "HeavyAssaultRifle" }, { "tool.intervention", "Intervention" },
            { "tool.grenadelauncher", "GrenadeLauncher" }, { "tool.gunlance", "scifigun" },
        };

        public static string FolderFor(ToolDefinition tool) => Root + (Folders.TryGetValue(tool.Key, out string f) ? f : tool.Key.Substring(tool.Key.IndexOf('.') + 1));

        private const string TrimmedRoot = SoundLibraryBuilder.WeaponsRoot + "_Trimmed/";
        private const float SilenceLevel = 0.03f;   // amplitude below which the start counts as silence
        private const float PreRoll = 0.004f;       // seconds kept before the first sound (no click)

        /// <summary>
        /// A copy of the clip without its leading silence (16-bit WAV under Assets/Audio/SFX/Weapons/_Trimmed), or the clip itself if
        /// it already starts at once. Generated: rebuilt with the content.
        /// </summary>
        private static AudioClip Trimmed(AudioClip clip, string outDir)
        {
            if (clip == null || clip.channels <= 0) return clip;
            var data = new float[clip.samples * clip.channels];
            if (!clip.GetData(data, 0)) return clip;
            int first = 0;
            while (first < data.Length && Mathf.Abs(data[first]) < SilenceLevel) first++;
            int frame = Mathf.Max(0, first / clip.channels - Mathf.RoundToInt(PreRoll * clip.frequency));
            if (frame < clip.frequency * 0.01f) return clip; // under 10 ms: fine as it is
            GeneratedAssets.EnsureFolder(outDir);
            string path = outDir + "/" + Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(clip)) + "_trimmed.wav";
            int start = frame * clip.channels;
            WriteWav(path, data, start, data.Length - start, clip.channels, clip.frequency);
            AssetDatabase.ImportAsset(path);
            Debug.Log($"[HowToHunt] {clip.name}: trimmed {frame / (float)clip.frequency:0.000} s of leading silence");
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        private static void WriteWav(string path, float[] data, int start, int count, int channels, int rate)
        {
            using var stream = new FileStream(path, FileMode.Create);
            using var w = new BinaryWriter(stream);
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + count * 2);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);
            w.Write((short)1);
            w.Write((short)channels);
            w.Write(rate);
            w.Write(rate * channels * 2);
            w.Write((short)(channels * 2));
            w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            w.Write(count * 2);
            for (int i = 0; i < count; i++) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(data[start + i] * 32767f), -32768, 32767));
        }

        /// <summary>True if the tool's folder exists and its sounds were (re)linked.</summary>
        public static bool Link(ToolDefinition tool)
        {
            string dir = FolderFor(tool);
            if (!AssetDatabase.IsValidFolder(dir)) return false;
            var fire = new List<AudioClip>();
            AudioClip reload = null, dry = null, equip = null, inspect = null;
            var paths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { dir })) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            paths.Sort(StringComparer.Ordinal); // fire_01, fire_02... in order
            foreach (string path in paths)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) continue;
                string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                // Named by what it is anywhere in the name (downloads keep their source names): anything else is a shot.
                if (name.Contains("reload")) reload ??= clip;
                else if (name.Contains("dry") || name.Contains("empty")) dry ??= clip;
                else if (name.Contains("equip") || name.Contains("draw")) equip ??= clip;
                else if (name.Contains("inspect")) inspect ??= clip;
                else fire.Add(clip);
            }
            // Downloads often start with a little silence: the shot must be heard the moment the gun fires.
            string outDir = TrimmedRoot + tool.Key.Substring(tool.Key.IndexOf('.') + 1);
            for (int i = 0; i < fire.Count; i++) fire[i] = Trimmed(fire[i], outDir);
            tool.FireSounds = fire.ToArray();
            tool.ReloadSound = reload != null ? Trimmed(reload, outDir) : null;
            tool.DryFireSound = dry != null ? Trimmed(dry, outDir) : null;
            tool.EquipSound = equip != null ? Trimmed(equip, outDir) : null;
            tool.InspectSound = inspect != null ? Trimmed(inspect, outDir) : null;
            return true;
        }
    }
}
