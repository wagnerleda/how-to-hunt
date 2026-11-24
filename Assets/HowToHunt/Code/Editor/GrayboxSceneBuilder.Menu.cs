using HowToHunt.Avatar;
using HowToHunt.Hunting;
using UnityEditor;
using UnityEngine;

namespace HowToHunt.Editor
{
    /// <summary>
    /// The main menu's background (owner, 2026-11-24): a hunter sitting on a boulder in the Plains with a rifle, looking
    /// west over the meadow into the sunset, and the camera's pose behind them (MenuVista owns the camera and the light
    /// while no party runs; RestingHunter poses the figure). The boulder stays in the game; the hunter only shows on
    /// the menu.
    /// </summary>
    public static partial class GrayboxSceneBuilder
    {
        private static readonly Vector3 MenuSeat = new Vector3(84f, 0f, 203f);   // east meadow, south of the river, away from the flower fields
        private static readonly Vector3 MenuLook = new Vector3(-1f, 0f, 0.3f);    // west, a little north: over the meadow
        private static readonly Vector3 MenuSunTravel = new Vector3(1f, -0.07f, -0.36f); // the light comes from the low western sun

        private const float RifleScale = 1.2f; // reads better at the figure's size

        private static void BuildMenuVista(Transform plains, Transform props)
        {
            Transform root = Group(plains, "Menu Vista");
            Vector3 look = MenuLook.normalized;
            float yaw = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;

            // The boulder (a real rock in the game, it collides).
            var rock = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(KenneyDir + "rock_largeA.fbx"), props);
            Vector3 seat = OnGround(MenuSeat);
            rock.name = "Menu Rock";
            Vector3 facing = Quaternion.Euler(0f, -14f, 0f) * look;
            rock.transform.SetPositionAndRotation(seat - new Vector3(0f, 0.12f, 0f), Quaternion.LookRotation(facing));
            // A low boulder to sit on: about 0.9 m high (above the grass), 1.7 m across, shallow front to back so the feet reach the grass.
            Bounds raw = RendererBounds(rock);
            rock.transform.localScale = new Vector3(1.7f / raw.size.x, 0.88f / raw.size.y, 0.95f / raw.size.z);
            GameObjectUtility.SetStaticEditorFlags(rock, StaticEditorFlags.BatchingStatic);
            Bounds rb = RendererBounds(rock);
            Box(props, "Menu Rock Collision", rb.center, rb.size * 0.85f, "Rock").GetComponent<MeshRenderer>().enabled = false;
            float seatHeight = rb.max.y - seat.y;

            // The hunter: the players' figure, sitting on the top of the rock, facing the sunset (a little to the camera).
            var hunter = new GameObject("Menu Hunter");
            hunter.transform.SetParent(root, false);
            // On the front of the top, so the legs hang over the edge to the ground.
            hunter.transform.SetPositionAndRotation(new Vector3(rb.center.x, rb.max.y - 0.04f + 0.06f, rb.center.z) + facing * 0.12f, Quaternion.LookRotation(facing));
            AvatarBuilder.BuildFigure(hunter.transform);
            foreach (Renderer r in hunter.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            // The rifle: the scout's model copied as plain meshes (no rig, no hands), its butt at the origin.
            Transform rifle = StaticCopy(AssetDatabase.LoadAssetAtPath<ToolDefinition>(GeneratedContent.ContentDir + "/Tools/Tool_Scout.asset")?.Viewmodel, hunter.transform, out float length);
            hunter.AddComponent<RestingHunter>().Configure(rifle, length, seatHeight + 0.08f);

            // The camera: behind the hunter's right shoulder, eye height above the ground, looking out with them.
            Vector3 right = Vector3.Cross(Vector3.up, look).normalized;
            Vector3 camAt = hunter.transform.position - look * 4.2f - right * 2.1f;
            camAt.y = Mathf.Max(GroundY(camAt.x, camAt.z) + 1.9f, hunter.transform.position.y + 0.55f);
            var shot = new GameObject("Menu Camera Pose").transform;
            shot.SetParent(root, false);
            shot.SetPositionAndRotation(camAt, Quaternion.LookRotation(look + Vector3.up * 0.015f));

            root.gameObject.AddComponent<MenuVista>().Configure(shot, hunter, MenuSunTravel.normalized);
        }

        private static Bounds RendererBounds(GameObject go)
        {
            Bounds b = default;
            bool any = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        /// <summary>A plain copy of a viewmodel's visible meshes (no scripts, no hands), its back end at the origin.</summary>
        private static Transform StaticCopy(GameObject model, Transform parent, out float length)
        {
            // Built at the world origin (the parts copy the instance's world poses), then hung under the parent.
            var root = new GameObject("Rifle").transform;
            length = 1.1f;
            if (model == null) { root.SetParent(parent, false); return root; }
            GameObject instance = Object.Instantiate(model);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var holder = new GameObject("Model").transform;
            holder.SetParent(root, false);
            Bounds b = default;
            bool any = false;
            foreach (MeshRenderer r in instance.GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.gameObject.activeInHierarchy || r.name.EndsWith("Hand", System.StringComparison.Ordinal)) continue;
                MeshFilter f = r.GetComponent<MeshFilter>();
                if (f == null || f.sharedMesh == null) continue;
                var part = new GameObject(r.name, typeof(MeshFilter), typeof(MeshRenderer));
                part.transform.SetParent(holder, false);
                part.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation);
                part.transform.localScale = r.transform.lossyScale;
                part.GetComponent<MeshFilter>().sharedMesh = f.sharedMesh;
                part.GetComponent<MeshRenderer>().sharedMaterials = r.sharedMaterials;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            Object.DestroyImmediate(instance);
            root.SetParent(parent, false);
            if (!any) return root;
            // Butt (the lowest z) at the origin, centred across.
            holder.localPosition = new Vector3(-b.center.x, -b.center.y, -b.min.z);
            length = b.size.z * RifleScale;
            root.localScale = Vector3.one * RifleScale;
            return root;
        }
    }
}
