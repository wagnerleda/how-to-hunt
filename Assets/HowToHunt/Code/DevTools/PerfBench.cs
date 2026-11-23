using System;
using HowToHunt.Core;
using HowToHunt.Foundation;
using Unity.Profiling;
using UnityEngine;

namespace HowToHunt.DevTools
{
    /// <summary>
    /// <c>perf_bench [passes] [quit]</c>: takes the main camera on a fixed tour of the map (camp, open plains, the surf
    /// ledge over the ravine, the pine forest) and logs, per stop, the frame time (average, 99th percentile, worst),
    /// main-thread time, hitches (frames over 50 ms), batches, SetPass calls, triangles, shadow casters and managed
    /// allocations per frame, then compares them with the budgets in ARCHITECTURE.md. Pass 1 is cold (first time
    /// things are drawn: shader and upload stalls show up as hitches), later passes are warm (steady state). Works in
    /// the editor and in builds (render counters need a development build): <c>HowToHunt.exe +host local +delay 8
    /// perf_bench 2 quit</c>. The player stays where they are; only the camera moves. Nothing allocates while sampling.
    /// </summary>
    [DefaultExecutionOrder(32000)] // after PlayerCamera: our camera pose wins
    public sealed class PerfBench : MonoBehaviour
    {
        public static readonly FloatCvar StopSeconds = new FloatCvar("perf_bench_seconds", 4f, "Seconds the benchmark spends at each stop.", CvarFlags.None, 1f, 30f);

        private const float HitchMs = 50f, SettleSeconds = 0.5f, BudgetFrameMs = 16.6f, BudgetMainMs = 10f;
        private const int BudgetBatches = 800, BudgetSetPass = 250, BudgetTriangles = 1_500_000, MaxFrames = 8192;

        /// <summary>A stop on the tour: the eye (x, height above the ground, z) moves from A to B while looking at a point.</summary>
        private readonly struct Stop
        {
            public readonly string Name;
            public readonly Vector3 EyeA, EyeB, Look;
            public readonly bool Fly;

            public Stop(string name, Vector3 eyeA, Vector3 eyeB, Vector3 look, bool fly)
            {
                Name = name;
                EyeA = eyeA;
                EyeB = eyeB;
                Look = look;
                Fly = fly;
            }
        }

        // Heights are above the ground under the eye (terrain), so the tour survives terrain changes.
        private static readonly Stop[] Tour =
        {
            new Stop("camp", new Vector3(-30f, 1.7f, 128f), new Vector3(-30f, 1.7f, 128f), new Vector3(-30f, 0f, 300f), false),
            new Stop("plains-east", new Vector3(-10f, 2f, 215f), new Vector3(-10f, 2f, 215f), new Vector3(150f, 0f, 215f), false),
            new Stop("plains-north", new Vector3(10f, 4f, 300f), new Vector3(10f, 4f, 300f), new Vector3(-30f, 0f, 135f), false),
            new Stop("fly-plains", new Vector3(-80f, 6f, 140f), new Vector3(120f, 6f, 230f), new Vector3(300f, -20f, 230f), true),
            new Stop("surf-ledge", new Vector3(140f, 2f, 227f), new Vector3(140f, 2f, 227f), new Vector3(430f, -50f, 227f), false),
            new Stop("fly-ravine", new Vector3(150f, 4f, 227f), new Vector3(370f, 6f, 215f), new Vector3(460f, -50f, 240f), true),
            new Stop("forest-camp", new Vector3(392f, 1.7f, 203f), new Vector3(392f, 1.7f, 203f), new Vector3(470f, -50f, 260f), false),
            new Stop("forest-deep", new Vector3(440f, 1.7f, 250f), new Vector3(440f, 1.7f, 250f), new Vector3(380f, -50f, 190f), false),
        };

        private static PerfBench _running;

        private readonly float[] _frameMs = new float[MaxFrames];
        private readonly float[] _sorted = new float[MaxFrames];
        private int _frames, _stop, _pass, _passes, _only;
        private float _stopTime;
        private bool _quit;
        private long _allocSum, _gcStart;
        private double _mainSum;
        private long _batchesMax, _setPassMax, _trisMax, _shadowMax;
        private int _hitchesTotal;
        private float _worstTotal;
        private SimTerrain[] _terrains;
        private ProfilerRecorder _main, _batches, _setPass, _tris, _shadows;

        public static void Register(ConsoleSystem console)
        {
            console.Register(StopSeconds);
            console.Register("perf_bench", "perf_bench [passes] [stop] [quit]: fly the camera around the map (or stay at one stop: camp, plains-east, surf-ledge...) and log frame times and render counters per stop.", args =>
            {
                if (_running != null) { console.Warn("perf_bench: already running."); return; }
                int passes = 2, only = -1;
                bool quit = false;
                for (int i = 0; i < args.Count; i++)
                {
                    if (args[i] == "quit") quit = true;
                    else if (int.TryParse(args[i], out int n)) passes = Mathf.Clamp(n, 1, 10);
                    else
                    {
                        only = Array.FindIndex(Tour, t => t.Name == args[i]);
                        if (only < 0) { console.Warn($"perf_bench: no stop '{args[i]}'."); return; }
                    }
                }
                Game.Root.gameObject.AddComponent<PerfBench>().Begin(passes, only, quit);
            });
        }

        private void Begin(int passes, int only, bool quit)
        {
            _running = this;
            _only = only;
            _passes = passes;
            _quit = quit;
            _terrains = FindObjectsByType<SimTerrain>(FindObjectsInactive.Exclude);
            _main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
            _batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1);
            _setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", 1);
            _tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", 1);
            _shadows = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shadow Casters Count", 1);
            Report($"[perf] bench start: {(only >= 0 ? 1 : Tour.Length)} stops x {passes} passes, {StopSeconds.Value:0.#} s each, {Screen.width}x{Screen.height}, r_drawdistance {RenderDistance.DrawDistance.Value:0}, quality '{QualitySettings.names[QualitySettings.GetQualityLevel()]}', {SystemInfo.graphicsDeviceName}");
            StartStop(FirstStop);
        }

        private int FirstStop => _only >= 0 ? _only : 0;

        private void StartStop(int index)
        {
            _stop = index;
            _stopTime = 0f;
            _frames = 0;
            _mainSum = 0;
            _batchesMax = _setPassMax = _trisMax = _shadowMax = 0;
        }

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Stop s = Tour[_stop];
            float duration = StopSeconds.Value + (s.Fly ? 0f : SettleSeconds);
            float t = s.Fly ? Mathf.Clamp01(_stopTime / duration) : 0f;
            Vector3 eye = Vector3.Lerp(s.EyeA, s.EyeB, t);
            eye.y += Ground(eye.x, eye.z);
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(s.Look - eye, Vector3.up));

            // Sample the frame that just ended (it was drawn from the previous pose).
            bool settled = s.Fly || _stopTime >= SettleSeconds;
            if (settled && _frames == 0)
            {
                _allocSum = 0;
                _gcStart = AllocationMeter.Collections;
            }
            if (settled && _frames < MaxFrames)
            {
                _frameMs[_frames++] = Time.unscaledDeltaTime * 1000f;
                _allocSum += AllocationMeter.LastFrameBytes;
                if (_main.Valid) _mainSum += _main.LastValue * 1e-6;
                _batchesMax = Math.Max(_batchesMax, _batches.Valid ? _batches.LastValue : 0);
                _setPassMax = Math.Max(_setPassMax, _setPass.Valid ? _setPass.LastValue : 0);
                _trisMax = Math.Max(_trisMax, _tris.Valid ? _tris.LastValue : 0);
                _shadowMax = Math.Max(_shadowMax, _shadows.Valid ? _shadows.LastValue : 0);
            }

            _stopTime += Time.unscaledDeltaTime;
            if (_stopTime < duration) return;
            Finish(s);
            if (_only < 0 && _stop + 1 < Tour.Length) { StartStop(_stop + 1); return; }
            _pass++;
            if (_pass < _passes) { StartStop(FirstStop); return; }
            End();
        }

        private void Finish(Stop s)
        {
            int n = Mathf.Max(1, _frames);
            float sum = 0f, worst = 0f;
            int hitches = 0;
            for (int i = 0; i < _frames; i++)
            {
                float ms = _frameMs[i];
                sum += ms;
                if (ms > worst) worst = ms;
                if (ms > HitchMs) hitches++;
                _sorted[i] = ms;
            }
            Array.Sort(_sorted, 0, _frames);
            float p99 = _frames > 0 ? _sorted[Mathf.Min(_frames - 1, (int)(_frames * 0.99f))] : 0f;
            float avg = sum / n;
            double main = _mainSum / n;
            long alloc = _allocSum / n;
            long gcs = AllocationMeter.Collections - _gcStart;
            _hitchesTotal += hitches;
            if (worst > _worstTotal) _worstTotal = worst;
            string over = (avg > BudgetFrameMs ? " FRAME" : "") + (main > BudgetMainMs ? " MAIN" : "") + (_batchesMax > BudgetBatches ? " DRAWS" : "")
                          + (_setPassMax > BudgetSetPass ? " SETPASS" : "") + (_trisMax > BudgetTriangles ? " TRIS" : "") + (alloc > 0 ? " ALLOC" : "");
            Report(FormattableString.Invariant(
                $"[perf] pass {_pass + 1} {s.Name,-12} {avg,5:0.0} ms avg ({1000f / Mathf.Max(0.01f, avg),4:0} fps)  p99 {p99,5:0.0}  worst {worst,6:0.0}  hitches {hitches,2}  main {main,5:0.0} ms  draws {_batchesMax,4}  setpass {_setPassMax,3}  tris {_trisMax / 1000f,6:0}k  shadow casters {_shadowMax,4}  alloc {alloc,6} B/frame{(AllocationMeter.Exact ? "" : " (heap growth)")}  gc {gcs}{(over.Length > 0 ? "  OVER BUDGET:" + over : "")}"));
        }

        private void End()
        {
            Report(FormattableString.Invariant($"[perf] bench done: worst frame {_worstTotal:0.0} ms, {_hitchesTotal} hitches over {HitchMs:0} ms in total"));
            _main.Dispose();
            _batches.Dispose();
            _setPass.Dispose();
            _tris.Dispose();
            _shadows.Dispose();
            _running = null;
            Destroy(this);
            if (_quit) Application.Quit();
        }

        private float Ground(float x, float z)
        {
            for (int i = 0; i < _terrains.Length; i++)
            {
                float h = _terrains[i] != null ? _terrains[i].HeightAt(x, z) : float.NaN;
                if (!float.IsNaN(h)) return h;
            }
            return 0f;
        }

        private static void Report(string line)
        {
            Debug.Log(line);
            Game.Console.Print(line);
        }

        private void OnDestroy()
        {
            if (_running != this) return;
            _running = null;
            _main.Dispose();
            _batches.Dispose();
            _setPass.Dispose();
            _tris.Dispose();
            _shadows.Dispose();
        }
    }
}
