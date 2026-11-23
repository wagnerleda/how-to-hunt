using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HowToHunt.Editor
{
    /// <summary>
    /// The Pine Forest's new animals (red squirrel, red fox, wild boar) and its boss (the Old Bear), modelled in code
    /// from flat-shaded tapered blocks in vertex colours, like the procedural pines: art direction A (low-poly, one
    /// colour per face), no texture, one material (M_Terrain). Faces +Z, pivot between the feet, real size in metres.
    /// Prefabs go to Prefabs/Prey; re-running rebuilds them in place (GUIDs kept). Logged in docs/ASSETS.md.
    /// </summary>
    public static class CreatureModels
    {
        public const string PrefabDir = GeneratedAssets.Root + "/Prefabs/Prey";
        public static string PrefabPath(string name) => $"{PrefabDir}/{name}.prefab";

        public static void BuildAll()
        {
            GeneratedAssets.EnsureFolder(PrefabDir);
            GeneratedAssets.EnsureFolder(GeneratedAssets.MeshesDir);
            Save("Squirrel", Squirrel());
            Save("Fox", Fox());
            Save("Boar", Boar());
            Save("OldBear", OldBear());
        }

        // --- the animals --------------------------------------------------------------------------------------------

        private static Mesh Squirrel()
        {
            var b = new Builder();
            Color rust = C(0.78f, 0.36f, 0.14f), cream = C(0.95f, 0.86f, 0.7f), dark = C(0.12f, 0.08f, 0.06f);
            b.Block(new Vector3(0f, 0.12f, 0f), new Vector3(0.11f, 0.11f, 0.2f), rust, front: 0.85f);
            b.Block(new Vector3(0f, 0.11f, 0.06f), new Vector3(0.08f, 0.08f, 0.08f), cream);                  // chest
            b.Block(new Vector3(0f, 0.2f, 0.12f), new Vector3(0.09f, 0.09f, 0.09f), rust, front: 0.8f);       // head
            b.Block(new Vector3(0f, 0.185f, 0.175f), new Vector3(0.05f, 0.05f, 0.05f), cream);              // muzzle
            for (int s = -1; s <= 1; s += 2)
            {
                b.Block(new Vector3(s * 0.03f, 0.265f, 0.11f), new Vector3(0.02f, 0.045f, 0.02f), rust, top: 0.4f); // ears
                b.Block(new Vector3(s * 0.04f, 0.215f, 0.16f), new Vector3(0.012f, 0.014f, 0.01f), dark);           // eyes
                b.Block(new Vector3(s * 0.04f, 0.03f, 0.07f), new Vector3(0.03f, 0.06f, 0.03f), rust);              // fore legs
                b.Block(new Vector3(s * 0.045f, 0.04f, -0.06f), new Vector3(0.04f, 0.08f, 0.05f), rust);            // hind legs
            }
            b.Block(new Vector3(0f, 0.2f, -0.15f), new Vector3(0.08f, 0.22f, 0.08f), rust, pitch: -20f, top: 1.3f); // bushy tail
            b.Block(new Vector3(0f, 0.33f, -0.11f), new Vector3(0.09f, 0.08f, 0.13f), rust, pitch: 15f);
            return b.Mesh("Creature_Squirrel");
        }

        private static Mesh Fox()
        {
            var b = new Builder();
            Color orange = C(0.9f, 0.45f, 0.15f), white = C(0.95f, 0.93f, 0.88f), black = C(0.12f, 0.1f, 0.1f);
            b.Block(new Vector3(0f, 0.4f, 0f), new Vector3(0.22f, 0.22f, 0.6f), orange, front: 0.9f);
            b.Block(new Vector3(0f, 0.34f, 0.25f), new Vector3(0.16f, 0.18f, 0.13f), white);                       // chest
            b.Block(new Vector3(0f, 0.52f, 0.38f), new Vector3(0.18f, 0.16f, 0.18f), orange, front: 0.75f);      // head
            b.Block(new Vector3(0f, 0.48f, 0.51f), new Vector3(0.08f, 0.07f, 0.14f), white, front: 0.6f);        // snout
            b.Block(new Vector3(0f, 0.49f, 0.585f), new Vector3(0.035f, 0.03f, 0.03f), black);                   // nose
            for (int s = -1; s <= 1; s += 2)
            {
                b.Block(new Vector3(s * 0.06f, 0.63f, 0.35f), new Vector3(0.055f, 0.1f, 0.03f), orange, top: 0.25f);    // ears
                b.Block(new Vector3(s * 0.06f, 0.54f, 0.47f), new Vector3(0.02f, 0.02f, 0.015f), black);                  // eyes
                b.Block(new Vector3(s * 0.07f, 0.15f, 0.21f), new Vector3(0.05f, 0.3f, 0.05f), black);                    // legs
                b.Block(new Vector3(s * 0.07f, 0.15f, -0.21f), new Vector3(0.055f, 0.3f, 0.055f), black);
            }
            b.Block(new Vector3(0f, 0.38f, -0.47f), new Vector3(0.11f, 0.11f, 0.36f), orange, pitch: 18f, front: 1.2f); // brush
            b.Block(new Vector3(0f, 0.31f, -0.67f), new Vector3(0.09f, 0.09f, 0.1f), white, pitch: 18f);              // white tip
            return b.Mesh("Creature_Fox");
        }

        private static Mesh Boar()
        {
            var b = new Builder();
            Color hide = C(0.33f, 0.27f, 0.22f), bristle = C(0.22f, 0.18f, 0.15f), snout = C(0.55f, 0.42f, 0.38f), ivory = C(0.93f, 0.89f, 0.76f), dark = C(0.1f, 0.08f, 0.07f);
            b.Block(new Vector3(0f, 0.56f, 0f), new Vector3(0.5f, 0.55f, 1f), hide, front: 1.1f);
            b.Block(new Vector3(0f, 0.86f, 0.15f), new Vector3(0.32f, 0.14f, 0.55f), bristle, top: 0.5f);          // bristly hump
            b.Block(new Vector3(0f, 0.55f, 0.62f), new Vector3(0.34f, 0.36f, 0.34f), hide, front: 0.7f);           // head
            b.Block(new Vector3(0f, 0.47f, 0.84f), new Vector3(0.19f, 0.17f, 0.2f), snout, front: 0.85f);
            b.Block(new Vector3(0f, 0.47f, 0.945f), new Vector3(0.15f, 0.13f, 0.03f), C(0.42f, 0.3f, 0.28f));     // snout disc
            for (int s = -1; s <= 1; s += 2)
            {
                b.Block(new Vector3(s * 0.095f, 0.47f, 0.88f), new Vector3(0.03f, 0.12f, 0.03f), ivory, pitch: -25f, top: 0.3f); // tusks
                b.Block(new Vector3(s * 0.13f, 0.77f, 0.56f), new Vector3(0.08f, 0.11f, 0.04f), bristle, top: 0.4f);              // ears
                b.Block(new Vector3(s * 0.12f, 0.63f, 0.74f), new Vector3(0.025f, 0.025f, 0.02f), dark);                           // eyes
                b.Block(new Vector3(s * 0.15f, 0.16f, 0.32f), new Vector3(0.1f, 0.33f, 0.1f), bristle);                            // legs
                b.Block(new Vector3(s * 0.15f, 0.16f, -0.32f), new Vector3(0.11f, 0.33f, 0.11f), bristle);
            }
            b.Block(new Vector3(0f, 0.66f, -0.53f), new Vector3(0.03f, 0.14f, 0.03f), bristle, pitch: 25f);         // tail
            return b.Mesh("Creature_Boar");
        }

        private static Mesh OldBear()
        {
            var b = new Builder();
            Color fur = C(0.28f, 0.19f, 0.12f), dark = C(0.18f, 0.12f, 0.08f), muzzle = C(0.47f, 0.35f, 0.23f);
            Color grey = C(0.55f, 0.5f, 0.45f), claw = C(0.9f, 0.86f, 0.75f), black = C(0.06f, 0.05f, 0.05f), scar = C(0.62f, 0.3f, 0.26f);
            b.Block(new Vector3(0f, 1.02f, 0f), new Vector3(1.1f, 1.0f, 1.9f), fur, front: 1.05f);
            b.Block(new Vector3(0f, 1.5f, 0.45f), new Vector3(0.8f, 0.32f, 0.75f), dark, top: 0.6f);                // shoulder hump
            b.Block(new Vector3(0f, 1.45f, -0.35f), new Vector3(0.7f, 0.12f, 0.7f), grey, top: 0.7f);               // old grey back
            b.Block(new Vector3(0f, 1.17f, 1.13f), new Vector3(0.62f, 0.56f, 0.56f), fur, front: 0.8f);             // head
            b.Block(new Vector3(0f, 1.06f, 1.46f), new Vector3(0.32f, 0.29f, 0.32f), muzzle, front: 0.8f);
            b.Block(new Vector3(0f, 1.1f, 1.625f), new Vector3(0.13f, 0.1f, 0.05f), black);                         // nose
            b.Block(new Vector3(0.2f, 1.28f, 1.37f), new Vector3(0.05f, 0.2f, 0.04f), scar, pitch: 25f);            // a scar over one eye
            for (int s = -1; s <= 1; s += 2)
            {
                b.Block(new Vector3(s * 0.23f, 1.47f, 1.03f), new Vector3(0.15f, 0.15f, 0.09f), dark, top: 0.6f);   // ears
                b.Block(new Vector3(s * 0.15f, 1.24f, 1.4f), new Vector3(0.05f, 0.05f, 0.03f), black);              // eyes
                b.Block(new Vector3(s * 0.36f, 0.38f, 0.62f), new Vector3(0.3f, 0.78f, 0.32f), dark, top: 1.15f);   // fore legs
                b.Block(new Vector3(s * 0.37f, 0.38f, -0.62f), new Vector3(0.32f, 0.78f, 0.36f), dark, top: 1.2f);  // hind legs
                b.Block(new Vector3(s * 0.36f, 0.05f, 0.84f), new Vector3(0.26f, 0.06f, 0.1f), claw);                // claws
            }
            return b.Mesh("Creature_OldBear");
        }

        // --- building --------------------------------------------------------------------------------------------------

        private static Color C(float r, float g, float b) => new Color(r, g, b);

        private static void Save(string name, Mesh mesh)
        {
            string meshPath = $"{GeneratedAssets.MeshesDir}/{mesh.name}.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                mesh = existing;
            }
            else AssetDatabase.CreateAsset(mesh, meshPath);

            var root = new GameObject(name);
            var model = new GameObject("Model", typeof(MeshFilter), typeof(MeshRenderer));
            model.transform.SetParent(root.transform, false);
            model.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = model.GetComponent<MeshRenderer>();
            r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(GeneratedAssets.ResourcesDir + "/FX/M_Terrain.mat");
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(name));
            Object.DestroyImmediate(root);
        }

        /// <summary>Tapered boxes, flat-shaded (each face its own vertices), one colour each with a little sun shading.</summary>
        private sealed class Builder
        {
            private readonly List<Vector3> _verts = new List<Vector3>();
            private readonly List<Color> _colors = new List<Color>();
            private readonly List<int> _tris = new List<int>();
            private static readonly Vector3 Sun = new Vector3(-0.4f, 0.8f, 0.45f).normalized;

            /// <summary>
            /// A box centred at <paramref name="center"/>. <paramref name="front"/> scales its +Z end (taper toward the
            /// head), <paramref name="top"/> its top face; <paramref name="pitch"/> tilts it about X (degrees).
            /// </summary>
            public void Block(Vector3 center, Vector3 size, Color color, float front = 1f, float top = 1f, float pitch = 0f)
            {
                Vector3 h = size * 0.5f;
                var c = new Vector3[8];
                for (int i = 0; i < 8; i++)
                {
                    float x = (i & 1) == 0 ? -h.x : h.x, y = (i & 2) == 0 ? -h.y : h.y, z = (i & 4) == 0 ? -h.z : h.z;
                    float k = (z > 0f ? front : 1f) * (y > 0f ? top : 1f);
                    var p = new Vector3(x * k, y * (z > 0f ? Mathf.Lerp(1f, front, 0.5f) : 1f), z);
                    c[i] = center + Quaternion.Euler(pitch, 0f, 0f) * p;
                }
                Quad(c[0], c[2], c[6], c[4], color, center); // left  (-x)
                Quad(c[1], c[5], c[7], c[3], color, center); // right (+x)
                Quad(c[2], c[3], c[7], c[6], color, center); // top
                Quad(c[0], c[4], c[5], c[1], color, center); // bottom
                Quad(c[4], c[6], c[7], c[5], color, center); // front (+z)
                Quad(c[0], c[1], c[3], c[2], color, center); // back
            }

            /// <summary>One face; wound so it faces away from the block's centre (Unity: Cross(b - a, c - a) outward).</summary>
            private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color, Vector3 blockCenter)
            {
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                if (Vector3.Dot(n, (a + b + c + d) * 0.25f - blockCenter) < 0f)
                {
                    (b, d) = (d, b);
                    n = -n;
                }
                Color lit = color * (0.82f + 0.18f * Mathf.Max(0f, Vector3.Dot(n, Sun)));
                lit.a = 1f;
                int i = _verts.Count;
                _verts.Add(a); _verts.Add(b); _verts.Add(c); _verts.Add(d);
                for (int k = 0; k < 4; k++) _colors.Add(lit);
                _tris.Add(i); _tris.Add(i + 1); _tris.Add(i + 2);
                _tris.Add(i); _tris.Add(i + 2); _tris.Add(i + 3);
            }

            public Mesh Mesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(_verts);
                mesh.SetColors(_colors);
                mesh.SetTriangles(_tris, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
