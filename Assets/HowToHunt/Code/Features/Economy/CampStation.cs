using System.Collections.Generic;
using UnityEngine;

namespace HowToHunt.Economy
{
    public enum CampStationKind { Sell, Shop }

    /// <summary>
    /// A place at camp where hunters sell their catch or buy upgrades (press Interact nearby). Level authors drop it
    /// in a scene; the host checks the player really is near one before accepting a sale or purchase.
    /// </summary>
    public sealed class CampStation : MonoBehaviour
    {
        [SerializeField] private CampStationKind _kind;
        [SerializeField] private float _radius = 2.5f;
        [SerializeField] private string _label = "";
        [Tooltip("Trader: a typical full-bag sale in this area (coins). Sales well below it make no cash burst; bigger ones make a bigger one.")]
        [SerializeField] private double _expectedSale = 50;

        private static readonly List<CampStation> Registered = new List<CampStation>();

        public static IReadOnlyList<CampStation> All => Registered;
        public CampStationKind Kind => _kind;
        public float Radius => _radius;
        public string Label => string.IsNullOrEmpty(_label) ? (_kind == CampStationKind.Sell ? "Trader" : "Outfitter") : _label;

        public double ExpectedSale => _expectedSale;

        public void Configure(CampStationKind kind, float radius, string label, double expectedSale = 50)
        {
            _expectedSale = expectedSale;
            _kind = kind;
            _radius = radius;
            _label = label;
        }

        /// <summary>The nearest station of that kind at any distance, or null.</summary>
        public static CampStation Closest(Vector3 position, CampStationKind kind)
        {
            CampStation best = null;
            float bestSq = float.MaxValue;
            for (int i = 0; i < Registered.Count; i++)
            {
                CampStation s = Registered[i];
                if (s._kind != kind) continue;
                float sq = (s.transform.position - position).sqrMagnitude;
                if (sq < bestSq) { best = s; bestSq = sq; }
            }
            return best;
        }

        /// <summary>The nearest station of that kind within its radius (+ slack), or null.</summary>
        public static CampStation Nearest(Vector3 position, CampStationKind kind, float slack = 0f)
        {
            CampStation best = null;
            float bestSq = float.MaxValue;
            for (int i = 0; i < Registered.Count; i++)
            {
                CampStation s = Registered[i];
                if (s._kind != kind) continue;
                Vector3 d = s.transform.position - position;
                d.y *= 0.5f; // players stand on the ground; stations are measured from their base
                float r = s._radius + slack;
                float sq = d.sqrMagnitude;
                if (sq <= r * r && sq < bestSq)
                {
                    best = s;
                    bestSq = sq;
                }
            }
            return best;
        }

        private void OnEnable() => Registered.Add(this);
        private void OnDisable() => Registered.Remove(this);

        private void OnDrawGizmos()
        {
            Gizmos.color = _kind == CampStationKind.Sell ? new Color(1f, 0.85f, 0.2f, 0.6f) : new Color(0.3f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, _radius);
        }
    }
}
