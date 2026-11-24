using System.Collections.Generic;
using HowToHunt.Economy;
using UnityEngine;

namespace HowToHunt.Npcs
{
    /// <summary>
    /// A camp character in the world (placed by the scene builder behind a stall). Knows who they are (their
    /// NpcDefinition) and the stall they run, if any. Not networked: every machine has the same characters, and what
    /// they say depends on the local player's own quest log.
    /// </summary>
    public sealed class Npc : MonoBehaviour
    {
        [SerializeField] private NpcDefinition _definition;
        [SerializeField] private float _talkRange = 4.5f;

        private static readonly List<Npc> Registered = new List<Npc>();
        public static IReadOnlyList<Npc> All => Registered;

        public NpcDefinition Definition => _definition;
        public float TalkRange => _talkRange;

        /// <summary>
        /// The stall right in front of them (null for a stall-less character). Looked up on demand, not in OnEnable:
        /// Unity enables scene objects in no guaranteed order, and an NPC enabled before its stall registered used to
        /// end up with no stall, so the player could never talk to them (Tomas, who gives the first quest).
        /// </summary>
        public CampStation Station
        {
            get
            {
                if (_station == null && _definition != null && _definition.HasStation) _station = FindStall();
                return _station;
            }
        }

        private const float StallSearchRadius = 8f;
        private CampStation _station;

        public void Configure(NpcDefinition definition, float talkRange)
        {
            _definition = definition;
            _talkRange = talkRange;
            _station = null;
        }

        private void OnEnable() => Registered.Add(this);

        private void OnDisable()
        {
            Registered.Remove(this);
            _station = null;
        }

        private CampStation FindStall()
        {
            CampStation found = null;
            float best = StallSearchRadius * StallSearchRadius;
            Vector3 here = transform.position;
            IReadOnlyList<CampStation> all = CampStation.All;
            for (int i = 0; i < all.Count; i++)
            {
                CampStation s = all[i];
                if (s.Kind != _definition.Station) continue;
                float d = (s.transform.position - here).sqrMagnitude;
                if (d < best) { best = d; found = s; }
            }
            return found;
        }

        /// <summary>The character who runs this stall, if one does.</summary>
        public static Npc At(CampStation station)
        {
            if (station == null) return null;
            for (int i = 0; i < Registered.Count; i++)
                if (Registered[i].Station == station) return Registered[i];
            return null;
        }

        /// <summary>The nearest stall-less character (at the fire, chopping wood...) within talking range of a point.</summary>
        public static Npc NearWanderer(Vector3 point)
        {
            Npc found = null;
            float best = float.MaxValue;
            for (int i = 0; i < Registered.Count; i++)
            {
                Npc n = Registered[i];
                if (n._definition == null || n._definition.HasStation) continue;
                float d = (n.transform.position - point).sqrMagnitude;
                if (d <= n._talkRange * n._talkRange && d < best) { best = d; found = n; }
            }
            return found;
        }

        /// <summary>The nearest character within talking range of a point (stall-less characters too).</summary>
        public static Npc Near(Vector3 point)
        {
            Npc found = null;
            float best = float.MaxValue;
            for (int i = 0; i < Registered.Count; i++)
            {
                Npc n = Registered[i];
                float d = (n.transform.position - point).sqrMagnitude;
                if (d <= n._talkRange * n._talkRange && d < best) { best = d; found = n; }
            }
            return found;
        }
    }
}
