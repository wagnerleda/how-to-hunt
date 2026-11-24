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
- `WanderRadius`: how far it wanders from its spawn point when calm (bosses stay in their den).
- Aggression (ADR 0014): `Aggression` (None, WhenHurt: charges whoever hurts it for `ProvokeSeconds`; Always: charges hunters within `AggroRange`), `LeashRadius` (gives up and walks home past it), `AttackRange`, `AttackWindup` (the dodgeable tell, at least 0.25 s), `AttackCooldown`, `AttackDamage`, `AttackPush`, `ChargeSpeed`, `RegenPerSecond` (heals once left alone). Ground prey only.
- Boss: `IsBoss` (must be Always aggressive and kill-only), `BossTitle` (the boss bar), `BossRewardCoins` / `BossRewardTrophies` (for every hunter who hurt it). Place it with a `PreySpawner` of count 1 in its den, `summoned: true` (it only comes out for its bait); `Voice = Roar` suits big animals.
- Boss moves (0 = never): `SlamEvery` / `SlamRadius` / `SlamWindup` / `SlamDamage` / `SlamPush` / `SlamDodgeHeight` (a ground slam all around), `PounceRange` / `PounceSpeed` / `PounceWindup` / `PounceCooldown` (a leap at hunters kiting it), `EnrageBelow` / `EnrageTempo` / `EnrageSpeed` (quicker and faster when low). ADR 0014.
- Shop columns: the Outfitter groups what it sells by item (each tool's upgrades; `InventorySlots` upgrades in "Your bag"; baits at a bait stall) and orders each column by `SortOrder`; chain them with `Requires`. Keep `Description` to one short sentence: the card shows the effect in bold, then it.
- Characters about camp: `NpcDefinition.Activity` (`Campfire`, `ChopWood`, `Whittle`; `Stall` for counter keepers) with `HasStation` off; place them with `NpcAbout(parent, key, feet, yaw)` in the scene builder (put a log or stump under sitting ones; the axe or stick is added for you). They can give quests like anyone.
- Font: everything uses The Wildeast by default (TMP and IMGUI); don't set another font. `UiFonts.Use(text)` on headings drops faux bold. Text the brush can't draw (accents, symbols) falls back to the plain face by itself.
- A new biome: a `GrayboxSceneBuilder.<Biome>.cs` (terrain height and colour functions, `RockWall` rim, camp with `Station`/`NpcAbout`, spawners, scatter, backdrop) and a `GeneratedContent.<Biome>.cs` (prey, NPCs, quests); gate the way in with a `WorldGate` on a quest flag and give the lab a test pad. A surf route between maps: ramps as `MeshBrush(SurfPrismMesh)`, a hazard under each ramp and gap (top just under the ramp's base) back to a checkpoint on the start ledge, each ramp starting below the lowest line you can ride on the last (~17 m for 12 m tall prisms), canyon visuals in the backdrop layer; prove it with a pilot demo (`MovementLabTools.DesertPilot`).
- Guns: an `UpgradeDefinition` with `Effect = UnlockTool`, `SoldByGunsmith`; `CampGunsmith` also puts it on the camp gunsmiths' counters (the Plains: only the Old Revolver), otherwise only the full armory sells it (`Station(..., CampStationKind.Workshop, ..., fullArmory: true)`, the movement lab).
- Bait: an `UpgradeDefinition` with `Effect = SummonBoss` and `Summons` (the boss), no tool; it is sold only at a bait stall (`Station(..., CampStationKind.Shop, ..., baitStall: true)` in the scene builder) and used up when bought.

### Tools (`ToolDefinition`, key `tool.*`)
`Archetype` (Net, Blade, Firearm), `Mode` (Capture or Kill), `Tier`, `Damage`, `CaptureStrength`, `HeadshotMultiplier`, `Range`, `ArcDegrees` (melee cone; 0 = ray), `MaxTargets` (prey one swing reaches: nets scoop several, each checked against net strength), `Knockback` (m/s shove to prey and items hit), `RecoilUp` / `RecoilSide` / `RecoilRecover` (firearm kick in degrees: the gun jumps in the hand and the view follows only the part that does not recover), `Cooldown`, hotbar `Slot`, `StartOwned`, `Viewmodel` (model held in first person, grip at the origin, pointing +Z).

Firearms also set: `MagazineSize` and `ReloadSeconds` (ammo is infinite, magazines are not), `Automatic` (hold to fire), `BulletSpeed` and `BulletGravity` (bullets fly; tracers are real), `FalloffStart` / `FalloffEnd` / `MinDamageFraction` (damage drops linearly with distance), `SpreadDegrees` / `MoveSpreadPerMps` / `MaxSpreadDegrees` / `AdsSpreadFactor` (the cone widens with your speed; 0 per m/s for a scout rifle; tighter down the sights), `AdsZoom`, `ScopeOverlay` (look through a scope instead of iron sights), `SightOffset` and `MuzzleOffset` (model-space points: the sight that meets your eye, and where bullets leave along the model's +Z), `Weight` (0-3: heavy guns slow you, `sv_load_slow` per class).

### Attachments (`AttachmentDefinition`, key `attachment.*`)
`Slot` (Muzzle, Barrel, Underbarrel, Magazine, Ammo, Optic, Top, Stock), `Tools` (empty = any gun with a mount for that slot), `CostCoins`, `SortOrder`; effects (1 = unchanged): `RecoilMultiplier`, `SpreadMultiplier`, `MoveSpreadMultiplier`, `AdsSpreadMultiplier`, `RangeMultiplier` (falloff distances), `DamageMultiplier`, `BulletSpeedMultiplier`, `NoiseMultiplier` (how far prey hear the shot), `ReloadMultiplier`, `MagazineAdd`, `WeightAdd`; optics: `AdsZoom`, `ScopeOverlay`, `SightLift` (eye point above the mount); `Visual` (model at the mount, pointing +Z). Guns list their mounts in `ToolDefinition.AttachmentAnchors`; a slot without a mount is locked on that gun. Weapon sounds: `FireSounds` (random per shot), `ReloadSound`, `DryFireSound`, `EquipSound`, `InspectSound`, linked by the content build from `Art/Weapons/<tool name>/` (see its README).

### Upgrades (`UpgradeDefinition`, key `upgrade.*`)
Target `Tool`, `Effect` (CaptureStrength +, DamageMultiplier x, Range +, UnlockTool, InventorySlots + (no tool)), `Value`, `CostCoins`, optional `Requires`, `SortOrder` (shop order).

## Placing content in a level
- `PreySpawner`: which prey, how many, radius, respawn seconds. Spawners are ordered by object name, so ids match on every machine; give them unique names.
- `CampStation`: Sell (Trader) or Shop (Outfitter) with a radius; players press Interact nearby. A Trader's `ExpectedSale` is a typical full-bag sale in that area: it scales the cash burst (none for sales well below it). Prey thrown into a Trader sell.
- Models: follow docs/ASSETS.md (Blender sources in `Art/Source`, exports in `Assets/HowToHunt/Art/Models/<Name>/`).

## Level geometry
- Add `SimBrush` to a cube (Box) or a convex mesh (ConvexMesh). Only `SimBrush` objects collide with players; plain meshes are decoration. Props that should block (trees, big rocks) get a box brush the size of the prop (see `GrayboxSceneBuilder.Plains.cs`). Boundaries are never invisible walls: use `RockWall(parent, name, a, b, ground, capTop, rng)` (a rocky ridge whose rock is the collision; only a cap high above it is collision-only).
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

## Writing dialogue (ADR 0015)
Camp characters are `NpcDefinition` assets (`npc.*`, written by `GeneratedContent.Quests.cs`): `Greetings` (one at
random when you walk up), `Idle`, `Barks` (short lines called out as you pass), `VoicePitch` (0.5-2: the pitch of
their voice blips) and an optional `VoiceClip` (one syllable of their own voice). Quest lines are in
`QuestDefinition` (`Offer`, `Waiting`, `Done`).
- A blank line in a line starts a new page (the player presses Interact to go on).
- Start a line (or page) with a style: `[shout]`, `[whisper]`, `[think]`, `[narrate]` (default: normal speech).
- Inside: `<shake>scared</shake>`, `<wave>hello</wave>`, and TextMeshPro tags (`<b>`, `<i>`, `<color=#c33>`).
- New character: add an `Npc(...)` and `Voice(...)` in `GeneratedContent.Quests.cs`, place it with `NpcAt` in the
  scene builder (behind a stall), rebuild content and scene. Quests name their giver.
- Look and timing (typing speed, pauses, bubble size, colours per style, barks, markers): the `DialogueTheme` asset
  in Resources/UI, in the Inspector. Menus: the `MenuTheme` asset next to it.
