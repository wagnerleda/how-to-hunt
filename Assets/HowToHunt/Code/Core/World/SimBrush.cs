using System.Collections.Generic;
using HowToHunt.Sim.Collision;
using UnityEngine;

namespace HowToHunt.Core
{
    /// <summary>
    /// Marks a mesh as solid level geometry for the movement simulation (a Source-style brush).
    /// Box: uses the mesh bounds and the transform (cubes, walls, floors; rotate freely).
    /// ConvexMesh: uses the mesh triangles (ramps, wedges). The mesh must be convex.
    /// Steep surfaces (normal.y below sv_standable) can't be stood on and become surf ramps automatically.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    public sealed class SimBrush : MonoBehaviour
    {
        /// <summary>Box: mesh bounds. ConvexMesh: the mesh's faces. Planes: a hull baked by editor code (k-DOP fitted to a prop).</summary>
        public enum ShapeKind { Box, ConvexMesh, Planes }

        [SerializeField] private ShapeKind _shape = ShapeKind.Box;
        [SerializeField] private SurfaceFlags _surface = SurfaceFlags.None;
        /// <summary>World-space planes for ShapeKind.Planes: xyz = outward normal, w = distance.</summary>
        [SerializeField] private Vector4[] _planes = System.Array.Empty<Vector4>();
        [SerializeField] private Vector3 _min, _max;

        /// <summary>Editor: a baked convex hull (outward planes in world space) and its bounds.</summary>
        public void SetPlanes(Vector4[] planes, Vector3 min, Vector3 max)
        {
            _shape = ShapeKind.Planes;
            _planes = planes;
            _min = min;
            _max = max;
        }

        public ShapeKind Shape { get => _shape; set => _shape = value; }
        public SurfaceFlags Surface { get => _surface; set => _surface = value; }

        private static readonly List<Vector3> VertexBuffer = new List<Vector3>(256);
        private static readonly List<int> TriangleBuffer = new List<int>(512);

        public Brush BuildBrush()
        {
            if (_shape == ShapeKind.Planes)
            {
                if (_planes.Length == 0) return null;
                var planes = new Plane3[_planes.Length];
                for (int i = 0; i < planes.Length; i++)
                    planes[i] = new Plane3(new System.Numerics.Vector3(_planes[i].x, _planes[i].y, _planes[i].z), _planes[i].w);
                return new Brush(planes, _min.ToSim(), _max.ToSim(), _surface);
            }
            Mesh mesh = GetComponent<MeshFilter>().sharedMesh;
            if (mesh == null) return null;
            Transform t = transform;

            if (_shape == ShapeKind.Box)
            {
                Bounds b = mesh.bounds;
                Vector3 center = t.TransformPoint(b.center);
                Vector3 half = Vector3.Scale(b.extents, Abs(t.lossyScale));
                return Brush.Oriented(center.ToSim(), half.ToSim(), t.rotation.ToSim(), _surface);
            }

            VertexBuffer.Clear();
            TriangleBuffer.Clear();
            mesh.GetVertices(VertexBuffer);
            mesh.GetTriangles(TriangleBuffer, 0);
            var verts = new System.Numerics.Vector3[VertexBuffer.Count];
            for (int i = 0; i < verts.Length; i++) verts[i] = t.TransformPoint(VertexBuffer[i]).ToSim();
            return Brush.FromConvexMesh(verts, TriangleBuffer.ToArray(), _surface);
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}
