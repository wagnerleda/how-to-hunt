using System.Collections.Generic;
using UnityEngine;

namespace HowToHunt.Core
{
    /// <summary>
    /// Renders a model to a small transparent texture for UI icons (inventory slots), once per model, cached.
    /// Uses a temporary camera far below the level so nothing else is in shot. Returns null on headless machines.
    /// </summary>
    public static class IconRenderer
    {
        private const int Size = 128;
        private static readonly Dictionary<(GameObject, int), Texture2D> Cache = new Dictionary<(GameObject, int), Texture2D>();
        private static readonly Vector3 Stage = new Vector3(0f, -5000f, 0f);

        /// <param name="yaw">Model yaw for the shot (e.g. -60 shows a tool from the side).</param>
        /// <summary>
        /// Removes child objects whose name ends with the suffix (e.g. the hand baked into a viewmodel). Removed rather
        /// than disabled: a disabled renderer left in the one-shot icon render came out in debug colours.
        /// </summary>
        public static void HideParts(GameObject root, string suffix)
        {
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                if (r != null && r.gameObject != root && r.gameObject.name.EndsWith(suffix, System.StringComparison.Ordinal)) Object.DestroyImmediate(r.gameObject);
        }

        public static Texture2D Get(GameObject model, float yaw = 30f, float pitch = 15f, string hideSuffix = null)
        {
            if (model == null || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return null;
            var key = (model, Mathf.RoundToInt(yaw * 10f + pitch) + (hideSuffix != null ? 1 << 20 : 0));
            if (Cache.TryGetValue(key, out Texture2D cached) && cached != null) return cached;

            GameObject instance = Object.Instantiate(model, Stage, Quaternion.Euler(0f, yaw, 0f));
            foreach (Animator a in instance.GetComponentsInChildren<Animator>()) a.enabled = false;
            if (hideSuffix != null) HideParts(instance, hideSuffix);
            Bounds b = new Bounds(Stage, Vector3.zero);
            bool any = false;
            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }

            var camGo = new GameObject("Icon Camera");
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.orthographic = true;
            float radius = Mathf.Max(0.05f, b.extents.magnitude);
            cam.orthographicSize = radius * 1.05f;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = radius * 6f;
            Quaternion view = Quaternion.Euler(pitch, 0f, 0f);
            camGo.transform.SetPositionAndRotation(b.center - view * Vector3.forward * radius * 3f, view);

            // A key light from the camera side so icons are never silhouettes (removed before anything else renders).
            var lightGo = new GameObject("Icon Light");
            Light keyLight = lightGo.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.intensity = 1.4f;
            keyLight.shadows = LightShadows.None;
            lightGo.transform.rotation = Quaternion.Euler(pitch + 25f, -35f, 0f);

            var rt = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            // No scene fog on icons: the camera is far from the stage and fog would tint the whole item.
            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;
            cam.Render();
            RenderSettings.fog = fog;
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = $"Icon {model.name}" };
            tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            tex.Apply(false, true);
            RenderTexture.active = previous;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);

            Object.DestroyImmediate(lightGo);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(instance);
            Cache[key] = tex;
            return tex;
        }
    }
}
