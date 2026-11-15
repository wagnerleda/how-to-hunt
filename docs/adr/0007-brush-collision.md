# ADR 0007 — Movement collision: Source-style convex brushes, not Unity physics

Status: accepted (2026-11-15)

- Level collision for movement (and later hits) is a set of convex **brushes** (planes) built from `SimBrush` components at scene load. Box traces use the Quake III algorithm (Minkowski-expanded planes, axial bevels). Exact, deterministic, allocation-free, and testable in EditMode without Unity.
- Authoring: any cube becomes a box brush (rotate freely); ramps and surf prisms are convex meshes. Surfaces with normal.y < `sv_standable_normal` (0.7) are surfable automatically; `SurfaceFlags` tag surf/hazard surfaces.
- Movement (`PlayerMove`) ports Source's ground/air acceleration, friction, jump-before-friction (bhop), slide-move velocity clipping (surf), step-up and stay-on-ground. Written from scratch from the publicly documented algorithms; no third-party code copied.
- Terrain/heightfields are not supported yet; biomes use brushes plus decorative meshes until a heightfield trace is needed.
