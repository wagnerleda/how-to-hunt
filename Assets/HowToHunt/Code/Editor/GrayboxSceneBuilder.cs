using System.Collections.Generic;
using System.IO;
using System.Linq;
using HowToHunt.Core;
using HowToHunt.Economy;
using HowToHunt.Player;
using HowToHunt.Sim.Collision;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace HowToHunt.Editor
{
    /// <summary>
    /// Builds the movement lab: spawn plaza with teleport pads, a bhop course and a gap-jump course over hazard pits,
    /// a two-ramp surf course, a slope gallery (walkable vs surfable), stairs, jump/crouch-jump ledges and a sprint
    /// lane with 5 m markers. Generated from code so it can be rebuilt after changes to brush or zone conventions.
    /// </summary>
    public static partial class GrayboxSceneBuilder
    {
        public const string ScenePath = GeneratedAssets.Root + "/Scenes/Graybox.unity";
        public const string VolumeProfilePath = GeneratedAssets.Root + "/Settings/PP_SunnyDiorama.asset";

        // Course anchor points (feet positions), shared with the scripted demos.
        public static readonly Vector3 BhopStart = new Vector3(0f, 3f, 21f);
        public static readonly Vector3 GapsStart = new Vector3(-18f, 2.5f, -25f);
        public static readonly Vector3 SurfStart = new Vector3(29f, 14f, -17f);
        public static readonly Vector3 PlazaReturn = new Vector3(0f, 0.1f, -2f);

        [MenuItem("How to Hunt/Rebuild Graybox Scene", priority = 2)]
        public static void Build()
        {
            GeneratedAssets.RebuildAll();
            GeneratedAssets.EnsureFolder(GeneratedAssets.Root + "/Scenes");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildLighting();
            Transform level = new GameObject("Level").transform;
            Transform zones = new GameObject("Zones").transform;
            Transform signs = new GameObject("Signs").transform;

            // Ground and boundary walls.
            Box(level, "Ground", new Vector3(0, -0.5f, 0), new Vector3(160, 1, 160), "Grass");
            // North wall: a walking gate into the Plains at x = -30, and the bhop course passes through (fenced on the
            // Plains side, GrayboxSceneBuilder.Plains.cs).
            Box(level, "Wall N West", new Vector3(-59.5f, 3, 80.5f), new Vector3(43, 8, 1), "Wall");
            Box(level, "Wall N Mid", new Vector3(-15.25f, 3, 80.5f), new Vector3(13.5f, 8, 1), "Wall");
            Box(level, "Wall N East", new Vector3(44.75f, 3, 80.5f), new Vector3(72.5f, 8, 1), "Wall");
            Box(level, "Wall S", new Vector3(0, 3, -80.5f), new Vector3(162, 8, 1), "Wall");
            Box(level, "Wall E", new Vector3(80.5f, 3, 0), new Vector3(1, 8, 160), "Wall");
            Box(level, "Wall W", new Vector3(-80.5f, 3, 0), new Vector3(1, 8, 160), "Wall");

            // Spawn plaza with three teleport pads.
            Box(level, "Plaza", new Vector3(0, 0.05f, 0), new Vector3(14, 0.1f, 14), "Sand");
            Transform spawns = new GameObject("Spawns").transform;
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                var sp = new GameObject($"Spawn {i + 1}", typeof(SpawnPoint));
                sp.transform.SetParent(spawns, false);
                sp.transform.SetPositionAndRotation(CampCenter + new Vector3(Mathf.Sin(a) * 4.5f, 0.1f, Mathf.Cos(a) * 4.5f - 2f), Quaternion.identity);
            }
            Transform plazaReturn = Marker(zones, "Plaza Return", PlazaReturn, 0f);
            Pad(level, zones, signs, "BHOP", new Vector3(5f, 0.1f, 5f), Marker(zones, "Bhop Start", BhopStart, 0f));
            Pad(level, zones, signs, "GAPS", new Vector3(-5f, 0.1f, 5f), Marker(zones, "Gaps Start", GapsStart, 270f));
            Pad(level, zones, signs, "SURF", new Vector3(5f, 0.1f, -5f), Marker(zones, "Surf Start", SurfStart, 90f));
            // The full armory (owner, 2026-11-24): every gun and attachment, to try them all. No keeper: walk up to it.
            Station(level, signs, CampStationKind.Workshop, "Armory", "ARMORY\n<size=55%>every gun, every attachment</size>",
                new Vector3(-4.5f, 0.1f, -3.5f), 180f, fullArmory: true);

            BuildBhopCourse(level, zones, signs, plazaReturn);
            BuildGapsCourse(level, zones, signs, plazaReturn);
            BuildSurfCourse(level, zones, signs, plazaReturn);
            BuildSlopeGallery(level, signs);
            BuildLedgesAndStairs(level, signs);
            BuildSprintLane(level, signs);
            BuildShootingRange(level, signs);
            BuildPlains(level, signs);
            BuildEastRoute(level, zones, signs);
            BuildPineForest(level, zones, signs);
            BuildRedDesert(level, zones, signs);
            BuildDesertRoute(level, zones, signs);
            StreamAreas(level);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            MovementLabTools.ExportWorld();
            Debug.Log($"[HowToHunt] Movement lab saved to {ScenePath}");
        }

        private static void BuildLighting()
        {
            var sun = new GameObject("Sun", typeof(Light));
            Light light = sun.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.94f, 0.82f);
            light.intensity = 1.5f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.7f;
            sun.transform.rotation = Quaternion.Euler(46f, -35f, 0f);
            RenderSettings.sun = light;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.6f, 0.72f, 0.88f);
            RenderSettings.ambientEquatorColor = new Color(0.66f, 0.68f, 0.6f);
            RenderSettings.ambientGroundColor = new Color(0.38f, 0.36f, 0.3f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.74f, 0.84f, 0.93f);
            RenderSettings.fogStartDistance = 110f;
            RenderSettings.fogEndDistance = 620f; // far enough to see the horizon mountains

            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            camGo.transform.SetPositionAndRotation(new Vector3(0, 14, -26), Quaternion.Euler(25, 0, 0));
            Camera cam = camGo.GetComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 900f;
            cam.clearFlags = CameraClearFlags.Skybox;
            UniversalAdditionalCameraData camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            var volumeGo = new GameObject("Post Processing", typeof(Volume));
            Volume volume = volumeGo.GetComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = EnsureVolumeProfile();
        }

        /// <summary>Art direction A: neutral tonemapping, softened saturation, gentle bloom and vignette.</summary>
        private static VolumeProfile EnsureVolumeProfile()
        {
            GeneratedAssets.EnsureFolder(Path.GetDirectoryName(VolumeProfilePath)!.Replace('\\', '/'));
            AssetDatabase.DeleteAsset(VolumeProfilePath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumeProfilePath);

            Tonemapping tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);
            ColorAdjustments color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.15f);
            color.saturation.Override(-12f);
            color.contrast.Override(8f);
            Bloom bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.35f);
            bloom.threshold.Override(1.1f);
            bloom.scatter.Override(0.6f);
            Vignette vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.18f);
            vignette.smoothness.Override(0.4f);
            foreach (VolumeComponent c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        /// <summary>North: bhop platforms (tops at 3 m) with gaps growing from 2.0 to 4.75 m over a hazard pit.</summary>
        private static void BuildBhopCourse(Transform level, Transform zones, Transform signs, Transform plazaReturn)
        {
            Transform root = Group(level, "Bhop Course");
            Box(root, "Bhop Start", new Vector3(0, 1.5f, 22f), new Vector3(6, 3, 6), "Rock");
            Zone(zones, "Bhop Checkpoint", new Vector3(0, 4f, 22f), new Vector3(6, 2, 6), ZoneKind.Checkpoint);
            Sign(signs, "BHOP\n<size=60%>hold jump or scroll, strafe + turn</size>", new Vector3(0, 6.5f, 19.5f), 0f, 2.2f);
            float z = 25f;
            float gap = 2f;
            for (int i = 0; i < 12; i++)
            {
                z += gap;
                Box(root, $"Bhop {i + 1}", new Vector3(0, 1.5f, z + 1.1f), new Vector3(2.2f, 3f, 2.2f), "Bhop");
                z += 2.2f;
                gap += 0.25f;
            }
            z += gap;
            Box(root, "Bhop End", new Vector3(0, 1.5f, z + 3f), new Vector3(6, 3, 6), "Rock");
            Zone(zones, "Bhop Finish", new Vector3(0, 4f, z + 4.5f), new Vector3(6, 2, 3), ZoneKind.Teleport, plazaReturn);
            Zone(zones, "Bhop Pit", new Vector3(0, 0.6f, (25f + z) * 0.5f), new Vector3(16, 1.6f, z - 25f + 2f), ZoneKind.Hazard);
            Visual(root, "Bhop Pit Floor", new Vector3(0, 0.02f, (25f + z) * 0.5f), new Vector3(16, 0.04f, z - 25f), "Hazard");
        }

        /// <summary>West: long jumps of 2, 3, 4, 5 and 6 m between platforms 2.5 m up, over a hazard pit.</summary>
        private static void BuildGapsCourse(Transform level, Transform zones, Transform signs, Transform plazaReturn)
        {
            Transform root = Group(level, "Gaps Course");
            Box(root, "Gaps Start", new Vector3(-20f, 1.25f, -25f), new Vector3(6, 2.5f, 6), "Rock");
            Zone(zones, "Gaps Checkpoint", new Vector3(-20f, 3.5f, -25f), new Vector3(6, 2, 6), ZoneKind.Checkpoint);
            Sign(signs, "GAPS 2-6 m", new Vector3(-22.5f, 5.5f, -25f), 270f, 2f);
            float x = -23f;
            for (int i = 0; i < 5; i++)
            {
                float gap = 2f + i;
                x -= gap;
                Box(root, $"Gap Platform {i + 1}", new Vector3(x - 2f, 1.25f, -25f), new Vector3(4, 2.5f, 6), i % 2 == 0 ? "Wood" : "Crate");
                Sign(signs, $"{gap:0} m", new Vector3(x + gap * 0.5f, 3.6f, -21.8f), 270f, 1.2f);
                x -= 4f;
            }
            Box(root, "Gaps End", new Vector3(x - 3f, 1.25f, -25f), new Vector3(6, 2.5f, 6), "Rock");
            Zone(zones, "Gaps Finish", new Vector3(x - 4.5f, 3.5f, -25f), new Vector3(3, 2, 6), ZoneKind.Teleport, plazaReturn);
            float mid = (-23f + x) * 0.5f;
            Zone(zones, "Gaps Pit", new Vector3(mid, 0.5f, -25f), new Vector3(-23f - x + 2f, 1.4f, 10f), ZoneKind.Hazard);
            Visual(root, "Gaps Pit Floor", new Vector3(mid, 0.02f, -25f), new Vector3(-23f - x, 0.04f, 8f), "Hazard");
        }

        /// <summary>South-east: step off a 14 m tower beside a 53° surf ramp (normal.y ≈ 0.6 < 0.7) that descends 5° along the course.</summary>
        private static void BuildSurfCourse(Transform level, Transform zones, Transform signs, Transform plazaReturn)
        {
            Transform root = Group(level, "Surf Course");
            Box(root, "Surf Tower", new Vector3(28f, 7f, -17f), new Vector3(6, 14, 6), "Rock");
            Zone(zones, "Surf Checkpoint", new Vector3(28f, 15f, -17f), new Vector3(6, 2, 6), ZoneKind.Checkpoint);
            Sign(signs, "SURF\n<size=60%>step off onto the ramp, look down the course (left), hold strafe INTO the ramp</size>", new Vector3(28f, 18f, -14f), 90f, 2.2f);
            // One 58 m ramp descending 5° along the course so gravity turns into forward speed (like real surf maps).
            MeshBrush(root, "Surf Ramp", GeneratedAssets.SurfPrismMesh, new Vector3(40f, -1.5f, -42f), new Vector3(18, 12, 58), 0f, "SurfRamp", SurfaceFlags.Surf, -5f);
            Box(root, "Surf End", new Vector3(40f, 2f, -75f), new Vector3(12, 4, 6), "Rock");
            Zone(zones, "Surf Finish", new Vector3(40f, 5f, -76.5f), new Vector3(12, 2, 3), ZoneKind.Teleport, plazaReturn);
            Zone(zones, "Surf Pit", new Vector3(40f, 0.6f, -41f), new Vector3(30, 1.4f, 62f), ZoneKind.Hazard);
            Visual(root, "Surf Pit Floor", new Vector3(40f, 0.02f, -41f), new Vector3(30, 0.04f, 62f), "Hazard");
        }

        /// <summary>East: 3 m high ramps at 20-60°. Up to ~45° you walk up; steeper you slide (surf).</summary>
        private static void BuildSlopeGallery(Transform level, Transform signs)
        {
            Transform root = Group(level, "Slope Gallery");
            float[] angles = { 20f, 30f, 40f, 44f, 50f, 60f };
            for (int i = 0; i < angles.Length; i++)
            {
                float a = angles[i];
                float length = 3f / Mathf.Tan(a * Mathf.Deg2Rad);
                float x = 22f + i * 5f;
                MeshBrush(root, $"Slope {a:0}°", GeneratedAssets.WedgeMesh, new Vector3(x, 0f, 12f + length * 0.5f), new Vector3(4, 3, length), 0f,
                    a > 45.5f ? "SurfRamp" : "Ramp", a > 45.5f ? SurfaceFlags.Surf : SurfaceFlags.None);
                Sign(signs, $"{a:0}°", new Vector3(x, 1.2f, 11f), 0f, 1.4f);
            }
        }

        private static void BuildLedgesAndStairs(Transform level, Transform signs)
        {
            Transform stairs = Group(level, "Stairs");
            for (int i = 0; i < 8; i++)
                Box(stairs, $"Step {i + 1}", new Vector3(16, 0.15f + i * 0.3f, -4 + i * 0.8f), new Vector3(4, 0.3f, 0.8f), "Wood");
            Box(stairs, "Landing", new Vector3(16, 1.2f, 3.2f), new Vector3(4, 2.4f, 2.4f), "Wood");
            Sign(signs, "STAIRS 30 cm", new Vector3(16, 1.5f, -5.5f), 0f, 1.2f);

            Transform ledges = Group(level, "Ledges");
            Box(ledges, "Ledge 1.2 m", new Vector3(-10, 0.6f, -12), new Vector3(4, 1.2f, 3), "Crate");
            Sign(signs, "JUMP 1.2 m", new Vector3(-10, 2.2f, -13.6f), 0f, 1.2f);
            Box(ledges, "Ledge 1.6 m", new Vector3(-16, 0.8f, -12), new Vector3(4, 1.6f, 3), "Crate");
            Sign(signs, "CROUCH-JUMP 1.6 m\n<size=60%>jump, then hold Ctrl</size>", new Vector3(-16, 2.7f, -13.6f), 0f, 1.2f);
            Box(ledges, "Low Tunnel Roof", new Vector3(-23, 1.95f, -12), new Vector3(4, 0.5f, 6), "Rock");
            Box(ledges, "Low Tunnel Wall L", new Vector3(-25.25f, 0.85f, -12), new Vector3(0.5f, 1.7f, 6), "Rock");
            Box(ledges, "Low Tunnel Wall R", new Vector3(-20.75f, 0.85f, -12), new Vector3(0.5f, 1.7f, 6), "Rock");
            Sign(signs, "CROUCH 1.7 m", new Vector3(-23, 2.9f, -15.2f), 0f, 1.2f);
        }

        /// <summary>South-west: 70 m lane with a marker every 5 m to judge speed and distance.</summary>
        private static void BuildSprintLane(Transform level, Transform signs)
        {
            Transform root = Group(level, "Sprint Lane");
            for (int i = 0; i < 14; i++)
                Visual(root, $"Stripe {i * 5}", new Vector3(-9f - i * 5f - 2.5f, 0.006f, 8f), new Vector3(5f, 0.012f, 4f), i % 2 == 0 ? "Sand" : "Stripe");
            for (int i = 0; i <= 14; i += 2)
                Sign(signs, $"{i * 5} m", new Vector3(-9f - i * 5f, 0.8f, 10.5f), 270f, 1f);
        }

        /// <summary>South: firing line and three moving target dummies to prove lag-compensated hits (M2).</summary>
        private static void BuildShootingRange(Transform level, Transform signs)
        {
            Transform root = Group(level, "Shooting Range");
            Box(root, "Firing Line", new Vector3(8f, 0.05f, -22f), new Vector3(12f, 0.1f, 3f), "Sand");
            Box(root, "Range Backstop", new Vector3(8f, 3f, -58f), new Vector3(30f, 6f, 1f), "Wall");
            Box(root, "Range Side L", new Vector3(-7.5f, 1f, -40f), new Vector3(1f, 2f, 36f), "Wall");
            Box(root, "Range Side R", new Vector3(23.5f, 1f, -40f), new Vector3(1f, 2f, 36f), "Wall");
            Sign(signs, "RANGE\n<size=60%>left click to shoot - cl_showhits 1 shows what the host checked</size>", new Vector3(8f, 3.5f, -23.5f), 180f, 1.6f);
            Dummy(root, "Target Dummy A", new Vector3(2f, 0f, -34f), new Vector3(14f, 0f, -34f), 3f);
            Dummy(root, "Target Dummy B", new Vector3(0f, 0f, -44f), new Vector3(16f, 0f, -44f), 6f);
            Dummy(root, "Target Dummy C", new Vector3(-2f, 0f, -54f), new Vector3(18f, 0f, -54f), 9f);
        }

        private static void Dummy(Transform parent, string name, Vector3 start, Vector3 end, float speed)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = start;
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.name = "Body";
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            body.transform.localScale = new Vector3(0.64f, 0.77f, 0.64f);
            body.GetComponent<MeshRenderer>().sharedMaterial = Palette.Get("Ramp");
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(head.GetComponent<Collider>());
            head.name = "Head";
            head.transform.SetParent(go.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.585f, 0f);
            head.transform.localScale = Vector3.one * 0.38f;
            head.GetComponent<MeshRenderer>().sharedMaterial = Palette.Get("Wall");
            GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(nose.GetComponent<Collider>());
            nose.name = "Facing";
            nose.transform.SetParent(go.transform, false);
            nose.transform.localPosition = new Vector3(0f, 1.1f, 0.3f);
            nose.transform.localScale = new Vector3(0.3f, 0.2f, 0.12f);
            nose.GetComponent<MeshRenderer>().sharedMaterial = Palette.Get("Visor");
            go.AddComponent<Hunting.TargetDummy>().Configure(start, end, speed);
        }

        // --- building blocks -------------------------------------------------------------------------------

        private static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        private static Transform Marker(Transform parent, string name, Vector3 feet, float yaw)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(feet + new Vector3(0, 0.02f, 0), Quaternion.Euler(0, yaw, 0));
            return t;
        }

        private static void Pad(Transform level, Transform zones, Transform signs, string label, Vector3 feet, Transform target)
        {
            Visual(level, $"Pad {label}", feet + new Vector3(0, 0.02f, 0), new Vector3(2.2f, 0.04f, 2.2f), "Pad");
            Zone(zones, $"Teleport {label}", feet + new Vector3(0, 1f, 0), new Vector3(1.6f, 2f, 1.6f), ZoneKind.Teleport, target);
            float yaw = Mathf.Atan2(feet.x, feet.z) * Mathf.Rad2Deg;
            Sign(signs, label, feet + new Vector3(0, 2.4f, 0), yaw, 0.9f);
        }

        private static void Zone(Transform parent, string name, Vector3 center, Vector3 size, ZoneKind kind, Transform target = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = size;
            Core.Zone zone = go.AddComponent<Core.Zone>();
            zone.Kind = kind;
            zone.Target = target;
        }

        /// <summary>World-space label. Readable when looking along the given yaw.</summary>
        private static void Sign(Transform parent, string text, Vector3 position, float yaw, float size)
        {
            var go = new GameObject($"Sign {text.Split('\n')[0]}");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            TextMeshPro tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            // The display font on every sign (generated by GeneratedAssets.EnsureDisplayFont before the scene is built).
            var display = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GeneratedAssets.DisplayFontPath);
            if (display != null) tmp.font = display;
            tmp.fontSize = size * 4.6f;
            tmp.alignment = TextAlignmentOptions.Center;
            // Dark slate reads well against the bright sky and walls (generated signs can't keep a per-sign outline material).
            tmp.color = new Color(0.16f, 0.21f, 0.26f);
            tmp.rectTransform.sizeDelta = new Vector2(12f, 4f);
            tmp.textWrappingMode = TextWrappingModes.Normal;
        }

        private static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, string material, float yaw = 0f)
        {
            GameObject go = Visual(parent, name, center, size, material, yaw);
            go.AddComponent<SimBrush>().Shape = SimBrush.ShapeKind.Box;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            return go;
        }

        /// <summary>Cube with no collision (decals, stripes, pads).</summary>
        private static GameObject Visual(Transform parent, string name, Vector3 center, Vector3 size, string material, float yaw = 0f)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(center, Quaternion.Euler(0, yaw, 0));
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = Palette.Get(material);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        private static GameObject MeshBrush(Transform parent, string name, Mesh mesh, Vector3 basePos, Vector3 size, float yaw, string material, SurfaceFlags flags, float pitch = 0f)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(basePos, Quaternion.Euler(pitch, yaw, 0));
            go.transform.localScale = size;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = Palette.Get(material);
            SimBrush brush = go.AddComponent<SimBrush>();
            brush.Shape = SimBrush.ShapeKind.ConvexMesh;
            brush.Surface = flags;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
            return go;
        }

        /// <summary>
        /// The world's chain for StreamedArea, start to end, maps and the surf routes between them interleaved. The
        /// lab's loose pieces are gathered under one root first. Each area's bounds are the area itself, without its
        /// horizon (Backdrop layer) or the invisible collision boxes; its bare renderers are its terrain (and the lab's
        /// floor and walls), all that shows while it's a neighbour.
        /// </summary>
        private static readonly (string name, bool route)[] Chain =
        {
            (LabArea, false), ("Plains", false), ("Surf Route East", true), ("Pine Forest", false),
            ("Surf Route South (hard)", true), ("Red Desert", false),
        };

        private const string LabArea = "Movement Lab";

        private static void StreamAreas(Transform level)
        {
            var lab = new GameObject(LabArea).transform;
            lab.SetParent(level, false);
            var loose = new List<Transform>();
            foreach (Transform child in level)
                if (child != lab && System.Array.FindIndex(Chain, c => c.name == child.name) < 0) loose.Add(child);
            foreach (Transform t in loose) t.SetParent(lab, true);

            for (int order = 0; order < Chain.Length; order++)
            {
                (string name, bool route) = Chain[order];
                Transform root = level.Find(name);
                if (root == null) { Debug.LogWarning($"[HowToHunt] No area '{name}' to stream"); continue; }
                Bounds b = default;
                bool any = false;
                var bare = new List<Renderer>();
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.GetComponent<SimTerrain>() != null || (root == lab && (r.name == "Ground" || r.name.StartsWith("Wall ", System.StringComparison.Ordinal)))) bare.Add(r);
                    if (!r.enabled || r.gameObject.layer == BackdropLayer || r is ParticleSystemRenderer || r.bounds.size == Vector3.zero) continue;
                    if (!any) { b = r.bounds; any = true; }
                    else b.Encapsulate(r.bounds);
                }
                if (!any) continue;
                root.gameObject.AddComponent<StreamedArea>().Configure(b, order, route, bare.ToArray());
            }
        }

        private static void AddToBuildSettings(string path)
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.Where(s => s.path != path && s.path != "Assets/Scenes/SampleScene.unity").ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
