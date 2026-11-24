using System.Collections.Generic;
using HowToHunt.Core;
using HowToHunt.Economy;
using UnityEditor;
using UnityEngine;

namespace HowToHunt.Editor
{
    /// <summary>
    /// The third biome, the Red Desert (started 2026-11-24): a basin of low dunes ringed by striped red mesas, south of
    /// the movement lab and far from the other maps. Dry Creek camp (Rosa's trading post, prices x2; Dusty by the fire),
    /// saguaros and barrel cacti, rock piles where lizards bask and rattlesnakes wait, scorpion flats, jackrabbit runs
    /// and coyote country. The way in is a pad at Ash's forest camp that appears once the Old Bear has fallen (the
    /// forest boss flag); the movement lab has a test pad. Pads at Dry Creek go back to the forest and to the Plains.
    /// Deterministic like the other maps, with its own random sequence.
    /// </summary>
    public static partial class GrayboxSceneBuilder
    {
        private const float DesertY = 0f;
        private const float DMinX = 380f, DMaxX = 520f, DMinZ = -640f, DMaxZ = -500f, DCorner = 30f, DBand = 26f;
        private const double DesertExpectedSale = 220;
        private const float DesertPriceMultiplier = 2f;
        private static readonly Vector3 DesertCamp = new Vector3(410f, DesertY, -530f);
        private static readonly Vector3[] JackrabbitRuns = { new Vector3(470f, 0f, -540f), new Vector3(440f, 0f, -600f), new Vector3(500f, 0f, -590f) };
        private static readonly Vector3[] LizardRocks = { new Vector3(430f, 0f, -560f), new Vector3(490f, 0f, -520f), new Vector3(405f, 0f, -610f) };
        private static readonly Vector3[] ScorpionFlats = { new Vector3(455f, 0f, -575f), new Vector3(500f, 0f, -625f), new Vector3(420f, 0f, -585f) };
        private static readonly Vector3[] SnakeRocks = { new Vector3(475f, 0f, -560f), new Vector3(398f, 0f, -572f), new Vector3(470f, 0f, -625f) };
        private static readonly Vector3[] CoyoteDens = { new Vector3(505f, 0f, -555f), new Vector3(450f, 0f, -625f) };

        private static float OutsideDesert(float x, float z)
        {
            float cx = (DMinX + DMaxX) * 0.5f, cz = (DMinZ + DMaxZ) * 0.5f;
            float hx = (DMaxX - DMinX) * 0.5f - DCorner, hz = (DMaxZ - DMinZ) * 0.5f - DCorner;
            float dx = Mathf.Abs(x - cx) - hx, dz = Mathf.Abs(z - cz) - hz;
            float outside = new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dz, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(dx, dz), 0f) - DCorner;
        }

        private static float DesertFlat(float x, float z) =>
            1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(((new Vector2(x, z) - new Vector2(DesertCamp.x, DesertCamp.z)).magnitude - 13f) / 9f));

        /// <summary>Low dunes in long wavy ridges across the basin; terraced mesas rise all round the edge.</summary>
        private static float DesertHeight(float x, float z)
        {
            float ridge = Mathf.Sin(x * 0.075f + 1.6f * Mathf.Sin(z * 0.03f)) * 0.5f + 0.5f;
            float dunes = 1.6f * ridge * ridge + 0.6f * Noise(x, z, 24f, 31.2f) + 0.25f * Noise(x, z, 8f, 4.4f);
            float edge = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(OutsideDesert(x, z) / DBand));
            float mesa = edge * (16f + 6f * Noise(x, z, 34f, 9.9f));
            mesa = Mathf.Lerp(mesa, Mathf.Round(mesa / 3.2f) * 3.2f, 0.75f); // terraces: the striped walls
            return DesertY + Mathf.Lerp(dunes + mesa, 0f, DesertFlat(x, z));
        }

        private static Color DesertGroundColor(float x, float z, float y, float up)
        {
            var sand = new Color(0.9f, 0.74f, 0.5f);
            var dune = new Color(0.86f, 0.66f, 0.42f);
            var packed = new Color(0.76f, 0.58f, 0.4f);
            var rock = new Color(0.74f, 0.36f, 0.22f);
            var stripe = new Color(0.56f, 0.25f, 0.16f);
            Color c = Color.Lerp(sand, dune, Mathf.Clamp01(0.5f + 0.5f * Noise(x, z, 18f, 2.2f)));
            if (DesertFlat(x, z) > 0.5f) c = Color.Lerp(c, packed, 0.6f);
            float h = y - DesertY;
            if (h > 2.5f || up < 0.82f)
            {
                // Red rock, banded by height like canyon walls.
                Color band = Mathf.Repeat(h, 3.2f) < 1.1f ? stripe : rock;
                c = Color.Lerp(c, band, Mathf.Clamp01(Mathf.Max((h - 2.5f) / 2f, (0.82f - up) * 5f)));
            }
            float j = 0.035f * Noise(x * 7.1f, z * 7.1f, 1f, 6.5f);
            return new Color(c.r + j, c.g + j, c.b + j * 0.5f);
        }

        private static SimTerrain BuildDesertTerrain(Transform desert)
        {
            float minX = DMinX - DBand - 3f, maxX = DMaxX + DBand + 3f, minZ = DMinZ - DBand - 3f, maxZ = DMaxZ + DBand + 3f;
            int nx = Mathf.CeilToInt((maxX - minX) / TerrainCell) + 1, nz = Mathf.CeilToInt((maxZ - minZ) / TerrainCell) + 1;
            var heights = new float[nx * nz];
            for (int z = 0; z < nz; z++)
                for (int x = 0; x < nx; x++)
                    heights[z * nx + x] = DesertHeight(minX + x * TerrainCell, minZ + z * TerrainCell);

            var go = new GameObject("Desert Terrain", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(desert, false);
            SimTerrain terrain = go.AddComponent<SimTerrain>();
            terrain.Configure(minX, minZ, TerrainCell, nx, nz, heights);

            var verts = new List<Vector3>();
            var colors = new List<Color>();
            var tris = new List<int>();
            var field = terrain.Field;
            for (int z = 0; z < nz - 1; z++)
                for (int x = 0; x < nx - 1; x++)
                {
                    Vector3 v00 = field.Vertex(x, z).ToUnity(), v10 = field.Vertex(x + 1, z).ToUnity();
                    Vector3 v01 = field.Vertex(x, z + 1).ToUnity(), v11 = field.Vertex(x + 1, z + 1).ToUnity();
                    AddTri(verts, colors, tris, v00, v01, v11, DesertGroundColor);
                    AddTri(verts, colors, tris, v00, v11, v10, DesertGroundColor);
                }
            var mesh = new Mesh { name = "Desert Terrain", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            SaveGeneratedMesh(mesh, "DesertTerrain");
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(GeneratedAssets.ResourcesDir + "/FX/M_Terrain.mat");
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
            return terrain;
        }

        // --- the map ------------------------------------------------------------------------------------------------

        private static void BuildRedDesert(Transform level, Transform zones, Transform signs)
        {
            Transform desert = Group(level, "Red Desert");
            Transform props = Group(desert, "Props");
            var rng = new System.Random(20261125);
            SimTerrain previous = _terrain;
            _terrain = BuildDesertTerrain(desert); // GroundY/Prop/Instance now place things on the desert floor

            // The rim: red rock ridges you can see, a few metres into the mesas.
            Transform walls = Group(desert, "Desert Rim (collision)");
            const int segments = 112;
            var ring = new List<Vector3>();
            for (int i = 0; i < segments; i++) ring.Add(DesertRingPoint(i / (float)segments, 4f));
            for (int i = 0; i < segments; i++)
                RockWall(walls, $"Rim {i}", ring[i], ring[(i + 1) % segments], GroundY, DesertY + 45f, rng, "Mesa", "MesaDark");

            BuildDesertCamp(desert, props, zones, signs);

            // Rock formations in the basin: stacked red blocks with flat tops (collide, you can climb the low ones).
            for (int i = 0; i < 9; i++)
            {
                Vector3 c = DesertOpenPoint(rng, 8f);
                MesaStack(props, c, rng, Range(rng, 3f, 7f));
            }

            // The animals' places: rock piles for lizards and snakes, flats for scorpions, open runs for jackrabbits.
            string[] rocks = { "rock_largeA", "rock_largeB", "rock_largeC", "rock_tallA" };
            for (int i = 0; i < LizardRocks.Length; i++)
            {
                Vector3 c = OnGround(LizardRocks[i]);
                for (int k = 0; k < 3; k++) Prop(props, rocks[rng.Next(rocks.Length)], c + Ring(rng, 2f, 5f), Range(rng, 2.5f, 3.5f), rng, collide: true);
                Spawner(desert, $"Lizards {i + 1}", "prey.lizard", c + new Vector3(0f, 0.2f, 0f), 3, 7f, 25f);
            }
            for (int i = 0; i < SnakeRocks.Length; i++)
            {
                Vector3 c = OnGround(SnakeRocks[i]);
                for (int k = 0; k < 4; k++) Prop(props, rocks[rng.Next(rocks.Length)], c + Ring(rng, 3f, 6f), Range(rng, 3f, 4.2f), rng, collide: true);
                Prop(props, "rock_smallA", c + Ring(rng, 1.5f, 3f), 3f, rng, collide: false);
                Spawner(desert, $"Rattlesnakes {i + 1}", "prey.rattlesnake", c + new Vector3(0f, 0.2f, 0f), 2, 4f, 60f);
            }
            for (int i = 0; i < ScorpionFlats.Length; i++)
            {
                Vector3 c = OnGround(ScorpionFlats[i]);
                Visual(props, $"Scorpion Flat {i + 1}", c + new Vector3(0f, 0.02f, 0f), new Vector3(9f, 0.04f, 7f), "Adobe", Range(rng, 0f, 90f));
                Spawner(desert, $"Scorpions {i + 1}", "prey.scorpion", c + new Vector3(0f, 0.2f, 0f), 2, 5f, 45f);
            }
            for (int i = 0; i < JackrabbitRuns.Length; i++)
                Spawner(desert, $"Jackrabbits {i + 1}", "prey.jackrabbit", OnGround(JackrabbitRuns[i]) + new Vector3(0f, 0.2f, 0f), 3, 14f, 30f);
            for (int i = 0; i < CoyoteDens.Length; i++)
            {
                Vector3 c = OnGround(CoyoteDens[i]);
                Prop(props, "rock_tallA", c + Ring(rng, 4f, 6f), 4f, rng, collide: true);
                Instanced(props, "Den Skull", CreatureModels.PropMesh("Prop_Skull"), c + Ring(rng, 2f, 3f), 1.4f, rng);
                Spawner(desert, $"Coyotes {i + 1}", "prey.coyote", c + new Vector3(0f, 0.2f, 0f), 2, 12f, 80f);
            }

            BuildDesertScatter(desert, props, rng);
            BuildDesertBackdrop(desert, rng);
            _terrain = previous;
        }

        private static Vector3 DesertRingPoint(float t, float outward)
        {
            float cx = (DMinX + DMaxX) * 0.5f, cz = (DMinZ + DMaxZ) * 0.5f;
            float angle = t * Mathf.PI * 2f;
            var dir = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
            float lo = 0f, hi = 200f;
            for (int k = 0; k < 40; k++)
            {
                float mid = (lo + hi) * 0.5f;
                if (OutsideDesert(cx + dir.x * mid, cz + dir.y * mid) < outward) lo = mid; else hi = mid;
            }
            return new Vector3(cx + dir.x * lo, 0f, cz + dir.y * lo);
        }

        /// <summary>A rock formation: two or three red blocks stacked, each a little smaller and turned.</summary>
        private static void MesaStack(Transform parent, Vector3 at, System.Random rng, float height)
        {
            float y = GroundY(at.x, at.z) - 0.5f, w = Range(rng, 4f, 7f), d = Range(rng, 3.5f, 6f);
            int tiers = 2 + rng.Next(2);
            for (int t = 0; t < tiers; t++)
            {
                float h = height / tiers + (t == 0 ? 0.5f : 0f);
                Box(parent, "Mesa Rock", new Vector3(at.x + Range(rng, -0.6f, 0.6f), y + h * 0.5f, at.z + Range(rng, -0.6f, 0.6f)), new Vector3(w, h, d), t % 2 == 0 ? "Mesa" : "MesaDark", Range(rng, 0f, 90f));
                y += h;
                w *= Range(rng, 0.6f, 0.8f);
                d *= Range(rng, 0.6f, 0.8f);
            }
        }

        private static void BuildDesertCamp(Transform desert, Transform props, Transform zones, Transform signs)
        {
            Transform camp = Group(desert, "Dry Creek");
            Vector3 c = DesertCamp;
            var rng = new System.Random(530);
            Prop(camp, "campfire_stones", c, 3f, rng, collide: false, yaw: 0f);
            Prop(camp, "campfire_logs", c, 3f, rng, collide: false, yaw: 30f);
            Campfire(camp, c + new Vector3(0f, 0.25f, 0f));
            Prop(camp, "tent_detailedOpen", c + new Vector3(-6f, 0f, 7f), 4f, rng, collide: true, yaw: 160f);
            // An adobe hut and a well: the camp's landmarks.
            Box(camp, "Adobe Hut", c + new Vector3(10f, 1.4f, 9f), new Vector3(5f, 2.8f, 4f), "Adobe", 10f);
            Visual(camp, "Adobe Roof", c + new Vector3(10f, 2.9f, 9f), new Vector3(5.6f, 0.25f, 4.6f), "Wood", 10f);
            Box(camp, "Well", c + new Vector3(-8f, 0.45f, -6f), new Vector3(1.8f, 0.9f, 1.8f), "Rock");
            Visual(camp, "Well Water", c + new Vector3(-8f, 0.85f, -6f), new Vector3(1.3f, 0.05f, 1.3f), "Surf");
            Box(camp, "Well Post L", c + new Vector3(-8.75f, 1.4f, -6f), new Vector3(0.15f, 1.9f, 0.15f), "Wood");
            Box(camp, "Well Post R", c + new Vector3(-7.25f, 1.4f, -6f), new Vector3(0.15f, 1.9f, 0.15f), "Wood");
            Visual(camp, "Well Beam", c + new Vector3(-8f, 2.3f, -6f), new Vector3(1.8f, 0.15f, 0.15f), "Wood");

            // Rosa's trading post: desert prices (x2), the best in the game so far.
            Station(camp, signs, CampStationKind.Sell, "Desert Trading Post", "DRY CREEK TRADING POST\n<size=55%>desert prices x2</size>",
                c + new Vector3(8f, 0f, -2f), 90f, "npc.desert_trader", DesertExpectedSale, DesertPriceMultiplier);
            // Dusty sits on a log at the fire.
            Prop(camp, "log", c + new Vector3(-2.7f, 0f, 0.4f), 3f, rng, collide: false, yaw: 90f);
            NpcAbout(camp, "npc.prospector", c + new Vector3(-2.3f, 0f, 0f), 90f);

            Sign(signs, "THE RED DESERT\n<size=55%>Dry Creek camp - mind the rocks, snakes wait in the shade</size>", c + new Vector3(0f, 3.8f, 12f), 180f, 1.4f);
            Transform arrival = Marker(zones, "Desert Arrival", c + new Vector3(0f, 0.1f, -6f), 0f);

            // Pads home: back to the forest camp, and to the Plains.
            Transform toForest = Marker(zones, "Forest Camp Arrival", ForestCamp + new Vector3(-4.5f, 0.1f, 5.5f), 0f);
            Transform toPlains = Marker(zones, "Plains Camp Arrival (desert)", CampCenter + new Vector3(0f, 0f, -4.5f), 0f);
            DesertPad(camp, zones, signs, "BACK TO THE PINES", c + new Vector3(-4f, 0f, -11f), toForest);
            DesertPad(camp, zones, signs, "BACK TO THE PLAINS", c + new Vector3(4f, 0f, -11f), toPlains);

            // The way in: a pad at Ash's camp that appears once the Old Bear has fallen (the forest boss flag).
            Transform gate = Group(desert, "Desert Road (open)");
            gate.gameObject.AddComponent<WorldGate>().Configure(GeneratedContent.ForestBossFlag, showWhenSet: true);
            Vector3 forestPad = ForestCamp + new Vector3(-7f, 0f, 9f);
            Visual(gate, "Pad Desert", forestPad + new Vector3(0f, 0.02f, 0f), new Vector3(2.2f, 0.04f, 2.2f), "Pad");
            Zone(gate, "Teleport Desert", forestPad + new Vector3(0f, 1f, 0f), new Vector3(1.6f, 2f, 1.6f), ZoneKind.Teleport, arrival);
            Sign(gate, "TO THE RED DESERT\n<size=55%>step on the pad</size>", forestPad + new Vector3(0f, 2.4f, 0f), 270f, 0.9f);
            Transform closed = Group(desert, "Desert Road (closed)");
            closed.gameObject.AddComponent<WorldGate>().Configure(GeneratedContent.ForestBossFlag, showWhenSet: false);
            Sign(closed, "THE DESERT ROAD\n<size=55%>Ash opens it once the Old Bear has fallen</size>", forestPad + new Vector3(0f, 2.4f, 0f), 270f, 0.9f);

            // The movement lab's test pad (the lab is the test zone: every gun, every map).
            Vector3 labPad = new Vector3(0f, 0.1f, 5.5f);
            Visual(desert, "Pad Desert (lab)", labPad + new Vector3(0f, 0.02f, 0f), new Vector3(2.2f, 0.04f, 2.2f), "Pad");
            Zone(zones, "Teleport Desert (lab)", labPad + new Vector3(0f, 1f, 0f), new Vector3(1.6f, 2f, 1.6f), ZoneKind.Teleport, arrival);
            Sign(signs, "DESERT\n<size=55%>test pad</size>", labPad + new Vector3(0f, 2.4f, 0f), 180f, 0.9f);
        }

        private static void DesertPad(Transform camp, Transform zones, Transform signs, string label, Vector3 feet, Transform target)
        {
            Visual(camp, $"Pad {label}", feet + new Vector3(0f, 0.02f, 0f), new Vector3(2.2f, 0.04f, 2.2f), "Pad");
            Zone(zones, $"Teleport {label}", feet + new Vector3(0f, 1f, 0f), new Vector3(1.6f, 2f, 1.6f), ZoneKind.Teleport, target);
            Sign(signs, $"{label}\n<size=55%>step on the pad</size>", feet + new Vector3(0f, 2.4f, 0f), 0f, 0.8f);
        }

        /// <summary>A point in the basin away from the camp, the rim and the animals' places.</summary>
        private static Vector3 DesertOpenPoint(System.Random rng, float clearance)
        {
            for (int attempt = 0; attempt < 60; attempt++)
            {
                var p = new Vector3(Range(rng, DMinX, DMaxX), 0f, Range(rng, DMinZ, DMaxZ));
                if (OutsideDesert(p.x, p.z) > -clearance - 4f || DesertFlat(p.x, p.z) > 0.05f) continue;
                if ((p - new Vector3(DesertCamp.x, 0f, DesertCamp.z)).magnitude < 20f + clearance) continue;
                bool blocked = false;
                foreach (Vector3[] set in new[] { JackrabbitRuns, LizardRocks, ScorpionFlats, SnakeRocks, CoyoteDens })
                    foreach (Vector3 q in set) if ((p - q).sqrMagnitude < (8f + clearance) * (8f + clearance)) blocked = true;
                if (!blocked) return p;
            }
            return new Vector3(Range(rng, DMinX + 30f, DMaxX - 30f), 0f, Range(rng, DMinZ + 30f, DMaxZ - 30f));
        }

        /// <summary>One prop mesh placed on the ground (scene object, vertex colours).</summary>
        private static GameObject Instanced(Transform parent, string name, Mesh mesh, Vector3 at, float scale, System.Random rng)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(OnGround(at), Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f));
            go.transform.localScale = Vector3.one * scale;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(GeneratedAssets.ResourcesDir + "/FX/M_Terrain.mat");
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        private static void BuildDesertScatter(Transform desert, Transform props, System.Random rng)
        {
            var go = new GameObject("Scatter (instanced)");
            go.transform.SetParent(desert, false);
            ScatterRenderer scatter = go.AddComponent<ScatterRenderer>();
            Material terrainMat = AssetDatabase.LoadAssetAtPath<Material>(GeneratedAssets.ResourcesDir + "/FX/M_Terrain.mat");
            Material[] mats = { terrainMat };

            // Saguaros stand alone across the basin; each has a trunk you bump into (the size of the one you see).
            Transform trunks = Group(props, "Cactus Trunks (collision)");
            Mesh[] saguaros = { CreatureModels.PropMesh("Prop_Saguaro0"), CreatureModels.PropMesh("Prop_Saguaro1") };
            var saguaroLists = new[] { new List<Matrix4x4>(), new List<Matrix4x4>() };
            for (int i = 0; i < 70; i++)
            {
                Vector3 p = DesertOpenPoint(rng, 1f);
                float s = Range(rng, 0.8f, 1.35f);
                saguaroLists[rng.Next(2)].Add(Instance(p, s, rng));
                float y = GroundY(p.x, p.z);
                Box(trunks, "Saguaro Collision", new Vector3(p.x, y + 2.1f * s, p.z), new Vector3(0.5f * s, 4.2f * s, 0.5f * s), "Leaf").GetComponent<MeshRenderer>().enabled = false;
            }
            for (int k = 0; k < 2; k++)
                if (saguaros[k] != null) scatter.SetLayer(saguaros[k], mats, saguaroLists[k], PropChunk, 420f, true);

            // Low stuff: barrel cacti, dead bushes, bleached skulls, pebbles.
            Mesh barrel = CreatureModels.PropMesh("Prop_Barrel"), bush = CreatureModels.PropMesh("Prop_DeadBush"), skull = CreatureModels.PropMesh("Prop_Skull");
            var barrels = new List<Matrix4x4>();
            var bushes = new List<Matrix4x4>();
            var skulls = new List<Matrix4x4>();
            var pebbles = new List<Matrix4x4>();
            for (int i = 0; i < 1400; i++)
            {
                var p = new Vector3(Range(rng, DMinX - 4f, DMaxX + 4f), 0f, Range(rng, DMinZ - 4f, DMaxZ + 4f));
                if (OutsideDesert(p.x, p.z) > 2f || DesertFlat(p.x, p.z) > 0.6f) continue;
                double r = rng.NextDouble();
                if (r < 0.22) barrels.Add(Instance(p, Range(rng, 0.8f, 1.5f), rng));
                else if (r < 0.62) bushes.Add(Instance(p, Range(rng, 0.9f, 1.8f), rng));
                else if (r < 0.66) skulls.Add(Instance(p, Range(rng, 1f, 1.5f), rng));
                else pebbles.Add(Instance(p, Range(rng, 1.2f, 2f), rng));
            }
            if (barrel != null) scatter.SetLayer(barrel, mats, barrels, ClutterChunk, 160f, true, PropFarDensity);
            if (bush != null) scatter.SetLayer(bush, mats, bushes, ClutterChunk, 140f, false, PropFarDensity);
            if (skull != null) scatter.SetLayer(skull, mats, skulls, ClutterChunk, 120f, false, PropFarDensity);
            AddLayer(scatter, "rock_smallA", pebbles, ClutterChunk, 120f, false, PropFarDensity);
        }

        /// <summary>Far-off mesas all round, and a few clouds (purely visual, past where anyone can go).</summary>
        private static void BuildDesertBackdrop(Transform desert, System.Random rng)
        {
            Transform sky = Group(desert, "Horizon");
            var center = new Vector3((DMinX + DMaxX) * 0.5f, DesertY, (DMinZ + DMaxZ) * 0.5f);
            for (int i = 0; i < 16; i++)
            {
                float angle = i / 16f * Mathf.PI * 2f + Range(rng, -0.1f, 0.1f);
                float dist = Range(rng, 170f, 240f);
                Vector3 at = center + new Vector3(Mathf.Sin(angle) * dist, 0f, Mathf.Cos(angle) * dist);
                float h = Range(rng, 30f, 60f), w = Range(rng, 40f, 90f);
                for (int t = 0; t < 2; t++)
                {
                    GameObject m = Visual(sky, $"Far Mesa {i}.{t}", at + new Vector3(0f, h * 0.5f * (t + 1) - 4f, 0f) + new Vector3(Range(rng, -6f, 6f), 0f, Range(rng, -6f, 6f)),
                        new Vector3(w * (1f - t * 0.35f), h * (t == 0 ? 1f : 0.4f), w * Range(rng, 0.5f, 0.8f) * (1f - t * 0.35f)), t == 0 ? "Mesa" : "MesaDark", Range(rng, 0f, 90f));
                    m.layer = BackdropLayer;
                    m.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
            var cloudMat = AssetDatabase.LoadAssetAtPath<Material>(GeneratedAssets.ResourcesDir + "/FX/M_Cloud.mat");
            Mesh cloud = AssetDatabase.LoadAssetAtPath<Mesh>(GeneratedAssets.MeshesDir + "/Cloud.asset");
            for (int i = 0; i < 5; i++)
            {
                var go = new GameObject($"Cloud {i}", typeof(MeshFilter), typeof(MeshRenderer));
                go.layer = BackdropLayer;
                go.transform.SetParent(sky, false);
                go.transform.position = center + new Vector3(Range(rng, -160f, 160f), Range(rng, 80f, 110f), Range(rng, -160f, 160f));
                go.transform.localScale = Vector3.one * Range(rng, 6f, 10f);
                go.GetComponent<MeshFilter>().sharedMesh = cloud;
                go.GetComponent<MeshRenderer>().sharedMaterial = cloudMat;
                go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.AddComponent<CloudDrift>().Configure(new Vector3(1f, 0f, 0.3f) * Range(rng, 0.6f, 1.2f), center, 180f);
            }
        }
    }
}
