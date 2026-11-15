using System.Collections.Generic;
using UnityEngine;

namespace HowToHunt.Editor
{
    /// <summary>Flat-shaded low-poly meshes for level geometry (wedge ramps, surf prisms).</summary>
    public static class ProceduralMeshes
    {
        /// <summary>Unit wedge: base 1x1 on y=0, rises to height 1 at z=+0.5. Pivot at base center.</summary>
        public static Mesh Wedge()
        {
            Vector3 a = new Vector3(-0.5f, 0, -0.5f), b = new Vector3(0.5f, 0, -0.5f);
            Vector3 c = new Vector3(0.5f, 0, 0.5f), d = new Vector3(-0.5f, 0, 0.5f);
            Vector3 e = new Vector3(0.5f, 1, 0.5f), f = new Vector3(-0.5f, 1, 0.5f);
            var faces = new List<Vector3[]>
            {
                new[] { a, d, c, b },       // bottom
                new[] { a, b, e, f },       // slope
                new[] { d, f, e, c },       // back (vertical)
                new[] { a, f, d },          // left side
                new[] { b, c, e },          // right side
            };
            return Build("Wedge", faces);
        }

        /// <summary>Unit surf prism: base x -0.5..0.5 on y=0, apex ridge at x=0, y=1, along z -0.5..0.5.</summary>
        public static Mesh SurfPrism()
        {
            Vector3 a = new Vector3(-0.5f, 0, -0.5f), b = new Vector3(0.5f, 0, -0.5f);
            Vector3 c = new Vector3(0.5f, 0, 0.5f), d = new Vector3(-0.5f, 0, 0.5f);
            Vector3 e = new Vector3(0, 1, -0.5f), f = new Vector3(0, 1, 0.5f);
            var faces = new List<Vector3[]>
            {
                new[] { a, d, c, b },  // bottom
                new[] { a, e, f, d },  // left slope
                new[] { b, c, f, e },  // right slope
                new[] { a, b, e },     // front cap
                new[] { d, f, c },     // back cap
            };
            return Build("SurfPrism", faces);
        }

        /// <summary>Faces are convex polygons; winding is fixed so normals point away from the centroid.</summary>
        private static Mesh Build(string name, List<Vector3[]> faces)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            Vector3 centroid = Vector3.zero;
            int count = 0;
            foreach (Vector3[] f in faces)
                foreach (Vector3 v in f) { centroid += v; count++; }
            centroid /= count;

            foreach (Vector3[] face in faces)
            {
                Vector3 n = Vector3.Cross(face[1] - face[0], face[2] - face[0]).normalized;
                Vector3 faceCenter = Vector3.zero;
                foreach (Vector3 v in face) faceCenter += v;
                faceCenter /= face.Length;
                bool flip = Vector3.Dot(n, faceCenter - centroid) < 0f;
                if (flip) n = -n;
                int start = verts.Count;
                foreach (Vector3 v in face)
                {
                    verts.Add(v);
                    normals.Add(n);
                }
                for (int i = 1; i + 1 < face.Length; i++)
                {
                    // Front face when Cross(p1 - p0, p2 - p0) points outward (Unity's convention).
                    if (!flip) { tris.Add(start); tris.Add(start + i); tris.Add(start + i + 1); }
                    else { tris.Add(start); tris.Add(start + i + 1); tris.Add(start + i); }
                }
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
