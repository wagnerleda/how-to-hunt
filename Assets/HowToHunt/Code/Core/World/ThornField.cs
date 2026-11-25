using System.Collections.Generic;
using UnityEngine;

namespace HowToHunt.Core
{
    /// <summary>
    /// Spiky things a hunter gets pricked by on contact (the desert's cacti): axis-aligned boxes baked by the scene
    /// builder. The host checks players against them (PlayerHealth). Data only; queries are allocation-free.
    /// </summary>
    public sealed class ThornField : MonoBehaviour
    {
        [SerializeField] private Vector3[] _centers = new Vector3[0];
        [SerializeField] private Vector3[] _halfExtents = new Vector3[0];
        [SerializeField] private Bounds _bounds;

        private static readonly List<ThornField> Registered = new List<ThornField>();

        public int Count => _centers.Length;

        public void Configure(Vector3[] centers, Vector3[] halfExtents)
        {
            _centers = centers;
            _halfExtents = halfExtents;
            _bounds = default;
            for (int i = 0; i < centers.Length; i++)
            {
                var b = new Bounds(centers[i], halfExtents[i] * 2f);
                if (i == 0) _bounds = b;
                else _bounds.Encapsulate(b);
            }
        }

        private void OnEnable() => Registered.Add(this);
        private void OnDisable() => Registered.Remove(this);

        /// <summary>
        /// Is a hull (centre, half extents) touching any thorn box grown by <paramref name="reach"/>? Gives the centre
        /// of the thorn touched (to push the hunter away from it).
        /// </summary>
        public static bool Touches(Vector3 center, Vector3 half, float reach, out Vector3 thorn)
        {
            thorn = default;
            for (int f = 0; f < Registered.Count; f++)
            {
                ThornField field = Registered[f];
                Bounds fb = field._bounds;
                if (center.x + half.x + reach < fb.min.x || center.x - half.x - reach > fb.max.x ||
                    center.y + half.y + reach < fb.min.y || center.y - half.y - reach > fb.max.y ||
                    center.z + half.z + reach < fb.min.z || center.z - half.z - reach > fb.max.z) continue;
                Vector3[] cs = field._centers, hs = field._halfExtents;
                for (int i = 0; i < cs.Length; i++)
                {
                    Vector3 c = cs[i], h = hs[i];
                    if (Mathf.Abs(center.x - c.x) > half.x + h.x + reach) continue;
                    if (Mathf.Abs(center.z - c.z) > half.z + h.z + reach) continue;
                    if (Mathf.Abs(center.y - c.y) > half.y + h.y + reach) continue;
                    thorn = c;
                    return true;
                }
            }
            return false;
        }
    }
}
