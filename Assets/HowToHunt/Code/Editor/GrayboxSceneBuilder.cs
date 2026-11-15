using System.Collections.Generic;
using System.Linq;
using HowToHunt.Core;
using HowToHunt.Player;
using HowToHunt.Sim.Collision;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HowToHunt.Editor
{
    /// <summary>
    /// Builds the M0/M1 movement test map: flat ground, crates, stairs, a walkable ramp, a bhop strip and a surf prism.
    /// Generated from code so it can be rebuilt after changes to brush conventions.
    /// </summary>
    public static class GrayboxSceneBuilder
    {
        public const string ScenePath = GeneratedAssets.Root + "/Scenes/Graybox.unity";

        [MenuItem("How to Hunt/Rebuild Graybox Scene", priority = 2)]
        public static void Build()
        {
            GeneratedAssets.RebuildAll();
            GeneratedAssets.EnsureFolder(GeneratedAssets.Root + "/Scenes");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Lighting: warm low sun, sky ambient, light fog (art direction A).
            var sun = new GameObject("Sun", typeof(Light));
            Light light = sun.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.84f);
            light.intensity = 1.6f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.75f;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            RenderSettings.sun = light;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.76f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(0.72f, 0.75f, 0.68f);
            RenderSettings.ambientGroundColor = new Color(0.42f, 0.4f, 0.32f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.72f, 0.84f, 0.95f);
            RenderSettings.fogStartDistance = 60f;
            RenderSettings.fogEndDistance = 220f;

            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            camGo.transform.SetPositionAndRotation(new Vector3(0, 12, -22), Quaternion.Euler(25, 0, 0));
            Camera cam = camGo.GetComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 400f;
            cam.clearFlags = CameraClearFlags.Skybox;

            Transform level = new GameObject("Level").transform;

            // Ground and boundary walls.
            Box(level, "Ground", new Vector3(0, -0.5f, 0), new Vector3(160, 1, 160), "Grass");
            Box(level, "Wall N", new Vector3(0, 3, 80.5f), new Vector3(162, 8, 1), "Wall");
            Box(level, "Wall S", new Vector3(0, 3, -80.5f), new Vector3(162, 8, 1), "Wall");
            Box(level, "Wall E", new Vector3(80.5f, 3, 0), new Vector3(1, 8, 160), "Wall");
            Box(level, "Wall W", new Vector3(-80.5f, 3, 0), new Vector3(1, 8, 160), "Wall");

            // Spawn plaza.
            Box(level, "Plaza", new Vector3(0, 0.05f, 0), new Vector3(14, 0.1f, 14), "Sand");
            Transform spawns = new GameObject("Spawns").transform;
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                var sp = new GameObject($"Spawn {i + 1}", typeof(SpawnPoint));
                sp.transform.SetParent(spawns, false);
                sp.transform.SetPositionAndRotation(new Vector3(Mathf.Sin(a) * 4f, 0.1f, Mathf.Cos(a) * 4f), Quaternion.Euler(0, 0, 0));
            }

            // Crates to run around.
            Box(level, "Crate A", new Vector3(8, 0.6f, 6), new Vector3(1.2f, 1.2f, 1.2f), "Crate");
            Box(level, "Crate B", new Vector3(9.4f, 0.6f, 6.3f), new Vector3(1.2f, 1.2f, 1.2f), "Crate", 15f);
            Box(level, "Crate C", new Vector3(8.6f, 1.8f, 6.1f), new Vector3(1.2f, 1.2f, 1.2f), "Crate", -10f);
            Box(level, "Crate D", new Vector3(-7, 0.5f, 8), new Vector3(1, 1, 1), "Crate", 30f);

            // Stairs: 30 cm steps (walkable via step-up).
            Transform stairs = new GameObject("Stairs").transform;
            stairs.SetParent(level, false);
            for (int i = 0; i < 8; i++)
                Box(stairs, $"Step {i + 1}", new Vector3(16, 0.15f + i * 0.3f, -4 + i * 0.8f), new Vector3(4, 0.3f, 0.8f), "Wood");
            Box(stairs, "Landing", new Vector3(16, 1.2f, 3.2f), new Vector3(4, 2.4f, 2.4f), "Wood");

            // Walkable ramp (~27°).
            Wedge(level, "Ramp", new Vector3(-16, 0, 0), new Vector3(5, 3, 6), 0f, "Ramp");
            Box(level, "Ramp Top", new Vector3(-16, 1.5f, 5), new Vector3(5, 3, 4), "Rock");

            // Bhop strip: platforms with growing gaps.
            Transform bhop = new GameObject("Bhop Strip").transform;
            bhop.SetParent(level, false);
            float z = 14f;
            for (int i = 0; i < 10; i++)
            {
                Box(bhop, $"Bhop {i + 1}", new Vector3(0, 0.6f, z), new Vector3(2.2f, 1.2f, 2.2f), "Bhop");
                z += 2.2f + 2.0f + i * 0.35f;
            }

            // Surf prism: 51° sides (normal.y ≈ 0.62 < 0.7 => surfable). Start tower at one end.
            Transform surf = new GameObject("Surf").transform;
            surf.SetParent(level, false);
            SurfPrism(surf, "Surf Ramp", new Vector3(-30, 0, -45), new Vector3(16, 10, 60), "Surf");
            Box(surf, "Surf Start Tower", new Vector3(-30, 7, -10), new Vector3(6, 14, 6), "Rock");
            Box(surf, "Surf Start Step", new Vector3(-26, 2, -12), new Vector3(2, 4, 2), "Rock");

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            Debug.Log($"[HowToHunt] Graybox scene saved to {ScenePath}");
        }

        private static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, string material, float yaw = 0f)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(center, Quaternion.Euler(0, yaw, 0));
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = Palette.Get(material);
            go.AddComponent<SimBrush>().Shape = SimBrush.ShapeKind.Box;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            return go;
        }

        private static GameObject MeshBrush(Transform parent, string name, Mesh mesh, Vector3 basePos, Vector3 size, float yaw, string material, SurfaceFlags flags)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(basePos, Quaternion.Euler(0, yaw, 0));
            go.transform.localScale = size;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = Palette.Get(material);
            SimBrush brush = go.AddComponent<SimBrush>();
            brush.Shape = SimBrush.ShapeKind.ConvexMesh;
            brush.Surface = flags;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
            return go;
        }

        private static GameObject Wedge(Transform parent, string name, Vector3 basePos, Vector3 size, float yaw, string material)
            => MeshBrush(parent, name, GeneratedAssets.WedgeMesh, basePos, size, yaw, material, SurfaceFlags.None);

        private static GameObject SurfPrism(Transform parent, string name, Vector3 basePos, Vector3 size, string material)
            => MeshBrush(parent, name, GeneratedAssets.SurfPrismMesh, basePos, size, 0f, material, SurfaceFlags.Surf);

        private static void AddToBuildSettings(string path)
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.Where(s => s.path != path && s.path != "Assets/Scenes/SampleScene.unity").ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
