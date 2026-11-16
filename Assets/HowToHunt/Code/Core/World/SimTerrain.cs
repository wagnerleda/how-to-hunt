using HowToHunt.Sim.Collision;
using UnityEngine;

namespace HowToHunt.Core
{
    /// <summary>
    /// Terrain collision for the movement sim: a heightfield whose triangles become prism brushes (see Heightfield).
    /// The visible mesh is generated from the same heights, so what you see is what you walk on. Holes (NaN) let
    /// other geometry show through.
    /// </summary>
    public sealed class SimTerrain : MonoBehaviour
    {
        [SerializeField] private float _originX;
        [SerializeField] private float _originZ;
        [SerializeField] private float _cell = 5f;
        [SerializeField] private int _countX;
        [SerializeField] private int _countZ;
        [SerializeField] private float[] _heights = System.Array.Empty<float>();

        private Heightfield _field;

        public Heightfield Field => _field ??= _heights.Length == _countX * _countZ && _countX > 1
            ? new Heightfield(_originX, _originZ, _cell, _countX, _countZ, _heights)
            : null;

        public void Configure(float originX, float originZ, float cell, int countX, int countZ, float[] heights)
        {
            _originX = originX;
            _originZ = originZ;
            _cell = cell;
            _countX = countX;
            _countZ = countZ;
            _heights = heights;
            _field = null;
        }

        /// <summary>Ground height at a world position (NaN outside or over a hole).</summary>
        public float HeightAt(float x, float z) => Field?.Sample(x, z) ?? float.NaN;
    }
}
