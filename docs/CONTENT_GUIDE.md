# Content guide

Status: M3. Adding prey, tools and upgrades needs no code; new behaviours (a new movement type, a new tool archetype) do.

## Content assets
- Content types derive from `ContentDefinition` (ScriptableObject): permanent `key` (e.g. `prey.butterfly`), display name, description. The key hashes to a stable `ContentId` used by saves and the network. **Never rename a key after release.**
- Runtime content lives in `Assets/HowToHunt/Resources/Content` (loaded by `ContentRegistry`). The Plains set is written by `GeneratedContent.cs` (menu **How to Hunt > Rebuild Content**): change numbers there, not in the assets.
- Validate: menu **How to Hunt > Validate Content**; CI runs `ContentValidationTests` and `EconomyPacingTests`.

### Prey (`PreyDefinition`, key `prey.*`)
- Take-down: `Allowed` (catch, kill or either), `MaxHealth`, `CaptureDifficulty` (net strength needed), `MinKillTier`, `OverkillRatio` (one hit above MaxHealth x this ruins the carcass).
- Hitboxes: capsules in model space (model faces +Z, pivot between the feet for walkers); mark the head `WeakPoint`.
- Economy: `BaseValue`, `TrophiesOnKill`. Behaviour: `Flyer` (seeded flight, no bandwidth) or `Ground` (host AI: walk/flee speeds, stamina, stun).
- Senses: `FleeRadius` (flees from anyone this close, seen or not), `SightRange` (flees from a player it can see this far, line of sight; halved against crouching players, `sv_crouch_sight`), `HearingRange` (flees from gunshots within it; 0 for prey unafraid of guns, like predators), `Wariness` (rare prey: multiplies FleeRadius and SightRange). `Voice` picks the hit sound.
- Presentation: `Visual` prefab or model, `VisualScale`, `Noun` ("a rabbit", used in feedback lines), `BloodColor`, `GibColor`, `GibCount`.
- Inventory: `StackSize` (how many fit in one slot: small prey stack, big prey take a slot each).

### Tools (`ToolDefinition`, key `tool.*`)
`Archetype` (Net, Blade, Firearm), `Mode` (Capture or Kill), `Tier`, `Damage`, `CaptureStrength`, `HeadshotMultiplier`, `Range`, `ArcDegrees` (melee cone; 0 = ray), `MaxTargets` (prey one swing reaches: nets scoop several, each checked against net strength), `Knockback` (m/s shove to prey and items hit), `RecoilUp` / `RecoilSide` / `RecoilRecover` (firearm aim kick in degrees, and the fraction that settles back by itself), `Cooldown`, hotbar `Slot`, `StartOwned`, `Viewmodel` (model held in first person, grip at the origin, pointing +Z).

### Upgrades (`UpgradeDefinition`, key `upgrade.*`)
Target `Tool`, `Effect` (CaptureStrength +, DamageMultiplier x, Range +, UnlockTool, InventorySlots + (no tool)), `Value`, `CostCoins`, optional `Requires`, `SortOrder` (shop order).

## Placing content in a level
- `PreySpawner`: which prey, how many, radius, respawn seconds. Spawners are ordered by object name, so ids match on every machine; give them unique names.
- `CampStation`: Sell (Trader) or Shop (Outfitter) with a radius; players press Interact nearby. A Trader's `ExpectedSale` is a typical full-bag sale in that area: it scales the cash burst (none for sales well below it). Prey thrown into a Trader sell.
- Models: follow docs/ASSETS.md (Blender sources in `Art/Source`, exports in `Assets/HowToHunt/Art/Models/<Name>/`).

## Level geometry
- Add `SimBrush` to a cube (Box) or a convex mesh (ConvexMesh). Only `SimBrush` objects collide with players; plain meshes are decoration. Props that should block (trees, big rocks) get an invisible box brush (see `GrayboxSceneBuilder.Plains.cs`).
- Walkable up to ~45.6° (`sv_standable_normal 0.7`); steeper faces are surfable automatically.
- Steps up to 0.457 m (`sv_stepsize`) are climbed without jumping. Crouched players fit 1.37 m gaps; standing needs 1.83 m.
- Surf ramps: use the SurfPrism mesh, sides 50-60°, and tilt the ramp ~5° down along the course so gravity becomes forward speed. Put the start platform *beside* the ramp so players step off onto the slope, not into the ramp's end face.
- Add `SpawnPoint` objects for players (the Plains camp has eight).

## Zones (no code)
Add a `Zone` component to an empty GameObject; its scale is the box size.
- **Checkpoint**: entering saves the respawn point (center of the zone's floor, the zone's facing).
- **Hazard**: touching sends the player to their last checkpoint (pits, water).
- **Teleport**: touching moves the player to `Target` (position + facing) and makes it their checkpoint.
Keep hazard boxes below the tops of platforms so standing players never overlap them.

## After changing the map
Run How to Hunt > Rebuild Graybox Scene (exports collision), then How to Hunt > Movement > Regenerate Scripted Demos if the change is intended, and run the tests.
