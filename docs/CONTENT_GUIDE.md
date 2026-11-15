# Content guide

Status: M1. Becomes the full guide in M3/M4, where adding prey, tools, biomes and gates needs no code.

## Content assets
- Content types derive from `ContentDefinition` (ScriptableObject): permanent `key` (e.g. `prey.butterfly`), display name, description. The key hashes to a stable `ContentId` used by saves and the network.
- Validate: menu **How to Hunt > Validate Content**; CI runs `ContentValidationTests`.

## Level geometry
- Add `SimBrush` to a cube (Box) or a convex mesh (ConvexMesh). Only `SimBrush` objects collide with players; plain meshes are decoration.
- Walkable up to ~45.6° (`sv_standable_normal 0.7`); steeper faces are surfable automatically.
- Steps up to 0.457 m (`sv_stepsize`) are climbed without jumping. Crouched players fit 1.37 m gaps; standing needs 1.83 m.
- Surf ramps: use the SurfPrism mesh, sides 50-60°, and tilt the ramp ~5° down along the course so gravity becomes forward speed. Put the start platform *beside* the ramp so players step off onto the slope, not into the ramp's end face.
- Add `SpawnPoint` objects for players.

## Zones (no code)
Add a `Zone` component to an empty GameObject; its scale is the box size.
- **Checkpoint**: entering saves the respawn point (center of the zone's floor, the zone's facing).
- **Hazard**: touching sends the player to their last checkpoint (pits, water).
- **Teleport**: touching moves the player to `Target` (position + facing) and makes it their checkpoint.
Keep hazard boxes below the tops of platforms so standing players never overlap them.

## After changing the lab map
Run How to Hunt > Rebuild Graybox Scene (exports collision), then How to Hunt > Movement > Regenerate Scripted Demos if the change is intended, and run the tests.
