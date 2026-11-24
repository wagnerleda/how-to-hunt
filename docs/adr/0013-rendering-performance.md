# ADR 0013 — Rendering performance: one pass per object, thinned scatter, no idle IMGUI

Status: accepted (2026-11-23)

## Context
The owner reported freezes and frame drops. `perf_bench` (DevTools: a fixed camera tour of camp, plains, surf ledge,
ravine and forest, two passes, logged per stop) measured a development build at 1080p (RX 7800 XT):

| | Before | After |
|---|---|---|
| Worst frame over the tour | 36.9 ms (spikes at the surf ledge and ravine, every pass) | under 10 ms |
| Surf ledge p99 | 22-28 ms | 2 ms |
| Plains triangles | 2.2-3.0 M | 1.1-1.5 M |
| Plains SetPass calls | 570-760 | 340-410 |
| Managed allocations, steady state | ~1.6 KB/frame (2-4 collections per 4 s) | ~0 (transient TMP growth only) |

Causes found:
- The URP template's SSAO ran at full resolution from a **depth-normals prepass**: every opaque object, including
  ~1 M triangles of instanced grass, was drawn twice each frame. The opaque and depth textures were also copied every
  frame with nothing reading them.
- Scatter chunks were tiny (ring trees: ~3 instances per draw call, forest pines ~8), each chunk x material being one
  instanced call, and trees drew out to 336 m, past `r_drawdistance` and the rest of the world.
- Four IMGUI menus (console, session menu, mask editor, dialogue box) kept an `OnGUI` method enabled while closed.
  Unity calls it twice a frame per component and each call allocates (~200 B).
- The zero-allocation budget was never really checked: `GC.GetAllocatedBytesForCurrentThread()` always returns 0 on
  Unity's Mono, so the overlay showed 0 B/frame and the allocation tests could not fail.

## Decision
- **SSAO** stays (it is part of the look) but runs **after opaques from a depth copy, at half resolution**
  (`Source = Depth`, normals reconstructed): no prepass. `r_ssao 0` switches it off. Opaque and depth textures are off
  in `PC_RPAsset`; turn them back on only for a shader that samples them.
- **Scatter**: bigger chunks (trees 100 m, pines 80 m, props 48 m, grass and flowers 36 m) so each call carries enough
  instances; ground clutter thins out with distance (`ScatterRenderer.Layer.FarDensity`: grass keeps 15 % at the edge,
  flowers 25 %, props 30 %; thinning starts at `r_scatter_fade` of the reach); nothing is drawn past `r_drawdistance`.
  Values live in `GrayboxSceneBuilder` (rebuild the scene after changing them).
- **IMGUI** draws through `ImGuiHost`, enabled only while its menu has something on screen. No `OnGUI` elsewhere.
- **Allocations** are measured with Unity's `GC Allocated In Frame` counter (`AllocationMeter`; heap growth in release
  builds) and tested with `AllocationAssert.None` (Unity's `Is.Not.AllocatingGCMemory`).
- Fish-Net sets `Application.targetFrameRate` to 9999 when a server or client starts or stops; `NetSession` re-applies
  `fps_max` / `mat_vsync` right after.

## Consequences
- The plains still exceed the 250 SetPass budget (~400): every scatter chunk x material is its own instanced call.
  Going further needs GPU-driven instancing (BatchRendererGroup or indirect draws with a custom shader); parked in
  docs/IDEAS.md.
- Grass far away is visibly sparser before the fog takes it; `r_scatter_fade 1` restores full density for screenshots.
- Re-measure after map or render changes: `HowToHunt.exe -screen-fullscreen 0 -nocfg +host local +delay 10 perf_bench 2 quit`
  (results in the log as `[perf]` lines, budgets flagged as OVER BUDGET).

## Follow-up (2026-11-24, owner: "still freezes in the Plains")
Development build, 1080p, RX 7800 XT, `perf_bench 2` (vsync on, -nocfg), after the third map, rock ridges and camp
characters:
- Averages are fine everywhere: ~4.2 ms (240 fps, the screen's rate); standing still in the Plains, worst frame 4.4 ms.
- Stalls of 30-40 ms come back during camera moves (fly-plains once per pass; ravine and forest several times), and
  once on the first pass at camp. `perf_bench` now logs a **hitch probe** on every frame over 30 ms: the time each of a
  set of engine markers took. Every stall shows the render thread waiting on the main thread
  (`Gfx.WaitForGfxCommandsFromMainThread` 21-31 ms) while none of the probed main-thread markers (GC, uploads,
  instantiation, scripts, canvases, text, physics, culling, shadows) took over 1 ms: the cause is somewhere not probed.
  Next step: a Profiler capture of a development build during `perf_bench 1 fly-ravine` (or `fly-plains`) to see the
  main thread on the stalled frame.
- Managed allocations are not zero any more: ~210 B/frame steady, up to ~1.2 KB/frame while hunting areas stream in
  (a GC every 20-60 s). Find them with the Profiler's GC Alloc column (Deep Profile off), most likely HUD text or the
  new UI.
- Draw calls are over budget in the Plains (1,000-2,600): scatter chunks plus the boundary rock ridges; SetPass
  210-450. The parked fix is GPU-driven instancing (IDEAS).

