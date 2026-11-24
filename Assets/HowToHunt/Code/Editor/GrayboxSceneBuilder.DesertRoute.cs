using System.Collections.Generic;
using HowToHunt.Core;
using HowToHunt.Economy;
using HowToHunt.Sim.Collision;
using UnityEngine;

namespace HowToHunt.Editor
{
    /// <summary>
    /// The way down to the Red Desert (owner, 2026-11-24: "a much harder surf course"): a trail out of the forest's
    /// south rim ends on a ledge over a deep canyon; five surf ramps zigzag down 90 m to the desert's north landing.
    /// Much harder than the forest route: narrower, steeper ramps (12 m wide down to 9, against 18), bigger gaps (13 to
    /// 16 m against 8), and each ramp sits to the side of the last, so you have to carry your line across the gap in the
    /// air. A fall anywhere puts you back on the ledge. The trail opens once the Old Bear has fallen (the forest boss
    /// flag); the movement lab keeps a test pad to the desert.
    /// </summary>
    public static partial class GrayboxSceneBuilder
    {
        public const float DesertRouteX = 440f;                 // the route runs south (-z) along this line
        private const float DesertLedgeZ = 142f;                // the ledge's south edge: ramp 1 starts just past it
        private const float DesertTrailHalfWidth = 6f;
        private const float HardRampHeight = 12f, HardRampPitch = 10f, HardRampStep = 17f; // each ramp starts below the lowest line you can ride on the last
        // Per ramp: sideways offset of its ridge from DesertRouteX, length, base width, and the gap after it.
        public static readonly float[] HardRampX = { 0f, -9f, 2f, -8f, 2f };
        public static readonly float[] HardRampLength = { 52f, 48f, 50f, 46f, 52f };
        public static readonly float[] HardRampWidth = { 12f, 11f, 10f, 9f, 9f };
        public static readonly float[] HardRampGap = { 13f, 14f, 14f, 15f };

        /// <summary>Ramp i's ridge line: from its raised start (north) to its low end (south), in world space.</summary>
        public static void HardRamp(int i, out Vector3 start, out Vector3 end)
        {
            float sin = Mathf.Sin(HardRampPitch * Mathf.Deg2Rad), cos = Mathf.Cos(HardRampPitch * Mathf.Deg2Rad);
            float z = DesertLedgeZ + 1f, ridge = ForestY - 1f; // tilted, the ridge starts ~2 m south of this: right at the ledge
            for (int k = 0; k < i; k++)
            {
                z -= HardRampLength[k] * cos + HardRampGap[k];
                ridge -= HardRampLength[k] * sin + HardRampStep;
            }
            start = new Vector3(DesertRouteX + HardRampX[i], ridge, z);
            end = start + new Vector3(0f, -HardRampLength[i] * sin, -HardRampLength[i] * cos);
        }

        /// <summary>Where the route lands in the desert (the far end of the last ramp, carried on a little).</summary>
        public static float DesertRouteEndZ
        {
            get
            {
                HardRamp(HardRampLength.Length - 1, out _, out Vector3 end);
                return end.z;
            }
        }

        public static Vector3 DesertRouteStart => new Vector3(DesertRouteX, ForestY + 0.1f, DesertLedgeZ + 3f);

        private static void BuildDesertRoute(Transform level, Transform zones, Transform signs)
        {
            Transform root = Group(level, "Surf Route South (hard)");
            var rng = new System.Random(4401);

            // The ledge at the end of the forest trail, and the checkpoint a fall brings you back to.
            // Its top sits 5 cm above the trail's ground: level with it, the two flickered (z-fighting).
            Box(root, "Canyon Ledge", new Vector3(DesertRouteX, ForestY - 10f + LedgeLift, DesertLedgeZ + 4f), new Vector3(2f * DesertTrailHalfWidth + 2f, 20f, 8f), "Rock");
            ZoneAt(zones, "Desert Route Start", new Vector3(DesertRouteX, ForestY + 1f, DesertLedgeZ + 4f), new Vector3(10f, 2f, 6f), ZoneKind.Checkpoint, 180f);
            Sign(signs, "TO THE RED DESERT\n<size=55%>the hard way down: five narrow ramps, each to the side of the last</size>",
                new Vector3(DesertRouteX, ForestY + 3.6f, DesertLedgeZ + 7.5f), 180f, 1.3f);

            // The five ramps: yaw 0, the raised end north; they descend toward -z.
            float lowest = float.MaxValue;
            for (int i = 0; i < HardRampLength.Length; i++)
            {
                HardRamp(i, out Vector3 start, out Vector3 end);
                Vector3 mid = (start + end) * 0.5f;
                float cos = Mathf.Cos(HardRampPitch * Mathf.Deg2Rad);
                MeshBrush(root, $"Hard Ramp {i + 1}", GeneratedAssets.SurfPrismMesh, new Vector3(mid.x, mid.y - HardRampHeight * cos, mid.z),
                    new Vector3(HardRampWidth[i], HardRampHeight, HardRampLength[i]), 0f, "SurfRamp", SurfaceFlags.Surf, -HardRampPitch);
                // A fall is only a fall once you are below anything you could still land on: under the ramp, below its
                // lowest base; over the gap after it, below the next ramp's base (riding low, you can still make it).
                float zTop = i == 0 ? DesertLedgeZ - 1f : start.z;
                FallZone(zones, $"Canyon Fall {i + 1}", zTop, end.z, end.y - HardRampHeight - 3f);
                if (i + 1 < HardRampLength.Length)
                {
                    HardRamp(i + 1, out Vector3 next, out _);
                    FallZone(zones, $"Canyon Fall {i + 1}-{i + 2}", end.z, next.z, next.y - HardRampHeight - 3f);
                }
                lowest = Mathf.Min(lowest, end.y - HardRampHeight);
            }

            // Far below: a red canyon floor and walls under both edges, so nobody looks into the void (no collision).
            float routeEnd = DesertRouteEndZ;
            float floorY = DesertY - 36f;
            GameObject floor = Visual(root, "Canyon Floor", new Vector3(DesertRouteX, floorY, (DesertLedgeZ + routeEnd) * 0.5f), new Vector3(260f, 1f, DesertLedgeZ - routeEnd + 20f), "MesaDark");
            floor.layer = BackdropLayer;
            for (int s = -1; s <= 1; s += 2)
            {
                // Canyon walls on both sides of the route, stepped like the desert's mesas.
                for (int k = 0; k < 7; k++)
                {
                    float z0 = DesertLedgeZ - k * (DesertLedgeZ - routeEnd) / 7f, z1 = z0 - (DesertLedgeZ - routeEnd) / 7f;
                    float x = DesertRouteX + s * Range(rng, 48f, 60f);
                    float top = Mathf.Lerp(ForestY + 6f, DesertY + 16f, k / 6f) + Range(rng, -4f, 4f);
                    GameObject wall = Visual(root, $"Canyon Wall {(s < 0 ? "W" : "E")} {k}", new Vector3(x, (floorY + top) * 0.5f, (z0 + z1) * 0.5f),
                        new Vector3(Range(rng, 14f, 24f), top - floorY, z0 - z1 + 2f), k % 2 == 0 ? "Mesa" : "MesaDark", Range(rng, -6f, 6f));
                    wall.layer = BackdropLayer;
                }
            }
            // Under the forest's south edge (low where the ledge is) and the desert's north edge (open at the landing).
            CliffFace(root, "Forest Cliff South W", FMinX - FBand - 20f, DesertRouteX - DesertTrailHalfWidth - 1f, DesertLedgeZ - 4f, DesertLedgeZ, floorY, ForestY + 16f);
            CliffFace(root, "Forest Cliff South E", DesertRouteX + DesertTrailHalfWidth + 1f, FMaxX + FBand + 20f, DesertLedgeZ - 4f, DesertLedgeZ, floorY, ForestY + 16f);
            CliffFace(root, "Forest Cliff Under Ledge", DesertRouteX - DesertTrailHalfWidth - 1f, DesertRouteX + DesertTrailHalfWidth + 1f, DesertLedgeZ - 4f, DesertLedgeZ, floorY, ForestY - 20f);
            float desertNorth = DMaxZ + DBand + 3f;
            CliffFace(root, "Desert Cliff North W", DMinX - DBand - 20f, DesertRouteX - DesertLandingHalfWidth, desertNorth, desertNorth + 4f, floorY, DesertY + 18f, "Mesa");
            CliffFace(root, "Desert Cliff North E", DesertRouteX + DesertLandingHalfWidth, DMaxX + DBand + 20f, desertNorth, desertNorth + 4f, floorY, DesertY + 18f, "Mesa");
            CliffFace(root, "Desert Cliff Landing", DesertRouteX - DesertLandingHalfWidth, DesertRouteX + DesertLandingHalfWidth, desertNorth, desertNorth + 4f, floorY, DesertY - 0.3f, "MesaDark");

            // The landing: once you're down, falls (there are none in the desert) and respawns stay here.
            ZoneAt(zones, "Desert Landing", new Vector3(DesertRouteX, DesertY + 1f, desertNorth - 14f), new Vector3(20f, 2f, 14f), ZoneKind.Checkpoint, 180f);
            Sign(signs, "THE RED DESERT\n<size=55%>you made it - Dry Creek camp is to the south-west</size>", new Vector3(DesertRouteX + 10f, DesertY + 3.6f, desertNorth - 20f), 0f, 1.3f);
        }

        private const float LedgeLift = 0.05f;

        /// <summary>A hazard box from z0 (north) to z1 (south), its top at topY, wide enough for any line.</summary>
        private static void FallZone(Transform zones, string name, float z0, float z1, float topY) =>
            ZoneAt(zones, name, new Vector3(DesertRouteX, topY - 30f, (z0 + z1) * 0.5f), new Vector3(120f, 60f, Mathf.Abs(z0 - z1)), ZoneKind.Hazard);

        /// <summary>The trail across the forest's south band to the ledge is flat at the forest floor.</summary>
        private static float DesertTrailFlat(float x, float z)
        {
            if (z > FMinZ + 6f || z < DesertLedgeZ - 2f) return 0f;
            float dx = Mathf.Abs(x - DesertRouteX);
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((dx - DesertTrailHalfWidth) / 6f));
        }

        /// <summary>SplitAtOpening for an opening across x (a north or south edge): the same cut with x and z swapped.</summary>
        private static void SplitAtOpeningX(Vector3 a, Vector3 b, float centerX, float halfWidth, out Vector3 inA, out Vector3 inB, List<Vector3> outside)
        {
            SplitAtOpening(Swap(a), Swap(b), centerX, halfWidth, out inA, out inB, outside);
            inA = Swap(inA);
            inB = Swap(inB);
            for (int i = 0; i < outside.Count; i++) outside[i] = Swap(outside[i]);
        }

        private static Vector3 Swap(Vector3 v) => new Vector3(v.z, v.y, v.x);
    }
}
