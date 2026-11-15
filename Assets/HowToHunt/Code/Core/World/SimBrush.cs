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
        public enum ShapeKind { Box, ConvexMesh }

        [SerializeField] private ShapeKind _shape = ShapeKind.Box;
        [SerializeField] private SurfaceFlags _surface = SurfaceFlags.None;

        public ShapeKind Shape { get => _shape; set => _shape = value; }
        public SurfaceFlags Surface { get => _surface; set => _surface = value; }

        private static readonly List<Vector3> VertexBuffer = new List<Vector3>(256);
        private static readonly List<int> TriangleBuffer = new List<int>(512);

        public Brush BuildBrush()
        {
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
