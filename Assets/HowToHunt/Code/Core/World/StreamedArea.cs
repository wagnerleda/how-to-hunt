using System.Collections.Generic;
using HowToHunt.Foundation;
using UnityEngine;

namespace HowToHunt.Core
{
    /// <summary>
    /// One map or surf route along the world's chain (lab, Plains, east route, Pine Forest, south route, Red Desert).
    /// Owner's rule (2026-11-25): only the map the camera is in is drawn in full; the previous and next maps along
    /// the chain show their bare terrain only; the surf routes between the current map and those two are drawn in
    /// full; everything else is not drawn at all (horizon mountains included). The current map is the one whose
    /// bounds are nearest the camera, so halfway down a route the map you're heading to takes over.
    /// Visual only (Renderer.forceRenderingOff, lights, instanced scatter): collision, prey and zones stay for players
    /// elsewhere. Checked a few times a second; no allocations after Start.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StreamedArea : MonoBehaviour
    {
        public static readonly BoolCvar Enabled = new BoolCvar("r_area_streaming", true,
            "Draw only the map you're in, the neighbouring maps' bare terrain and the surf routes to them (0 = draw everything).", CvarFlags.Archive);
        public static readonly FloatCvar Interval = new FloatCvar("r_area_interval", 0.25f,
            "How often (seconds) map visibility is checked.", CvarFlags.Archive, 0.05f, 2f);

        public enum Mode : byte { Full, Bare, Hidden }

        [Tooltip("The area itself (terrain, props, ramps), without its far horizon. Set by the scene builder.")]
        [SerializeField] private Bounds _bounds;
        [Tooltip("Place along the world's chain (maps and routes interleaved, start to end).")]
        [SerializeField] private int _order;
        [Tooltip("A surf route between two maps (not a map you can be in).")]
        [SerializeField] private bool _route;
        [Tooltip("What stays drawn when this map is a neighbour: its bare terrain.")]
        [SerializeField] private Renderer[] _bare = new Renderer[0];

        private static readonly List<StreamedArea> Registered = new List<StreamedArea>();
        public static IReadOnlyList<StreamedArea> All => Registered;
        private static float _nextCheck;
        private static int _checkedFrame = -1;

        private Renderer[] _renderers;
        private bool[] _isBare;
        private Light[] _lights;
        private bool[] _lightsOn;
        private ScatterRenderer[] _scatters;
        private Mode _mode = Mode.Full;
        private bool _started;

        public Bounds Bounds => _bounds;
        public int Order => _order;
        public bool IsRoute => _route;
        public Mode Current => _mode;
        public bool Shown => _mode != Mode.Hidden;

        public void Configure(Bounds bounds, int order, bool route, Renderer[] bare)
        {
            _bounds = bounds;
            _order = order;
            _route = route;
            _bare = bare ?? new Renderer[0];
        }

        public static void Register(ConsoleSystem console)
        {
            console.Register(Enabled);
            console.Register(Interval);
        }

        private void OnEnable() => Registered.Add(this);

        private void OnDisable()
        {
            Registered.Remove(this);
            Apply(Mode.Full);
        }

        private void Start()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _isBare = new bool[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
                for (int j = 0; j < _bare.Length; j++)
                    if (_bare[j] == _renderers[i]) { _isBare[i] = true; break; }
            _lights = GetComponentsInChildren<Light>(true);
            _lightsOn = new bool[_lights.Length];
            for (int i = 0; i < _lights.Length; i++) _lightsOn[i] = _lights[i].enabled;
            _scatters = GetComponentsInChildren<ScatterRenderer>(true);
            _started = true;
        }

        private void Update()
        {
            // Whichever area updates first does the whole chain, once per check.
            if (!Application.isPlaying || _checkedFrame == Time.frameCount) return;
            _checkedFrame = Time.frameCount;
            float now = Time.unscaledTime;
            if (now < _nextCheck) return;
            _nextCheck = now + Interval.Value;
            Camera cam = Camera.main;
            if (cam == null || !Enabled.Value) { ApplyAll(Mode.Full); return; }
            StreamedArea here = CurrentMap(cam.transform.position);
            if (here == null) { ApplyAll(Mode.Full); return; }
            int prev = NeighbourMap(here._order, -1), next = NeighbourMap(here._order, 1);
            for (int i = 0; i < Registered.Count; i++)
            {
                StreamedArea a = Registered[i];
                a.Apply(ModeFor(a._order, a._route, here._order, prev, next));
            }
        }

        /// <summary>
        /// The rule, by chain order: the current map in full, the routes between it and its neighbour maps in full,
        /// the neighbour maps bare, the rest hidden. prev/next are int.MinValue/MaxValue when there is none.
        /// </summary>
        public static Mode ModeFor(int order, bool route, int current, int prev, int next)
        {
            if (order == current) return Mode.Full;
            if (route) return (order > prev && order < current) || (order > current && order < next) ? Mode.Full : Mode.Hidden;
            return order == prev || order == next ? Mode.Bare : Mode.Hidden;
        }

        /// <summary>The map (not a route) nearest this point: inside its bounds, or closest to them.</summary>
        private static StreamedArea CurrentMap(Vector3 p)
        {
            StreamedArea best = null;
            float bestEdge = float.MaxValue, bestCenter = float.MaxValue;
            for (int i = 0; i < Registered.Count; i++)
            {
                StreamedArea a = Registered[i];
                if (a._route) continue;
                float edge = a._bounds.SqrDistance(p);
                float center = (a._bounds.center - p).sqrMagnitude;
                if (edge < bestEdge || (edge == bestEdge && center < bestCenter))
                {
                    best = a;
                    bestEdge = edge;
                    bestCenter = center;
                }
            }
            return best;
        }

        /// <summary>The order of the nearest map along the chain in this direction (+1 / -1).</summary>
        private static int NeighbourMap(int from, int direction)
        {
            int best = direction > 0 ? int.MaxValue : int.MinValue;
            for (int i = 0; i < Registered.Count; i++)
            {
                StreamedArea a = Registered[i];
                if (a._route) continue;
                if (direction > 0 ? a._order > from && a._order < best : a._order < from && a._order > best) best = a._order;
            }
            return best;
        }

        private static void ApplyAll(Mode mode)
        {
            for (int i = 0; i < Registered.Count; i++) Registered[i].Apply(mode);
        }

        private void Apply(Mode mode)
        {
            if (mode == _mode || !_started) return;
            bool wasFull = _mode == Mode.Full;
            _mode = mode;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].forceRenderingOff = !(mode == Mode.Full || (mode == Mode.Bare && _isBare[i]));
            bool full = mode == Mode.Full;
            for (int i = 0; i < _lights.Length; i++)
            {
                if (_lights[i] == null) continue;
                if (wasFull) _lightsOn[i] = _lights[i].enabled; // remember what gameplay had on
                _lights[i].enabled = full && _lightsOn[i];
            }
            for (int i = 0; i < _scatters.Length; i++)
                if (_scatters[i] != null) _scatters[i].Hidden = !full;
        }
    }
}
