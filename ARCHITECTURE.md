# Architecture

Source-engine spirit: a fixed 64 Hz tick, cheap bounded simulation, everything tunable from the console, host-authoritative netcode with prediction and lag compensation. Details and reasons are in [docs/adr](docs/adr).

## Assemblies

```
HowToHunt.Foundation   (no UnityEngine)  EventBus, ConsoleSystem + cvars, RoomCode, ContentId, GameIdentity
HowToHunt.Sim          (no UnityEngine)  Collision (convex brushes, box traces), Movement (PlayerMove), later: combat, capture, economy, AI
HowToHunt.Core         Unity             GameRoot bootstrap, IFeatureModule + ModuleRegistry, DevConsoleView, CoreCvars,
                                         ContentDefinition, SimBrush/SimWorld, IPlatformServices, OfflinePlatform
HowToHunt.Platform     Unity + Steamworks.NET   SteamPlatform, SteamLobbyService (room codes)
HowToHunt.Net          Unity + Fish-Net  NetSession (host / join by code / local connect), transports
HowToHunt.Player       feature           PlayerMotor (predicted), input, camera, spawns, movement cvars
HowToHunt.DevTools     feature           DebugOverlay, net_graph
HowToHunt.UI           feature           Session menu (M0; real lobby UI in M2)
HowToHunt.Bosses       feature           Boss bar and announcements (bosses are aggressive prey, ADR 0014)
HowToHunt.Editor       editor            generated prefabs, graybox builder, build script, content validator, model import rules
```

Rules:
- Core knows no feature. Features declare `[assembly: FeatureModule(typeof(X))]` and register cvars, commands, content, panels in `Register`, start in `Initialize`.
- Features talk through the typed `EventBus` (struct events, zero allocation).
- Deterministic logic lives in `Sim` and is tested in EditMode without a scene.
- Gameplay never calls Steam directly (only `IPlatformServices`). Network adapters (e.g. `PlayerMotor`) derive from Fish-Net types; the logic they run is in `Sim`.

## Tick path (movement today, all gameplay later)

```
Input System ──frame──> PlayerInputSampler (mouse look every frame, keys latched)
      │ every tick (64 Hz)
      ▼
MoveCmd ──Fish-Net [Replicate]──> host runs PlayerMove.Step (same code) ──[Reconcile] state──> owner
  └── owner predicts PlayerMove.Step immediately; on reconcile it rewinds and replays pending cmds
Remote players: Fish-Net NetworkTickSmoother interpolates the Visual child; the camera follows the smoothed Visual.
```

## Hit registration (M2)

Host keeps a 1 s ring buffer per tick of hitbox poses (capsules/spheres, in `Sim`). A shot carries the shooter's tick and interpolation fraction; the host rewinds targets (capped by `sv_maxunlag`), traces in `Sim`, applies damage or capture from content data, and sends hit/kill/capture events back. Ambient insects are rewound analytically from seed + time. Fish-Net's built-in lag compensation is a paid Pro feature, so we own this (ADR 0001).

## Parties (Steam invites)

Host: start the Fish-Net server on the Steam transport, create a **friends-only** Steam lobby with `hth_game`, `hth_proto` (= build version), `hth_locked`, `hth_host`, and set rich presence `connect = +connect_lobby <id>`. Invite: `ActivateGameOverlayInviteDialog` (Esc > Invite friends) or the Steam friends list. Joiner: accepting an invite raises `GameLobbyJoinRequested_t` (or `GameRichPresenceJoinRequested_t`) → join lobby → check `hth_game` and `hth_proto` ("update required" on mismatch) → connect P2P to the lobby owner's Steam ID through Valve's relay. The menu also lists friends currently in a How to Hunt lobby (`GetFriendGamePlayed` + `RequestLobbyData`). Local sessions (solo, LAN, editor multi-instance, bots) use Tugboat UDP.

## Budgets (law; shown in the overlay, enforced by tests where possible)

| Budget | Target |
|---|---|
| Frame | ≤ 16.6 ms on a mid-range laptop at 1080p; main thread ≤ 10 ms |
| Host sim tick | ≤ 1.0 ms with 8 players + 64 active prey |
| GC allocations | 0 B per steady-state gameplay frame/tick (EditMode tests assert Sim and EventBus) |
| Rendering | ≤ 800 batches, ≤ 250 SetPass, ≤ 1.5 M visible triangles |
| Ambient insects | 500 for ≤ 0.3 ms |
| Network | ≤ 8 KB/s up per client, ≤ 24 KB/s down per client, ≤ 200 KB/s host up with 8 players |
| Loads | biome ≤ 5 s, respawn ≤ 2 s, memory ≤ 2 GB |

Measured live with `cl_showfps 2` and over a fixed camera tour with `perf_bench` (DevTools, ADR 0013). Allocations come from
`AllocationMeter` (Unity's own counter), not `GC.GetAllocatedBytesForCurrentThread`, which is always 0 on Mono.
