using System.Collections.Generic;
using HowToHunt.Core;
using HowToHunt.Economy;
using HowToHunt.Sim.Collision;
using UnityEditor;
using UnityEngine;

namespace HowToHunt.Editor
{
    /// <summary>
    /// Phase 6 (M4): the second map. The east clearing of the Plains (opened by the "Beyond the Trees" quest) leads
    /// along a trail to a cliff ledge; from there three surf ramps descend over a ravine to the Pine Forest, a smaller
    /// valley with its own trading post (Ash, better prices), deer glades and a pad back to the Plains camp (the quick
    /// way home). Falling into the ravine returns you to the ledge. Deterministic like the Plains.
    /// </summary>
    public static partial class GrayboxSceneBuilder
    {
        // --- the surf route --------------------------------------------------------------------------------------
        private const float RouteZ = ClearingZ + 12f;     // ridge line of the ramps, just north of the trail
        private const float LedgeTop = 0f;                // level with the flattened trail
        private const float RampLength = 64f, RampHeight = 12f, RampWidth = 18f, RampPitch = 7f;
        private const float RampGap = 8f, RampStep = 7f;  // gap between ramps and how much lower each one starts
        private const int RampCount = 3;
        private const float RouteStartX = 136f;

        // --- the forest --------------------------------------------------------------------------------------------
        private const float ForestY = -50f;
        private const float FMinX = 365f, FMaxX = 485f, FMinZ = 167f, FMaxZ = 287f, FCorner = 26f, FBand = 22f;
        private const double ForestExpectedSale = 120;
        private const float ForestPriceMultiplier = 1.5f;
        private const float LandingHalfWidth = 14f; // the forest's opening for the surf route
        private const float PineChunk = 80f; // ~700 pines per mesh: big chunks keep it to a few dozen draw calls
        private static readonly Vector3 ForestCamp = new Vector3(392f, ForestY, 203f);
        // The middle glade moved north (was 462, 196) to keep deer hunters out of the Old Bear's reach.
        private static readonly Vector3[] DeerGlades = { new Vector3(448f, 0f, 258f), new Vector3(452f, 0f, 232f), new Vector3(410f, 0f, 270f) };
        // The forest boss's den (south-east), ~70 m from Ash's camp: the bear never chases further than its leash.
        private static readonly Vector3 BearDen = new Vector3(456f, 0f, 180f);
        private static readonly Vector3[] SquirrelPines = { new Vector3(408f, 0f, 240f), new Vector3(440f, 0f, 270f), new Vector3(478f, 0f, 212f), new Vector3(414f, 0f, 220f) };
        private static readonly Vector3[] FoxDens = { new Vector3(380f, 0f, 262f), new Vector3(472f, 0f, 254f) };
        private static readonly Vector3[] BoarWallows = { new Vector3(428f, 0f, 250f), new Vector3(402f, 0f, 180f) };
        private static readonly Vector3[] ForestWarrens = { new Vector3(432f, 0f, 214f), new Vector3(470f, 0f, 236f) };

        private static void BuildEastRoute(Transform level, Transform zones, Transform signs)
        {
            Transform root = Group(level, "Surf Route East");

            // The trail out of the clearing ends on a rock ledge over the ravine; step off its north edge onto ramp 1.
            Box(root, "Cliff Ledge", new Vector3(140.5f, LedgeTop - 10f, ClearingZ), new Vector3(21f, 20f, 6f), "Rock");
            // The trail between rock walls you can see (no invisible walls: owner, 2026-11-24). The ledge itself is
            // open on every side: step off north onto ramp 1; a fall into the ravine brings you back to the ledge.
            Transform walls = Group(root, "Trail Walls (collision)");
            var rocks = new System.Random(211);
            for (int s = -1; s <= 1; s += 2)
            {
                float side = ClearingZ + s * (TrailHalfWidth + 1f);
                RockWall(walls, s < 0 ? "Trail S" : "Trail N", new Vector3(98f, 0f, side), new Vector3(134.5f, 0f, side), GroundY, 40f, rocks);
                RockWall(walls, s < 0 ? "Ledge S" : "Ledge N", new Vector3(134.5f, 0f, side), new Vector3(134.5f, 0f, ClearingZ + s * 4.6f), GroundY, 40f, rocks);
            }
            Sign(signs, "SURF TO THE PINE FOREST\n<size=55%>step off to the left onto the ramp, hold strafe INTO it, ride it down</size>",
                new Vector3(145f, LedgeTop + 3.2f, ClearingZ - 2.6f), 90f, 1.3f);
            ZoneAt(zones, "Surf Route Start", new Vector3(144f, LedgeTop + 1f, ClearingZ), new Vector3(6f, 2f, 6f), ZoneKind.Checkpoint, 60f);

            // Three ramps in a line, each starting lower than the last ended: fly the gap, land on the next face.
            float sin = Mathf.Sin(RampPitch * Mathf.Deg2Rad), cos = Mathf.Cos(RampPitch * Mathf.Deg2Rad);
            float ridgeStart = LedgeTop - 1f, x0 = RouteStartX;
            for (int i = 0; i < RampCount; i++)
            {
                float baseY = ridgeStart - RampHeight * cos - RampLength * 0.5f * sin;
                // Yaw -90: the prism's raised end faces west (the ledge), it runs and descends toward +x.
                MeshBrush(root, $"Surf Ramp {i + 1}", GeneratedAssets.SurfPrismMesh, new Vector3(x0 + RampLength * 0.5f, baseY, RouteZ),
                    new Vector3(RampWidth, RampHeight, RampLength), -90f, "SurfRamp", SurfaceFlags.Surf, -RampPitch);
                ridgeStart -= RampLength * sin + RampStep;
                x0 += RampLength + RampGap;
            }
            float routeEndX = x0 - RampGap;

            // The ravine: a fall anywhere sends you back to the ledge (or to the forest landing once you're there).
            float midX = (RouteStartX + routeEndX) * 0.5f;
            ZoneAt(zones, "Ravine Fall", new Vector3(midX - 5f, ForestY - 36f, RouteZ), new Vector3(routeEndX - RouteStartX + 30f, 60f, 80f), ZoneKind.Hazard);

            // Far below: a wooded valley floor (pines are added with the forest scatter) and cliff faces under the
            // edges of both maps, so nobody looks into the void. No collision: the hazard catches you long before.
            GameObject floor = Visual(root, "Ravine Floor", new Vector3((RouteStartX + FMaxX) * 0.5f + 100f, RavineFloorY - 0.5f, RouteZ), new Vector3(FMaxX - RouteStartX + 500f, 1f, 900f), "Grass");
            floor.layer = BackdropLayer; // drawn past r_drawdistance, out to the horizon
            float cliffBottom = RavineFloorY - 1f;
            // Just past each terrain's edge, tall enough to hide it (the forest bands rise up to ~15 m); low where
            // the route passes: under the ledge and ramp 1, and where you fly in over the forest landing strip.
            const float cliffTop = 13f;
            float rampStartBase = LedgeTop - 1f - RampHeight - 2f, rampSide = RouteZ + RampWidth * 0.5f + 1f;
            CliffFace(root, "Plains Cliff S", 135f, 140f, RouteZ - 210f, ClearingZ - 3.4f, cliffBottom, cliffTop);
            CliffFace(root, "Plains Cliff Under Ledge", 135f, 140f, ClearingZ - 3.4f, rampSide, cliffBottom, rampStartBase);
            CliffFace(root, "Plains Cliff N", 135f, 140f, rampSide, RouteZ + 210f, cliffBottom, cliffTop);
            float forestEdge = routeEndX - 3f;
            CliffFace(root, "Forest Cliff S", forestEdge - 5f, forestEdge, FMinZ - FBand - 3f, RouteZ - 10f, cliffBottom, ForestY + cliffTop + 3f);
            CliffFace(root, "Forest Cliff Landing", forestEdge - 5f, forestEdge, RouteZ - 10f, RouteZ + 10f, cliffBottom, ForestY - 0.3f);
            CliffFace(root, "Forest Cliff N", forestEdge - 5f, forestEdge, RouteZ + 10f, FMaxZ + FBand + 3f, cliffBottom, ForestY + cliffTop + 3f);
        }

        private const float RavineFloorY = ForestY - 40f;

        private static void CliffFace(Transform parent, string name, float x0, float x1, float z0, float z1, float bottom, float top) =>
            Visual(parent, name, new Vector3((x0 + x1) * 0.5f, (bottom + top) * 0.5f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, top - bottom, z1 - z0), "Rock");



        private static void ZoneAt(Transform parent, string name, Vector3 center, Vector3 size, ZoneKind kind, float yaw = 0f)
        {
            Zone(parent, name, center, size, kind);
            // The spawn yaw comes from the zone's rotation; keep footprints square so the bounds don't care.
            parent.GetChild(parent.childCount - 1).rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        // --- forest terrain ------------------------------------------------------------------------------------------

        private static float OutsideForest(float x, float z)
        {
            float cx = (FMinX + FMaxX) * 0.5f, cz = (FMinZ + FMaxZ) * 0.5f;
            float hx = (FMaxX - FMinX) * 0.5f - FCorner, hz = (FMaxZ - FMinZ) * 0.5f - FCorner;
            float dx = Mathf.Abs(x - cx) - hx, dz = Mathf.Abs(z - cz) - hz;
            float outside = new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dz, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(dx, dz), 0f) - FCorner;
        }

        /// <summary>Flattened to the valley floor: the camp and the landing strip below the last ramp.</summary>
        private static float ForestFlat(float x, float z)
        {
            float camp = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(((new Vector2(x, z) - new Vector2(ForestCamp.x, ForestCamp.z)).magnitude - 11f) / 8f));
            float dz = Mathf.Abs(z - RouteZ);
            float landing = x < FMinX + 20f && dz < 16f
                ? (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((dz - 9f) / 7f))) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((x - (FMinX + 6f)) / 12f)))
                : 0f;
            return Mathf.Max(camp, landing);
        }

        private static float ForestHeight(float x, float z)
        {
            float h = 2.4f * Noise(x, z, 46f, 21.7f) + 0.9f * Noise(x, z, 17f, 3.3f) + 0.3f * Noise(x, z, 7f, 8.8f);
            h += Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(OutsideForest(x, z) / FBand)) * (11f + 4f * Noise(x, z, 30f, 6.1f));
            return ForestY + Mathf.Lerp(h, 0f, ForestFlat(x, z));
        }

        private static Color ForestGroundColor(float x, float z, float y, float up)
        {
            var moss = new Color(0.27f, 0.42f, 0.21f);
            var needles = new Color(0.4f, 0.32f, 0.2f);
            var deep = new Color(0.18f, 0.3f, 0.16f);
            var dirt = new Color(0.6f, 0.48f, 0.33f);
            var rock = new Color(0.5f, 0.5f, 0.47f);
            Color c = Color.Lerp(moss, needles, Mathf.Clamp01(0.5f + 0.5f * Noise(x, z, 14f, 7.7f)) * 0.65f);
            c = Color.Lerp(c, deep, Mathf.Clamp01(OutsideForest(x, z) / 10f));
            float flat = ForestFlat(x, z);
            if (flat > 0.55f) c = Color.Lerp(c, dirt, Mathf.Clamp01((flat - 0.55f) * 3f) * 0.8f);
            if (up < 0.8f) c = Color.Lerp(c, rock, Mathf.Clamp01((0.8f - up) * 4f));
            float j = 0.04f * Noise(x * 7.3f, z * 7.3f, 1f, 2.5f);
            return new Color(c.r + j, c.g + j, c.b + j * 0.5f);
        }

        private static SimTerrain BuildForestTerrain(Transform forest)
        {
            float minX = RouteStartX + (RampLength + RampGap) * RampCount - RampGap - 3f; // tucks under the last ramp's end
            float maxX = FMaxX + FBand + 3f, minZ = FMinZ - FBand - 3f, maxZ = FMaxZ + FBand + 3f;
            int nx = Mathf.CeilToInt((maxX - minX) / TerrainCell) + 1, nz = Mathf.CeilToInt((maxZ - minZ) / TerrainCell) + 1;
            var heights = new float[nx * nz];
            for (int z = 0; z < nz; z++)
                for (int x = 0; x < nx; x++)
                    heights[z * nx + x] = ForestHeight(minX + x * TerrainCell, minZ + z * TerrainCell);

            var go = new GameObject("Forest Terrain", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(forest, false);
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
                    AddTri(verts, colors, tris, v00, v01, v11, ForestGroundColor);
                    AddTri(verts, colors, tris, v00, v11, v10, ForestGroundColor);
                }
            var mesh = new Mesh { name = "Forest Terrain", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            SaveGeneratedMesh(mesh, "ForestTerrain");
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(GeneratedAssets.ResourcesDir + "/FX/M_Terrain.mat");
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
            return terrain;
        }

        // --- the forest map ----------------------------------------------------------------------------------------

        private static void BuildPineForest(Transform level, Transform zones, Transform signs)
        {
            Transform forest = Group(level, "Pine Forest");
            Transform props = Group(forest, "Props");
            var rng = new System.Random(20261117);
            SimTerrain plainsTerrain = _terrain;
            _terrain = BuildForestTerrain(forest); // GroundY/Prop/Instance now place things on the forest floor

            // Landing strip below the last ramp: once you're down, a fall respawns you here, not back at the ledge.
            ZoneAt(zones, "Forest Landing", new Vector3(FMinX - 2f, ForestY + 1f, RouteZ), new Vector3(16f, 2f, 16f), ZoneKind.Checkpoint, 90f);
            Sign(signs, "PINE FOREST\n<size=55%>deer country - the trading post is to the south</size>", new Vector3(FMinX + 14f, ForestY + 3.6f, RouteZ - 6f), 90f, 1.4f);

            BuildForestBoundary(forest);
            BuildForestCamp(forest, props, zones, signs);

            // Deer glades: open ground with a log or two, ringed by pines (placed below, kept out of the glade).
            for (int g = 0; g < DeerGlades.Length; g++)
            {
                Vector3 c = OnGround(DeerGlades[g]);
                Prop(props, "log_large", c + Ring(rng, 5f, 8f), 3.5f, rng, collide: true);
                Prop(props, "stump_old", c + Ring(rng, 6f, 9f), 3.5f, rng, collide: true);
                Spawner(forest, $"Deer {g + 1}", "prey.deer", c + new Vector3(0f, 0.2f, 0f), 2, 14f, 45f);
            }
            for (int w = 0; w < ForestWarrens.Length; w++)
            {
                Vector3 c = OnGround(ForestWarrens[w]);
                Prop(props, "stump_round", c + Ring(rng, 4f, 7f), 3f, rng, collide: true);
                Prop(props, "log", c + Ring(rng, 5f, 8f), 3f, rng, collide: true);
                Spawner(forest, $"Forest Rabbits {w + 1}", "prey.rabbit", c + new Vector3(0f, 0.2f, 0f), 3, 10f, 25f);
            }

            // Rocks and fallen trees between the pines (collide).
            string[] rocks = { "rock_largeA", "rock_largeB", "rock_largeC", "rock_tallA", "stone_tallA" };
            for (int i = 0; i < 16; i++) Prop(props, rocks[rng.Next(rocks.Length)], ForestOpenPoint(rng, 4f), Range(rng, 2.5f, 4.2f), rng, collide: true);
            for (int i = 0; i < 8; i++) Prop(props, rng.Next(2) == 0 ? "log_large" : "log", ForestOpenPoint(rng, 4f), Range(rng, 3.5f, 5f), rng, collide: true);
            for (int i = 0; i < 10; i++) Prop(props, rng.Next(2) == 0 ? "tree_pineRoundA" : "tree_pineRoundC", ForestOpenPoint(rng, 3f), Range(rng, 4.4f, 5.6f), rng, collide: true, trunkOnly: true);

            BuildForestScatter(forest, props, rng);
            BuildForestBackdrop(forest, rng);
            BuildForestFauna(forest, props, signs);
            _terrain = plainsTerrain;
        }

        /// <summary>
        /// The forest's own animals (2026-11-24): squirrels in the pines, foxes, boars, and the Old Bear's den. Own
        /// random sequence, so adding them leaves the rest of the forest where it was.
        /// </summary>
        private static void BuildForestFauna(Transform forest, Transform props, Transform signs)
        {
            var rng = new System.Random(20261124);
            for (int i = 0; i < SquirrelPines.Length; i++)
                Spawner(forest, $"Squirrels {i + 1}", "prey.squirrel", OnGround(SquirrelPines[i]) + new Vector3(0f, 0.2f, 0f), 3, 8f, 30f);
            for (int i = 0; i < FoxDens.Length; i++)
            {
                Vector3 c = OnGround(FoxDens[i]);
                Prop(props, "stump_old", c + Ring(rng, 4f, 6f), 3f, rng, collide: true);
                Spawner(forest, $"Foxes {i + 1}", "prey.fox", c + new Vector3(0f, 0.2f, 0f), 2, 12f, 60f);
            }
            for (int i = 0; i < BoarWallows.Length; i++)
            {
                Vector3 c = OnGround(BoarWallows[i]);
                Visual(props, $"Wallow {i + 1}", c + new Vector3(0f, 0.02f, 0f), new Vector3(5f, 0.04f, 4f), "Sand");
                Spawner(forest, $"Boars {i + 1}", "prey.boar", c + new Vector3(0f, 0.2f, 0f), 2, 12f, 75f);
            }

            // The den: a ring of boulders with gaps, old bones, a warning sign facing the camp.
            Vector3 den = OnGround(BearDen);
            string[] boulders = { "rock_largeA", "rock_largeB", "rock_largeC", "rock_tallA" };
            for (int i = 0; i < 6; i++)
            {
                float a = (i + 0.25f + (float)rng.NextDouble() * 0.4f) / 6f * Mathf.PI * 2f;
                Vector3 at = den + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * Range(rng, 10f, 12f);
                Prop(props, boulders[i % boulders.Length], OnGround(at), Range(rng, 3.5f, 4.5f), rng, collide: true);
            }
            for (int i = 0; i < 7; i++)
            {
                Vector3 at = OnGround(den + new Vector3(Range(rng, -6f, 6f), 0f, Range(rng, -6f, 6f)));
                Visual(props, "Bone", at + new Vector3(0f, 0.06f, 0f), new Vector3(0.09f, 0.09f, Range(rng, 0.5f, 0.9f)), "Wall", Range(rng, 0f, 360f));
            }
            Sign(signs, "BEAR DEN\n<size=55%>the Old Bear of the Pines sleeps here - a bait from Ash's camp wakes it</size>", den + new Vector3(-14f, 3.4f, 0f), 90f, 1.2f);
            Spawner(forest, "Boss Old Bear", "prey.old_bear", den + new Vector3(0f, 0.3f, 0f), 1, 2f, 0f, summoned: true);
        }

        private static void BuildForestBoundary(Transform forest)
        {
            Transform walls = Group(forest, "Forest Wall (collision)");
            var rocks = new System.Random(96);
            const int segments = 96;
            var ring = new List<Vector3>();
            for (int i = 0; i < segments; i++) ring.Add(ForestRingPoint(i / (float)segments, 3f));
            for (int i = 0; i < segments; i++)
            {
                Vector3 a = ring[i], b = ring[(i + 1) % segments];
                Vector3 mid = (a + b) * 0.5f;
                if (mid.x >= FMinX)
                {
                    RockWall(walls, $"Wall {i}", a, b, ForestHeight, ForestY + 40f, rocks);
                    continue;
                }
                // West side: the landing strip from the ravine is cut out exactly, so surfers arriving off-centre
                // don't meet a leftover piece of invisible wall in the air.
                SplitAtOpening(a, b, RouteZ, LandingHalfWidth, out _, out _, Pieces);
                for (int k = 0; k < Pieces.Count; k += 2) RockWall(walls, $"Wall {i}.{k / 2}", Pieces[k], Pieces[k + 1], ForestHeight, ForestY + 40f, rocks);
            }
            // Sides of the landing strip, from the ravine edge to the ring (the ravine end itself stays open).
            float edge = RouteStartX + (RampLength + RampGap) * RampCount - RampGap - 3f;
            for (int s = -1; s <= 1; s += 2)
            {
                float z = RouteZ + s * LandingHalfWidth;
                float ringX = FMinX;
                for (int k = 0; k < 40; k++) if (OutsideForest(ringX, z) < 3f) ringX -= 0.5f;
                RockWall(walls, $"Landing Side {(s < 0 ? "S" : "N")}", new Vector3(edge + 1f, 0f, z), new Vector3(ringX, 0f, z), ForestHeight, ForestY + 40f, rocks);
            }
        }

        private static Vector3 ForestRingPoint(float t, float outward)
        {
            float cx = (FMinX + FMaxX) * 0.5f, cz = (FMinZ + FMaxZ) * 0.5f;
            float angle = t * Mathf.PI * 2f;
            var dir = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
            float lo = 0f, hi = 200f;
            for (int k = 0; k < 40; k++)
            {
                float mid = (lo + hi) * 0.5f;
                if (OutsideForest(cx + dir.x * mid, cz + dir.y * mid) < outward) lo = mid; else hi = mid;
            }
            return new Vector3(cx + dir.x * lo, 0f, cz + dir.y * lo);
        }

        private static void BuildForestCamp(Transform forest, Transform props, Transform zones, Transform signs)
        {
            Transform camp = Group(forest, "Forest Camp");
            Vector3 c = ForestCamp;
            var rng = new System.Random(417);
            Prop(camp, "campfire_stones", c, 3f, rng, collide: false, yaw: 0f);
            Prop(camp, "campfire_logs", c, 3f, rng, collide: false, yaw: 30f);
            Campfire(camp, c + new Vector3(0f, 0.25f, 0f));
            Prop(camp, "log", c + new Vector3(-2.4f, 0f, 0.6f), 3f, rng, collide: false, yaw: 80f);
            Prop(camp, "tent_smallOpen", c + new Vector3(-3f, 0f, -7.5f), 4f, rng, collide: true, yaw: 20f);
            Prop(camp, "log_stack", c + new Vector3(4f, 0f, -6.5f), 3f, rng, collide: true, yaw: 0f);

            // Ash's trading post: forest prices (x1.5), so a full forest bag is worth carrying here.
            Station(camp, signs, CampStationKind.Sell, "Trading Post", "TRADING POST\n<size=55%>forest prices x1.5</size>",
                c + new Vector3(7f, 0f, 0f), 90f, "npc.forest_trader", ForestExpectedSale, ForestPriceMultiplier);

            // The bait stall: the Honeycomb Bait wakes the Old Bear in its den (the boss only comes out for a bait).
            Station(camp, signs, CampStationKind.Shop, "Bait Stall", "BAIT STALL\n<size=55%>wake the Old Bear</size>",
                c + new Vector3(1f, 0f, 7f), 0f, baitStall: true);

            // The quick way back: a pad to the Plains camp.
            Transform home = Marker(zones, "Plains Camp Arrival", CampCenter + new Vector3(0f, 0f, -4.5f), 0f);
            Vector3 pad = c + new Vector3(-7f, 0f, 2f);
            Visual(camp, "Pad Plains", pad + new Vector3(0f, 0.02f, 0f), new Vector3(2.2f, 0.04f, 2.2f), "Pad");
            Zone(zones, "Teleport Plains", pad + new Vector3(0f, 1f, 0f), new Vector3(1.6f, 2f, 1.6f), ZoneKind.Teleport, home);
            Sign(signs, "BACK TO THE PLAINS\n<size=55%>step on the pad</size>", pad + new Vector3(0f, 2.4f, 0f), 270f, 0.9f);
        }

        /// <summary>A point inside the forest away from the camp, the landing strip, the glades and the warrens.</summary>
        private static Vector3 ForestOpenPoint(System.Random rng, float clearance)
        {
            for (int attempt = 0; attempt < 60; attempt++)
            {
                var p = new Vector3(Range(rng, FMinX, FMaxX), 0f, Range(rng, FMinZ, FMaxZ));
                if (OutsideForest(p.x, p.z) > -clearance || ForestFlat(p.x, p.z) > 0.05f) continue;
                if ((p - new Vector3(ForestCamp.x, 0f, ForestCamp.z)).magnitude < 16f + clearance) continue;
                bool blocked = false;
                foreach (Vector3 g in DeerGlades) if ((p - g).sqrMagnitude < (9f + clearance) * (9f + clearance)) blocked = true;
                foreach (Vector3 w in ForestWarrens) if ((p - w).sqrMagnitude < (5f + clearance) * (5f + clearance)) blocked = true;
                if ((p - BearDen).sqrMagnitude < (12f + clearance) * (12f + clearance)) blocked = true; // the den stays open
                if (!blocked) return p;
            }
            return new Vector3(Range(rng, FMinX + 30f, FMaxX - 30f), 0f, Range(rng, FMinZ + 30f, FMaxZ - 30f));
        }

        // --- pines, undergrowth, horizon ---------------------------------------------------------------------------

        private static void BuildForestScatter(Transform forest, Transform props, System.Random rng)
        {
            var go = new GameObject("Scatter (instanced)");
            go.transform.SetParent(forest, false);
            ScatterRenderer scatter = go.AddComponent<ScatterRenderer>();
            Material terrainMat = AssetDatabase.LoadAssetAtPath<Material>(GeneratedAssets.ResourcesDir + "/FX/M_Terrain.mat");
            Mesh[] pines = { PineMesh(0, 5, 2.4f, 1.0f), PineMesh(1, 6, 2.0f, 1.25f), PineMesh(2, 4, 2.8f, 0.85f) };
            var pineLists = new List<Matrix4x4>[pines.Length];
            for (int k = 0; k < pines.Length; k++) pineLists[k] = new List<Matrix4x4>();

            // The dark ring of tall pines all round (the invisible wall stops players), open at the landing strip.
            float minX = RouteStartX + (RampLength + RampGap) * RampCount - RampGap;
            for (float x = minX; x <= FMaxX + FBand; x += 4.6f)
                for (float z = FMinZ - FBand; z <= FMaxZ + FBand; z += 4.6f)
                {
                    float px = x + Range(rng, -2f, 2f), pz = z + Range(rng, -2f, 2f);
                    if (OutsideForest(px, pz) < -1f + Range(rng, 0f, 3f)) continue;
                    if (px < FMinX + 4f && Mathf.Abs(pz - RouteZ) < 12f) continue;
                    pineLists[rng.Next(pines.Length)].Add(Instance(new Vector3(px, 0f, pz), Range(rng, 1.1f, 1.6f), rng));
                }
            // Pines through the valley in loose stands; these block (an invisible trunk each).
            Transform trunks = Group(props, "Pine Trunks (collision)");
            for (int stand = 0; stand < 22; stand++)
            {
                Vector3 c = ForestOpenPoint(rng, 5f);
                int n = 3 + rng.Next(5);
                for (int i = 0; i < n; i++)
                {
                    Vector3 p = c + Disc(rng, 6f);
                    if (OutsideForest(p.x, p.z) > -2f || ForestFlat(p.x, p.z) > 0.05f) continue;
                    float s = Range(rng, 0.9f, 1.4f);
                    pineLists[rng.Next(pines.Length)].Add(Instance(p, s, rng));
                    float y = GroundY(p.x, p.z);
                    GameObject trunk = Box(trunks, "Pine Collision", new Vector3(p.x, y + 3f, p.z), new Vector3(0.5f * s, 6f, 0.5f * s), "Rock");
                    trunk.GetComponent<MeshRenderer>().enabled = false;
                }
            }
            // The valley floor far below the surf route: big pines, so the ravine looks deep and wooded.
            float routeEnd = RouteStartX + (RampLength + RampGap) * RampCount - RampGap;
            for (float x = RouteStartX - 2f; x <= routeEnd; x += 7f)
                for (float z = RouteZ - 200f; z <= RouteZ + 200f; z += 7f)
                {
                    float px = x + Range(rng, -3f, 3f), pz = z + Range(rng, -3f, 3f);
                    if (Noise(px, pz, 40f, 12.4f) < -0.35f) continue; // clearings
                    var p = new Vector3(px, RavineFloorY - 0.2f, pz);
                    pineLists[rng.Next(pines.Length)].Add(Matrix4x4.TRS(p, Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), Vector3.one * Range(rng, 1.6f, 2.4f)));
                }
            for (int k = 0; k < pines.Length; k++)
                if (pineLists[k].Count > 0) scatter.SetLayer(pines[k], new[] { terrainMat }, pineLists[k], PineChunk, 420f, true);

            // Sparse grass, ferny bushes, mushrooms and small rocks on the forest floor.
            string[] floor = { "grass", "grass_leafs", "plant_bush", "plant_bushSmall", "mushroom_redGroup", "mushroom_tanGroup", "rock_smallA", "rock_smallC" };
            var lists = new List<Matrix4x4>[floor.Length];
            for (int k = 0; k < floor.Length; k++) lists[k] = new List<Matrix4x4>();
            for (int i = 0; i < 5000; i++)
            {
                var p = new Vector3(Range(rng, FMinX - 4f, FMaxX + 4f), 0f, Range(rng, FMinZ - 4f, FMaxZ + 4f));
                if (OutsideForest(p.x, p.z) > 4f || ForestFlat(p.x, p.z) > 0.6f) continue;
                int k = rng.Next(floor.Length);
                if (k < 2 && rng.NextDouble() < 0.4) continue; // grass thinner than in the meadow
                if (k >= 4 && rng.NextDouble() < 0.75) continue; // mushrooms and pebbles: a few here and there
                lists[k].Add(Instance(p, k >= 4 ? Range(rng, 1.1f, 1.7f) : Range(rng, 2f, 3.2f), rng));
            }
            for (int k = 0; k < floor.Length; k++) AddLayer(scatter, floor[k], lists[k], ClutterChunk, k < 2 ? 85f : 120f, k == 2 || k == 3, k < 2 ? GrassFarDensity : PropFarDensity);
        }

        /// <summary>
        /// A low-poly conifer (ours: the Nature Kit only has round pines): a hexagonal trunk and stacked seven-sided
        /// cones, one colour per face, ~8 m tall at scale 1. Uses the terrain material (vertex colours).
        /// </summary>
        private static Mesh PineMesh(int variant, int tiers, float baseRadius, float slim)
        {
            var verts = new List<Vector3>();
            var colors = new List<Color>();
            var tris = new List<int>();
            var bark = new Color(0.36f, 0.25f, 0.17f);
            void Face(Vector3 a, Vector3 b, Vector3 c, Color col)
            {
                int i = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                colors.Add(col); colors.Add(col); colors.Add(col);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            }
            Vector3 Around(float angle, float r, float y) => new Vector3(Mathf.Sin(angle) * r, y, Mathf.Cos(angle) * r);

            const int trunkSides = 6;
            const float trunkR = 0.22f, trunkH = 2.2f;
            for (int s = 0; s < trunkSides; s++)
            {
                float a0 = s * Mathf.PI * 2f / trunkSides, a1 = (s + 1) * Mathf.PI * 2f / trunkSides;
                Vector3 b0 = Around(a0, trunkR, 0f), b1 = Around(a1, trunkR, 0f), t0 = Around(a0, trunkR * 0.8f, trunkH), t1 = Around(a1, trunkR * 0.8f, trunkH);
                Color col = bark * (0.9f + 0.1f * (s % 2));
                Face(b0, t0, t1, col);
                Face(b0, t1, b1, col);
            }

            const int sides = 7;
            float y = 1.4f, tierH = 2.6f * slim, step = 1.45f * slim;
            for (int t = 0; t < tiers; t++)
            {
                float r = baseRadius * (1f - t / (float)(tiers + 0.6f));
                float twist = t * 0.45f + variant;
                var shade = Color.Lerp(new Color(0.16f, 0.33f, 0.2f), new Color(0.24f, 0.45f, 0.25f), t / (float)tiers);
                Vector3 apex = new Vector3(0f, y + tierH, 0f);
                for (int s = 0; s < sides; s++)
                {
                    float a0 = twist + s * Mathf.PI * 2f / sides, a1 = twist + (s + 1) * Mathf.PI * 2f / sides;
                    Vector3 p0 = Around(a0, r, y), p1 = Around(a1, r, y);
                    float lit = 0.88f + 0.12f * Mathf.Cos((a0 + a1) * 0.5f - 0.8f); // a hint of a sun side
                    Face(p0, apex, p1, shade * lit);
                    Face(p0, p1, new Vector3(0f, y, 0f), shade * 0.7f); // underside
                }
                y += step;
            }

            var mesh = new Mesh { name = $"Pine{variant}" };
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            SaveGeneratedMesh(mesh, $"Pine{variant}");
            return AssetDatabase.LoadAssetAtPath<Mesh>(GeneratedAssets.MeshesDir + $"/Pine{variant}.asset");
        }

        private static void BuildForestBackdrop(Transform forest, System.Random rng)
        {
            Transform sky = Group(forest, "Horizon");
            var material = AssetDatabase.LoadAssetAtPath<Material>(GeneratedAssets.ResourcesDir + "/FX/M_Terrain.mat");
            var center = new Vector3((FMinX + FMaxX) * 0.5f, ForestY, (FMinZ + FMaxZ) * 0.5f);
            for (int i = 0; i < 9; i++)
            {
                float angle = i / 9f * Mathf.PI * 2f + Range(rng, -0.12f, 0.12f);
                if (Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, 270f)) < 40f) continue; // the ravine and the Plains are that way
                float dist = Range(rng, 170f, 220f);
                var pos = center + new Vector3(Mathf.Sin(angle) * dist, -4f, Mathf.Cos(angle) * dist);
                Mesh m = Mountain(rng, Range(rng, 70f, 120f), Range(rng, 60f, 100f), 100 + i);
                var go = new GameObject($"Mountain {i}", typeof(MeshFilter), typeof(MeshRenderer));
                go.layer = BackdropLayer;
                go.transform.SetParent(sky, false);
                go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f));
                go.GetComponent<MeshFilter>().sharedMesh = m;
                go.GetComponent<MeshRenderer>().sharedMaterial = material;
                go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var cloudMat = AssetDatabase.LoadAssetAtPath<Material>(GeneratedAssets.ResourcesDir + "/FX/M_Cloud.mat");
            Mesh cloud = AssetDatabase.LoadAssetAtPath<Mesh>(GeneratedAssets.MeshesDir + "/Cloud.asset");
            for (int i = 0; i < 8; i++)
            {
                var go = new GameObject($"Cloud {i}", typeof(MeshFilter), typeof(MeshRenderer));
                go.layer = BackdropLayer;
                go.transform.SetParent(sky, false);
                go.transform.position = center + new Vector3(Range(rng, -160f, 160f), Range(rng, 70f, 100f), Range(rng, -160f, 160f));
                go.transform.localScale = Vector3.one * Range(rng, 6f, 11f);
                go.GetComponent<MeshFilter>().sharedMesh = cloud;
                go.GetComponent<MeshRenderer>().sharedMaterial = cloudMat;
                go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.AddComponent<CloudDrift>().Configure(new Vector3(1.2f, 0f, 0.4f) * Range(rng, 0.6f, 1.4f), center, 180f);
            }
        }
    }
}
