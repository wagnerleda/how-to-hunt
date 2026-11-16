# Plan after the M3 playtest (owner feedback, 2026-11-16)

The owner's notes after playing M3 are grouped into phases. Each phase ends with a playable build and a short
checkpoint; nothing in a later phase starts before the owner has tried the earlier one.

## Phase 1: feel pass (now)
Small, independent fixes to what is already in the game.

| # | Change | Where |
|---|---|---|
| 1 | Knife: a bright, metallic "shing" swish instead of the sandy Kenney slice | `ProceduralSounds.KnifeSwish/KnifeHit`, `ViewmodelController`, `HuntFeedback` |
| 2 | Hands: one simple style, a round "ball" glove (cartoon, matches the low-poly look) on a sleeve | `Art/Source/{Net,Knife,Revolver}.blend` |
| 3 | Net: sweeps across the upper screen in a curve (owner's sketch), right to left then left to right when spamming | `ViewmodelController.Sweep` |
| 4 | Arms react to movement: dip on landing (scaled by impact), lift on jump, lag behind strafing, sway in the air, all spring-driven, not straight-line moves | `ViewmodelController` (springs) |
| 5 | Plains about 25 % smaller (play area 200 m instead of 260 m) | `GrayboxSceneBuilder.Plains.cs` |
| 6 | Cover for PvP: boulder clusters and fallen trees scattered through the meadow (collide), not the core of the design | same |

## Phase 2: inventory and holding prey (How to Fish-style)
A bigger change to the hunting loop; designed here, built after Phase 1 is approved.

- **Inventory bar** at the bottom of the screen replaces the hidden "bag": a row of slots (start: 6) holding tools,
  weapons and prey. Number keys and the mouse wheel select a slot; the selected item is what you hold.
- **Prey are items.** A rabbit takes one slot; small prey (butterflies, later insects) stack (up to 10 per slot).
- **Catch vs kill (owner rule, 2026-11-16):** a *caught* prey goes straight into your inventory (the net's advantage
  on top of its 1.5x value). A *killed* prey drops where it died as a carcass (flung a little by the hit, falling out
  of the air for flyers); you have to go and pick it up (E). Carcasses lie there for 2 minutes, and anyone in the
  party can take them. Full inventory: you can't pick more up; a catch with no free slot falls at your feet.
- **Held prey are physical.** Holding a rabbit shows it in your hand; Fire throws it (an arc with real collision
  against the level), Q drops it. Thrown/dropped prey lie on the ground as pick-up-able items (E) for everyone in
  the party, so friends can hand catches to each other, and steal them in PvP.
- **Selling** at the trader sells every prey in your inventory; tools stay.
- **Shop upgrades** add slots (6, then 8, then 10) next to the tool upgrades.
- **Controls:** 1-9 select a slot (tools and prey alike), Fire throws the held prey, Q drops it, E picks up items
  (and still uses camp stations).
- **Tech:** the inventory lives in the host ledger like coins today. World items (carcasses, drops, throws) are
  simulated with the sim's brush collision (no PhysX); the simulation is deterministic, so only spawn, pick-up and
  "came to rest here" messages are sent. Icons are rendered from the 3D models at startup. Save migration turns the
  old bag into inventory items.

### Phase 2 build order
1. Inventory model (slots, stacking, capacity) in the ledger, net messages, save migration; tests.
2. Inventory bar UI with model icons; slot selection drives the held tool or prey.
3. World items: sim body (gravity, bounce, rest), spawn/remove messages, visuals; carcasses from kills.
4. Pick up (E), throw (Fire), drop (Q); held prey in first person; selling from the inventory; slot upgrades.
5. Tests (stacking, migration, item physics, allocation), bots/lag-comp/installer, checkpoint.

## Phase 3: physical catching (later, after Phase 2 is played)
More physics in the interactions themselves, in the spirit of How to Fish: the net as a swept volume that scoops
whatever it passes through (several butterflies in one swing), prey knocked back by hits, thrown prey stunning other
prey or players. Designed once Phase 2's items exist.

## Still planned for M4 (unchanged)
Player health, downed/revive, PvP damage, menus moved to uGUI, ambience sounds.
