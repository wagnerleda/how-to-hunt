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
    /// <c>Assets/HowToHunt/Art/Weapons/&lt;tool name&gt;/</c> (the tool key without "tool.", e.g. revolver, scout),
    /// files named by what they are: <c>fire*</c> (several = picked at random), <c>reload*</c>, <c>dry*</c> (empty
    /// click), <c>equip*</c>, <c>inspect*</c>. The model in the same folder is fitted by hand once (orientation, grip,
    /// sights), then kept as the tool's viewmodel. See docs/ASSETS.md, "Weapon drop-in".
    /// </summary>
    public static class WeaponSounds
    {
        public const string Root = GeneratedAssets.Root + "/Art/Weapons/";

        public static string FolderFor(ToolDefinition tool) => Root + tool.Key.Substring(tool.Key.IndexOf('.') + 1);

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
                if (name.StartsWith("fire") || name.StartsWith("shot")) fire.Add(clip);
                else if (name.StartsWith("reload")) reload ??= clip;
                else if (name.StartsWith("dry")) dry ??= clip;
                else if (name.StartsWith("equip") || name.StartsWith("draw")) equip ??= clip;
                else if (name.StartsWith("inspect")) inspect ??= clip;
                else Debug.LogWarning($"[HowToHunt] {path}: unknown sound name (use fire*, reload*, dry*, equip*, inspect*).");
            }
            tool.FireSounds = fire.ToArray();
            tool.ReloadSound = reload;
            tool.DryFireSound = dry;
            tool.EquipSound = equip;
            tool.InspectSound = inspect;
            return true;
        }
    }
}
