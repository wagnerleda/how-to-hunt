#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;

namespace HowToHunt.Sim.Collision
{
    /// <summary>Result of sweeping an axis-aligned box through the world.</summary>
    public struct TraceResult
    {
        /// <summary>0..1: how far along start→end the box moved before touching something.</summary>
        public float Fraction;
        public Vector3 EndPosition;
        /// <summary>Surface normal of what was hit (zero when nothing was hit).</summary>
        public Vector3 Normal;
        /// <summary>The box started inside a brush.</summary>
        public bool StartSolid;
        /// <summary>The box never left solid (stuck).</summary>
        public bool AllSolid;
        /// <summary>Surface flags of what was hit (e.g. surf).</summary>
        public SurfaceFlags Surface;

        public bool Hit => Fraction < 1f;
    }

    [Flags]
    public enum SurfaceFlags : byte
    {
        None = 0,
        /// <summary>Authored surf ramp (informational: surfing emerges from slope steepness).</summary>
        Surf = 1 << 0,
        /// <summary>Touching this resets the player to the last checkpoint.</summary>
        Hazard = 1 << 1,
        /// <summary>Not solid: a water volume. Traces pass through; movement slows inside (see InWater).</summary>
        Water = 1 << 2,
    }

    public interface ICollisionWorld
    {
        TraceResult TraceBox(Vector3 start, Vector3 end, Vector3 halfExtents);
        /// <summary>Is this point inside a water volume?</summary>
        bool InWater(Vector3 point);
    }

    public readonly struct Plane3
    {
        public readonly Vector3 Normal;
        public readonly float Distance;

        public Plane3(Vector3 normal, float distance)
        {
            Normal = normal;
            Distance = distance;
        }

        public static Plane3 FromPointNormal(Vector3 point, Vector3 normal)
        {
            Vector3 n = Vector3.Normalize(normal);
            return new Plane3(n, Vector3.Dot(n, point));
        }
    }

    /// <summary>
    /// Convex solid defined by outward-facing planes, like a Quake/Source brush.
    /// Boxes, ramps and wedges are all brushes. Tracing a box against brushes is exact, cheap and deterministic.
    /// </summary>
    public sealed class Brush
    {
        public readonly Plane3[] Planes;
        public readonly Vector3 Min;
        public readonly Vector3 Max;
        public readonly SurfaceFlags Surface;

        public Brush(Plane3[] planes, Vector3 min, Vector3 max, SurfaceFlags surface = SurfaceFlags.None)
        {
            Surface = surface;
            Min = min;
            Max = max;
            // Add axial bevel planes so box traces don't snag on edges of rotated brushes.
            var list = new List<Plane3>(planes.Length + 6);
            list.AddRange(planes);
            AddIfMissing(list, new Plane3(Vector3.UnitX, max.X));
            AddIfMissing(list, new Plane3(-Vector3.UnitX, -min.X));
            AddIfMissing(list, new Plane3(Vector3.UnitY, max.Y));
            AddIfMissing(list, new Plane3(-Vector3.UnitY, -min.Y));
            AddIfMissing(list, new Plane3(Vector3.UnitZ, max.Z));
            AddIfMissing(list, new Plane3(-Vector3.UnitZ, -min.Z));
            Planes = list.ToArray();
        }

        private static void AddIfMissing(List<Plane3> planes, Plane3 p)
        {
            foreach (Plane3 q in planes)
                if (Vector3.Dot(q.Normal, p.Normal) > 0.9999f && MathF.Abs(q.Distance - p.Distance) < 1e-4f) return;
            planes.Add(p);
        }

        /// <summary>Axis-aligned box brush.</summary>
        public static Brush Box(Vector3 center, Vector3 halfExtents, SurfaceFlags surface = SurfaceFlags.None)
            => Oriented(center, halfExtents, Quaternion.Identity, surface);

        /// <summary>Oriented box brush.</summary>
        public static Brush Oriented(Vector3 center, Vector3 halfExtents, Quaternion rotation, SurfaceFlags surface = SurfaceFlags.None)
        {
            Vector3 ax = Vector3.Transform(Vector3.UnitX, rotation);
            Vector3 ay = Vector3.Transform(Vector3.UnitY, rotation);
            Vector3 az = Vector3.Transform(Vector3.UnitZ, rotation);
            var planes = new[]
            {
                Plane3.FromPointNormal(center + ax * halfExtents.X, ax),
                Plane3.FromPointNormal(center - ax * halfExtents.X, -ax),
                Plane3.FromPointNormal(center + ay * halfExtents.Y, ay),
                Plane3.FromPointNormal(center - ay * halfExtents.Y, -ay),
                Plane3.FromPointNormal(center + az * halfExtents.Z, az),
                Plane3.FromPointNormal(center - az * halfExtents.Z, -az),
            };
            Vector3 ext = new Vector3(
                MathF.Abs(ax.X) * halfExtents.X + MathF.Abs(ay.X) * halfExtents.Y + MathF.Abs(az.X) * halfExtents.Z,
                MathF.Abs(ax.Y) * halfExtents.X + MathF.Abs(ay.Y) * halfExtents.Y + MathF.Abs(az.Y) * halfExtents.Z,
                MathF.Abs(ax.Z) * halfExtents.X + MathF.Abs(ay.Z) * halfExtents.Y + MathF.Abs(az.Z) * halfExtents.Z);
            return new Brush(planes, center - ext, center + ext, surface);
        }

        /// <summary>
        /// One terrain triangle as a solid prism: the triangle on top, vertical sides through its edges, flat bottom.
        /// Neighbouring prisms share side planes, so walking across the seams is smooth.
        /// </summary>
        public static Brush TerrainPrism(Vector3 a, Vector3 b, Vector3 c, float bottomY, SurfaceFlags surface = SurfaceFlags.None)
        {
            Vector3 top = Vector3.Normalize(Vector3.Cross(b - a, c - a));
            if (top.Y < 0f) top = -top;
            var centroid = new Vector3((a.X + b.X + c.X) / 3f, 0f, (a.Z + b.Z + c.Z) / 3f);
            var planes = new Plane3[5];
            planes[0] = new Plane3(top, Vector3.Dot(top, a));
            planes[1] = new Plane3(-Vector3.UnitY, -bottomY);
            planes[2] = Side(a, b, centroid);
            planes[3] = Side(b, c, centroid);
            planes[4] = Side(c, a, centroid);
            Vector3 min = Vector3.Min(Vector3.Min(a, b), c), max = Vector3.Max(Vector3.Max(a, b), c);
            min.Y = bottomY;
            return new Brush(planes, min, max, surface);
        }

        private static Plane3 Side(Vector3 p, Vector3 q, Vector3 centroid)
        {
            var edge = new Vector3(q.X - p.X, 0f, q.Z - p.Z);
            var n = Vector3.Normalize(new Vector3(edge.Z, 0f, -edge.X));
            var flatP = new Vector3(p.X, 0f, p.Z);
            if (Vector3.Dot(n, centroid - flatP) > 0f) n = -n;
            return new Plane3(n, Vector3.Dot(n, flatP));
        }

        /// <summary>Builds a convex brush from the triangles of a convex mesh (world space). Coplanar faces are merged.</summary>
        public static Brush FromConvexMesh(IReadOnlyList<Vector3> vertices, IReadOnlyList<int> triangles, SurfaceFlags surface = SurfaceFlags.None)
        {
            var planes = new List<Plane3>();
            Vector3 min = new Vector3(float.MaxValue), max = new Vector3(float.MinValue);
            foreach (Vector3 v in vertices)
            {
                min = Vector3.Min(min, v);
                max = Vector3.Max(max, v);
            }
            Vector3 centroid = (min + max) * 0.5f;
            for (int i = 0; i + 2 < triangles.Count; i += 3)
            {
                Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (n.LengthSquared() < 1e-12f) continue;
                n = Vector3.Normalize(n);
                // Make sure normals face outward regardless of winding convention.
                if (Vector3.Dot(n, a - centroid) < 0f) n = -n;
                var p = new Plane3(n, Vector3.Dot(n, a));
                bool dup = false;
                foreach (Plane3 q in planes)
                    if (Vector3.Dot(q.Normal, p.Normal) > 0.9999f && MathF.Abs(q.Distance - p.Distance) < 1e-3f) { dup = true; break; }
                if (!dup) planes.Add(p);
            }
            return new Brush(planes.ToArray(), min, max, surface);
        }
    }

    /// <summary>Static collision world made of convex brushes. Zero allocations per trace.</summary>
    public sealed class BrushWorld : ICollisionWorld
    {
        /// <summary>Distance kept between the hull and surfaces to avoid precision snags (≈ Source's 1/32 unit).</summary>
        public const float SurfaceEpsilon = 0.001f;
        /// <summary>Float tolerance for "touching" a face.</summary>
        private const float TouchTolerance = 1e-4f;

        private Brush[] _brushes = Array.Empty<Brush>();
        private int _count;

        // Broadphase: a uniform XZ grid (CSR layout) built lazily after brushes change. Traces only test brushes in the
        // cells their sweep touches; a per-brush stamp skips brushes listed in several cells. No allocations per trace.
        private const float CellSize = 8f;
        private bool _gridDirty = true;
        private float _gridMinX, _gridMinZ;
        private int _gridW, _gridH;
        private int[] _cellStart = Array.Empty<int>();
        private int[] _cellItems = Array.Empty<int>();
        private int[] _stamp = Array.Empty<int>();
        private int _query;

        public int BrushCount => _count;

        public IEnumerable<Brush> Brushes
        {
            get { for (int i = 0; i < _count; i++) yield return _brushes[i]; }
        }

        public void Clear()
        {
            _count = 0;
            _waterCount = 0;
            _gridDirty = true;
        }

        private Brush[] _water = Array.Empty<Brush>();
        private int _waterCount;

        /// <summary>True if the point is inside any water volume (all its planes behind the point).</summary>
        public bool InWater(Vector3 point)
        {
            for (int i = 0; i < _waterCount; i++)
            {
                Brush b = _water[i];
                if (point.X < b.Min.X || point.X > b.Max.X || point.Y < b.Min.Y || point.Y > b.Max.Y || point.Z < b.Min.Z || point.Z > b.Max.Z) continue;
                bool inside = true;
                Plane3[] planes = b.Planes;
                for (int p = 0; p < planes.Length && inside; p++)
                    if (Vector3.Dot(planes[p].Normal, point) > planes[p].Distance) inside = false;
                if (inside) return true;
            }
            return false;
        }

        public void Add(Brush brush)
        {
            if ((brush.Surface & SurfaceFlags.Water) != 0)
            {
                // Water isn't solid: kept apart, never traced against.
                if (_waterCount == _water.Length) Array.Resize(ref _water, Math.Max(16, _water.Length * 2));
                _water[_waterCount++] = brush;
                return;
            }
            if (_count == _brushes.Length) Array.Resize(ref _brushes, Math.Max(16, _brushes.Length * 2));
            _brushes[_count++] = brush;
            _gridDirty = true;
        }

        private void Cell(float x, float z, out int cx, out int cz)
        {
            cx = Math.Clamp((int)MathF.Floor((x - _gridMinX) / CellSize), 0, _gridW - 1);
            cz = Math.Clamp((int)MathF.Floor((z - _gridMinZ) / CellSize), 0, _gridH - 1);
        }

        private void BuildGrid()
        {
            _gridDirty = false;
            if (_count == 0) { _gridW = _gridH = 0; return; }
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            for (int i = 0; i < _count; i++)
            {
                Brush b = _brushes[i];
                minX = MathF.Min(minX, b.Min.X); minZ = MathF.Min(minZ, b.Min.Z);
                maxX = MathF.Max(maxX, b.Max.X); maxZ = MathF.Max(maxZ, b.Max.Z);
            }
            _gridMinX = minX; _gridMinZ = minZ;
            _gridW = Math.Max(1, (int)MathF.Ceiling((maxX - minX) / CellSize) + 1);
            _gridH = Math.Max(1, (int)MathF.Ceiling((maxZ - minZ) / CellSize) + 1);
            int cells = _gridW * _gridH;
            var counts = new int[cells + 1];
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 1)
                {
                    _cellStart = new int[cells + 1];
                    for (int c = 0; c < cells; c++) _cellStart[c + 1] = _cellStart[c] + counts[c];
                    _cellItems = new int[_cellStart[cells]];
                    Array.Clear(counts, 0, counts.Length);
                }
                for (int i = 0; i < _count; i++)
                {
                    Brush b = _brushes[i];
                    Cell(b.Min.X, b.Min.Z, out int x0, out int z0);
                    Cell(b.Max.X, b.Max.Z, out int x1, out int z1);
                    for (int z = z0; z <= z1; z++)
                        for (int x = x0; x <= x1; x++)
                        {
                            int c = z * _gridW + x;
                            if (pass == 1) _cellItems[_cellStart[c] + counts[c]] = i;
                            counts[c]++;
                        }
                }
            }
            _stamp = new int[_count];
            _query = 0;
        }

        public TraceResult TraceBox(Vector3 start, Vector3 end, Vector3 halfExtents)
        {
            var trace = new TraceResult { Fraction = 1f };
            Vector3 sweepMin = Vector3.Min(start, end) - halfExtents;
            Vector3 sweepMax = Vector3.Max(start, end) + halfExtents;

            if (_gridDirty) BuildGrid();
            if (_count > 0)
            {
                if (++_query == int.MaxValue) { Array.Clear(_stamp, 0, _stamp.Length); _query = 1; }
                Cell(sweepMin.X, sweepMin.Z, out int x0, out int z0);
                Cell(sweepMax.X, sweepMax.Z, out int x1, out int z1);
                for (int z = z0; z <= z1 && !trace.AllSolid; z++)
                {
                    for (int x = x0; x <= x1 && !trace.AllSolid; x++)
                    {
                        int c = z * _gridW + x;
                        for (int k = _cellStart[c], end2 = _cellStart[c + 1]; k < end2; k++)
                        {
                            int i = _cellItems[k];
                            if (_stamp[i] == _query) continue;
                            _stamp[i] = _query;
                            Brush b = _brushes[i];
                            if (b.Max.X < sweepMin.X || b.Min.X > sweepMax.X ||
                                b.Max.Y < sweepMin.Y || b.Min.Y > sweepMax.Y ||
                                b.Max.Z < sweepMin.Z || b.Min.Z > sweepMax.Z) continue;
                            ClipBoxToBrush(start, end, halfExtents, b, ref trace);
                            if (trace.AllSolid) break;
                        }
                    }
                }
            }

            trace.EndPosition = trace.Fraction >= 1f ? end : start + (end - start) * trace.Fraction;
            return trace;
        }

        // Quake III CM_TraceThroughBrush, adapted for a centered AABB.
        private static void ClipBoxToBrush(Vector3 start, Vector3 end, Vector3 ext, Brush brush, ref TraceResult trace)
        {
            float enterFrac = -1f;
            float leaveFrac = 1f;
            bool startOut = false;
            bool getOut = false;
            Vector3 clipNormal = default;

            Plane3[] planes = brush.Planes;
            for (int i = 0; i < planes.Length; i++)
            {
                Plane3 p = planes[i];
                Vector3 n = p.Normal;
                // Expand the plane by the box's support distance along the normal (Minkowski sum).
                float dist = p.Distance + MathF.Abs(n.X) * ext.X + MathF.Abs(n.Y) * ext.Y + MathF.Abs(n.Z) * ext.Z;
                // TouchTolerance: exactly touching a face counts as outside, so a hull resting on the floor isn't "stuck in solid".
                float d1 = Vector3.Dot(start, n) - dist + TouchTolerance;
                float d2 = Vector3.Dot(end, n) - dist + TouchTolerance;

                if (d2 > 0f) getOut = true;
                if (d1 > 0f) startOut = true;

                // Completely in front of this plane: no contact with the brush.
                if (d1 > 0f && (d2 >= SurfaceEpsilon || d2 >= d1)) return;
                // Completely behind: this plane doesn't clip.
                if (d1 <= 0f && d2 <= 0f) continue;

                if (d1 > d2)
                {
                    float f = (d1 - SurfaceEpsilon) / (d1 - d2);
                    if (f < 0f) f = 0f;
                    if (f > enterFrac)
                    {
                        enterFrac = f;
                        clipNormal = n;
                    }
                }
                else
                {
                    float f = (d1 + SurfaceEpsilon) / (d1 - d2);
                    if (f > 1f) f = 1f;
                    if (f < leaveFrac) leaveFrac = f;
                }
            }

            if (!startOut)
            {
                trace.StartSolid = true;
                if (!getOut)
                {
                    trace.AllSolid = true;
                    trace.Fraction = 0f;
                }
                return;
            }

            if (enterFrac < leaveFrac && enterFrac > -1f && enterFrac < trace.Fraction)
            {
                trace.Fraction = enterFrac < 0f ? 0f : enterFrac;
                trace.Normal = clipNormal;
                trace.Surface = brush.Surface;
            }
        }
    }
}
