using System.Collections.Generic;
using UnityEngine;

namespace HowToHunt.Economy
{
    public enum CampStationKind { Sell, Shop, Workshop }

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
        [Tooltip("Shop: a bait stall (sells baits that wake a boss) instead of an outfitter.")]
        [SerializeField] private bool _baitStall;

        private static readonly List<CampStation> Registered = new List<CampStation>();

        public static IReadOnlyList<CampStation> All => Registered;
        public CampStationKind Kind => _kind;
        public float Radius => _radius;
        public string Label => string.IsNullOrEmpty(_label) ? (_kind == CampStationKind.Sell ? "Trader" : _kind == CampStationKind.Shop ? (_baitStall ? "Bait Stall" : "Outfitter") : "Gunsmith") : _label;

        public double ExpectedSale => _expectedSale;
        public bool BaitStall => _baitStall;
        /// <summary>This area's prices: a trader far from the Plains pays more for the same catch (per-area economy).</summary>
        public float PriceMultiplier => _priceMultiplier <= 0f ? 1f : _priceMultiplier;
        [SerializeField] private float _priceMultiplier = 1f;

        public void Configure(CampStationKind kind, float radius, string label, double expectedSale = 50, float priceMultiplier = 1f, bool baitStall = false)
        {
            _baitStall = baitStall;
            _expectedSale = expectedSale;
            _priceMultiplier = priceMultiplier;
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

        /// <summary>The nearest shop within its radius (+ slack) that sells baits (or, baits false, that doesn't), or null.</summary>
        public static CampStation NearestShop(Vector3 position, bool baits, float slack = 0f)
        {
            CampStation best = null;
            float bestSq = float.MaxValue;
            for (int i = 0; i < Registered.Count; i++)
            {
                CampStation s = Registered[i];
                if (s._kind != CampStationKind.Shop || s._baitStall != baits) continue;
                Vector3 d = s.transform.position - position;
                d.y *= 0.5f;
                float r = s._radius + slack;
                float sq = d.sqrMagnitude;
                if (sq <= r * r && sq < bestSq) { best = s; bestSq = sq; }
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
