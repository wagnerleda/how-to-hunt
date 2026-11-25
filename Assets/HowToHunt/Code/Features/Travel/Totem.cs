using System.Collections.Generic;
using HowToHunt.Core;
using UnityEngine;

namespace HowToHunt.Travel
{
    /// <summary>
    /// A sacred totem by a camp (owner, 2026-11-25): consult it and it takes you to any part of the world you have
    /// been to. Each stands for one region; its area is where you discover that region by going there, and travellers
    /// arrive at its Arrival point. Placed by the scene builder.
    /// </summary>
    public sealed class Totem : MonoBehaviour
    {
        [SerializeField] private RegionDefinition _region;
        [Tooltip("Walking into this box discovers the region (its map, roughly).")]
        [SerializeField] private Bounds _area;
        [Tooltip("How close you must stand to consult it (metres from its base).")]
        [SerializeField] private float _reach = 3f;
        [Tooltip("Where travellers arrive (feet), facing its forward.")]
        [SerializeField] private Transform _arrival;

        private static readonly List<Totem> Registered = new List<Totem>();
        public static IReadOnlyList<Totem> All => Registered;

        public RegionDefinition Region => _region;
        public Bounds Area => _area;
        public float Reach => _reach;

        public void Configure(RegionDefinition region, Bounds area, float reach, Transform arrival)
        {
            _region = region;
            _area = area;
            _reach = reach;
            _arrival = arrival;
        }

        private void OnEnable() => Registered.Add(this);
        private void OnDisable() => Registered.Remove(this);

        /// <summary>Feet and facing for someone arriving here.</summary>
        public void Arrival(out Vector3 feet, out float yaw)
        {
            Transform t = _arrival != null ? _arrival : transform;
            feet = t.position;
            yaw = t.eulerAngles.y;
        }

        /// <summary>Within consulting reach of this point (height counts half, like the camp stations)?</summary>
        public bool InReach(Vector3 point, float slack = 0f)
        {
            Vector3 d = transform.position - point;
            d.y *= 0.5f;
            float r = _reach + slack;
            return d.sqrMagnitude <= r * r;
        }

        /// <summary>The nearest totem within reach of a point (plus slack), or null.</summary>
        public static Totem Near(Vector3 point, float slack = 0f)
        {
            Totem best = null;
            float bestSq = float.MaxValue;
            for (int i = 0; i < Registered.Count; i++)
            {
                Totem t = Registered[i];
                if (!t.InReach(point, slack)) continue;
                float sq = (t.transform.position - point).sqrMagnitude;
                if (sq < bestSq) { best = t; bestSq = sq; }
            }
            return best;
        }

        /// <summary>The totem of a region, or null.</summary>
        public static Totem Of(RegionDefinition region)
        {
            for (int i = 0; i < Registered.Count; i++)
                if (Registered[i]._region == region) return Registered[i];
            return null;
        }

        /// <summary>The region whose area holds this point (the first one), or null.</summary>
        public static RegionDefinition RegionAt(Vector3 point)
        {
            for (int i = 0; i < Registered.Count; i++)
                if (Registered[i]._region != null && Registered[i]._area.Contains(point)) return Registered[i]._region;
            return null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.3f);
            Gizmos.DrawWireCube(_area.center, _area.size);
            Gizmos.DrawWireSphere(transform.position, _reach);
        }
    }
}
