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
            // The Red Desert (2026-11-24).
            Save("Jackrabbit", Jackrabbit());
            Save("Lizard", Lizard());
            Save("Scorpion", Scorpion());
            Save("Rattlesnake", Rattlesnake());
            Save("Coyote", Coyote());
            SaveMesh(Saguaro(0));
            SaveMesh(Saguaro(1));
            SaveMesh(BarrelCactus());
            SaveMesh(DeadBush());
            SaveMesh(Skull());
        }

        /// <summary>A desert prop mesh (vertex colours, M_Terrain), built by BuildAll: Prop_Saguaro0/1, Prop_Barrel, Prop_DeadBush, Prop_Skull.</summary>
        public static Mesh PropMesh(string name) => AssetDatabase.LoadAssetAtPath<Mesh>($"{GeneratedAssets.MeshesDir}/{name}.asset");

        // --- the desert ----------------------------------------------------------------------------------------------

        private static Mesh Jackrabbit()
        {
            var b = new Builder();
            Color tan = C(0.72f, 0.6f, 0.45f), pale = C(0.92f, 0.86f, 0.74f), pink = C(0.85f, 0.6f, 0.55f), dark = C(0.12f, 0.09f, 0.07f);
            b.Block(new Vector3(0f, 0.22f, -0.02f), new Vector3(0.2f, 0.2f, 0.34f), tan, front: 0.85f);
            b.Block(new Vector3(0f, 0.17f, 0.08f), new Vector3(0.14f, 0.12f, 0.12f), pale);                       // belly
            b.Block(new Vector3(0f, 0.33f, 0.19f), new Vector3(0.12f, 0.12f, 0.15f), tan, front: 0.75f);         // head
            for (int s = -1; s <= 1; s += 2)
            {
                b.Block(new Vector3(s * 0.035f, 0.5f, 0.14f), new Vector3(0.05f, 0.24f, 0.025f), tan, top: 0.6f, pitch: -12f); // long ears
                b.Block(new Vector3(s * 0.035f, 0.5f, 0.155f), new Vector3(0.03f, 0.18f, 0.01f), pink, top: 0.6f, pitch: -12f);
                b.Block(new Vector3(s * 0.055f, 0.36f, 0.24f), new Vector3(0.015f, 0.02f, 0.015f), dark);                         // eyes
                b.Block(new Vector3(s * 0.05f, 0.06f, 0.12f), new Vector3(0.035f, 0.12f, 0.035f), tan);                           // fore legs
                b.Block(new Vector3(s * 0.07f, 0.08f, -0.12f), new Vector3(0.05f, 0.14f, 0.16f), tan);                            // long hind legs
            }
            b.Block(new Vector3(0f, 0.25f, -0.21f), new Vector3(0.07f, 0.07f, 0.06f), pale);                     // tail
            return b.Mesh("Creature_Jackrabbit");
        }

        private static Mesh Lizard()
        {
            var b = new Builder();
            Color green = C(0.55f, 0.6f, 0.35f), band = C(0.85f, 0.55f, 0.25f), dark = C(0.12f, 0.12f, 0.08f);
            b.Block(new Vector3(0f, 0.05f, 0f), new Vector3(0.09f, 0.05f, 0.22f), green, front: 0.8f);
            b.Block(new Vector3(0f, 0.075f, -0.02f), new Vector3(0.06f, 0.012f, 0.16f), band);                   // a stripe down the back
            b.Block(new Vector3(0f, 0.055f, 0.15f), new Vector3(0.06f, 0.04f, 0.08f), green, front: 0.6f);       // head
            b.Block(new Vector3(0f, 0.04f, -0.2f), new Vector3(0.04f, 0.03f, 0.2f), green, front: 1.6f);         // tail
            for (int s = -1; s <= 1; s += 2)
            {
                b.Block(new Vector3(s * 0.025f, 0.07f, 0.17f), new Vector3(0.012f, 0.012f, 0.012f), dark);       // eyes
                b.Block(new Vector3(s * 0.065f, 0.02f, 0.07f), new Vector3(0.05f, 0.02f, 0.025f), green);         // legs
                b.Block(new Vector3(s * 0.065f, 0.02f, -0.07f), new Vector3(0.05f, 0.02f, 0.025f), green);
            }
            return b.Mesh("Creature_Lizard");
        }

        private static Mesh Scorpion()
        {
            var b = new Builder();
            Color shell = C(0.72f, 0.25f, 0.12f), dark = C(0.35f, 0.12f, 0.07f), sting = C(0.15f, 0.08f, 0.05f);
            b.Block(new Vector3(0f, 0.06f, 0f), new Vector3(0.14f, 0.06f, 0.2f), shell, front: 0.8f);           // body
            b.Block(new Vector3(0f, 0.06f, 0.11f), new Vector3(0.09f, 0.05f, 0.06f), dark);                      // head
            // The tail: five segments arching up and over the back, the sting at the end.
            Vector3 p = new Vector3(0f, 0.07f, -0.11f);
            float[] pitch = { 25f, 55f, 85f, 120f, 150f }; // up and back, then forward over the body
            for (int i = 0; i < pitch.Length; i++)
            {
                Vector3 dir = Quaternion.Euler(pitch[i], 0f, 0f) * new Vector3(0f, 0f, -1f);
                p += dir * 0.05f;
                b.Block(p, new Vector3(0.05f - i * 0.005f, 0.045f, 0.06f), i % 2 == 0 ? shell : dark, pitch: pitch[i]);
            }
            b.Block(p + new Vector3(0f, 0.02f, 0.04f), new Vector3(0.025f, 0.03f, 0.05f), sting, pitch: 40f, front: 0.3f);
            for (int s = -1; s <= 1; s += 2)
            {
                b.Block(new Vector3(s * 0.08f, 0.06f, 0.15f), new Vector3(0.03f, 0.025f, 0.09f), shell);            // arms
                b.Block(new Vector3(s * 0.1f, 0.06f, 0.22f), new Vector3(0.05f, 0.035f, 0.07f), dark, front: 0.6f); // claws
                for (int k = 0; k < 4; k++)
                    b.Block(new Vector3(s * 0.1f, 0.025f, 0.06f - k * 0.045f), new Vector3(0.09f, 0.015f, 0.015f), dark); // legs
            }
            return b.Mesh("Creature_Scorpion");
        }

        private static Mesh Rattlesnake()
        {
            var b = new Builder();
            Color sand = C(0.62f, 0.5f, 0.32f), diamond = C(0.36f, 0.27f, 0.17f), belly = C(0.82f, 0.74f, 0.55f), rattle = C(0.85f, 0.78f, 0.6f), dark = C(0.08f, 0.06f, 0.04f);
            // The body lies in a lazy S; the head is raised at the front, the rattle up at the back.
            const int n = 12;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                float z = Mathf.Lerp(-0.5f, 0.3f, t);
                float x = Mathf.Sin(t * Mathf.PI * 2.2f) * 0.1f;
                float w = Mathf.Lerp(0.05f, 0.09f, Mathf.Sin(t * Mathf.PI));
                b.Block(new Vector3(x, 0.04f, z), new Vector3(w, 0.06f, 0.08f), i % 2 == 0 ? sand : diamond);
                b.Block(new Vector3(x, 0.012f, z), new Vector3(w * 0.8f, 0.01f, 0.07f), belly);
            }
            b.Block(new Vector3(0f, 0.1f, 0.34f), new Vector3(0.05f, 0.1f, 0.05f), sand, pitch: 20f);          // the raised neck
            b.Block(new Vector3(0f, 0.17f, 0.41f), new Vector3(0.08f, 0.05f, 0.1f), diamond, front: 0.6f);      // the head
            for (int s = -1; s <= 1; s += 2) b.Block(new Vector3(s * 0.035f, 0.185f, 0.43f), new Vector3(0.01f, 0.01f, 0.01f), dark);
            b.Block(new Vector3(0.02f, 0.09f, -0.55f), new Vector3(0.035f, 0.1f, 0.035f), rattle, top: 0.5f, pitch: 20f); // the rattle
            return b.Mesh("Creature_Rattlesnake");
        }

        private static Mesh Coyote()
        {
            var b = new Builder();
            Color coat = C(0.7f, 0.58f, 0.42f), grey = C(0.5f, 0.45f, 0.4f), pale = C(0.9f, 0.85f, 0.75f), black = C(0.12f, 0.1f, 0.1f);
            b.Block(new Vector3(0f, 0.46f, 0f), new Vector3(0.24f, 0.24f, 0.66f), coat, front: 0.9f);
            b.Block(new Vector3(0f, 0.58f, 0.05f), new Vector3(0.18f, 0.06f, 0.5f), grey);                     // darker back
            b.Block(new Vector3(0f, 0.4f, 0.27f), new Vector3(0.17f, 0.2f, 0.14f), pale);                      // chest
            b.Block(new Vector3(0f, 0.58f, 0.42f), new Vector3(0.18f, 0.16f, 0.18f), coat, front: 0.75f);     // head
            b.Block(new Vector3(0f, 0.54f, 0.56f), new Vector3(0.08f, 0.07f, 0.16f), pale, front: 0.6f);      // long snout
            b.Block(new Vector3(0f, 0.55f, 0.645f), new Vector3(0.035f, 0.03f, 0.03f), black);
            for (int s = -1; s <= 1; s += 2)
            {
                b.Block(new Vector3(s * 0.06f, 0.7f, 0.38f), new Vector3(0.06f, 0.12f, 0.03f), coat, top: 0.25f);   // big ears
                b.Block(new Vector3(s * 0.06f, 0.6f, 0.51f), new Vector3(0.02f, 0.02f, 0.015f), black);
                b.Block(new Vector3(s * 0.075f, 0.18f, 0.23f), new Vector3(0.05f, 0.36f, 0.05f), coat);              // long legs
                b.Block(new Vector3(s * 0.075f, 0.18f, -0.23f), new Vector3(0.055f, 0.36f, 0.055f), coat);
            }
            b.Block(new Vector3(0f, 0.36f, -0.48f), new Vector3(0.1f, 0.1f, 0.34f), grey, pitch: 30f, front: 1.1f); // drooping tail
            b.Block(new Vector3(0f, 0.25f, -0.62f), new Vector3(0.08f, 0.08f, 0.08f), black, pitch: 30f);
            return b.Mesh("Creature_Coyote");
        }

        /// <summary>A saguaro: a ribbed column with one or two arms bent up, ~4.5 m at scale 1.</summary>
        private static Mesh Saguaro(int variant)
        {
            var b = new Builder();
            Color green = C(0.35f, 0.52f, 0.3f), rib = C(0.28f, 0.44f, 0.25f), top = C(0.5f, 0.62f, 0.38f);
            b.Block(new Vector3(0f, 2.1f, 0f), new Vector3(0.5f, 4.2f, 0.5f), green, top: 0.8f);
            b.Block(new Vector3(0f, 2.1f, 0f), new Vector3(0.56f, 4f, 0.3f), rib, top: 0.75f);
            b.Block(new Vector3(0f, 4.3f, 0f), new Vector3(0.36f, 0.25f, 0.36f), top, top: 0.6f);
            for (int s = -1; s <= 1; s += 2)
            {
                if (variant == 1 && s > 0) continue;
                float h = s < 0 ? 1.8f : 2.4f;
                b.Block(new Vector3(s * 0.5f, h, 0f), new Vector3(0.6f, 0.32f, 0.32f), green);                     // the elbow
                b.Block(new Vector3(s * 0.72f, h + 0.75f, 0f), new Vector3(0.32f, 1.5f, 0.32f), green, top: 0.8f); // the arm up
                b.Block(new Vector3(s * 0.72f, h + 1.55f, 0f), new Vector3(0.24f, 0.16f, 0.24f), top, top: 0.6f);
            }
            return b.Mesh($"Prop_Saguaro{variant}");
        }

        private static Mesh BarrelCactus()
        {
            var b = new Builder();
            Color green = C(0.38f, 0.55f, 0.3f), rib = C(0.3f, 0.45f, 0.24f), flower = C(0.95f, 0.45f, 0.4f);
            b.Block(new Vector3(0f, 0.3f, 0f), new Vector3(0.6f, 0.6f, 0.6f), green, top: 0.7f);
            b.Block(new Vector3(0f, 0.3f, 0f), new Vector3(0.66f, 0.55f, 0.35f), rib, top: 0.7f);
            b.Block(new Vector3(0f, 0.3f, 0f), new Vector3(0.35f, 0.55f, 0.66f), rib, top: 0.7f);
            b.Block(new Vector3(0f, 0.63f, 0f), new Vector3(0.16f, 0.08f, 0.16f), flower, top: 1.4f);
            return b.Mesh("Prop_Barrel");
        }

        private static Mesh DeadBush()
        {
            var b = new Builder();
            Color wood = C(0.55f, 0.45f, 0.33f), dark = C(0.42f, 0.33f, 0.24f);
            for (int i = 0; i < 7; i++)
            {
                Vector3 dir = Quaternion.Euler(-35f - (i % 3) * 12f, i * 51f, 0f) * Vector3.forward;
                b.Block(dir * 0.3f + new Vector3(0f, 0.18f, 0f), new Vector3(0.05f, 0.05f, 0.65f), i % 2 == 0 ? wood : dark, front: 0.4f);
            }
            return b.Mesh("Prop_DeadBush");
        }

        private static Mesh Skull()
        {
            var b = new Builder();
            Color bone = C(0.93f, 0.9f, 0.82f), dark = C(0.25f, 0.2f, 0.17f);
            b.Block(new Vector3(0f, 0.12f, 0f), new Vector3(0.28f, 0.22f, 0.3f), bone, front: 0.8f);
            b.Block(new Vector3(0f, 0.08f, 0.24f), new Vector3(0.16f, 0.12f, 0.24f), bone, front: 0.7f);      // snout
            for (int s = -1; s <= 1; s += 2)
            {
                b.Block(new Vector3(s * 0.08f, 0.16f, 0.14f), new Vector3(0.06f, 0.05f, 0.03f), dark);             // eye holes
                b.Block(new Vector3(s * 0.2f, 0.24f, -0.02f), new Vector3(0.3f, 0.05f, 0.05f), bone, front: 0.5f); // horns
            }
            return b.Mesh("Prop_Skull");
        }

        private static void SaveMesh(Mesh mesh)
        {
            string meshPath = $"{GeneratedAssets.MeshesDir}/{mesh.name}.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
            }
            else AssetDatabase.CreateAsset(mesh, meshPath);
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
