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
            BuildPlayerPrefab();
            BuildNetworkManagerPrefab();
            BuildButterflyPrefab();
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

                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);

                // Placeholder body until the Blender character lands: orange jumpsuit capsule with a dark visor showing facing.
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Body";
                UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
                body.transform.SetParent(visual.transform, false);
                body.transform.localScale = new Vector3(0.8f, 0.9144f, 0.8f);
                body.GetComponent<MeshRenderer>().sharedMaterial = Palette.Get("PlayerSuit");

                GameObject visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visor.name = "Visor";
                UnityEngine.Object.DestroyImmediate(visor.GetComponent<Collider>());
                visor.transform.SetParent(visual.transform, false);
                visor.transform.localPosition = new Vector3(0f, 0.62f, 0.33f);
                visor.transform.localScale = new Vector3(0.5f, 0.16f, 0.2f);
                visor.GetComponent<MeshRenderer>().sharedMaterial = Palette.Get("Visor");

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

        public const string ButterflyModelPath = Root + "/Art/Models/Butterfly/Butterfly.fbx";
        public const string ButterflyPrefabPath = Root + "/Prefabs/Ambient/Butterfly.prefab";

        /// <summary>Asset-pipeline proof: Blender model + rig + Flap clip -> animated prefab. Gameplay comes in M3.</summary>
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
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                instance.name = "Butterfly";
                Animator animator = instance.TryGetComponent(out Animator existing) ? existing : instance.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.cullingMode = AnimatorCullingMode.CullCompletely;
                foreach (SkinnedMeshRenderer smr in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    smr.updateWhenOffscreen = false;
                    smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
                PrefabUtility.SaveAsPrefabAsset(instance, ButterflyPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
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
                Tugboat tugboat = go.AddComponent<Tugboat>();
                var steam = go.AddComponent<FishySteamworks.FishySteamworks>();

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
