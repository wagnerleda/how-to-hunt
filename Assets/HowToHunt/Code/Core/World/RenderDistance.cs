using HowToHunt.Foundation;
using UnityEngine;

namespace HowToHunt.Core
{
    /// <summary>
    /// The player's fog and how far the world is drawn (owner, 2026-11-25: fog about 350 m out softens the far
    /// world; some levels and cases will bring it closer). The fog is fully thick at the fog distance and starts at
    /// r_fog_start of it; nothing is drawn past the fog distance (it couldn't be seen), horizon included, and the
    /// camera's far plane sits just past it. The distance is the nearest of: r_fog_distance (the player's setting),
    /// the current map's own limit (<see cref="StreamedArea"/>, set by the scene builder; 0 = none) and a gameplay
    /// limit (<see cref="CaseFog"/>, e.g. a storm; 0 = none). Changes glide instead of snapping. Grass, flowers and
    /// other scatter use their own, shorter distance, scaled by r_scatter_distance. Allocation-free.
    /// </summary>
    public static class RenderDistance
    {
        public const string BackdropLayerName = "Backdrop";

        public static readonly FloatCvar FogDistance = new FloatCvar("r_fog_distance", 350f,
            "Distance where the fog is fully thick (metres); nothing is drawn past it. Lower is faster. Maps can bring it closer.", CvarFlags.Archive, 60f, 900f);
        public static readonly FloatCvar FogStart = new FloatCvar("r_fog_start", 0.35f,
            "Where the fog begins, as a share of the fog distance.", CvarFlags.Archive, 0f, 0.95f);
        public static readonly FloatCvar ScatterScale = new FloatCvar("r_scatter_distance", 0.8f, "Grass and flower draw distance, as a multiple of the default. Lower is faster.", CvarFlags.Archive, 0.2f, 2f);

        private const float GlideSpeed = 120f;   // metres a second the fog moves when its distance changes
        private const float FarPlaneMargin = 5f; // the far clip plane, just past the fog

        /// <summary>The current map's own fog limit, metres (0 = none). Set by StreamedArea.</summary>
        public static float LevelFog { get; set; }
        /// <summary>A gameplay fog limit, metres (0 = none): weather, a cave, a cutscene.</summary>
        public static float CaseFog { get; set; }

        /// <summary>The fog distance being shown right now (it glides toward <see cref="TargetDistance"/>).</summary>
        public static float Distance => _shown > 0f ? _shown : TargetDistance;

        /// <summary>The nearest of the player's setting, the map's limit and the gameplay limit.</summary>
        public static float TargetDistance
        {
            get
            {
                float d = FogDistance.Value;
                if (LevelFog > 0f) d = Mathf.Min(d, LevelFog);
                if (CaseFog > 0f) d = Mathf.Min(d, CaseFog);
                return d;
            }
        }

        private static readonly float[] Distances = new float[32];
        private static float _applied = -1f, _shown = -1f;
        private static Camera _camera;

        public static void Register(ConsoleSystem console)
        {
            console.Register(FogDistance);
            console.Register(FogStart);
            console.Register(ScatterScale);
        }

        /// <summary>Call every frame with the gameplay camera: sets the fog, the far plane and per-layer culling.</summary>
        public static void Apply(Camera cam)
        {
            if (cam == null) return;
            float target = TargetDistance;
            _shown = _shown <= 0f ? target : Mathf.MoveTowards(_shown, target, GlideSpeed * Time.unscaledDeltaTime);
            float d = _shown;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = d * FogStart.Value;
            RenderSettings.fogEndDistance = d;
            if (cam == _camera && Mathf.Approximately(d, _applied)) return;
            _camera = cam;
            _applied = d;
            for (int i = 0; i < Distances.Length; i++) Distances[i] = 0f; // 0 = the far clip plane
            cam.layerCullSpherical = true;
            cam.layerCullDistances = Distances;
            cam.farClipPlane = d + FarPlaneMargin;
        }
    }
}
