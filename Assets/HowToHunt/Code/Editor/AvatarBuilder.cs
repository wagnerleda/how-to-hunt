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
            AddStrap(mask.transform, Load<Mesh>("MaskStrap"), Load<Material>("M_Strap"));

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

        /// <summary>The strap behind the head, a child of the mask (so it follows the mask wherever it is worn).</summary>
        public static void AddStrap(Transform mask, Mesh strap, Material leather)
        {
            var go = new GameObject("Strap");
            go.transform.SetParent(mask, false);
            go.AddComponent<MeshFilter>().sharedMesh = strap;
            go.AddComponent<MeshRenderer>().sharedMaterial = leather;
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
            Lit("M_Strap", new Color(0.24f, 0.15f, 0.09f), 0.25f, clip: false);
            SaveMesh("MaskPlate", MaskPlate());
            SaveMesh("MaskStrap", StrapMesh());
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

        /// <summary>
        /// The mask shell (owner, 2026-11-25: the painting should sit on the 3D model): wrapped round the head on an
        /// ellipsoid, curling back over the brow and under the chin, with a nose and a brow ridge pushed out of it, so
        /// the paint follows the face and catches the light. UVs cover the whole painting; u = 0 is its left as you
        /// face the mask (the figure's right, +X). Origin at AvatarShape.MaskOffset from the head's centre.
        /// </summary>
        private static Mesh MaskPlate()
        {
            const int cols = 32, rows = 36;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            Vector3 head = AvatarShape.HeadFromMask;
            float span = AvatarShape.MaskSpan * Mathf.Deg2Rad;
            for (int y = 0; y <= rows; y++)
            for (int x = 0; x <= cols; x++)
            {
                float u = x / (float)cols, v = y / (float)rows;
                float angle = (0.5f - u) * 2f * span;
                float py = (v - 0.5f) * AvatarShape.MaskHeight;          // height in the mask's space
                float fromCentre = py - head.y;                            // height from the head's centre
                float k = Mathf.Clamp01(fromCentre / AvatarShape.MaskVerticalRadius);
                float radius = AvatarShape.MaskRadius * Mathf.Sqrt(Mathf.Max(0.05f, 1f - k * k));
                // The nose (deepest at its tip, nothing past the sides of the face) and the brow ridge.
                float side = radius * Mathf.Sin(angle);
                float across = Mathf.Exp(-(side * side) / (AvatarShape.NoseWidth * AvatarShape.NoseWidth));
                float along = py > AvatarShape.NoseTop ? 0f
                    : py > AvatarShape.NoseTip ? (AvatarShape.NoseTop - py) / (AvatarShape.NoseTop - AvatarShape.NoseTip)
                    : Mathf.Max(0f, 1f - (AvatarShape.NoseTip - py) / 0.02f);
                float brow = AvatarShape.BrowHeight * Mathf.Exp(-Mathf.Pow((py - AvatarShape.BrowY) / 0.015f, 2f)) * Mathf.Cos(angle);
                radius += AvatarShape.NoseDepth * across * along * along + brow;
                verts.Add(head + new Vector3(radius * Mathf.Sin(angle), fromCentre, radius * Mathf.Cos(angle)));
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
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// The leather strap (owner, 2026-11-25): a band round the back of the head from one side of the mask to the
        /// other, tucked just under its edges. Same space as the mask.
        /// </summary>
        private static Mesh StrapMesh()
        {
            const int segments = 28;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            Vector3 head = AvatarShape.HeadFromMask;
            float from = AvatarShape.StrapFrom * Mathf.Deg2Rad, to = (360f - AvatarShape.StrapFrom) * Mathf.Deg2Rad;
            float y = AvatarShape.StrapY, half = AvatarShape.StrapWidth * 0.5f, r = AvatarShape.StrapRadius;
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.Lerp(from, to, i / (float)segments);
                var around = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                // Slightly narrower towards the buckle-less back, wider where it meets the mask.
                float w = half * (0.85f + 0.15f * Mathf.Abs(Mathf.Cos(a)));
                verts.Add(head + around * r + new Vector3(0f, y + w, 0f));
                verts.Add(head + around * r + new Vector3(0f, y - w, 0f));
            }
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2;
                tris.Add(a); tris.Add(a + 2); tris.Add(a + 1);
                tris.Add(a + 1); tris.Add(a + 2); tris.Add(a + 3);
            }
            var mesh = new Mesh();
            mesh.SetVertices(verts);
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
