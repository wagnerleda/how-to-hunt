using HowToHunt.Core;
using HowToHunt.Travel;
using UnityEditor;
using UnityEngine;

namespace HowToHunt.Editor
{
    /// <summary>
    /// The sacred totems by each camp (owner, 2026-11-25): consult one to travel to any region you have been to. A
    /// carved pole of three faces (bear, owl, and a thunderbird with spread wings on top) on a stone base, glowing eyes,
    /// a warm light above. Its discovery area is its map's bounds, set once the areas are known (StreamAreas).
    /// </summary>
    public static partial class GrayboxSceneBuilder
    {
        private const float TotemReach = 3.2f;

        private static Totem BuildTotem(Transform parent, string regionKey, Vector3 feet, float yaw)
        {
            string id = regionKey.Substring(regionKey.IndexOf('.') + 1);
            var region = AssetDatabase.LoadAssetAtPath<RegionDefinition>($"{GeneratedContent.ContentDir}/Regions/Region_{id}.asset");
            if (region == null) Debug.LogWarning($"[HowToHunt] No region {regionKey} for its totem");
            feet.y = GroundY(feet.x, feet.z);
            var root = new GameObject($"Totem ({id})").transform;
            root.SetParent(parent, false);
            root.SetPositionAndRotation(feet, Quaternion.Euler(0f, yaw, 0f));

            // Stone base and the pole (solid: you walk round it, not through it).
            Part(root, "Base", new Vector3(0f, 0.2f, 0f), new Vector3(1.5f, 0.4f, 1.5f), "Rock", collide: true);
            Part(root, "Base Step", new Vector3(0f, 0.48f, 0f), new Vector3(1.15f, 0.16f, 1.15f), "CliffDark");
            Part(root, "Pole", new Vector3(0f, 2.1f, 0f), new Vector3(0.8f, 3.1f, 0.72f), "TotemWood", collide: true);

            // Bottom: the bear (wide brows, a muzzle, bone teeth).
            Face(root, 1.05f, "TotemRed", 0.18f);
            Part(root, "Bear Muzzle", new Vector3(0f, 0.9f, 0.46f), new Vector3(0.46f, 0.3f, 0.22f), "TotemWood");
            Part(root, "Bear Nose", new Vector3(0f, 1.0f, 0.58f), new Vector3(0.16f, 0.1f, 0.06f), "Visor");
            for (int i = -1; i <= 1; i += 2)
            {
                Part(root, "Bear Tooth", new Vector3(0.1f * i, 0.73f, 0.55f), new Vector3(0.07f, 0.12f, 0.05f), "TotemBone");
                Part(root, "Bear Ear", new Vector3(0.36f * i, 1.42f, 0.1f), new Vector3(0.18f, 0.2f, 0.18f), "TotemWood");
            }
            Part(root, "Band 1", new Vector3(0f, 1.6f, 0f), new Vector3(0.86f, 0.1f, 0.78f), "TotemTeal");

            // Middle: the owl (big round eyes, a short hooked beak, feather marks).
            Face(root, 2.15f, "TotemTeal", 0.22f);
            Part(root, "Owl Beak", new Vector3(0f, 1.95f, 0.44f), new Vector3(0.14f, 0.22f, 0.14f), "TotemBone");
            for (int i = -1; i <= 1; i += 2)
                for (int k = 0; k < 3; k++)
                    Part(root, "Owl Feather", new Vector3(0.24f * i, 1.78f - k * 0.1f, 0.37f), new Vector3(0.16f, 0.04f, 0.02f), "TotemRed");
            Part(root, "Band 2", new Vector3(0f, 2.62f, 0f), new Vector3(0.86f, 0.1f, 0.78f), "TotemRed");

            // Top: the thunderbird, wings spread, beak forward.
            Face(root, 3.15f, "TotemRed", 0.16f);
            Part(root, "Bird Beak", new Vector3(0f, 3.0f, 0.5f), new Vector3(0.16f, 0.14f, 0.34f), "TotemBone");
            Part(root, "Bird Beak Hook", new Vector3(0f, 2.9f, 0.62f), new Vector3(0.12f, 0.12f, 0.1f), "TotemBone");
            for (int i = -1; i <= 1; i += 2)
            {
                Part(root, "Wing", new Vector3(0.95f * i, 3.35f, -0.05f), new Vector3(1.2f, 0.32f, 0.12f), "TotemTeal", roll: 14f * i);
                Part(root, "Wing Tip", new Vector3(1.62f * i, 3.62f, -0.05f), new Vector3(0.42f, 0.24f, 0.1f), "TotemRed", roll: 30f * i);
                Part(root, "Wing Stripe", new Vector3(0.95f * i, 3.37f, 0.02f), new Vector3(1.0f, 0.07f, 0.02f), "TotemBone", roll: 14f * i);
            }
            Part(root, "Crest", new Vector3(0f, 3.78f, -0.05f), new Vector3(0.3f, 0.26f, 0.5f), "TotemTeal");

            // The light above: a glowing stone and a warm lamp.
            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = "Spirit Light";
            Object.DestroyImmediate(orb.GetComponent<Collider>());
            orb.transform.SetParent(root, false);
            orb.transform.localPosition = new Vector3(0f, 4.3f, 0f);
            orb.transform.localScale = Vector3.one * 0.32f;
            orb.GetComponent<MeshRenderer>().sharedMaterial = Glow();
            orb.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var lamp = new GameObject("Spirit Lamp", typeof(Light)).GetComponent<Light>();
            lamp.transform.SetParent(root, false);
            lamp.transform.localPosition = new Vector3(0f, 4.1f, 0.4f);
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.8f, 0.45f);
            lamp.range = 7f;
            lamp.intensity = 1.6f;
            lamp.shadows = LightShadows.None;

            // Where travellers arrive: in front of it, facing away from it (looking at the camp).
            var arrival = new GameObject("Arrival").transform;
            arrival.SetParent(root, false);
            arrival.localPosition = new Vector3(0f, 0f, 2.4f);
            arrival.localRotation = Quaternion.identity;
            Vector3 a = arrival.position;
            arrival.position = new Vector3(a.x, GroundY(a.x, a.z) + 0.02f, a.z);

            Totem totem = root.gameObject.AddComponent<Totem>();
            totem.Configure(region, new Bounds(feet, Vector3.one * 40f), TotemReach, arrival);
            return totem;
        }

        /// <summary>A carved face band: a slightly wider block, two glowing eyes and brows above them.</summary>
        private static void Face(Transform root, float y, string paint, float eyeGap)
        {
            Part(root, "Face", new Vector3(0f, y, 0.05f), new Vector3(0.9f, 0.62f, 0.72f), "TotemWood");
            Part(root, "Paint", new Vector3(0f, y + 0.02f, 0.415f), new Vector3(0.78f, 0.5f, 0.02f), paint);
            for (int i = -1; i <= 1; i += 2)
            {
                Part(root, "Eye Socket", new Vector3(eyeGap * i, y + 0.08f, 0.43f), new Vector3(0.2f, 0.14f, 0.02f), "Visor");
                GameObject eye = Part(root, "Eye", new Vector3(eyeGap * i, y + 0.08f, 0.445f), new Vector3(0.1f, 0.07f, 0.02f), null);
                eye.GetComponent<MeshRenderer>().sharedMaterial = Glow();
                eye.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Part(root, "Brow", new Vector3(eyeGap * i, y + 0.2f, 0.44f), new Vector3(0.26f, 0.06f, 0.04f), "TotemBone", roll: -12f * i);
            }
        }

        private static GameObject Part(Transform root, string name, Vector3 local, Vector3 size, string material, bool collide = false, float roll = 0f)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(root, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, roll);
            go.transform.localScale = size;
            if (material != null) go.GetComponent<MeshRenderer>().sharedMaterial = Palette.Get(material);
            if (collide) go.AddComponent<SimBrush>().Shape = SimBrush.ShapeKind.Box;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        private static Material Glow() => AssetDatabase.LoadAssetAtPath<Material>(GeneratedAssets.ResourcesDir + "/FX/M_TotemGlow.mat");

        /// <summary>Each totem discovers its region over its map's whole area (the StreamedArea's bounds).</summary>
        private static void SetTotemAreas(Transform level)
        {
            foreach (Totem t in level.GetComponentsInChildren<Totem>(true))
            {
                StreamedArea area = t.GetComponentInParent<StreamedArea>();
                if (area == null) { Debug.LogWarning($"[HowToHunt] {t.name} is in no area"); continue; }
                Bounds b = area.Bounds;
                b.Expand(new Vector3(0f, 40f, 0f)); // up and down: jumping or standing on a ledge still counts
                t.Configure(t.Region, b, t.Reach, t.transform.Find("Arrival"));
            }
        }
    }
}
