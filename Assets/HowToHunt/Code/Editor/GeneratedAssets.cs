using System;
using System.IO;
using System.Linq;
using FishNet.Component.Transforming.Beta;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Managing.Timing;
using FishNet.Managing.Transporting;
using FishNet.Object;
using FishNet.Transporting.Tugboat;
using HowToHunt.Core;
using HowToHunt.Player;
using UnityEditor;
using UnityEngine;

namespace HowToHunt.Editor
{
    /// <summary>
    /// Builds the generated assets (network manager, player prefab, materials, platform settings) from code,
    /// so they are reproducible and reviewable. Run from the menu, or from the CLI via script eval.
    /// </summary>
    public static class GeneratedAssets
    {
        public const string Root = "Assets/HowToHunt";
        public const string ResourcesDir = Root + "/Resources";
        public const string MaterialsDir = Root + "/Art/Materials";
        public const string MeshesDir = Root + "/Art/Meshes";
        public const string NetworkManagerPath = ResourcesDir + "/Net/NetworkManager.prefab";
        public const string PlayerPrefabPath = ResourcesDir + "/Player/PlayerAvatar.prefab";

        [MenuItem("How to Hunt/Rebuild Generated Assets", priority = 1)]
        public static void RebuildAll()
        {
            EnsurePlatformSettings();
            Palette.EnsureMaterials();
            EnsureMeshes();
            EnsureFxMaterials();
            BuildPlayerPrefab();
            BuildNetworkManagerPrefab();
            BuildButterflyPrefab();
            BuildScoutPrefab();
            DropInWeapons.BuildAll();
            BuildAttachmentPrefabs();
            CreatureModels.BuildAll();
            GeneratedContent.Build();
            EditorApplication.ExecuteMenuItem("Tools/Fish-Networking/Utility/Refresh Default Prefabs");
            AssetDatabase.SaveAssets();
            Debug.Log("[HowToHunt] Generated assets rebuilt.");
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>Materials loaded at runtime by FX (must live in Resources so builds include them).</summary>
        private static void EnsureFxMaterials()
        {
            UnlitFx("M_Tracer", new Color(1f, 0.86f, 0.5f));
            UnlitFx("M_DebugSeen", new Color(0.3f, 0.55f, 1f));
            UnlitFx("M_DebugChecked", new Color(0.3f, 1f, 0.45f));
            const string path = ResourcesDir + "/FX/M_Dust.mat";
            EnsureFolder(ResourcesDir + "/FX");
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", new Color(0.86f, 0.78f, 0.6f));
            m.SetFloat("_Smoothness", 0.05f);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);

            // Gore: particle cubes take their colour from the particle (vertex colour); gibs and splats are tinted
            // copies of M_Gib made at runtime, one per colour. Slightly glossy so blood reads as wet.
            LitFx("M_Gore", "Universal Render Pipeline/Particles/Lit", Color.white, 0.5f);
            LitFx("M_Gib", "Universal Render Pipeline/Lit", Color.white, 0.45f);
            LitFx("M_Flame", "Universal Render Pipeline/Particles/Unlit", Color.white, 0f);
            // Terrain and mountains: lit, colour from the mesh's vertex colours (one colour per face).
            LitFx("M_Terrain", "Universal Render Pipeline/Particles/Lit", Color.white, 0.05f);
            LitFx("M_Water", "Universal Render Pipeline/Lit", new Color(0.27f, 0.62f, 0.86f), 0.85f);
            LitFx("M_Cloud", "Universal Render Pipeline/Lit", new Color(0.98f, 0.98f, 1f), 0f);
            // Explosions: low-poly orbs, coloured per orb (fire glows, smoke is lit).
            UnlitFx("M_Fire", Color.white);
            LitFx("M_Smoke", "Universal Render Pipeline/Lit", Color.white, 0.05f);
            TransparentFx("M_Plasma"); // the Gunlance's layered plasma orbs, tinted per layer
        }

        private static void LitFx(string name, string shader, Color color, float smoothness)
        {
            string path = ResourcesDir + "/FX/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
        }

        private static void UnlitFx(string name, Color color)
        {
            string path = ResourcesDir + "/FX/" + name + ".mat";
            EnsureFolder(ResourcesDir + "/FX");
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(m);
        }

        /// <summary>Unlit, alpha-blended, no depth writes: see-through layers tinted with a property block.</summary>
        private static void TransparentFx(string name)
        {
            string path = ResourcesDir + "/FX/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 2f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
        }

        private static void EnsurePlatformSettings()
        {
            const string path = ResourcesDir + "/PlatformSettings.asset";
            if (AssetDatabase.LoadAssetAtPath<PlatformSettings>(path) != null) return;
            EnsureFolder(ResourcesDir);
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<PlatformSettings>(), path);
        }

        public static Mesh WedgeMesh => AssetDatabase.LoadAssetAtPath<Mesh>(MeshesDir + "/Wedge.asset");
        public static Mesh SurfPrismMesh => AssetDatabase.LoadAssetAtPath<Mesh>(MeshesDir + "/SurfPrism.asset");

        private static void EnsureMeshes()
        {
            EnsureFolder(MeshesDir);
            SaveMesh(ProceduralMeshes.Wedge(), MeshesDir + "/Wedge.asset");
            SaveMesh(ProceduralMeshes.SurfPrism(), MeshesDir + "/SurfPrism.asset");
        }

        private static void SaveMesh(Mesh mesh, string path)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                existing.name = Path.GetFileNameWithoutExtension(path);
                EditorUtility.SetDirty(existing);
                return;
            }
            AssetDatabase.CreateAsset(mesh, path);
        }

        private static void BuildPlayerPrefab()
        {
            EnsureFolder(Path.GetDirectoryName(PlayerPrefabPath)!.Replace('\\', '/'));
            var root = new GameObject("PlayerAvatar");
            try
            {
                NetworkObject nob = root.AddComponent<NetworkObject>();
                root.AddComponent<PlayerMotor>();
                root.AddComponent<PlayerVisual>();
                root.AddComponent<PlayerHealth>();
                root.AddComponent<Hunting.PlayerTools>();

                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);

                // The hunters: a stick figure with a leaf and a painted tribe mask (posed by StickFigure).
                AvatarBuilder.BuildFigure(visual.transform);
                root.AddComponent<Avatar.StickFigure>();

                NetworkTickSmoother smoother = visual.AddComponent<NetworkTickSmoother>();
                var smootherSo = new SerializedObject(smoother);
                smootherSo.FindProperty("_initializationSettings.TargetTransform").objectReferenceValue = root.transform;
                foreach (string settings in new[] { "_controllerMovementSettings", "_spectatorMovementSettings" })
                {
                    // Teleports (respawn, pads) snap instead of sliding across the map.
                    smootherSo.FindProperty(settings + ".EnableTeleport").boolValue = true;
                    smootherSo.FindProperty(settings + ".TeleportThreshold").floatValue = 4f;
                }
                smootherSo.ApplyModifiedPropertiesWithoutUndo();

                var nobSo = new SerializedObject(nob);
                nobSo.FindProperty("_enablePrediction").boolValue = true;
                nobSo.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        public const string AttachmentsDir = Root + "/Prefabs/Attachments";

        /// <summary>
        /// Stand-in attachment models from primitives (origin at the mount, pointing +Z), until the owner's gun assets
        /// land. Ammo, speed loaders and magazines inside the gun have no model.
        /// </summary>
        private static void BuildAttachmentPrefabs()
        {
            EnsureFolder(AttachmentsDir);
            Attachment("MuzzleBrake", root =>
            {
                Part(root, "Body", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.03f), new Vector3(0.04f, 0.03f, 0.04f), new Vector3(90f, 0f, 0f), "Gunmetal");
                Part(root, "Vent L", PrimitiveType.Cube, new Vector3(-0.022f, 0f, 0.035f), new Vector3(0.008f, 0.02f, 0.03f), Vector3.zero, "Trim");
                Part(root, "Vent R", PrimitiveType.Cube, new Vector3(0.022f, 0f, 0.035f), new Vector3(0.008f, 0.02f, 0.03f), Vector3.zero, "Trim");
            });
            Attachment("Suppressor", root =>
                Part(root, "Can", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.09f), new Vector3(0.042f, 0.09f, 0.042f), new Vector3(90f, 0f, 0f), "Gunmetal"));
            Attachment("Laser", root =>
            {
                Part(root, "Body", PrimitiveType.Cube, new Vector3(0f, -0.018f, 0f), new Vector3(0.024f, 0.022f, 0.06f), Vector3.zero, "Gunmetal");
                Part(root, "Lens", PrimitiveType.Cube, new Vector3(0f, -0.018f, 0.031f), new Vector3(0.012f, 0.012f, 0.004f), Vector3.zero, "Hazard");
            });
            Attachment("RedDot", root =>
            {
                Part(root, "Base", PrimitiveType.Cube, new Vector3(0f, 0.004f, 0f), new Vector3(0.028f, 0.008f, 0.05f), Vector3.zero, "Gunmetal");
                // A hollow window (four bars) with the dot floating in the middle: you look through it.
                Part(root, "Frame L", PrimitiveType.Cube, new Vector3(-0.016f, 0.029f, 0.012f), new Vector3(0.004f, 0.036f, 0.006f), Vector3.zero, "Gunmetal");
                Part(root, "Frame R", PrimitiveType.Cube, new Vector3(0.016f, 0.029f, 0.012f), new Vector3(0.004f, 0.036f, 0.006f), Vector3.zero, "Gunmetal");
                Part(root, "Frame T", PrimitiveType.Cube, new Vector3(0f, 0.046f, 0.012f), new Vector3(0.036f, 0.004f, 0.006f), Vector3.zero, "Gunmetal");
                Part(root, "Frame B", PrimitiveType.Cube, new Vector3(0f, 0.012f, 0.012f), new Vector3(0.036f, 0.004f, 0.006f), Vector3.zero, "Gunmetal");
                Part(root, "Dot", PrimitiveType.Cube, new Vector3(0f, 0.03f, 0.014f), new Vector3(0.0025f, 0.0025f, 0.001f), Vector3.zero, "Hazard");
            });
            Attachment("Foregrip", root =>
                Part(root, "Grip", PrimitiveType.Cube, new Vector3(0f, -0.045f, 0f), new Vector3(0.03f, 0.08f, 0.035f), new Vector3(10f, 0f, 0f), "Gunmetal"));
            Attachment("ExtendedMag", root =>
                Part(root, "Mag", PrimitiveType.Cube, new Vector3(0f, -0.055f, 0f), new Vector3(0.03f, 0.09f, 0.05f), new Vector3(8f, 0f, 0f), "Gunmetal"));
            Attachment("PaddedStock", root =>
                Part(root, "Pad", PrimitiveType.Cube, new Vector3(0f, 0f, -0.015f), new Vector3(0.05f, 0.11f, 0.03f), Vector3.zero, "Rock"));
        }

        private static void Attachment(string name, System.Action<GameObject> build)
        {
            var root = new GameObject(name);
            try
            {
                build(root);
                PrefabUtility.SaveAsPrefabAsset(root, $"{AttachmentsDir}/{name}.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        public const string ScoutPrefabPath = Root + "/Prefabs/Weapons/ScoutRifle.prefab";

        /// <summary>
        /// Stand-in scout rifle built from primitives (grip at the origin, barrel along +Z, scope on top), until real
        /// weapon models land (PLAN Phase 4: owner-approved source). Ball hands, as agreed for the graybox.
        /// </summary>
        private static void BuildScoutPrefab()
        {
            EnsureFolder(Path.GetDirectoryName(ScoutPrefabPath)!.Replace('\\', '/'));
            var root = new GameObject("ScoutRifle");
            try
            {
                Part(root, "Stock", PrimitiveType.Cube, new Vector3(0f, -0.035f, -0.24f), new Vector3(0.045f, 0.1f, 0.3f), Vector3.zero, "Wood");
                Part(root, "Receiver", PrimitiveType.Cube, new Vector3(0f, 0.02f, 0.06f), new Vector3(0.04f, 0.06f, 0.3f), Vector3.zero, "Gunmetal");
                Part(root, "Fore-end", PrimitiveType.Cube, new Vector3(0f, -0.002f, 0.33f), new Vector3(0.046f, 0.045f, 0.28f), Vector3.zero, "Wood");
                Part(root, "Barrel", PrimitiveType.Cylinder, new Vector3(0f, 0.03f, 0.53f), new Vector3(0.022f, 0.25f, 0.022f), new Vector3(90f, 0f, 0f), "Gunmetal");
                Part(root, "Scope", PrimitiveType.Cylinder, new Vector3(0f, 0.09f, 0.07f), new Vector3(0.036f, 0.13f, 0.036f), new Vector3(90f, 0f, 0f), "Gunmetal");
                Part(root, "Scope Mount", PrimitiveType.Cube, new Vector3(0f, 0.062f, 0.07f), new Vector3(0.02f, 0.03f, 0.1f), Vector3.zero, "Gunmetal");
                Part(root, "Bolt", PrimitiveType.Cube, new Vector3(0.035f, 0.035f, -0.02f), new Vector3(0.05f, 0.018f, 0.018f), Vector3.zero, "Gunmetal");
                Part(root, "Grip", PrimitiveType.Cube, new Vector3(0f, -0.05f, -0.03f), new Vector3(0.035f, 0.09f, 0.04f), new Vector3(-15f, 0f, 0f), "Wood");
                Part(root, "Hand", PrimitiveType.Sphere, new Vector3(0.01f, -0.075f, -0.03f), Vector3.one * 0.095f, Vector3.zero, "Skin");
                Part(root, "FrontHand", PrimitiveType.Sphere, new Vector3(-0.005f, -0.045f, 0.34f), Vector3.one * 0.085f, Vector3.zero, "Skin");
                PrefabUtility.SaveAsPrefabAsset(root, ScoutPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void Part(GameObject root, string name, PrimitiveType type, Vector3 position, Vector3 scale, Vector3 euler, string material)
        {
            GameObject g = GameObject.CreatePrimitive(type);
            g.name = name;
            UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(root.transform, false);
            g.transform.localPosition = position;
            g.transform.localRotation = Quaternion.Euler(euler);
            g.transform.localScale = scale;
            var r = g.GetComponent<MeshRenderer>();
            r.sharedMaterial = Palette.Get(material);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public const string ButterflyModelPath = Root + "/Art/Models/Butterfly/Butterfly.fbx";
        public const string ButterflyPrefabPath = Root + "/Prefabs/Ambient/Butterfly.prefab";

        /// <summary>Blender model + rig + Flap clip -> animated prefab (the butterfly prey visual).</summary>
        private static void BuildButterflyPrefab()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ButterflyModelPath);
            AnimationClip flap = AssetDatabase.LoadAllAssetsAtPath(ButterflyModelPath).OfType<AnimationClip>().FirstOrDefault(c => c.name == "Flap");
            if (model == null || flap == null)
            {
                Debug.LogWarning("[HowToHunt] Butterfly model or Flap clip missing; skipping prefab.");
                return;
            }
            string controllerPath = Path.ChangeExtension(ButterflyModelPath, ".controller");
            AssetDatabase.DeleteAsset(controllerPath);
            UnityEditor.Animations.AnimatorController controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPathWithClip(controllerPath, flap);

            EnsureFolder(Path.GetDirectoryName(ButterflyPrefabPath)!.Replace('\\', '/'));
            // The source faces -Y in Blender, which imports facing -Z: turn it to face +Z (Unity forward) under a root.
            var root = new GameObject("Butterfly");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            instance.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            try
            {
                Animator animator = instance.TryGetComponent(out Animator existing) ? existing : instance.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.cullingMode = AnimatorCullingMode.CullCompletely;
                foreach (SkinnedMeshRenderer smr in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    smr.updateWhenOffscreen = false;
                    smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
                PrefabUtility.SaveAsPrefabAsset(root, ButterflyPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void BuildNetworkManagerPrefab()
        {
            EnsureFolder(Path.GetDirectoryName(NetworkManagerPath)!.Replace('\\', '/'));
            var go = new GameObject("NetworkManager");
            try
            {
                NetworkManager nm = go.AddComponent<NetworkManager>();
                TimeManager tm = go.AddComponent<TimeManager>();
                TransportManager transportManager = go.AddComponent<TransportManager>();
                Tugboat tugboat = go.AddComponent<Net.MeteredTugboat>();
                var steam = go.AddComponent<Net.MeteredFishySteamworks>();

                var tmSo = new SerializedObject(tm);
                tmSo.FindProperty("_tickRate").intValue = Foundation.GameIdentity.TickRate;
                tmSo.FindProperty("_physicsMode").enumValueIndex = (int)PhysicsMode.Disabled;
                tmSo.ApplyModifiedPropertiesWithoutUndo();

                transportManager.Transport = tugboat;

                var steamSo = new SerializedObject(steam);
                steamSo.FindProperty("_peerToPeer").boolValue = true;
                steamSo.ApplyModifiedPropertiesWithoutUndo();

                var nmSo = new SerializedObject(nm);
                nmSo.FindProperty("_spawnablePrefabs").objectReferenceValue = AssetDatabase.LoadAssetAtPath<DefaultPrefabObjects>("Assets/DefaultPrefabObjects.asset");
                SerializedProperty persistence = nmSo.FindProperty("_persistence");
                persistence.enumValueIndex = Array.IndexOf(persistence.enumNames, nameof(NetworkManager.PersistenceType.DestroyOldest));
                nmSo.FindProperty("_dontDestroyOnLoad").boolValue = true;
                nmSo.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(go, NetworkManagerPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
