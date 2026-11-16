using System.Collections.Generic;
using HowToHunt.Core;
using HowToHunt.Foundation;
using HowToHunt.Hunting;
using HowToHunt.Sim.Hunting;
using UnityEngine;
using Vec3 = System.Numerics.Vector3;

namespace HowToHunt.Economy
{
    /// <summary>
    /// What a world item looks like and how big it is: the model (prey visual, or the tool without its hand), the pose
    /// it lies in (carcasses on their side, live catches upright, tools flat) and its bounds in that pose. The collision
    /// box follows the model's real size and orientation. Measured once per item type; identical on every machine.
    /// </summary>
    public static class ItemShapes
    {
        public sealed class Shape
        {
            public GameObject Prefab;
            public float Scale = 1f;
            public Quaternion Lie = Quaternion.identity;
            public Bounds LocalBounds;   // model space, unscaled
            public Bounds LieBounds;     // in the lying pose (Lie applied), unscaled: the collision box and visual centre
            public bool HideHand;
        }

        private static readonly Dictionary<(ItemKind, uint, bool), Shape> Cache = new Dictionary<(ItemKind, uint, bool), Shape>();

        public static Shape Get(in ItemStack item)
        {
            bool carcass = item.Kind == ItemKind.Prey && !item.Captured;
            var key = (item.Kind, item.Content, carcass);
            if (Cache.TryGetValue(key, out Shape s)) return s;
            s = new Shape();
            if (item.Kind == ItemKind.Tool)
            {
                ToolDefinition t = ContentRegistry.Get<ToolDefinition>(new ContentId(item.Content));
                s.Prefab = t != null ? t.Viewmodel : null;
                s.HideHand = true;
            }
            else
            {
                PreyDefinition p = ContentRegistry.Get<PreyDefinition>(new ContentId(item.Content));
                s.Prefab = p != null ? p.Visual : null;
                s.Scale = p != null ? p.VisualScale : 1f;
                if (carcass) s.Lie = Quaternion.Euler(0f, 0f, 90f);
            }
            s.LocalBounds = Measure(s.Prefab, s.HideHand);
            s.LieBounds = Rotated(s.LocalBounds, s.Lie);
            if (item.Kind == ItemKind.Tool)
            {
                // Tools lie on their natural resting face, measured from the mesh by the content build.
                ToolDefinition t = ContentRegistry.Get<ToolDefinition>(new ContentId(item.Content));
                if (t != null && t.RestSize.sqrMagnitude > 1e-6f)
                {
                    s.Lie = Quaternion.Euler(t.RestEuler);
                    s.LieBounds = new Bounds(t.RestCenter, t.RestSize);
                }
            }
            Cache[key] = s;
            return s;
        }

        private static Bounds Measure(GameObject prefab, bool hideHand)
        {
            var fallback = new Bounds(Vector3.zero, Vector3.one * 0.2f);
            if (prefab == null) return fallback;
            GameObject probe = Object.Instantiate(prefab, new Vector3(0f, -9000f, 0f), Quaternion.identity);
            Transform root = probe.transform;
            Bounds b = default;
            bool any = false;
            foreach (MeshFilter mf in probe.GetComponentsInChildren<MeshFilter>(true))
                Encapsulate(ref b, ref any, root, mf.transform, mf.sharedMesh, hideHand && mf.gameObject.name.EndsWith("Hand", System.StringComparison.Ordinal));
            foreach (SkinnedMeshRenderer sk in probe.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                Encapsulate(ref b, ref any, root, sk.transform, sk.sharedMesh, false);
            Object.DestroyImmediate(probe);
            return any ? b : fallback;
        }

        private static void Encapsulate(ref Bounds b, ref bool any, Transform root, Transform part, Mesh mesh, bool skip)
        {
            if (mesh == null || skip) return;
            Bounds mb = mesh.bounds;
            Matrix4x4 m = root.worldToLocalMatrix * part.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? mb.min.x : mb.max.x, (i & 2) == 0 ? mb.min.y : mb.max.y, (i & 4) == 0 ? mb.min.z : mb.max.z);
                Vector3 p = m.MultiplyPoint3x4(corner);
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                else b.Encapsulate(p);
            }
        }

        private static Bounds Rotated(Bounds b, Quaternion r)
        {
            var result = new Bounds(r * b.center, Vector3.zero);
            for (int i = 0; i < 8; i++)
                result.Encapsulate(r * new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z));
            return result;
        }

        /// <summary>Yaw the item lies at on the ground (from its id, so every machine agrees).</summary>
        public static float Yaw(ushort id) => (id * 137.5f) % 360f;

        /// <summary>Half extents of the collision box: the model's bounds in its lying pose, scaled.</summary>
        public static Vec3 HalfExtents(in ItemStack item, ushort id)
        {
            Shape s = Get(item);
            Quaternion r = Quaternion.Euler(0f, Yaw(id), 0f);
            Vector3 e = s.LieBounds.extents * s.Scale;
            Vector3 x = r * new Vector3(e.x, 0f, 0f), y = r * new Vector3(0f, e.y, 0f), z = r * new Vector3(0f, 0f, e.z);
            var half = new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x), Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y), Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
            return new Vec3(Mathf.Max(0.04f, half.x), Mathf.Max(0.04f, half.y), Mathf.Max(0.04f, half.z));
        }
    }
}
