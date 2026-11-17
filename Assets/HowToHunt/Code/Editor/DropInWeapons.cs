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
            public string Rig;        // moving parts: "partName=Kind;partName=Kind" (RigPartKind)
            public string Detachable; // parts that are attachments ("a;b"): left out when measuring the bare gun
        }

        public static readonly Spec[] Specs =
        {
            new Spec { Name = "Glock", Model = W + "Glock/cgtrader_optimized_GLOCK19.fbx", Euler = new Vector3(0f, 90f, 0f), Length = 0.19f, GripAlong = 0.22f, GripUp = 0.35f, Rig = "g2_low=Slide;m_low=Magazine" },
            new Spec { Name = "Uzi", Model = W + "Uzi/uzI+1+v34.fbx", Euler = new Vector3(90f, 180f, 0f), Length = 0.47f, GripAlong = 0.45f, GripUp = 0.22f, TwoHanded = true, Rig = "Body18=Magazine", Detachable = "Body14;Body19;Body15;Body21" },
            new Spec { Name = "SawedOff", Model = W + "Sawed-Off/sawed_off.FBX", Euler = new Vector3(0f, 180f, 0f), Length = 0.55f, GripAlong = 0.12f, GripUp = 0.35f, TwoHanded = true },
            new Spec { Name = "HandCannon", Model = W + "Handcannon/Colt+navy+1851.fbx", Euler = new Vector3(0f, 180f, 0f), Length = 0.36f, GripAlong = 0.12f, GripUp = 0.3f, Rig = "12=Cylinder" },
            new Spec { Name = "ScoutSniper", Model = W + "ScoutSniper/scoutsniper.fbx", Euler = new Vector3(0f, 0f, 0f), Length = 1.05f, GripAlong = 0.27f, GripUp = 0.3f, TwoHanded = true, Rig = "magazine.002_low=Magazine;magazine.003_low=Magazine;bolt_handle .001_low=Bolt", Detachable = "suppressor ;stock.001_low" },
            new Spec { Name = "HeavyRifle", Model = W + "HeavyAssaultRifle/SCAR_H_PR.fbx", Euler = new Vector3(0f, 90f, 0f), Length = 0.97f, GripAlong = 0.33f, GripUp = 0.3f, TwoHanded = true, Rig = "Plane.017=Magazine;Plane.019=ChargingHandle", Detachable = "Cylinder;Plane.005;Plane.006;Plane.010;Plane.014;Plane.031;Plane.038" },
            new Spec { Name = "Intervention", Model = W + "Intervention/Cheytac3.6.fbx", Euler = new Vector3(0f, 180f, 0f), Length = 1.4f, GripAlong = 0.27f, GripUp = 0.25f, TwoHanded = true, Rig = "Cube_low=Magazine;Cylinder.011_low.001=Magazine;Cylinder.043_low=Magazine;Cylinder.044_low.002=Magazine;Cylinder.012_low=Bolt", Detachable = "Cube.003_low;Cylinder.034_low;Cylinder.038_low" },
            new Spec { Name = "GrenadeLauncher", Model = W + "GrenadeLauncher/GL37.fbx", Euler = new Vector3(0f, -90f, 0f), Length = 0.75f, GripAlong = 0.42f, GripUp = 0.25f, TwoHanded = true, Rig = "GL37_launcherHead=Hinge" },
            new Spec { Name = "Gunlance", Model = W + "scifigun/model/gunlance-closed-static.fbx", Euler = new Vector3(0f, -90f, 0f), Length = 1.0f, GripAlong = 0.35f, GripUp = 0.3f, TwoHanded = true, Rig = "LOD0 | PORT LEAF ASSEMBLY=LeafPort;LOD0 | STARBOARD LEAF ASSEMBLY=LeafStarboard" },
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
                // Measure the bare gun: integrated attachments (suppressor, stock, bipod...) are shown only when fitted.
                var skip = new HashSet<string>(Split(spec.Detachable));
                b = Measure(root.transform, points, skip);
                float len = b.size.z;
                Vector3 muzzle = SliceCenter(points, b.max.z - 0.04f * len, b.max.z + 1f);
                muzzle.z = b.max.z;
                float top = MaxY(points, b.min.z + 0.3f * len, b.max.z - 0.15f * len, out float topZ);
                fit.Muzzle = new Vector3(0f, muzzle.y, muzzle.z);
                // Sight line: the rear sight is the highest point near the grip, the front sight the highest near the
                // muzzle. Aiming tilts the gun so this line points at the target.
                float rearY = MaxY(points, -0.12f, 0.12f, out float rearZ);
                float frontY = MaxY(points, b.max.z * 0.62f, b.max.z, out float frontZ);
                fit.Sight = new Vector3(0f, rearY + 0.003f, rearZ);
                fit.SightFront = new Vector3(0f, frontY + 0.003f, frontZ);
                foreach (string entry in Split(spec.Rig))
                {
                    int eq = entry.LastIndexOf('=');
                    if (eq <= 0 || !System.Enum.TryParse(entry.Substring(eq + 1), out RigPartKind kind)) continue;
                    string partName = entry.Substring(0, eq);
                    Transform part = FindDeep(root.transform, partName);
                    if (part == null) { Debug.LogWarning($"[HowToHunt] {spec.Name}: no part named '{partName}'"); continue; }
                    Bounds pb = PartBounds(root.transform, part);
                    Vector3 pivotPoint = kind switch
                    {
                        RigPartKind.Hinge => new Vector3(pb.center.x, pb.min.y, pb.min.z),
                        RigPartKind.LeafPort or RigPartKind.LeafStarboard => new Vector3(pb.center.x, pb.center.y, pb.min.z),
                        RigPartKind.Bolt => new Vector3(0f, fit.Muzzle.y, pb.center.z), // turns about the bore
                        _ => pb.center,
                    };
                    fit.Parts.Add(new RigPart { Name = partName, Kind = kind, Pivot = pivotPoint });
                }
                float underZ = Mathf.Lerp(0f, b.max.z, 0.55f);
                float underY = MinY(points, underZ - 0.05f, underZ + 0.05f);
                fit.Anchors.Add(new AttachmentAnchor { Slot = AttachmentSlot.Muzzle, Position = fit.Muzzle });
                fit.Anchors.Add(new AttachmentAnchor { Slot = AttachmentSlot.Optic, Position = new Vector3(0f, top, Mathf.Clamp(topZ, b.min.z * 0.3f, b.max.z * 0.4f)) });
                fit.Anchors.Add(new AttachmentAnchor { Slot = AttachmentSlot.Underbarrel, Position = new Vector3(0f, underY, underZ) });
                fit.Anchors.Add(new AttachmentAnchor { Slot = AttachmentSlot.Magazine, Position = new Vector3(0f, MinY(points, 0.02f, 0.12f), 0.07f) });
                fit.Anchors.Add(new AttachmentAnchor { Slot = AttachmentSlot.Ammo, Position = new Vector3(0f, muzzle.y * 0.5f, 0.05f) });
                if (spec.TwoHanded)
                    fit.Anchors.Add(new AttachmentAnchor { Slot = AttachmentSlot.Stock, Position = new Vector3(0f, SliceCenter(points, b.min.z - 1f, b.min.z + 0.04f * len).y, b.min.z) });

                // Built-in attachments start hidden: they show only when fitted (AttachmentVisuals).
                foreach (string part in skip)
                {
                    Transform t = FindDeep(root.transform, part);
                    if (t != null) t.gameObject.SetActive(false);
                }

                // The hand grips: centred on the grip, big enough to wrap it (it hides part of the gun, as a hand does).
                GeneratedAssetsParts.Ball(root, "Hand", new Vector3(0f, -0.005f, 0f), 0.1f);
                if (spec.TwoHanded) GeneratedAssetsParts.Ball(root, "FrontHand", new Vector3(0f, underY - 0.015f, underZ), 0.09f);
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
            const string path = GeneratedAssets.Root + "/Art/Models/RedDotSight/RedDotSight_Open.fbx"; // the pack's model, window glass removed in Blender
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (fbx == null) return;
            GeneratedAssets.EnsureFolder(GeneratedAssets.Root + "/Prefabs/Attachments");
            var root = new GameObject("RedDotModel");
            try
            {
                // The file has the hood facing away from the shooter: turn it round (hood at the back, near the eye).
                var turn = new GameObject("Sight").transform;
                turn.SetParent(root.transform, false);
                turn.localRotation = Quaternion.Euler(0f, 180f, 0f);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
                model.transform.SetParent(turn, false);
                var points = new List<Vector3>(4096);
                Bounds b = Measure(root.transform, points);
                float scale = 0.075f / Mathf.Max(0.001f, b.size.z);
                turn.localScale = Vector3.one * scale;
                turn.localPosition = -new Vector3(b.center.x, b.min.y, b.center.z) * scale;
                b = Measure(root.transform, points);

                // The window is open (no glass faces), so the body is plain opaque.
                Material glass = RedDotMaterial();
                foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
                {
                    r.sharedMaterial = glass;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }

                // The window is in the hood (the tall end), 61% to 97% of the height: the dot floats in its middle.
                float tall = b.min.y + 0.8f * b.size.y, zSum = 0f;
                int n = 0;
                foreach (Vector3 p in points) if (p.y > tall) { zSum += p.z; n++; }
                float hoodZ = n > 0 ? zSum / n : b.min.z;
                var dotPos = new Vector3(0f, b.min.y + 0.79f * b.size.y, hoodZ);
                GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dot.name = "Reticle";
                Object.DestroyImmediate(dot.GetComponent<Collider>());
                dot.transform.SetParent(root.transform, false);
                dot.transform.localPosition = dotPos;
                dot.transform.localScale = Vector3.one * 0.0022f;
                var dr = dot.GetComponent<MeshRenderer>();
                dr.sharedMaterial = ReticleMaterial();
                dr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ViewmodelFit fit = root.AddComponent<ViewmodelFit>();
                fit.Sight = dotPos;
                PrefabUtility.SaveAsPrefabAsset(root, RedDotPrefab);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private const string RedDotTextures = GeneratedAssets.Root + "/Art/Models/RedDotSight/Textures/";

        /// <summary>The pack's colour and normal maps on an opaque lit material.</summary>
        private static Material RedDotMaterial()
        {
            string matPath = PrefabDir + "Materials/M_RedDot_Glass.mat";
            GeneratedAssets.EnsureFolder(PrefabDir + "Materials");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, matPath); }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(RedDotTextures + "RedDotSight_Diff.png"));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_AlphaClip", 0f);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.SetFloat("_Cull", 0f); // the hood is thin: both faces
            mat.SetFloat("_Smoothness", 0.3f);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(RedDotTextures + "RedDotSight_Nor.png");
            if (normal != null) { mat.SetTexture("_BumpMap", normal); mat.EnableKeyword("_NORMALMAP"); }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material ReticleMaterial()
        {
            string matPath = PrefabDir + "Materials/M_RedDot_Reticle.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(mat, matPath); }
            mat.SetColor("_BaseColor", new Color(1f, 0.08f, 0.05f));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // --- measuring ---------------------------------------------------------------------------------------------

        private static IEnumerable<string> Split(string list)
        {
            if (string.IsNullOrEmpty(list)) yield break;
            foreach (string s in list.Split(';'))
                if (s.Length > 0) yield return s;
        }

        private static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                Transform f = FindDeep(t.GetChild(i), name);
                if (f != null) return f;
            }
            return null;
        }

        /// <summary>A part's bounds in the prefab's space (its mesh corners).</summary>
        private static Bounds PartBounds(Transform root, Transform part)
        {
            var mf = part.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return new Bounds(root.InverseTransformPoint(part.position), Vector3.zero);
            Matrix4x4 m = root.worldToLocalMatrix * part.localToWorldMatrix;
            Bounds mb = mf.sharedMesh.bounds;
            var b = new Bounds(m.MultiplyPoint3x4(mb.min), Vector3.zero);
            for (int i = 0; i < 8; i++)
                b.Encapsulate(m.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? mb.min.x : mb.max.x, (i & 2) == 0 ? mb.min.y : mb.max.y, (i & 4) == 0 ? mb.min.z : mb.max.z)));
            return b;
        }

        private static Bounds Measure(Transform root, List<Vector3> points, HashSet<string> skip = null)
        {
            points.Clear();
            Matrix4x4 toRoot = root.worldToLocalMatrix;
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
                if (skip == null || !skip.Contains(mf.gameObject.name)) Add(points, toRoot * mf.transform.localToWorldMatrix, mf.sharedMesh);
            foreach (SkinnedMeshRenderer sk in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (skip == null || !skip.Contains(sk.gameObject.name)) Add(points, toRoot * sk.transform.localToWorldMatrix, sk.sharedMesh);
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
