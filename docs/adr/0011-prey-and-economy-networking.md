# ADR 0011 — Prey simulation and the economy ledger

Status: accepted (M3)

## Prey
- **Ids without spawn messages.** Prey slots come from the scene's `PreySpawner`s, ordered by name, so every machine derives the same ids. Only life changes travel (`PreyLifeBroadcast`: alive, seed, position).
- **Flyers cost no bandwidth.** Butterflies follow `ButterflyFlight.PoseAt(seed, home, tick)`: every machine computes the same pose for any tick, so lag compensation and drawing need nothing but the seed.
- **Ground prey are host-simulated** (`PreyBrain`: idle, wander, flee, stunned) and sent as 16 Hz snapshots (position, packed yaw, AI state, health %) on the unreliable channel. Clients draw them at the view tick from a 12-entry interpolation buffer, the same tick their swings and shots are checked at. The owner accepted extra bandwidth for prey (M2 checkpoint); measured in `bots-test` (8 players, IL2CPP), a client receives ~24.8 KB/s vs ~20 KB/s before prey (+~5 KB/s for 9 rabbits; flyers cost nothing), within the ADR 0009 budget.
- **Hits are host-authoritative** and lag-compensated like the M2 dummies: the host rewinds prey to the user's view tick (ground prey from a pose history, flyers from the seed), resolves `Takedown.Resolve`, and broadcasts effects to everyone.
- Updates go only to authenticated clients; a client that finishes loading gets every prey's life state.

## Economy
- Each player's profile (coins, trophies, research, upgrades, unsold bag) is **personal and persistent on their own machine** (`ProfileStore`, Steam Cloud).
- During a party the **host keeps the live copy (ledger)**: the client sends its profile once when its player spawns; the host credits catches, sells the bag (near the Trader) and sells upgrades (near the Outfitter), and returns the whole ledger after every change. The client saves what the host sends. Clients never change their own coins.
- The host trusts the profile a client joins with. That is acceptable for friends-only parties (ADR 0005) and keeps saves local; a hostile client could edit its own save, which only affects that player.
- Messages are small (whole ledger: tens of bytes plus 2 bytes per bagged catch) and event-driven, so they don't count against the tick budget.

## Alternatives rejected
- Host-side profiles for guests: progress would depend on who hosts.
- Client-authoritative coins: trivial to cheat even by accident, and the host could not validate purchases.

## Inventory and world items (Phase 2, 2026-11-16)
- The bag became an **inventory** (`Sim/Hunting/Inventory`): a row of slots, owned tools first, then prey stacks (butterflies stack to 10, rabbits take a slot each). It lives in the host ledger and travels in the same state message, slot by slot, so slot numbers match on both sides. Saves are v2 (stacks); v1 bags migrate.
- **Catch vs kill:** a catch goes straight into the inventory; a kill drops a **carcass** where the prey died, flung by the hit. Anyone in the party can pick it up (E).
- **World items** (carcasses, drops, throws) use `ItemPhysics` (gravity, bounces, friction, rest) on the shared brush world. Like the butterflies, the motion is deterministic, so the host sends a spawn message (position, velocity, tick) and clients simulate it themselves, catching up from the spawn tick; after that only "came to rest here" (a correction) and removals travel. Pick-ups, throws and drops are requests the host validates (distance, free slot, slot contents).
