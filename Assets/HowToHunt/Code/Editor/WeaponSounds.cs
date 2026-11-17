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

        /// <summary>Folders named differently from the tool key (the owner's drop-ins).</summary>
        private static readonly System.Collections.Generic.Dictionary<string, string> Folders = new System.Collections.Generic.Dictionary<string, string>
        {
            { "tool.glock", "Glock" }, { "tool.uzi", "Uzi" }, { "tool.sawedoff", "Sawed-Off" }, { "tool.handcannon", "Handcannon" },
            { "tool.scout", "ScoutSniper" }, { "tool.heavyrifle", "HeavyAssaultRifle" }, { "tool.intervention", "Intervention" },
            { "tool.grenadelauncher", "GrenadeLauncher" }, { "tool.gunlance", "scifigun" },
        };

        public static string FolderFor(ToolDefinition tool) => Root + (Folders.TryGetValue(tool.Key, out string f) ? f : tool.Key.Substring(tool.Key.IndexOf('.') + 1));

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
            tool.FireSounds = fire.ToArray();
            tool.ReloadSound = reload;
            tool.DryFireSound = dry;
            tool.EquipSound = equip;
            tool.InspectSound = inspect;
            return true;
        }
    }
}
