using System.Collections.Generic;
using UnityEngine;

namespace HowToHunt.Editor
{
    /// <summary>
    /// How an item lies when it settles on the ground: the orientation with the lowest centre of mass over its lowest
    /// point (its natural resting face), found by searching every "down" direction on a sphere and refining the best.
    /// A net tips over onto its side instead of standing on its bag; a gun lies flat. Runs at content-build time
    /// (meshes are readable in the Editor), so players only read the stored result.
    /// </summary>
    public static class ItemRestPose
    {
        private const int Samples = 600;

        public static bool Compute(GameObject prefab, string skipSuffix, out Vector3 euler, out Vector3 center, out Vector3 size)
        {
            euler = center = size = Vector3.zero;
            if (prefab == null) return false;
            var points = new List<Vector3>(4096);
            Vector3 centroid = Collect(prefab, skipSuffix, points);
            if (points.Count == 0) return false;

            float Height(Vector3 down)
            {
                float lowest = float.MinValue;
                for (int i = 0; i < points.Count; i++) lowest = Mathf.Max(lowest, Vector3.Dot(points[i], down));
                return lowest - Vector3.Dot(centroid, down);
            }

            // Coarse: Fibonacci sphere of down directions. Then refine around the best with shrinking steps.
            Vector3 best = Vector3.down;
            float bestH = Height(best);
            float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));
            for (int i = 0; i < Samples; i++)
            {
                float y = 1f - 2f * (i + 0.5f) / Samples;
                float r = Mathf.Sqrt(1f - y * y);
                var d = new Vector3(Mathf.Cos(golden * i) * r, y, Mathf.Sin(golden * i) * r);
                float h = Height(d);
                if (h < bestH - 1e-5f) { bestH = h; best = d; }
            }
            float step = 6f;
            for (int iter = 0; iter < 24; iter++)
            {
                bool improved = false;
                Vector3 a = Vector3.Cross(best, Mathf.Abs(best.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                Vector3 b = Vector3.Cross(best, a);
                foreach (Vector3 axis in new[] { a, -a, b, -b })
                {
                    Vector3 d = Quaternion.AngleAxis(step, axis) * best;
                    float h = Height(d);
                    if (h < bestH - 1e-6f) { bestH = h; best = d; improved = true; }
                }
                if (!improved) step *= 0.5f;
                if (step < 0.2f) break;
            }

            Quaternion lie = Quaternion.FromToRotation(best, Vector3.down);
            euler = lie.eulerAngles;
            var bounds = new Bounds(lie * points[0], Vector3.zero);
            for (int i = 1; i < points.Count; i++) bounds.Encapsulate(lie * points[i]);
            center = bounds.center;
            size = bounds.size;
            return true;
        }

        /// <summary>Every vertex in prefab space (skipping hand parts); returns the area-weighted surface centroid.</summary>
        private static Vector3 Collect(GameObject prefab, string skipSuffix, List<Vector3> points)
        {
            Vector3 sum = Vector3.zero;
            float area = 0f;
            Transform root = prefab.transform;
            foreach (MeshFilter mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                if (!string.IsNullOrEmpty(skipSuffix) && mf.gameObject.name.EndsWith(skipSuffix, System.StringComparison.Ordinal)) continue;
                Matrix4x4 m = root.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                Vector3[] v = mf.sharedMesh.vertices;
                int[] tri = mf.sharedMesh.triangles;
                int start = points.Count;
                for (int i = 0; i < v.Length; i++) points.Add(m.MultiplyPoint3x4(v[i]));
                for (int i = 0; i + 2 < tri.Length; i += 3)
                {
                    Vector3 p0 = points[start + tri[i]], p1 = points[start + tri[i + 1]], p2 = points[start + tri[i + 2]];
                    float a = Vector3.Cross(p1 - p0, p2 - p0).magnitude * 0.5f;
                    sum += (p0 + p1 + p2) / 3f * a;
                    area += a;
                }
            }
            return area > 0f ? sum / area : Vector3.zero;
        }
    }
}
