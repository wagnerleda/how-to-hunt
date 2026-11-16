using HowToHunt.Foundation;
using UnityEngine;

namespace HowToHunt.Core
{
    /// <summary>
    /// How far the world is drawn. Everything past r_drawdistance is culled per layer (fog hides the edge), except
    /// the Backdrop layer (horizon mountains, clouds), which keeps the camera's full range. Grass, flowers and other
    /// scatter use their own distance, scaled by r_scatter_distance. Allocation-free: the per-layer table is rebuilt
    /// only when a value changes.
    /// </summary>
    public static class RenderDistance
    {
        public const string BackdropLayerName = "Backdrop";

        public static readonly FloatCvar DrawDistance = new FloatCvar("r_drawdistance", 220f, "How far the world is drawn, metres (the horizon is always drawn). Lower is faster.", CvarFlags.Archive, 60f, 900f);
        public static readonly FloatCvar ScatterScale = new FloatCvar("r_scatter_distance", 0.8f, "Grass and flower draw distance, as a multiple of the default. Lower is faster.", CvarFlags.Archive, 0.2f, 2f);

        private static readonly float[] Distances = new float[32];
        private static float _applied = -1f;
        private static Camera _camera;

        public static void Register(ConsoleSystem console)
        {
            console.Register(DrawDistance);
            console.Register(ScatterScale);
        }

        /// <summary>Call every frame with the main camera; it only does work when the camera or the value changed.</summary>
        public static void Apply(Camera cam)
        {
            if (cam == null) return;
            float d = DrawDistance.Value;
            if (cam == _camera && Mathf.Approximately(d, _applied)) return;
            _camera = cam;
            _applied = d;
            for (int i = 0; i < Distances.Length; i++) Distances[i] = d;
            int backdrop = LayerMask.NameToLayer(BackdropLayerName);
            if (backdrop >= 0) Distances[backdrop] = 0f; // 0 = the far clip plane
            cam.layerCullSpherical = true;
            cam.layerCullDistances = Distances;
        }
    }
}
