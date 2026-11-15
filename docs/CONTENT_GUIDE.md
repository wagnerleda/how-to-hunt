# Content guide

Status: skeleton (M0). Becomes the full guide in M3/M4, where adding prey, tools, biomes and gates needs no code.

## Today
- Content types derive from `ContentDefinition` (ScriptableObject): permanent `key` (e.g. `prey.butterfly`), display name, description. The key hashes to a stable `ContentId` used by saves and the network.
- Validate: menu **How to Hunt > Validate Content**; CI runs `ContentValidationTests`.
- Level geometry: add `SimBrush` to a cube (Box) or a convex mesh (ConvexMesh). Steep slopes (> ~45.6°) are surf ramps automatically. Add `SpawnPoint` objects for players.

## Coming
Prey (take-down methods, health, resistances, behavior profile, values for kill vs capture), Tool (archetype, take-down mode, stats), Upgrade, Currency, Biome, Gate, TraversalCourse.
