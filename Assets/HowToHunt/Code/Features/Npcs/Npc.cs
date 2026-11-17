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
        public CampStation Station { get; private set; }

        public void Configure(NpcDefinition definition, float talkRange)
        {
            _definition = definition;
            _talkRange = talkRange;
        }

        private void OnEnable()
        {
            Registered.Add(this);
            if (_definition != null && _definition.HasStation)
            {
                // The stall right in front of them.
                float best = 8f;
                foreach (CampStation s in CampStation.All)
                {
                    if (s.Kind != _definition.Station) continue;
                    float d = Vector3.Distance(s.transform.position, transform.position);
                    if (d < best) { best = d; Station = s; }
                }
            }
        }

        private void OnDisable() => Registered.Remove(this);

        /// <summary>The character who runs this stall, if one does.</summary>
        public static Npc At(CampStation station)
        {
            for (int i = 0; i < Registered.Count; i++)
                if (Registered[i].Station == station) return Registered[i];
            return null;
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
