using System.Collections.Generic;
using HowToHunt.Core;
using HowToHunt.Economy;
using HowToHunt.Hunting;
using UnityEditor;
using UnityEngine;

namespace HowToHunt.Editor
{
    /// <summary>
    /// The Plains (M3 slice): a walled meadow north of the movement lab. Camp by the gate (spawns, campfire, trader,
    /// outfitter), three flower fields with butterflies, three rabbit warrens, and Kenney Nature Kit props. Trees and
    /// big rocks get invisible box brushes so the movement sim collides with them; grass and flowers don't.
    /// Deterministic (seeded), so rebuilding gives the same map.
    /// </summary>
    public static partial class GrayboxSceneBuilder
    {
        public static readonly Vector3 CampCenter = new Vector3(-30f, 0f, 99f);
        private const float GateX = -30f;
        private const float PlainsMinZ = 80f, PlainsMaxZ = 220f, PlainsHalfWidth = 80f;
        private const string KenneyDir = GeneratedAssets.Root + "/Art/ThirdParty/KenneyNatureKit/";

        private static readonly Vector3[] FlowerFields = { new Vector3(-32f, 0f, 130f), new Vector3(30f, 0f, 152f), new Vector3(-6f, 0f, 190f) };
        private static readonly Vector3[] Warrens = { new Vector3(-46f, 0f, 172f), new Vector3(44f, 0f, 118f), new Vector3(30f, 0f, 200f) };

        private static void BuildPlains(Transform level, Transform signs)
        {
            Transform plains = Group(level, "Plains");
            Transform props = Group(plains, "Props");
            var rng = new System.Random(20261115);

            // Ground and walls.
            Box(plains, "Plains Ground", new Vector3(0f, -0.5f, (PlainsMinZ + PlainsMaxZ) * 0.5f), new Vector3(162f, 1f, PlainsMaxZ - PlainsMinZ), "Grass");
            Box(plains, "Plains Wall N", new Vector3(0f, 3f, PlainsMaxZ + 0.5f), new Vector3(162f, 8f, 1f), "Wall");
            Box(plains, "Plains Wall E", new Vector3(PlainsHalfWidth + 0.5f, 3f, (PlainsMinZ + PlainsMaxZ) * 0.5f), new Vector3(1f, 8f, PlainsMaxZ - PlainsMinZ), "Wall");
            Box(plains, "Plains Wall W", new Vector3(-PlainsHalfWidth - 0.5f, 3f, (PlainsMinZ + PlainsMaxZ) * 0.5f), new Vector3(1f, 8f, PlainsMaxZ - PlainsMinZ), "Wall");
            // Gate: a lintel over the opening, one sign on each face (readable when walking through).
            Box(plains, "Gate Lintel", new Vector3(GateX, 6.5f, 80.5f), new Vector3(16f, 1f, 1.2f), "Wood");
            Sign(signs, "PLAINS\n<size=55%>hunt, sell at the trader, upgrade at the outfitter</size>", new Vector3(GateX, 4.9f, 79.8f), 0f, 1.4f);
            Sign(signs, "MOVEMENT LAB", new Vector3(GateX, 5.3f, 81.2f), 180f, 1.2f);
            Visual(plains, "Gate Path", new Vector3(GateX, 0.012f, 85f), new Vector3(6f, 0.02f, 10f), "Sand");

            // The bhop course runs on past the old north wall: keep it in its own enclosure.
            Box(plains, "Bhop Enclosure W", new Vector3(-8.75f, 3f, 92.75f), new Vector3(0.5f, 8f, 24.5f), "Wall");
            Box(plains, "Bhop Enclosure E", new Vector3(8.75f, 3f, 92.75f), new Vector3(0.5f, 8f, 24.5f), "Wall");
            Box(plains, "Bhop Enclosure N", new Vector3(0f, 3f, 105.25f), new Vector3(18f, 8f, 0.5f), "Wall");

            BuildCamp(plains, props, signs);

            // Butterfly fields: dense flowers, a few bushes around.
            string[] flowers = { "flower_purpleA", "flower_purpleB", "flower_redA", "flower_redB", "flower_yellowA", "flower_yellowB" };
            for (int f = 0; f < FlowerFields.Length; f++)
            {
                Vector3 c = FlowerFields[f];
                for (int i = 0; i < 70; i++) Prop(props, flowers[rng.Next(flowers.Length)], c + Disc(rng, 10f), 2.2f, rng, collide: false);
                for (int i = 0; i < 6; i++) Prop(props, "plant_bush", c + Ring(rng, 11f, 14f), 3f, rng, collide: false);
                Spawner(plains, $"Butterflies {f + 1}", "prey.butterfly", c + new Vector3(0f, 1.3f, 0f), 5, 8f, 15f);
            }

            // Rabbit warrens: low grass, logs and stumps to hop around.
            for (int w = 0; w < Warrens.Length; w++)
            {
                Vector3 c = Warrens[w];
                for (int i = 0; i < 25; i++) Prop(props, rng.Next(2) == 0 ? "grass_large" : "grass_leafsLarge", c + Disc(rng, 12f), 2.5f, rng, collide: false);
                Prop(props, "log_large", c + Ring(rng, 4f, 7f), 3f, rng, collide: true);
                Prop(props, "stump_round", c + Ring(rng, 5f, 9f), 3f, rng, collide: true);
                Spawner(plains, $"Rabbits {w + 1}", "prey.rabbit", c + new Vector3(0f, 0.2f, 0f), 3, 12f, 20f);
            }

            // Tree line along the walls, a few clumps in the open, rocks and bushes, grass everywhere.
            string[] trees = { "tree_default", "tree_oak", "tree_fat", "tree_detailed", "tree_tall", "tree_pineRoundA", "tree_pineRoundC" };
            for (int i = 0; i < 70; i++)
            {
                Vector3 p = EdgePoint(rng, 9f);
                if (NearCamp(p, 18f) || Reserved(p, 2f)) continue;
                Prop(props, trees[rng.Next(trees.Length)], p, Range(rng, 4f, 5.2f), rng, collide: true, trunkOnly: true);
            }
            for (int clump = 0; clump < 6; clump++)
            {
                Vector3 c = OpenPoint(rng, 12f);
                for (int i = 0; i < 4; i++) Prop(props, trees[rng.Next(trees.Length)], c + Disc(rng, 5f), Range(rng, 4f, 5f), rng, collide: true, trunkOnly: true);
                Prop(props, "rock_largeA", c + Ring(rng, 5f, 7f), 3f, rng, collide: true);
            }
            string[] rocks = { "rock_largeA", "rock_largeB", "rock_largeC", "rock_tallA", "stone_tallA" };
            for (int i = 0; i < 22; i++) Prop(props, rocks[rng.Next(rocks.Length)], OpenPoint(rng, 6f), Range(rng, 2.5f, 3.5f), rng, collide: true);
            string[] small = { "rock_smallA", "rock_smallB", "rock_smallC", "mushroom_redGroup", "mushroom_tanGroup", "plant_bushSmall", "plant_bushDetailed" };
            for (int i = 0; i < 60; i++) Prop(props, small[rng.Next(small.Length)], OpenPoint(rng, 3f), Range(rng, 2.2f, 3f), rng, collide: false);
            string[] grass = { "grass", "grass_large", "grass_leafs" };
            for (int i = 0; i < 320; i++) Prop(props, grass[rng.Next(grass.Length)], OpenPoint(rng, 1f), Range(rng, 2.2f, 3f), rng, collide: false);
        }

        private static void BuildCamp(Transform plains, Transform props, Transform signs)
        {
            Transform camp = Group(plains, "Camp");
            Vector3 c = CampCenter;
            Visual(camp, "Camp Ground", c + new Vector3(0f, 0.012f, 0f), new Vector3(28f, 0.02f, 18f), "Sand");
            var rng = new System.Random(96);

            Prop(camp, "campfire_stones", c + new Vector3(0f, 0f, 2f), 3f, rng, collide: false, yaw: 0f);
            Prop(camp, "campfire_logs", c + new Vector3(0f, 0f, 2f), 3f, rng, collide: false, yaw: 30f);
            Campfire(camp, c + new Vector3(0f, 0.25f, 2f));
            Prop(camp, "log", c + new Vector3(-2.6f, 0f, 2.2f), 3f, rng, collide: false, yaw: 80f);
            Prop(camp, "log", c + new Vector3(2.6f, 0f, 2.6f), 3f, rng, collide: false, yaw: 100f);
            Prop(camp, "tent_detailedOpen", c + new Vector3(-10f, 0f, 6f), 4f, rng, collide: true, yaw: 150f);
            Prop(camp, "tent_smallOpen", c + new Vector3(10.5f, 0f, 6.5f), 4f, rng, collide: true, yaw: 210f);
            for (int i = 0; i < 7; i++)
            {
                float x = -12f + i * 4f;
                if (Mathf.Abs(x) < 4f) continue;
                Prop(camp, "fence_simple", c + new Vector3(x, 0f, -8.6f), 3f, rng, collide: false, yaw: 0f);
            }

            // Trader: sells the bag. Outfitter: the upgrade shop.
            Station(camp, signs, CampStationKind.Sell, "Trader", "TRADER\n<size=55%>sell your catch</size>", c + new Vector3(8f, 0f, -1f), 90f);
            Station(camp, signs, CampStationKind.Shop, "Outfitter", "OUTFITTER\n<size=55%>tools and upgrades</size>", c + new Vector3(-8f, 0f, -1f), 270f);
            Prop(camp, "log_stack", c + new Vector3(-10.5f, 0f, -2.5f), 3f, rng, collide: true, yaw: 90f);
            Prop(camp, "pot_large", c + new Vector3(10.3f, 0f, -2.8f), 3f, rng, collide: false, yaw: 0f);
            Prop(camp, "crop_pumpkin", c + new Vector3(9.4f, 0f, 1.3f), 3f, rng, collide: false, yaw: 20f);
        }

        /// <summary>Chunky cube flames (art direction A) and a warm flickerless light; built into the scene, no code at runtime.</summary>
        private static void Campfire(Transform camp, Vector3 position)
        {
            var go = new GameObject("Campfire Flames");
            go.transform.SetParent(camp, false);
            go.transform.position = position;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.26f);
            main.startRotation3D = true;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.12f), new Color(1f, 0.85f, 0.3f));
            main.maxParticles = 64;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 26f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.35f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.9f, 0.5f), 0f), new GradientColorKey(new Color(0.9f, 0.25f, 0.05f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            r.mesh = cube.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(cube);
            r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(GeneratedAssets.ResourcesDir + "/FX/M_Flame.mat");
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var lightGo = new GameObject("Campfire Light", typeof(Light));
            lightGo.transform.SetParent(camp, false);
            lightGo.transform.position = position + Vector3.up * 0.8f;
            Light light = lightGo.GetComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.62f, 0.3f);
            light.range = 7f;
            light.intensity = 2.5f;
            light.shadows = LightShadows.None;
        }

        private static void Station(Transform camp, Transform signs, CampStationKind kind, string label, string signText, Vector3 feet, float yaw)
        {
            Transform t = Group(camp, label);
            t.SetPositionAndRotation(feet, Quaternion.Euler(0f, yaw, 0f));
            Vector3 fwd = t.forward;
            Vector3 right = t.right;
            // Counter with an awning on two posts.
            Box(t, "Counter", feet + fwd * 1.2f + new Vector3(0f, 0.5f, 0f), new Vector3(3.2f, 1f, 1f), "Wood", yaw);
            Visual(t, "Counter Top", feet + fwd * 1.2f + new Vector3(0f, 1.03f, 0f), new Vector3(3.4f, 0.08f, 1.15f), "Crate", yaw);
            Box(t, "Post L", feet + fwd * 1.8f - right * 1.6f + new Vector3(0f, 1.3f, 0f), new Vector3(0.18f, 2.6f, 0.18f), "Wood", yaw);
            Box(t, "Post R", feet + fwd * 1.8f + right * 1.6f + new Vector3(0f, 1.3f, 0f), new Vector3(0.18f, 2.6f, 0.18f), "Wood", yaw);
            Visual(t, "Awning", feet + fwd * 1.3f + new Vector3(0f, 2.65f, 0f), new Vector3(3.8f, 0.12f, 1.8f), kind == CampStationKind.Sell ? "Bhop" : "Surf", yaw);
            Sign(signs, signText, feet + fwd * 1.85f + new Vector3(0f, 3.35f, 0f), yaw, 1.1f);

            var station = new GameObject($"Station {label}");
            station.transform.SetParent(t, false);
            station.transform.position = feet;
            station.AddComponent<CampStation>().Configure(kind, 2.8f, label);
        }

        private static void Spawner(Transform parent, string name, string preyKey, Vector3 position, int count, float radius, float respawn)
        {
            var prey = AssetDatabase.LoadAssetAtPath<PreyDefinition>(FindContent(preyKey));
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.AddComponent<PreySpawner>().Configure(prey, count, radius, respawn);
        }

        private static string FindContent(string key)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:PreyDefinition", new[] { GeneratedContent.ContentDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.LoadAssetAtPath<PreyDefinition>(path)?.Key == key) return path;
            }
            Debug.LogWarning($"[HowToHunt] Content '{key}' not found.");
            return string.Empty;
        }

        /// <summary>Places a Kenney prop; collide adds an invisible box brush from its bounds (trunk only for trees).</summary>
        private static void Prop(Transform parent, string model, Vector3 feet, float scale, System.Random rng, bool collide, bool trunkOnly = false, float yaw = float.NaN)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(KenneyDir + model + ".fbx");
            if (asset == null) { Debug.LogWarning($"[HowToHunt] Missing Kenney model {model}"); return; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            float y = float.IsNaN(yaw) ? (float)rng.NextDouble() * 360f : yaw;
            go.transform.SetPositionAndRotation(feet, Quaternion.Euler(0f, y, 0f));
            go.transform.localScale = Vector3.one * scale;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            foreach (Transform child in go.GetComponentsInChildren<Transform>()) GameObjectUtility.SetStaticEditorFlags(child.gameObject, StaticEditorFlags.BatchingStatic);
            if (!collide) return;

            Bounds b = default;
            bool any = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            if (!any) return;
            Vector3 size = trunkOnly ? new Vector3(0.12f * scale, b.size.y, 0.12f * scale) : Vector3.Scale(b.size, new Vector3(0.8f, 1f, 0.8f));
            Vector3 center = trunkOnly ? new Vector3(feet.x, feet.y + b.size.y * 0.5f, feet.z) : b.center;
            GameObject brush = Box(parent, model + " Collision", center, size, "Rock");
            brush.GetComponent<MeshRenderer>().enabled = false;
        }

        private static Vector3 Disc(System.Random rng, float radius)
        {
            double a = rng.NextDouble() * Mathf.PI * 2.0, d = System.Math.Sqrt(rng.NextDouble()) * radius;
            return new Vector3((float)(System.Math.Sin(a) * d), 0f, (float)(System.Math.Cos(a) * d));
        }

        private static Vector3 Ring(System.Random rng, float min, float max)
        {
            double a = rng.NextDouble() * Mathf.PI * 2.0, d = min + rng.NextDouble() * (max - min);
            return new Vector3((float)(System.Math.Sin(a) * d), 0f, (float)(System.Math.Cos(a) * d));
        }

        private static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        /// <summary>A point within <paramref name="band"/> metres of the Plains walls.</summary>
        private static Vector3 EdgePoint(System.Random rng, float band)
        {
            int side = rng.Next(4);
            float along = (float)rng.NextDouble();
            float inset = 2f + (float)rng.NextDouble() * band;
            switch (side)
            {
                case 0: return new Vector3(-PlainsHalfWidth + inset, 0f, Mathf.Lerp(PlainsMinZ + 4f, PlainsMaxZ - 2f, along));
                case 1: return new Vector3(PlainsHalfWidth - inset, 0f, Mathf.Lerp(PlainsMinZ + 4f, PlainsMaxZ - 2f, along));
                case 2: return new Vector3(Mathf.Lerp(-PlainsHalfWidth + 2f, PlainsHalfWidth - 2f, along), 0f, PlainsMaxZ - inset);
                default: return new Vector3(Mathf.Lerp(-PlainsHalfWidth + 2f, PlainsHalfWidth - 2f, along), 0f, PlainsMinZ + 1.5f + inset * 0.5f);
            }
        }

        /// <summary>A point in the open meadow, away from camp, the gate path and the hunting grounds' centres.</summary>
        private static Vector3 OpenPoint(System.Random rng, float clearance)
        {
            for (int attempt = 0; attempt < 40; attempt++)
            {
                var p = new Vector3(Range(rng, -PlainsHalfWidth + 4f, PlainsHalfWidth - 4f), 0f, Range(rng, PlainsMinZ + 6f, PlainsMaxZ - 4f));
                if (NearCamp(p, 16f + clearance)) continue;
                if (Reserved(p, clearance)) continue;
                bool blocked = false;
                foreach (Vector3 f in FlowerFields) if ((p - f).sqrMagnitude < (6f + clearance) * (6f + clearance)) blocked = true;
                foreach (Vector3 w in Warrens) if ((p - w).sqrMagnitude < (4f + clearance) * (4f + clearance)) blocked = true;
                if (!blocked) return p;
            }
            return new Vector3(Range(rng, -60f, 60f), 0f, Range(rng, 160f, 210f));
        }

        private static bool NearCamp(Vector3 p, float radius) => (new Vector3(p.x, 0f, p.z) - CampCenter).sqrMagnitude < radius * radius;

        /// <summary>The gate path and the bhop enclosure.</summary>
        private static bool Reserved(Vector3 p, float clearance)
            => (Mathf.Abs(p.x - GateX) < 4f + clearance && p.z < 90f) || (Mathf.Abs(p.x) < 10f + clearance && p.z < 107f + clearance);
    }
}
