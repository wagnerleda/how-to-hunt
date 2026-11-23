using System.Collections.Generic;
using HowToHunt.Core;
using HowToHunt.Economy;
using HowToHunt.Hunting;
using UnityEditor;
using UnityEngine;

namespace HowToHunt.Editor
{
    /// <summary>
    /// The Plains test zone (owner layout, M3 feedback): a rolling low-poly meadow (200 m square, rounded corners)
    /// ringed by a forest you can't get through, a river, a camp by the south corridor to the movement lab, and a
    /// clearing on the east side that will open onto the next zone (blocked for now). Dense instanced grass and
    /// flowers, tree clusters, rocks, low-poly mountains and clouds on the horizon. Deterministic: rebuilding gives
    /// the same map. Terrain collision is a SimTerrain heightfield; trees and big rocks get invisible box brushes.
    /// </summary>
    public static partial class GrayboxSceneBuilder
    {
        // Play area (inside the forest ring) and the forest band around it.
        private const float PlayMinX = -100f, PlayMaxX = 100f, PlayMinZ = 115f, PlayMaxZ = 315f, CornerRadius = 34f;
        private const float Band = 30f;
        private const float TerrainCell = 5f;
        private const double PlainsExpectedSale = 50;
        private static int BackdropLayer => Mathf.Max(0, LayerMask.NameToLayer(HowToHunt.Core.RenderDistance.BackdropLayerName));
        private const float GateX = -30f;
        private const float ClearingZ = 215f;
        public static readonly Vector3 CampCenter = new Vector3(GateX, 0f, 135f);
        private const string KenneyDir = GeneratedAssets.Root + "/Art/ThirdParty/KenneyNatureKit/";

        private static readonly Vector3[] FlowerFields = { new Vector3(-62f, 0f, 168f), new Vector3(52f, 0f, 175f), new Vector3(-25f, 0f, 282f), new Vector3(62f, 0f, 268f) };
        private static readonly Vector3[] Warrens = { new Vector3(62f, 0f, 138f), new Vector3(-72f, 0f, 228f), new Vector3(18f, 0f, 296f), new Vector3(12f, 0f, 172f) };

        private static SimTerrain _terrain;

        private static void BuildPlains(Transform level, Transform signs)
        {
            Transform plains = Group(level, "Plains");
            Transform props = Group(plains, "Props");
            var rng = new System.Random(20261116);

            _terrain = BuildTerrain(plains);
            BuildWater(plains);
            BuildBoundary(plains, signs);
            BuildCamp(plains, props, signs);

            // Butterfly meadows: flowers are instanced below; a few bushes frame each field.
            for (int f = 0; f < FlowerFields.Length; f++)
            {
                Vector3 c = OnGround(FlowerFields[f]);
                for (int i = 0; i < 7; i++) Prop(props, "plant_bushLarge", c + Ring(rng, 12f, 16f), 3f, rng, collide: false);
                Spawner(plains, $"Butterflies {f + 1}", "prey.butterfly", c + new Vector3(0f, 1.3f, 0f), 5, 9f, 15f);
            }

            // Rabbit warrens: logs and stumps to hop around.
            for (int w = 0; w < Warrens.Length; w++)
            {
                Vector3 c = OnGround(Warrens[w]);
                Prop(props, "log_large", c + Ring(rng, 4f, 7f), 3f, rng, collide: true);
                Prop(props, "stump_round", c + Ring(rng, 5f, 9f), 3f, rng, collide: true);
                Prop(props, "stump_old", c + Ring(rng, 6f, 10f), 3f, rng, collide: true);
                Spawner(plains, $"Rabbits {w + 1}", "prey.rabbit", c + new Vector3(0f, 0.2f, 0f), 3, 12f, 20f);
            }

            // Tree groves and lone trees in the meadow (collide), big rocks (collide).
            string[] trees = { "tree_default", "tree_oak", "tree_fat", "tree_detailed", "tree_tall", "tree_pineRoundA", "tree_pineRoundC" };
            // Cover for players who want to fight each other (not the core of the design): boulder clusters you can
            // crouch behind and fallen trees lying across the grass. All collide.
            for (int i = 0; i < 9; i++)
            {
                Vector3 c = OpenPoint(rng, 6f);
                int n = 2 + rng.Next(3);
                for (int k = 0; k < n; k++)
                    Prop(props, rng.Next(3) == 0 ? "rock_tallA" : (rng.Next(2) == 0 ? "rock_largeA" : "rock_largeC"), c + Disc(rng, 3.5f), Range(rng, 3.2f, 4.8f), rng, collide: true);
            }
            for (int i = 0; i < 10; i++)
            {
                Vector3 c = OpenPoint(rng, 5f);
                Prop(props, "log_large", c, Range(rng, 4f, 5.5f), rng, collide: true);
                if (rng.Next(2) == 0) Prop(props, "stump_old", c + Ring(rng, 3f, 5f), 3.5f, rng, collide: true);
            }

            for (int grove = 0; grove < 10; grove++)
            {
                Vector3 c = OpenPoint(rng, 14f);
                int n = 3 + rng.Next(5);
                for (int i = 0; i < n; i++) Prop(props, trees[rng.Next(trees.Length)], c + Disc(rng, 7f), Range(rng, 4f, 5.4f), rng, collide: true, trunkOnly: true);
                Prop(props, rng.Next(2) == 0 ? "rock_largeA" : "rock_largeB", c + Ring(rng, 6f, 9f), Range(rng, 2.5f, 3.5f), rng, collide: true);
            }
            for (int i = 0; i < 18; i++) Prop(props, trees[rng.Next(trees.Length)], OpenPoint(rng, 8f), Range(rng, 4f, 5.4f), rng, collide: true, trunkOnly: true);
            string[] rocks = { "rock_largeA", "rock_largeB", "rock_largeC", "rock_tallA", "stone_tallA" };
            for (int i = 0; i < 26; i++) Prop(props, rocks[rng.Next(rocks.Length)], OpenPoint(rng, 5f), Range(rng, 2.5f, 4f), rng, collide: true);
            for (int i = 0; i < 18; i++) Prop(props, rng.Next(2) == 0 ? "log" : "stump_round", OpenPoint(rng, 4f), 3f, rng, collide: true);

            BuildScatter(plains, rng);
            BuildBackdrop(plains, rng);
        }

        // --- terrain ----------------------------------------------------------------------------------------

        /// <summary>Signed distance outside the rounded play rectangle (negative inside).</summary>
        private static float OutsidePlay(float x, float z)
        {
            float cx = (PlayMinX + PlayMaxX) * 0.5f, cz = (PlayMinZ + PlayMaxZ) * 0.5f;
            float hx = (PlayMaxX - PlayMinX) * 0.5f - CornerRadius, hz = (PlayMaxZ - PlayMinZ) * 0.5f - CornerRadius;
            float dx = Mathf.Abs(x - cx) - hx, dz = Mathf.Abs(z - cz) - hz;
            float outside = new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dz, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(dx, dz), 0f) - CornerRadius;
        }

        private static float RiverZ(float x) => 222f + 28f * Mathf.Sin(x / 50f + 0.6f) + 10f * Mathf.Sin(x / 19f);

        private static float RiverDistance(float x, float z)
        {
            // The river runs roughly east-west; correct the vertical distance by the local slope of the curve.
            float dzdx = (RiverZ(x + 0.5f) - RiverZ(x - 0.5f));
            return Mathf.Abs(z - RiverZ(x)) / Mathf.Sqrt(1f + dzdx * dzdx);
        }

        private static float Noise(float x, float z, float scale, float seed) => Mathf.PerlinNoise(x / scale + seed, z / scale + seed * 1.7f) * 2f - 1f;

        /// <summary>Terrain before the river is carved: rolling hills, rising into the forest ring.</summary>
        private static float Hills(float x, float z)
        {
            float h = 3.4f * Noise(x, z, 75f, 11.3f) + 1.3f * Noise(x, z, 31f, 4.7f) + 0.35f * Noise(x, z, 12f, 8.1f);
            float outside = OutsidePlay(x, z);
            h += Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(outside / Band)) * (7f + 4f * Noise(x, z, 40f, 2.2f));
            return h;
        }

        /// <summary>How much an area is flattened to height 0 (camp, corridor to the lab, bhop course, lab edge).</summary>
        private static float FlatMask(float x, float z)
        {
            float camp = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(((new Vector2(x, z) - new Vector2(CampCenter.x, CampCenter.z)).magnitude - 20f) / 14f));
            float corridor = Mathf.Abs(x - GateX) < 6f + 8f && z < CampCenter.z ? 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Mathf.Abs(x - GateX) - 6f) / 8f)) : 0f;
            float bhop = Mathf.Abs(x) < 18f && z < 112f ? 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Mathf.Abs(x) - 12f) / 6f)) : 0f;
            float labEdge = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((z - 82f) / 10f));
            // Phase 6: the trail from the east clearing through the forest band to the surf route.
            float trail = x > PlayMaxX - 12f && Mathf.Abs(z - ClearingZ) < 11f
                ? (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Mathf.Abs(z - ClearingZ) - 5f) / 6f))) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((x - (PlayMaxX - 12f)) / 6f))
                : 0f;
            return Mathf.Max(Mathf.Max(Mathf.Max(camp, corridor), Mathf.Max(bhop, labEdge)), trail);
        }

        private static float Height(float x, float z)
        {
            float h = Hills(x, z);
            float river = RiverDistance(x, z);
            // River bed: flat bottom 1.8 m down within 4 m of the centre line, banks rising to the meadow by 10 m.
            if (river < 10f && OutsidePlay(x, z) < 10f) h -= 1.8f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((river - 4f) / 6f)));
            return Mathf.Lerp(h, 0f, FlatMask(x, z));
        }

        private static bool BhopHole(float x, float z) => Mathf.Abs(x) <= 5.01f && z < 107f;

        private static SimTerrain BuildTerrain(Transform plains)
        {
            float minX = PlayMinX - Band - 5f, maxX = PlayMaxX + Band + 5f, minZ = 80f, maxZ = PlayMaxZ + Band + 5f;
            int nx = Mathf.CeilToInt((maxX - minX) / TerrainCell) + 1, nz = Mathf.CeilToInt((maxZ - minZ) / TerrainCell) + 1;
            var heights = new float[nx * nz];
            for (int z = 0; z < nz; z++)
                for (int x = 0; x < nx; x++)
                {
                    float wx = minX + x * TerrainCell, wz = minZ + z * TerrainCell;
                    heights[z * nx + x] = BhopHole(wx, wz) ? float.NaN : Height(wx, wz);
                }

            var go = new GameObject("Terrain", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(plains, false);
            SimTerrain terrain = go.AddComponent<SimTerrain>();
            terrain.Configure(minX, minZ, TerrainCell, nx, nz, heights);

            // Flat-shaded mesh, one colour per triangle (faceted low-poly look like the reference).
            var verts = new List<Vector3>();
            var colors = new List<Color>();
            var tris = new List<int>();
            var field = terrain.Field;
            for (int z = 0; z < nz - 1; z++)
                for (int x = 0; x < nx - 1; x++)
                {
                    Vector3 v00 = field.Vertex(x, z).ToUnity(), v10 = field.Vertex(x + 1, z).ToUnity();
                    Vector3 v01 = field.Vertex(x, z + 1).ToUnity(), v11 = field.Vertex(x + 1, z + 1).ToUnity();
                    if (float.IsNaN(v00.y) || float.IsNaN(v10.y) || float.IsNaN(v01.y) || float.IsNaN(v11.y)) continue;
                    AddTri(verts, colors, tris, v00, v01, v11);
                    AddTri(verts, colors, tris, v00, v11, v10);
                }
            var mesh = new Mesh { name = "Plains Terrain", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            SaveGeneratedMesh(mesh, "PlainsTerrain");
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(GeneratedAssets.ResourcesDir + "/FX/M_Terrain.mat");
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);

            // The bhop course keeps a flat brush floor where the terrain has its hole (the replay tests use it).
            Box(plains, "Bhop Ground", new Vector3(0f, -0.5f, 93.5f), new Vector3(20f, 1f, 27f), "Grass");
            return terrain;
        }

        private static void AddTri(List<Vector3> verts, List<Color> colors, List<int> tris, Vector3 a, Vector3 b, Vector3 c) =>
            AddTri(verts, colors, tris, a, b, c, GroundColor);

        private static void AddTri(List<Vector3> verts, List<Color> colors, List<int> tris, Vector3 a, Vector3 b, Vector3 c, System.Func<float, float, float, float, Color> colour)
        {
            Vector3 centroid = (a + b + c) / 3f;
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            Color col = colour(centroid.x, centroid.z, centroid.y, n.y);
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            colors.Add(col); colors.Add(col); colors.Add(col);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }

        private static Color GroundColor(float x, float z, float y, float up)
        {
            var light = new Color(0.52f, 0.76f, 0.33f);
            var dark = new Color(0.36f, 0.6f, 0.26f);
            var forest = new Color(0.3f, 0.46f, 0.22f);
            var dirt = new Color(0.68f, 0.56f, 0.38f);
            var sand = new Color(0.85f, 0.78f, 0.56f);
            var rock = new Color(0.55f, 0.53f, 0.48f);
            float t = Mathf.Clamp01(0.5f + 0.5f * Noise(x, z, 18f, 3.3f)) * 0.7f + Mathf.Clamp01(y / 8f) * 0.3f;
            Color c = Color.Lerp(dark, light, t);
            c = Color.Lerp(c, forest, Mathf.Clamp01(OutsidePlay(x, z) / 12f));
            float flat = FlatMask(x, z);
            if (flat > 0.55f && z > 84f) c = Color.Lerp(c, dirt, Mathf.Clamp01((flat - 0.55f) * 3f) * 0.85f);
            float river = RiverDistance(x, z);
            if (river < 7f) c = Color.Lerp(c, sand, 1f - Mathf.Clamp01((river - 3f) / 4f));
            if (up < 0.8f) c = Color.Lerp(c, rock, Mathf.Clamp01((0.8f - up) * 4f));
            // A touch of per-face variation so facets read.
            float j = 0.04f * Noise(x * 7.3f, z * 7.3f, 1f, 5.5f);
            return new Color(c.r + j, c.g + j, c.b + j * 0.5f);
        }

        private static void SaveGeneratedMesh(Mesh mesh, string name)
        {
            string path = GeneratedAssets.MeshesDir + "/" + name + ".asset";
            GeneratedAssets.EnsureFolder(GeneratedAssets.MeshesDir);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
        }

        /// <summary>Ground height under a point (from the collision heightfield, so props sit exactly on it).</summary>
        private static float GroundY(float x, float z)
        {
            float h = _terrain != null ? _terrain.HeightAt(x, z) : float.NaN;
            return float.IsNaN(h) ? 0f : h;
        }

        private static Vector3 OnGround(Vector3 p) => new Vector3(p.x, GroundY(p.x, p.z), p.z);

        // --- river ------------------------------------------------------------------------------------------

        private static void BuildWater(Transform plains)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            const float halfWidth = 7f; // the edges run under the banks, so only the recessed middle shows
            for (float x = PlayMinX - Band; x <= PlayMaxX + Band; x += 2.5f)
            {
                float z = RiverZ(x);
                float dzdx = (RiverZ(x + 0.5f) - RiverZ(x - 0.5f));
                Vector2 side = new Vector2(-dzdx, 1f).normalized * halfWidth;
                float y = WaterLevel(x);
                int i = verts.Count;
                verts.Add(new Vector3(x + side.x, y, z + side.y));
                verts.Add(new Vector3(x - side.x, y, z - side.y));
                if (i >= 2)
                {
                    tris.Add(i - 2); tris.Add(i); tris.Add(i - 1);
                    tris.Add(i - 1); tris.Add(i); tris.Add(i + 1);
                }
            }
            var mesh = new Mesh { name = "Plains River" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            SaveGeneratedMesh(mesh, "PlainsRiver");
            var go = new GameObject("River", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(plains, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(GeneratedAssets.ResourcesDir + "/FX/M_Water.mat");
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Water volumes along the river (not solid): the movement sim slows players wading through them.
            Transform volumes = Group(plains, "River Water (volumes)");
            for (float x = PlayMinX - Band; x <= PlayMaxX + Band; x += 2.5f)
            {
                float z = RiverZ(x + 1.25f);
                float dzdx = RiverZ(x + 1.75f) - RiverZ(x + 0.75f);
                float yaw = Mathf.Atan2(1f, dzdx) * Mathf.Rad2Deg; // box length along the river
                float top = WaterLevel(x + 1.25f);
                GameObject v = Box(volumes, $"Water {x:0}", new Vector3(x + 1.25f, top - 1.5f, z), new Vector3(9f, 3f, 3.2f), "Surf", yaw); // local z along the river, x across
                v.GetComponent<MeshRenderer>().enabled = false;
                v.GetComponent<SimBrush>().Surface = HowToHunt.Sim.Collision.SurfaceFlags.Water;
            }
        }

        /// <summary>Water surface: 0.6 m above the lowest point of the bed across the river at x (always below the banks).</summary>
        private static float WaterLevel(float x)
        {
            float z = RiverZ(x);
            float bed = float.MaxValue;
            for (float d = -4f; d <= 4f; d += 1f) bed = Mathf.Min(bed, GroundY(x, z + d));
            return bed + 0.6f;
        }

        // --- forest ring, exits -----------------------------------------------------------------------------

        private static void BuildBoundary(Transform plains, Transform signs)
        {
            Transform walls = Group(plains, "Forest Wall (collision)");
            // Phase 6: everything closing the east clearing sits in a gate that opens with the quest (world flag).
            Transform blocked = Group(plains, "East Gate (blocked)");
            blocked.gameObject.AddComponent<WorldGate>().Configure(GeneratedContent.EastClearingFlag, showWhenSet: false);
            // Invisible wall a few metres into the trees all the way round, except the south corridor and the east
            // clearing (which has its own barricade until the quest opens it).
            const int segments = 160;
            var ring = new List<Vector3>();
            for (int i = 0; i < segments; i++) ring.Add(RingPoint(i / (float)segments, 4f));
            for (int i = 0; i < segments; i++)
            {
                Vector3 a = ring[i], b = ring[(i + 1) % segments];
                Vector3 mid = (a + b) * 0.5f;
                if (Mathf.Abs(mid.x - GateX) < 7f && mid.z < PlayMinZ + 10f) continue; // corridor opening
                float len = new Vector2(b.x - a.x, b.z - a.z).magnitude + 0.6f;
                float yaw = Mathf.Atan2(b.x - a.x, b.z - a.z) * Mathf.Rad2Deg;
                bool clearing = mid.x > PlayMaxX - 6f && Mathf.Abs(mid.z - ClearingZ) < 8f;
                GameObject w = Box(clearing ? blocked : walls, $"Wall {i}", new Vector3(mid.x, 15f, mid.z), new Vector3(1f, 40f, len), "Wall", yaw);
                w.GetComponent<MeshRenderer>().enabled = false;
            }
            // Corridor sides from the lab gate to the meadow.
            for (int s = -1; s <= 1; s += 2)
            {
                GameObject w = Box(walls, $"Corridor {(s < 0 ? "W" : "E")}", new Vector3(GateX + s * 6.5f, 15f, (81f + PlayMinZ + 6f) * 0.5f), new Vector3(1f, 40f, PlayMinZ + 6f - 81f), "Wall");
                w.GetComponent<MeshRenderer>().enabled = false;
            }
            // Gate from the lab: a lintel and a sign on each face.
            Box(plains, "Gate Lintel", new Vector3(GateX, 6.5f, 80.5f), new Vector3(16f, 1f, 1.2f), "Wood");
            Sign(signs, "PLAINS\n<size=55%>hunt, sell at the trader, upgrade at the outfitter</size>", new Vector3(GateX, 4.9f, 79.8f), 0f, 1.4f);
            Sign(signs, "MOVEMENT LAB", new Vector3(GateX, 5.3f, 81.2f), 180f, 1.2f);

            // The bhop course runs on past the old north wall: keep it in its own enclosure.
            Box(plains, "Bhop Enclosure W", new Vector3(-8.75f, 3f, 92.75f), new Vector3(0.5f, 8f, 24.5f), "Wall");
            Box(plains, "Bhop Enclosure E", new Vector3(8.75f, 3f, 92.75f), new Vector3(0.5f, 8f, 24.5f), "Wall");
            Box(plains, "Bhop Enclosure N", new Vector3(0f, 3f, 105.25f), new Vector3(18f, 8f, 0.5f), "Wall");

            // East clearing toward the next zone: a fallen-tree barricade until the quest opens it.
            var rng = new System.Random(7);
            Vector3 gate = OnGround(new Vector3(PlayMaxX + 3f, 0f, ClearingZ));
            // Phase 6: the logs and their sign go when the party finishes "Beyond the Trees" (world flag), on every machine.
            for (int i = -2; i <= 2; i++)
                Prop(blocked, "log_large", gate + new Vector3(0f, 0.3f + (i & 1) * 0.7f, i * 2.4f), 3.5f, rng, collide: true, yaw: 0f);
            Sign(blocked, "THE WAY EAST\n<size=55%>blocked by fallen trees - a hunter at camp may know a way</size>", gate + new Vector3(-3f, 3.8f, 0f), 90f, 1.2f);
            Transform open = Group(plains, "East Gate (open)");
            open.gameObject.AddComponent<WorldGate>().Configure(GeneratedContent.EastClearingFlag, showWhenSet: true);
            Sign(open, "THE WAY EAST\n<size=55%>surf the slopes down to the pine forest</size>", gate + new Vector3(-3f, 3.8f, 0f), 90f, 1.2f);
        }

        /// <summary>A point on the rounded play rectangle (t = 0..1 around), pushed <paramref name="outward"/> metres out.</summary>
        private static Vector3 RingPoint(float t, float outward)
        {
            float cx = (PlayMinX + PlayMaxX) * 0.5f, cz = (PlayMinZ + PlayMaxZ) * 0.5f;
            float angle = t * Mathf.PI * 2f;
            var dir = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
            // March outward until we cross the boundary + offset (robust for rounded rectangles).
            float lo = 0f, hi = 400f;
            for (int k = 0; k < 40; k++)
            {
                float mid = (lo + hi) * 0.5f;
                if (OutsidePlay(cx + dir.x * mid, cz + dir.y * mid) < outward) lo = mid; else hi = mid;
            }
            return new Vector3(cx + dir.x * lo, 0f, cz + dir.y * lo);
        }

        // --- camp ---------------------------------------------------------------------------------------------

        private static void BuildCamp(Transform plains, Transform props, Transform signs)
        {
            Transform camp = Group(plains, "Camp");
            Vector3 c = CampCenter;
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
            Station(camp, signs, CampStationKind.Sell, "Trader", "TRADER\n<size=55%>sell your catch</size>", c + new Vector3(8f, 0f, -1f), 90f, "npc.trader");
            Station(camp, signs, CampStationKind.Shop, "Outfitter", "OUTFITTER\n<size=55%>tools and upgrades</size>", c + new Vector3(-8f, 0f, -1f), 270f, "npc.outfitter");
            // Gunsmith (Phase 5): attachments for your guns.
            Station(camp, signs, CampStationKind.Workshop, "Gunsmith", "GUNSMITH\n<size=55%>attachments</size>", c + new Vector3(0f, 0f, 7.5f), 0f, "npc.gunsmith");
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

        private static void Station(Transform camp, Transform signs, CampStationKind kind, string label, string signText, Vector3 feet, float yaw, string npcKey = null, double expectedSale = PlainsExpectedSale, float priceMultiplier = 1f)
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
            Visual(t, "Awning", feet + fwd * 1.3f + new Vector3(0f, 2.65f, 0f), new Vector3(3.8f, 0.12f, 1.8f), kind == CampStationKind.Sell ? "Bhop" : kind == CampStationKind.Shop ? "Surf" : "Ramp", yaw);
            Sign(signs, signText, feet + fwd * 1.85f + new Vector3(0f, 3.35f, 0f), yaw, 1.1f);

            var station = new GameObject($"Station {label}");
            station.transform.SetParent(t, false);
            station.transform.position = feet;
            // Plains: a full bag sells for roughly 50 coins (Phase 3 economy); that sets the size of the cash burst.
            station.AddComponent<CampStation>().Configure(kind, 2.8f, label, expectedSale, priceMultiplier);
            // Phase 6: the stall's keeper stands behind the counter, facing you.
            if (npcKey != null) NpcAt(t, npcKey, feet + fwd * 2.05f, yaw + 180f);
        }

        /// <summary>A camp character (Phase 6): the hunters' stick figure with their own mask, posed by NpcBody.</summary>
        private static void NpcAt(Transform parent, string key, Vector3 feet, float yaw)
        {
            var def = AssetDatabase.LoadAssetAtPath<NpcDefinition>($"{GeneratedContent.ContentDir}/Npcs/Npc_{key.Substring(key.IndexOf('.') + 1)}.asset");
            if (def == null) { Debug.LogWarning($"[HowToHunt] No NPC {key}"); return; }
            var go = new GameObject($"NPC {def.DisplayName}");
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(feet + new Vector3(0f, MovementConfigHalfHeight, 0f), Quaternion.Euler(0f, yaw, 0f));
            AvatarBuilder.BuildFigure(go.transform);
            go.AddComponent<Npcs.Npc>().Configure(def, 4.5f); // customers stand across the counter
            go.AddComponent<Npcs.NpcBody>();
        }

        private const float MovementConfigHalfHeight = 0.9144f; // the hunters' hull: the figure is built about its centre

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

        // --- props --------------------------------------------------------------------------------------------

        /// <summary>
        /// Places a Kenney prop on the ground (its y is taken from the terrain); collide adds an invisible box brush
        /// from its bounds (trunk only for trees).
        /// </summary>
        private static void Prop(Transform parent, string model, Vector3 feet, float scale, System.Random rng, bool collide, bool trunkOnly = false, float yaw = float.NaN)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(KenneyDir + model + ".fbx");
            if (asset == null) { Debug.LogWarning($"[HowToHunt] Missing Kenney model {model}"); return; }
            feet.y += GroundY(feet.x, feet.z);
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
            if (trunkOnly)
            {
                // Trees: only the trunk blocks (walk under the canopy).
                GameObject trunk = Box(parent, model + " Collision", new Vector3(feet.x, feet.y + b.size.y * 0.5f, feet.z), new Vector3(0.12f * scale, b.size.y, 0.12f * scale), "Rock");
                trunk.GetComponent<MeshRenderer>().enabled = false;
                return;
            }
            // Rocks, logs, stumps, tents: a 26-sided hull fitted to the model's own vertices, so the collision follows
            // the shape (a log lying diagonally is a log, not a big square box).
            var hull = new GameObject(model + " Collision");
            hull.transform.SetParent(parent, false);
            hull.AddComponent<SimBrush>();
            Vector4[] planes = FitHull(go, out Vector3 min, out Vector3 max);
            hull.GetComponent<SimBrush>().SetPlanes(planes, min, max);
        }

        private static readonly Vector3[] HullAxes =
        {
            new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 0, 1),
            new Vector3(1, 1, 0), new Vector3(1, -1, 0), new Vector3(1, 0, 1), new Vector3(1, 0, -1), new Vector3(0, 1, 1), new Vector3(0, 1, -1),
            new Vector3(1, 1, 1), new Vector3(1, 1, -1), new Vector3(1, -1, 1), new Vector3(-1, 1, 1),
        };

        /// <summary>
        /// A 26-DOP around every vertex of the model (13 directions, both ways), pulled in 2 cm so you brush against
        /// the surface rather than an invisible shell. Always convex, so it is a valid brush.
        /// </summary>
        private static Vector4[] FitHull(GameObject go, out Vector3 min, out Vector3 max)
        {
            var points = new List<Vector3>();
            var buffer = new List<Vector3>();
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                buffer.Clear();
                mf.sharedMesh.GetVertices(buffer);
                Matrix4x4 m = mf.transform.localToWorldMatrix;
                foreach (Vector3 v in buffer) points.Add(m.MultiplyPoint3x4(v));
            }
            const float inset = 0.02f;
            var planes = new List<Vector4>(26);
            min = Vector3.one * float.MaxValue;
            max = Vector3.one * float.MinValue;
            foreach (Vector3 p in points) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
            foreach (Vector3 axis in HullAxes)
            {
                Vector3 n = axis.normalized;
                float hi = float.MinValue, lo = float.MaxValue;
                foreach (Vector3 p in points)
                {
                    float d = Vector3.Dot(n, p);
                    if (d > hi) hi = d;
                    if (d < lo) lo = d;
                }
                if (hi - lo < inset * 3f) { hi += inset; lo -= inset; } // keep very thin props solid
                planes.Add(new Vector4(n.x, n.y, n.z, hi - inset));
                planes.Add(new Vector4(-n.x, -n.y, -n.z, -(lo + inset)));
            }
            min += Vector3.one * inset;
            max -= Vector3.one * inset;
            return planes.ToArray();
        }

        // --- instanced scatter (grass, flowers, bushes, forest ring) ------------------------------------------

        // Scatter chunks: each chunk x material is one instanced draw call, so chunks hold enough instances to be worth
        // a call (trees: a hundred metres; ground clutter: smaller, so it can thin out with distance chunk by chunk).
        private const float TreeChunk = 100f, ClutterChunk = 36f, PropChunk = 48f;
        // Share of grass, flowers and small props still drawn at the edge of their draw distance (fog hides it).
        private const float GrassFarDensity = 0.15f, FlowerFarDensity = 0.25f, PropFarDensity = 0.3f;

        private static void BuildScatter(Transform plains, System.Random rng)
        {
            var go = new GameObject("Scatter (instanced)");
            go.transform.SetParent(plains, false);
            ScatterRenderer scatter = go.AddComponent<ScatterRenderer>();

            // Forest ring: dense trees through the whole band (no per-tree collision: the invisible wall stops players).
            string[] ringTrees = { "tree_default", "tree_oak", "tree_fat", "tree_detailed", "tree_tall", "tree_pineRoundA", "tree_pineRoundC", "tree_simple" };
            var ringPoints = new List<(Vector3 p, float s)>();
            for (float x = PlayMinX - Band; x <= PlayMaxX + Band; x += 5.2f)
                for (float z = 82f; z <= PlayMaxZ + Band; z += 5.2f)
                {
                    float px = x + Range(rng, -2.2f, 2.2f), pz = z + Range(rng, -2.2f, 2.2f);
                    float outside = OutsidePlay(px, pz);
                    if (outside < -1f + Range(rng, 0f, 3f)) continue;               // ragged inner edge
                    if (Mathf.Abs(px - GateX) < 6f && pz < PlayMinZ + 4f) continue;  // corridor to the lab
                    if (Mathf.Abs(px) < 11f && pz < 108f) continue;                  // bhop enclosure
                    if (px > PlayMaxX - 6f && Mathf.Abs(pz - ClearingZ) < 9f) continue; // east clearing
                    if (pz < 84f) continue;
                    ringPoints.Add((new Vector3(px, 0f, pz), Range(rng, 4.2f, 6f)));
                }
            for (int k = 0; k < ringTrees.Length; k++)
            {
                var list = new List<Matrix4x4>();
                for (int i = k; i < ringPoints.Count; i += ringTrees.Length)
                    list.Add(Instance(ringPoints[i].p, ringPoints[i].s, rng));
                AddLayer(scatter, ringTrees[k], list, TreeChunk, 420f, true);
            }

            // Grass everywhere in the meadow, thicker in hollows; none on paths, camp and water.
            string[] grass = { "grass", "grass_large", "grass_leafs", "grass_leafsLarge" };
            var grassLists = new List<Matrix4x4>[grass.Length];
            for (int k = 0; k < grass.Length; k++) grassLists[k] = new List<Matrix4x4>();
            for (int i = 0; i < 20000; i++)
            {
                var p = new Vector3(Range(rng, PlayMinX - 6f, PlayMaxX + 6f), 0f, Range(rng, PlayMinZ - 6f, PlayMaxZ + 6f));
                if (OutsidePlay(p.x, p.z) > 6f || FlatMask(p.x, p.z) > 0.6f || RiverDistance(p.x, p.z) < 3.5f) continue;
                if (Noise(p.x, p.z, 22f, 9.9f) < -0.45f && rng.NextDouble() < 0.6) continue; // open patches
                int k = rng.Next(grass.Length);
                grassLists[k].Add(Instance(p, Range(rng, 2f, 3.2f), rng));
            }
            for (int k = 0; k < grass.Length; k++) AddLayer(scatter, grass[k], grassLists[k], ClutterChunk, 85f, false, GrassFarDensity);

            // Flowers: thick in the butterfly meadows, sprinkled elsewhere.
            string[] flowers = { "flower_purpleA", "flower_purpleB", "flower_redA", "flower_redB", "flower_yellowA", "flower_yellowB" };
            var flowerLists = new List<Matrix4x4>[flowers.Length];
            for (int k = 0; k < flowers.Length; k++) flowerLists[k] = new List<Matrix4x4>();
            foreach (Vector3 field in FlowerFields)
                for (int i = 0; i < 420; i++) flowerLists[rng.Next(flowers.Length)].Add(Instance(field + Disc(rng, 14f), Range(rng, 1.8f, 2.6f), rng));
            for (int i = 0; i < 1800; i++)
            {
                var p = new Vector3(Range(rng, PlayMinX, PlayMaxX), 0f, Range(rng, PlayMinZ, PlayMaxZ));
                if (OutsidePlay(p.x, p.z) > 0f || FlatMask(p.x, p.z) > 0.5f || RiverDistance(p.x, p.z) < 4f) continue;
                flowerLists[rng.Next(flowers.Length)].Add(Instance(p, Range(rng, 1.8f, 2.6f), rng));
            }
            for (int k = 0; k < flowers.Length; k++) AddLayer(scatter, flowers[k], flowerLists[k], ClutterChunk, 110f, false, FlowerFarDensity);

            // Bushes, small rocks and mushrooms; reeds by the river.
            string[] small = { "plant_bush", "plant_bushSmall", "plant_bushDetailed", "rock_smallA", "rock_smallB", "rock_smallC", "mushroom_redGroup", "mushroom_tanGroup" };
            var smallLists = new List<Matrix4x4>[small.Length];
            for (int k = 0; k < small.Length; k++) smallLists[k] = new List<Matrix4x4>();
            for (int i = 0; i < 1600; i++)
            {
                var p = new Vector3(Range(rng, PlayMinX - 10f, PlayMaxX + 10f), 0f, Range(rng, PlayMinZ - 10f, PlayMaxZ + 10f));
                if (OutsidePlay(p.x, p.z) > 8f || FlatMask(p.x, p.z) > 0.5f || RiverDistance(p.x, p.z) < 3f) continue;
                int k = rng.Next(small.Length);
                smallLists[k].Add(Instance(p, Range(rng, 2.2f, 3.4f), rng));
            }
            for (int k = 0; k < small.Length; k++) AddLayer(scatter, small[k], smallLists[k], PropChunk, 140f, k < 3, PropFarDensity);
            var reeds = new List<Matrix4x4>();
            for (float x = PlayMinX; x <= PlayMaxX; x += 1.1f)
            {
                float z = RiverZ(x) + (rng.Next(2) == 0 ? -1f : 1f) * Range(rng, 3.2f, 5f);
                reeds.Add(Instance(new Vector3(x, 0f, z), Range(rng, 2.4f, 3.4f), rng));
            }
            AddLayer(scatter, "plant_flatTall", reeds, PropChunk, 100f, false, PropFarDensity);
        }

        private static Matrix4x4 Instance(Vector3 p, float scale, System.Random rng)
        {
            p.y = GroundY(p.x, p.z) - 0.03f;
            return Matrix4x4.TRS(p, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), Vector3.one * scale);
        }

        /// <summary>Adds an instanced layer from a Kenney model (its first mesh, with that mesh's own transform baked in).</summary>
        private static void AddLayer(ScatterRenderer scatter, string model, List<Matrix4x4> instances, float chunk, float distance, bool shadows, float farDensity = 1f)
        {
            if (instances.Count == 0) return;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(KenneyDir + model + ".fbx");
            MeshFilter mf = asset != null ? asset.GetComponentInChildren<MeshFilter>() : null;
            if (mf == null) { Debug.LogWarning($"[HowToHunt] No mesh in {model}"); return; }
            Matrix4x4 local = asset.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            for (int i = 0; i < instances.Count; i++) instances[i] = instances[i] * local;
            scatter.SetLayer(mf.sharedMesh, mf.GetComponent<MeshRenderer>().sharedMaterials, instances, chunk, distance, shadows, farDensity);
        }

        // --- horizon ------------------------------------------------------------------------------------------

        private static void BuildBackdrop(Transform plains, System.Random rng)
        {
            Transform sky = Group(plains, "Horizon");
            var material = AssetDatabase.LoadAssetAtPath<Material>(GeneratedAssets.ResourcesDir + "/FX/M_Terrain.mat");
            var center = new Vector3(0f, 0f, (PlayMinZ + PlayMaxZ) * 0.5f);
            // Mountains in a broken ring beyond the forest (purely visual, past where anyone can go).
            for (int i = 0; i < 13; i++)
            {
                float angle = i / 13f * Mathf.PI * 2f + Range(rng, -0.15f, 0.15f);
                if (Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, 180f)) < 30f) continue; // keep the lab side open
                if (Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, 90f)) < 35f) continue;  // and the surf route to the forest
                float dist = Range(rng, 290f, 360f);
                var pos = center + new Vector3(Mathf.Sin(angle) * dist, -4f, Mathf.Cos(angle) * dist);
                Mesh m = Mountain(rng, Range(rng, 55f, 110f), Range(rng, 70f, 120f), i);
                var go = new GameObject($"Mountain {i}", typeof(MeshFilter), typeof(MeshRenderer));
                go.layer = BackdropLayer; // drawn past r_drawdistance (the horizon)
                go.transform.SetParent(sky, false);
                go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f));
                go.GetComponent<MeshFilter>().sharedMesh = m;
                go.GetComponent<MeshRenderer>().sharedMaterial = material;
                go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            // Puffy low-poly clouds that drift slowly.
            var cloudMat = AssetDatabase.LoadAssetAtPath<Material>(GeneratedAssets.ResourcesDir + "/FX/M_Cloud.mat");
            Mesh cloud = CloudMesh(rng);
            for (int i = 0; i < 16; i++)
            {
                var go = new GameObject($"Cloud {i}", typeof(MeshFilter), typeof(MeshRenderer));
                go.layer = BackdropLayer;
                go.transform.SetParent(sky, false);
                go.transform.position = center + new Vector3(Range(rng, -260f, 260f), Range(rng, 55f, 85f), Range(rng, -260f, 260f));
                go.transform.localScale = Vector3.one * Range(rng, 6f, 12f);
                go.GetComponent<MeshFilter>().sharedMesh = cloud;
                go.GetComponent<MeshRenderer>().sharedMaterial = cloudMat;
                go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.AddComponent<CloudDrift>().Configure(new Vector3(1.2f, 0f, 0.4f) * Range(rng, 0.6f, 1.4f), center, 300f);
            }
        }

        private static Mesh Mountain(System.Random rng, float height, float radius, int index)
        {
            const int sides = 9, rings = 4;
            var verts = new List<Vector3>();
            var colors = new List<Color>();
            var tris = new List<int>();
            var grid = new Vector3[rings + 1, sides];
            for (int r = 0; r <= rings; r++)
                for (int s = 0; s < sides; s++)
                {
                    float t = r / (float)rings;
                    float a = s / (float)sides * Mathf.PI * 2f + (r % 2) * 0.3f;
                    float rad = radius * (1f - t) * Range(rng, 0.8f, 1.15f);
                    float y = height * Mathf.Pow(t, 0.85f) * (r == rings ? 1f : Range(rng, 0.9f, 1.05f));
                    grid[r, s] = new Vector3(Mathf.Sin(a) * rad, y, Mathf.Cos(a) * rad);
                }
            var peak = new Vector3(Range(rng, -4f, 4f), height * 1.08f, Range(rng, -4f, 4f));
            void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                float y = (a.y + b.y + c.y) / 3f / height;
                Color col = y > 0.72f ? new Color(0.95f, 0.96f, 0.98f) : y > 0.25f ? Color.Lerp(new Color(0.5f, 0.45f, 0.4f), new Color(0.62f, 0.58f, 0.52f), Range(rng, 0f, 1f)) : new Color(0.42f, 0.6f, 0.32f);
                int i = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                colors.Add(col); colors.Add(col); colors.Add(col);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            }
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < sides; s++)
                {
                    int s1 = (s + 1) % sides;
                    Tri(grid[r, s], grid[r + 1, s], grid[r + 1, s1]);
                    Tri(grid[r, s], grid[r + 1, s1], grid[r, s1]);
                }
            for (int s = 0; s < sides; s++) Tri(grid[rings, s], peak, grid[rings, (s + 1) % sides]);
            var mesh = new Mesh { name = $"Mountain {index}" };
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            SaveGeneratedMesh(mesh, $"Mountain{index}");
            return mesh;
        }

        private static Mesh CloudMesh(System.Random rng)
        {
            var combine = new List<CombineInstance>();
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Mesh lowSphere = LowPolySphere();
            Object.DestroyImmediate(sphere);
            for (int i = 0; i < 5; i++)
            {
                var pos = new Vector3(Range(rng, -1.6f, 1.6f), Range(rng, 0f, 0.4f), Range(rng, -0.6f, 0.6f));
                float s = Range(rng, 0.9f, 1.5f);
                combine.Add(new CombineInstance { mesh = lowSphere, transform = Matrix4x4.TRS(pos, Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), new Vector3(s, s * 0.7f, s)) });
            }
            var mesh = new Mesh { name = "Cloud" };
            mesh.CombineMeshes(combine.ToArray(), true, true);
            mesh.RecalculateNormals();
            SaveGeneratedMesh(mesh, "Cloud");
            return mesh;
        }

        /// <summary>An icosahedron with flat faces (one vertex per face corner).</summary>
        private static Mesh LowPolySphere()
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var p = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            int[] f =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < f.Length; i++)
            {
                verts.Add(p[f[i]].normalized * 0.5f);
                tris.Add(i);
            }
            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        // --- helpers ------------------------------------------------------------------------------------------

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

        /// <summary>A point in the open meadow, away from camp, the river and the hunting grounds' centres.</summary>
        private static Vector3 OpenPoint(System.Random rng, float clearance)
        {
            for (int attempt = 0; attempt < 60; attempt++)
            {
                var p = new Vector3(Range(rng, PlayMinX, PlayMaxX), 0f, Range(rng, PlayMinZ, PlayMaxZ));
                if (OutsidePlay(p.x, p.z) > -clearance) continue;
                if (FlatMask(p.x, p.z) > 0.05f || (new Vector3(p.x, 0f, p.z) - CampCenter).magnitude < 25f + clearance) continue;
                if (RiverDistance(p.x, p.z) < 7f + clearance) continue;
                bool blocked = false;
                foreach (Vector3 f in FlowerFields) if ((p - f).sqrMagnitude < (10f + clearance) * (10f + clearance)) blocked = true;
                foreach (Vector3 w in Warrens) if ((p - w).sqrMagnitude < (6f + clearance) * (6f + clearance)) blocked = true;
                if (!blocked) return p;
            }
            return new Vector3(Range(rng, -50f, 50f), 0f, Range(rng, 250f, 290f));
        }
    }
}
