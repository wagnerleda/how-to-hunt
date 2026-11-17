using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HowToHunt.Editor
{
    /// <summary>
    /// Art direction A ("sunny diorama"): flat colors, matte surfaces. One material per palette entry so
    /// the SRP batcher keeps draw calls cheap. Biome palettes extend this list.
    /// </summary>
    public static class Palette
    {
        private static readonly Dictionary<string, (Color color, float smoothness)> Entries = new Dictionary<string, (Color, float)>
        {
            { "Grass", (Hex("86B85A"), 0.05f) },
            { "Sand", (Hex("E8D29A"), 0.05f) },
            { "Rock", (Hex("9AA3A8"), 0.1f) },
            { "Wood", (Hex("B07A4A"), 0.1f) },
            { "Wall", (Hex("D9C9A8"), 0.05f) },
            { "Crate", (Hex("C98E4E"), 0.1f) },
            { "Bhop", (Hex("F2C14E"), 0.15f) },
            { "Surf", (Hex("4FA3D9"), 0.25f) },
            { "Ramp", (Hex("E07A5F"), 0.1f) },
            { "PlayerSuit", (Hex("F28C28"), 0.2f) },
            { "Visor", (Hex("27323A"), 0.6f) },
            { "Skin", (Hex("B97C58"), 0.15f) },   // the players: bare skin (body and first-person hands)
            { "Leaf", (Hex("3F8A2E"), 0.35f) },
            { "Trim", (Hex("3D5A6C"), 0.1f) },
            { "Hazard", (Hex("C8553D"), 0.3f) },
            { "Pad", (Hex("6C63FF"), 0.4f) },
            { "Stripe", (Hex("CDB98A"), 0.05f) },
            { "Gunmetal", (Hex("33383D"), 0.45f) },
        };

        public static Material Get(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Material>($"{GeneratedAssets.MaterialsDir}/M_{name}.mat");
        }

        public static void EnsureMaterials()
        {
            GeneratedAssets.EnsureFolder(GeneratedAssets.MaterialsDir);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            foreach (KeyValuePair<string, (Color color, float smoothness)> e in Entries)
            {
                string path = $"{GeneratedAssets.MaterialsDir}/M_{e.Key}.mat";
                Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(lit);
                    AssetDatabase.CreateAsset(m, path);
                }
                m.shader = lit;
                m.SetColor("_BaseColor", e.Value.color);
                m.SetFloat("_Smoothness", e.Value.smoothness);
                m.SetFloat("_Metallic", 0f);
                m.enableInstancing = true;
                EditorUtility.SetDirty(m);
            }
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color c);
            return c;
        }
    }
}
