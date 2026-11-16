using System.Collections.Generic;
using UnityEngine;

namespace HowToHunt.Hunting
{
    /// <summary>
    /// Places prey in a level: which prey, how many, how far from this point, how fast they come back.
    /// Level authors drop it in a scene; no code. Spawners are ordered by name, so every machine derives the same
    /// prey ids without spawn messages.
    /// </summary>
    public sealed class PreySpawner : MonoBehaviour
    {
        [SerializeField] private PreyDefinition _prey;
        [SerializeField] private int _count = 6;
        [SerializeField] private float _radius = 12f;
        [SerializeField] private float _respawnSeconds = 20f;

        private static readonly List<PreySpawner> Registered = new List<PreySpawner>();

        public static IReadOnlyList<PreySpawner> All
        {
            get
            {
                Registered.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                return Registered;
            }
        }

        public PreyDefinition Prey => _prey;
        public int Count => _count;
        public float Radius => _radius;
        public float RespawnSeconds => _respawnSeconds;

        public void Configure(PreyDefinition prey, int count, float radius, float respawnSeconds)
        {
            _prey = prey;
            _count = count;
            _radius = radius;
            _respawnSeconds = respawnSeconds;
        }

        private void OnEnable() => Registered.Add(this);
        private void OnDisable() => Registered.Remove(this);

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, _radius);
        }
    }
}
