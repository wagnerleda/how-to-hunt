using UnityEngine;

namespace HowToHunt.Core
{
    /// <summary>Moves a decorative cloud slowly with the wind and wraps it around a centre (visual only, local).</summary>
    public sealed class CloudDrift : MonoBehaviour
    {
        [SerializeField] private Vector3 _velocity = new Vector3(1f, 0f, 0.3f);
        [SerializeField] private Vector3 _center;
        [SerializeField] private float _radius = 300f;

        public void Configure(Vector3 velocity, Vector3 center, float radius)
        {
            _velocity = velocity;
            _center = center;
            _radius = radius;
        }

        private void Update()
        {
            Vector3 p = transform.position + _velocity * Time.deltaTime;
            Vector3 d = p - _center;
            d.y = 0f;
            if (d.sqrMagnitude > _radius * _radius) p -= d * 2f; // re-enter on the far side
            transform.position = p;
        }
    }
}
