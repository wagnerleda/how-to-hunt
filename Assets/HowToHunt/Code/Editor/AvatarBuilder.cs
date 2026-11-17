using System.Collections.Generic;
using HowToHunt.Avatar;
using UnityEditor;
using UnityEngine;

namespace HowToHunt.Editor
{
    /// <summary>
    /// The players' stick figure, built into the player prefab: a capsule torso, a ball head behind a curved mask plate,
    /// thin limb segments (posed every frame by StickFigure), ball hands and feet, and a leaf. The mask plate, leaf and
    /// their materials live in Resources/Avatar so the menu's mask preview can use them too.
    /// </summary>
    public static class AvatarBuilder
    {
        private const string Dir = GeneratedAssets.ResourcesDir + "/Avatar";

        public static void BuildFigure(Transform visual)
        {
            EnsureAssets();
            Material skin = Load<Material>("M_Skin"), leafMat = Load<Material>("M_Leaf"), maskMat = Load<Material>("M_Mask");
            var figure = new GameObject("Figure").transform;
            figure.SetParent(visual, false);

            Prim(figure, "Torso", PrimitiveType.Capsule, AvatarShape.TorsoCenter, AvatarShape.TorsoScale, skin);
            Prim(figure, "Neck", PrimitiveType.Cylinder, AvatarShape.NeckCenter, AvatarShape.NeckScale, skin);
            Transform head = Prim(figure, "Head", PrimitiveType.Sphere, AvatarShape.HeadCenter, Vector3.one * AvatarShape.HeadSize, skin);
            // The head is scaled; the mask is its child, so undo the scale for it.
            var mask = new GameObject("Mask");
            mask.transform.SetParent(head, false);
            mask.transform.localPosition = AvatarShape.MaskOffset / AvatarShape.HeadSize;
            mask.transform.localScale = Vector3.one / AvatarShape.HeadSize;
            mask.AddComponent<MeshFilter>().sharedMesh = Load<Mesh>("MaskPlate");
            mask.AddComponent<MeshRenderer>().sharedMaterial = maskMat;

            var leaf = new GameObject("Leaf");
            leaf.transform.SetParent(figure, false);
            leaf.transform.localPosition = AvatarShape.LeafCenter;
            leaf.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);
            leaf.AddComponent<MeshFilter>().sharedMesh = Load<Mesh>("Leaf");
            leaf.AddComponent<MeshRenderer>().sharedMaterial = leafMat;

            foreach (string limb in new[] { "UpperArm.L", "Forearm.L", "UpperArm.R", "Forearm.R", "Thigh.L", "Shin.L", "Thigh.R", "Shin.R" })
                Prim(figure, limb, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.045f, 0.2f, 0.045f), skin);
            foreach (string hand in new[] { "Hand.L", "Hand.R" })
                Prim(figure, hand, PrimitiveType.Sphere, Vector3.zero, Vector3.one * AvatarShape.HandSize, skin);
            foreach (string foot in new[] { "Foot.L", "Foot.R" })
                Prim(figure, foot, PrimitiveType.Sphere, Vector3.zero, new Vector3(AvatarShape.FootSize, AvatarShape.FootSize * 0.8f, AvatarShape.FootSize * 1.3f), skin);
        }

        private static Transform Prim(Transform parent, string name, PrimitiveType type, Vector3 at, Vector3 scale, Material m)
        {
            GameObject g = GameObject.CreatePrimitive(type);
            g.name = name;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = at;
            g.transform.localScale = scale;
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
            return g.transform;
        }

        private static T Load<T>(string name) where T : Object
        {
            string ext = typeof(T) == typeof(Material) ? ".mat" : ".asset";
            return AssetDatabase.LoadAssetAtPath<T>($"{Dir}/{name}{ext}");
        }

        // --- assets ------------------------------------------------------------------------------------------------

        public static void EnsureAssets()
        {
            GeneratedAssets.EnsureFolder(Dir);
            Lit("M_Skin", new Color(0.725f, 0.486f, 0.345f), 0.15f, clip: false);
            Lit("M_Leaf", new Color(0.25f, 0.55f, 0.18f), 0.35f, clip: false);
            Lit("M_Mask", Color.white, 0.2f, clip: true);
            SaveMesh("MaskPlate", MaskPlate());
            SaveMesh("Leaf", LeafMesh());
        }

        private static void Lit(string name, Color color, float smoothness, bool clip)
        {
            string path = $"{Dir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Cull", 0f); // the leaf and mask are thin: both sides
            if (clip)
            {
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", 0.5f);
                m.EnableKeyword("_ALPHATEST_ON");
            }
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
        }

        private static void SaveMesh(string name, Mesh mesh)
        {
            string path = $"{Dir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            mesh.name = name;
            if (existing == null) AssetDatabase.CreateAsset(mesh, path);
            else
            {
                EditorUtility.CopySerialized(mesh, existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(mesh);
            }
        }

        /// <summary>A plate curved round the face (centred on its own origin), UVs over the whole painting.</summary>
        private static Mesh MaskPlate()
        {
            const int cols = 10, rows = 8;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            float r = AvatarShape.MaskCurve;
            for (int y = 0; y <= rows; y++)
            for (int x = 0; x <= cols; x++)
            {
                float u = x / (float)cols, v = y / (float)rows;
                // u = 0 is the left of the painting as you face the mask, which is the figure's right (+X).
                float px = (0.5f - u) * AvatarShape.MaskWidth, py = (v - 0.5f) * AvatarShape.MaskHeight;
                // Wrap round a cylinder of radius r (and a little round the brow and chin).
                float angle = px / r;
                float z = r * (Mathf.Cos(angle) - 1f) - 0.25f * py * py;
                verts.Add(new Vector3(r * Mathf.Sin(angle), py, z));
                uvs.Add(new Vector2(u, v));
            }
            for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
            {
                int i = y * (cols + 1) + x;
                tris.Add(i); tris.Add(i + cols + 1); tris.Add(i + 1);
                tris.Add(i + 1); tris.Add(i + cols + 1); tris.Add(i + cols + 2);
            }
            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A pointed leaf hanging down (tip at the bottom), gently cupped, with flat-shaded halves.</summary>
        private static Mesh LeafMesh()
        {
            const int segments = 8;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            float len = AvatarShape.LeafLength, w = AvatarShape.LeafWidth * 0.5f;
            // Midrib from the stem (top) to the tip (bottom); edges bulge widest a third of the way down.
            for (int side = -1; side <= 1; side += 2)
            {
                int start = verts.Count;
                for (int i = 0; i <= segments; i++)
                {
                    float t = i / (float)segments;
                    float y = len * 0.5f - t * len;
                    float width = w * Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.75f)) * (t < 0.08f ? t / 0.08f * 0.6f + 0.4f : 1f);
                    verts.Add(new Vector3(0f, y, 0.01f));                              // rib
                    verts.Add(new Vector3(side * width, y - 0.01f, -0.012f * width / w)); // edge, curled back
                }
                for (int i = 0; i < segments; i++)
                {
                    int a = start + i * 2;
                    if (side < 0) { tris.Add(a); tris.Add(a + 1); tris.Add(a + 2); tris.Add(a + 1); tris.Add(a + 3); tris.Add(a + 2); }
                    else { tris.Add(a); tris.Add(a + 2); tris.Add(a + 1); tris.Add(a + 1); tris.Add(a + 2); tris.Add(a + 3); }
                }
            }
            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
