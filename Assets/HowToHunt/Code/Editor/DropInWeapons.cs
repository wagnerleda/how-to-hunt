using System.Collections.Generic;
using System.IO;
using HowToHunt.Hunting;
using UnityEditor;
using UnityEngine;

namespace HowToHunt.Editor
{
    /// <summary>
    /// Turns the owner's gun models (Art/Weapons/&lt;folder&gt;/) into viewmodels: each one is turned to point +Z, scaled
    /// to a real length, placed so the grip is at the origin, given textured materials from the files in its folder
    /// and ball hands, and measured (muzzle, sight line, attachment mounts) into a <see cref="ViewmodelFit"/> that the
    /// content build copies onto the tool. One table row per gun: only the turn, length and grip need judging by eye.
    /// </summary>
    public static class DropInWeapons
    {
        public struct Spec
        {
            public string Name;       // prefab name
            public string Model;      // path under Art/Weapons
            public Vector3 Euler;     // turn so the barrel points +Z and the top is +Y
            public float Length;      // real length, metres
            public float GripAlong;   // grip position from the back (0) to the muzzle (1)
            public float GripUp;      // grip height from the bottom (0) to the top (1)
            public bool TwoHanded;    // a front hand under the barrel
        }

        public static readonly Spec[] Specs =
        {
            new Spec { Name = "Glock", Model = W + "Glock/cgtrader_optimized_GLOCK19.fbx", Euler = new Vector3(0f, 90f, 0f), Length = 0.19f, GripAlong = 0.22f, GripUp = 0.35f },
            new Spec { Name = "Uzi", Model = W + "Uzi/uzI+1+v34.fbx", Euler = new Vector3(90f, 180f, 0f), Length = 0.47f, GripAlong = 0.45f, GripUp = 0.22f, TwoHanded = true },
            new Spec { Name = "SawedOff", Model = W + "Sawed-Off/sawed_off.FBX", Euler = new Vector3(0f, 180f, 0f), Length = 0.55f, GripAlong = 0.12f, GripUp = 0.35f, TwoHanded = true },
            new Spec { Name = "HandCannon", Model = W + "Handcannon/Colt+navy+1851.fbx", Euler = new Vector3(0f, 180f, 0f), Length = 0.36f, GripAlong = 0.12f, GripUp = 0.3f },
            new Spec { Name = "ScoutSniper", Model = W + "ScoutSniper/scoutsniper.fbx", Euler = new Vector3(0f, 0f, 0f), Length = 1.05f, GripAlong = 0.27f, GripUp = 0.3f, TwoHanded = true },
            new Spec { Name = "HeavyRifle", Model = W + "HeavyAssaultRifle/SCAR_H_PR.fbx", Euler = new Vector3(0f, 90f, 0f), Length = 0.97f, GripAlong = 0.33f, GripUp = 0.3f, TwoHanded = true },
            new Spec { Name = "Intervention", Model = W + "Intervention/Cheytac3.6.fbx", Euler = new Vector3(0f, 180f, 0f), Length = 1.4f, GripAlong = 0.27f, GripUp = 0.25f, TwoHanded = true },
            new Spec { Name = "GrenadeLauncher", Model = W + "GrenadeLauncher/GL37.fbx", Euler = new Vector3(0f, -90f, 0f), Length = 0.75f, GripAlong = 0.42f, GripUp = 0.25f, TwoHanded = true },
            new Spec { Name = "Gunlance", Model = W + "scifigun/model/gunlance-closed-static.fbx", Euler = new Vector3(0f, -90f, 0f), Length = 1.0f, GripAlong = 0.35f, GripUp = 0.3f, TwoHanded = true },
            new Spec { Name = "KnifeV2", Model = GeneratedAssets.Root + "/Art/Models/KnifeV2/knifev2.fbx", Euler = new Vector3(0f, -90f, 0f), Length = 0.3f, GripAlong = 0.2f, GripUp = 0.5f },
        };

        private const string W = GeneratedAssets.Root + "/Art/Weapons/";

        private const string PrefabDir = GeneratedAssets.Root + "/Prefabs/Weapons/";

        public static string PrefabPath(string name) => PrefabDir + name + ".prefab";

        public static void BuildAll()
        {
            GeneratedAssets.EnsureFolder(PrefabDir.TrimEnd('/'));
            foreach (Spec s in Specs) Build(s);
            BuildRedDot();
        }

        private static void Build(Spec spec)
        {
            string modelPath = spec.Model;
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (fbx == null) { Debug.LogWarning($"[HowToHunt] Missing gun model {modelPath}"); return; }
            var root = new GameObject(spec.Name);
            try
            {
                var pivot = new GameObject("Gun").transform;
                pivot.SetParent(root.transform, false);
                pivot.localRotation = Quaternion.Euler(spec.Euler);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
                model.transform.SetParent(pivot, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                // The grip goes at the origin and the gun is scaled to its real length.
                var points = new List<Vector3>(8192);
                Bounds b = Measure(root.transform, points);
                float scale = spec.Length / Mathf.Max(0.001f, b.size.z);
                var grip = new Vector3(b.center.x, b.min.y + spec.GripUp * b.size.y, b.min.z + spec.GripAlong * b.size.z);
                pivot.localScale = Vector3.one * scale;
                pivot.localPosition = -grip * scale;
                b = Measure(root.transform, points);

                Materials(spec, model);
                foreach (Renderer r in model.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                ViewmodelFit fit = root.AddComponent<ViewmodelFit>();
                float len = b.size.z;
                Vector3 muzzle = SliceCenter(points, b.max.z - 0.04f * len, b.max.z + 1f);
                muzzle.z = b.max.z;
                float top = MaxY(points, b.min.z + 0.3f * len, b.max.z - 0.15f * len, out float topZ);
                fit.Muzzle = new Vector3(0f, muzzle.y, muzzle.z);
                fit.Sight = new Vector3(0f, top + 0.004f, Mathf.Min(topZ, 0.05f));
                float underZ = Mathf.Lerp(0f, b.max.z, 0.55f);
                float underY = MinY(points, underZ - 0.05f, underZ + 0.05f);
                fit.Anchors.Add(new AttachmentAnchor { Slot = AttachmentSlot.Muzzle, Position = fit.Muzzle });
                fit.Anchors.Add(new AttachmentAnchor { Slot = AttachmentSlot.Optic, Position = new Vector3(0f, top, Mathf.Clamp(topZ, b.min.z * 0.3f, b.max.z * 0.4f)) });
                fit.Anchors.Add(new AttachmentAnchor { Slot = AttachmentSlot.Underbarrel, Position = new Vector3(0f, underY, underZ) });
                fit.Anchors.Add(new AttachmentAnchor { Slot = AttachmentSlot.Magazine, Position = new Vector3(0f, MinY(points, 0.02f, 0.12f), 0.07f) });
                fit.Anchors.Add(new AttachmentAnchor { Slot = AttachmentSlot.Ammo, Position = new Vector3(0f, muzzle.y * 0.5f, 0.05f) });
                if (spec.TwoHanded && b.min.z < -0.2f)
                    fit.Anchors.Add(new AttachmentAnchor { Slot = AttachmentSlot.Stock, Position = new Vector3(0f, SliceCenter(points, b.min.z - 1f, b.min.z + 0.04f * len).y, b.min.z) });

                GeneratedAssetsParts.Ball(root, "Hand", new Vector3(0.005f, -0.035f, -0.01f), 0.085f);
                if (spec.TwoHanded) GeneratedAssetsParts.Ball(root, "FrontHand", new Vector3(0f, underY - 0.03f, underZ), 0.085f);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(spec.Name));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        public const string RedDotPrefab = GeneratedAssets.Root + "/Prefabs/Attachments/RedDotModel.prefab";

        /// <summary>The owner's red dot sight as the Red Dot attachment: 7.5 cm long, sitting on its base at the mount.</summary>
        private static void BuildRedDot()
        {
            const string path = GeneratedAssets.Root + "/Art/Models/RedDotSight/REdDotSight_FBX.fbx";
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (fbx == null) return;
            GeneratedAssets.EnsureFolder(GeneratedAssets.Root + "/Prefabs/Attachments");
            var root = new GameObject("RedDotModel");
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
                model.transform.SetParent(root.transform, false);
                var points = new List<Vector3>(4096);
                Bounds b = Measure(root.transform, points);
                float scale = 0.075f / Mathf.Max(0.001f, b.size.z);
                model.transform.localScale = Vector3.one * scale;
                model.transform.localPosition = -new Vector3(b.center.x, b.min.y, b.center.z) * scale;
                Materials(new Spec { Name = "RedDot", Model = path }, model);
                foreach (Renderer r in model.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                PrefabUtility.SaveAsPrefabAsset(root, RedDotPrefab);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // --- measuring ---------------------------------------------------------------------------------------------

        private static Bounds Measure(Transform root, List<Vector3> points)
        {
            points.Clear();
            Matrix4x4 toRoot = root.worldToLocalMatrix;
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true)) Add(points, toRoot * mf.transform.localToWorldMatrix, mf.sharedMesh);
            foreach (SkinnedMeshRenderer sk in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) Add(points, toRoot * sk.transform.localToWorldMatrix, sk.sharedMesh);
            if (points.Count == 0) return new Bounds(Vector3.zero, Vector3.one * 0.1f);
            var b = new Bounds(points[0], Vector3.zero);
            for (int i = 1; i < points.Count; i++) b.Encapsulate(points[i]);
            return b;
        }

        private static void Add(List<Vector3> points, Matrix4x4 m, Mesh mesh)
        {
            if (mesh == null) return;
            Vector3[] v = mesh.vertices;
            int step = Mathf.Max(1, v.Length / 20000); // plenty for bounds and slices
            for (int i = 0; i < v.Length; i += step) points.Add(m.MultiplyPoint3x4(v[i]));
        }

        private static Vector3 SliceCenter(List<Vector3> points, float z0, float z1)
        {
            var b = new Bounds();
            bool any = false;
            foreach (Vector3 p in points)
            {
                if (p.z < z0 || p.z > z1) continue;
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                else b.Encapsulate(p);
            }
            return any ? b.center : Vector3.zero;
        }

        private static float MaxY(List<Vector3> points, float z0, float z1, out float atZ)
        {
            float best = float.MinValue;
            atZ = 0f;
            foreach (Vector3 p in points)
                if (p.z >= z0 && p.z <= z1 && p.y > best) { best = p.y; atZ = p.z; }
            return best == float.MinValue ? 0.05f : best;
        }

        private static float MinY(List<Vector3> points, float z0, float z1)
        {
            float best = float.MaxValue;
            foreach (Vector3 p in points)
                if (p.z >= z0 && p.z <= z1 && p.y < best) best = p.y;
            return best == float.MaxValue ? -0.03f : best;
        }

        // --- materials ---------------------------------------------------------------------------------------------

        /// <summary>
        /// URP Lit materials from the textures in the gun's folder, matched to each imported material by name
        /// (falling back to the folder's only colour map). UDIM tile sets (.1002 and up) can't map onto one material:
        /// those parts get a plain gunmetal look.
        /// </summary>
        private static void Materials(Spec spec, GameObject model)
        {
            // The gun's own folder (textures may sit in a subfolder: textures/, bakes/, texture 2k/).
            string folder = Path.GetDirectoryName(spec.Model)!.Replace('\\', '/');
            if (folder.EndsWith("/model") || folder.EndsWith("/bakes")) folder = Path.GetDirectoryName(folder)!.Replace('\\', '/');
            var textures = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder })) textures.Add(AssetDatabase.GUIDToAssetPath(guid));
            bool udim = textures.Exists(t => t.Contains(".1002."));
            string outDir = PrefabDir + "Materials";
            GeneratedAssets.EnsureFolder(outDir);
            var made = new Dictionary<string, Material>();
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    string name = mats[i] != null ? mats[i].name : "Default";
                    if (!made.TryGetValue(name, out Material m))
                    {
                        m = MakeMaterial(spec.Name, name, udim ? null : textures, outDir);
                        made[name] = m;
                    }
                    mats[i] = m;
                }
                r.sharedMaterials = mats;
            }
        }

        private static Material MakeMaterial(string gun, string materialName, List<string> textures, string outDir)
        {
            string path = $"{outDir}/M_{gun}_{Sanitize(materialName)}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", textures == null ? new Color(0.24f, 0.25f, 0.27f) : Color.white);
            mat.SetFloat("_Smoothness", 0.4f);
            mat.SetFloat("_Metallic", 0.2f);
            mat.SetTexture("_BaseMap", null);
            mat.SetTexture("_BumpMap", null);
            mat.SetTexture("_MetallicGlossMap", null);
            mat.DisableKeyword("_NORMALMAP");
            mat.DisableKeyword("_METALLICSPECGLOSSMAP");
            if (textures != null)
            {
                string color = Pick(textures, materialName, "basecolor", "base_color", "diffuse", "albedo", "_diff", "_col");
                string normal = Pick(textures, materialName, "normal_opengl", "normal", "_nor");
                string metal = Pick(textures, materialName, "metallicsmoothness", "metallic");
                if (color != null) mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(color));
                else mat.SetColor("_BaseColor", new Color(0.3f, 0.31f, 0.33f)); // no colour map shipped: plain gun steel
                if (normal != null)
                {
                    mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal));
                    mat.EnableKeyword("_NORMALMAP");
                }
                if (metal != null)
                {
                    mat.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(metal));
                    mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                    mat.SetFloat("_Smoothness", metal.ToLowerInvariant().Contains("smoothness") ? 1f : 0.35f);
                }
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>The texture of that kind whose name shares a word with the material (or the only one of that kind).</summary>
        private static string Pick(List<string> textures, string materialName, params string[] kinds)
        {
            var candidates = new List<string>();
            foreach (string kind in kinds)
            {
                foreach (string t in textures)
                    if (Path.GetFileNameWithoutExtension(t).ToLowerInvariant().Contains(kind) && !candidates.Contains(t)) candidates.Add(t);
                if (candidates.Count > 0) break;
            }
            if (candidates.Count == 0) return null;
            if (candidates.Count == 1) return candidates[0];
            string[] words = materialName.ToLowerInvariant().Split(new[] { '_', '-', ' ', '.', '(', ')' }, System.StringSplitOptions.RemoveEmptyEntries);
            foreach (string t in candidates)
            {
                string file = Path.GetFileNameWithoutExtension(t).ToLowerInvariant();
                foreach (string w in words)
                    if (w.Length >= 3 && !w.StartsWith("mat") && file.Contains(w)) return t;
            }
            return candidates[0];
        }

        private static string Sanitize(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace(' ', '_');
        }
    }

    /// <summary>Small shared helper: a ball "hand" part (no collider, no shadows).</summary>
    public static class GeneratedAssetsParts
    {
        public static void Ball(GameObject root, string name, Vector3 position, float size)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            g.name = name;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(root.transform, false);
            g.transform.localPosition = position;
            g.transform.localScale = Vector3.one * size;
            var r = g.GetComponent<MeshRenderer>();
            r.sharedMaterial = Palette.Get("PlayerSuit");
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
