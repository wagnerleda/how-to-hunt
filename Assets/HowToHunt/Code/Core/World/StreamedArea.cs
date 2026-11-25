using System.Collections.Generic;
using HowToHunt.Foundation;
using UnityEngine;

namespace HowToHunt.Core
{
    /// <summary>
    /// One map or route (the Plains, the Pine Forest, the Red Desert, a surf route), drawn only while the camera is near
    /// it: past r_area_distance from its bounds, nothing under it is drawn (renderers including its horizon mountains
    /// and clouds, instanced scatter, lights). Collision, animals and zones stay: the host still simulates players
    /// elsewhere, and walking back shows it again. Uses Renderer.forceRenderingOff (nothing else sets it), so gameplay
    /// code that turns renderers on and off isn't overridden. Checked a few times a second; no allocations after Start.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StreamedArea : MonoBehaviour
    {
        public static readonly FloatCvar Distance = new FloatCvar("r_area_distance", 160f,
            "Maps and routes farther than this (metres from their edge) are not drawn at all. Lower is faster.", CvarFlags.Archive, 60f, 5000f);
        public static readonly FloatCvar Interval = new FloatCvar("r_area_interval", 0.25f,
            "How often (seconds) map visibility is checked.", CvarFlags.Archive, 0.05f, 2f);
        private const float Hysteresis = 25f; // shown until this much past the distance, so the edge doesn't flicker

        [Tooltip("The area itself (terrain, props, ramps), without its far horizon. Set by the scene builder.")]
        [SerializeField] private Bounds _bounds;

        private static readonly List<StreamedArea> Registered = new List<StreamedArea>();
        public static IReadOnlyList<StreamedArea> All => Registered;

        private Renderer[] _renderers;
        private Light[] _lights;
        private bool[] _lightsOn;
        private ScatterRenderer[] _scatters;
        private bool _shown = true;
        private float _nextCheck;

        public Bounds Bounds => _bounds;
        public bool Shown => _shown;

        public void Configure(Bounds bounds) => _bounds = bounds;

        public static void Register(ConsoleSystem console)
        {
            console.Register(Distance);
            console.Register(Interval);
        }

        private void OnEnable() => Registered.Add(this);

        private void OnDisable()
        {
            Registered.Remove(this);
            Show(true);
        }

        private void Start()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _lights = GetComponentsInChildren<Light>(true);
            _lightsOn = new bool[_lights.Length];
            for (int i = 0; i < _lights.Length; i++) _lightsOn[i] = _lights[i].enabled;
            _scatters = GetComponentsInChildren<ScatterRenderer>(true);
        }

        private void Update()
        {
            if (!Application.isPlaying || _renderers == null) return;
            float now = Time.unscaledTime;
            if (now < _nextCheck) return;
            _nextCheck = now + Interval.Value;
            Camera cam = Camera.main;
            if (cam == null) { Show(true); return; }
            float d = Mathf.Sqrt(_bounds.SqrDistance(cam.transform.position));
            Show(_shown ? d < Distance.Value + Hysteresis : d < Distance.Value);
        }

        private void Show(bool show)
        {
            if (show == _shown || _renderers == null) return;
            _shown = show;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].forceRenderingOff = !show;
            for (int i = 0; i < _lights.Length; i++)
            {
                if (_lights[i] == null) continue;
                if (!show) _lightsOn[i] = _lights[i].enabled;
                _lights[i].enabled = show && _lightsOn[i];
            }
            for (int i = 0; i < _scatters.Length; i++)
                if (_scatters[i] != null) _scatters[i].Hidden = !show;
        }
    }
}
