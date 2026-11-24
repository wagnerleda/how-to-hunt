# How to Hunt

Co-op incremental first-person hunting game (Unity 6, URP, Windows). Hunt prey by catching or killing it, sell it, upgrade your gear, unlock new biomes through bunny-hop and surf traversal courses. 1–8 players over Steam peer-to-peer, PvP allowed.

Status (2026-11-24): **M4 in progress**: three maps (Plains, Pine Forest with the Old Bear boss, Red Desert), quests, camp characters, shops, gunsmith and attachments, poison. **New here? Start with [docs/HANDOVER.md](docs/HANDOVER.md)**, then [CLAUDE.md](CLAUDE.md) (conventions and commands), [ARCHITECTURE.md](ARCHITECTURE.md), [docs/DESIGN.md](docs/DESIGN.md), [docs/PLAN.md](docs/PLAN.md) and [docs/adr](docs/adr).

## Run it

1. Open the project with Unity **6000.6.9f1** (Unity Hub, or `unity open .`).
2. Open `Assets/HowToHunt/Scenes/Graybox.unity` and press Play.
3. Click **Play solo**, or **Host a party** (needs Steam running), then Esc > **Invite friends**. Your friend must have the game open to accept the Steam invite (or use "Friends' parties" in the menu).
4. Console: the key left of `1` (or F1). Try `help`, `find sv_`, `status`, `net_graph 2`.

Controls: WASD, mouse, Space or mouse wheel to jump, Ctrl to crouch, Shift to walk, Esc for the menu (invite friends, leave).

Movement lab (the test zone): step on the purple pads in the plaza (BHOP, GAPS, SURF, DESERT); the full Armory (every gun) stands next to the plaza. Useful console: `sv_cheats 1`, `noclip`, `getpos`, `setpos x y z`, `record name` / `stop` / `playdemo name`, `cl_showspeed 2`, `net_graph 2`, `find sv_`.

Quests faster (host, `sv_cheats 1`): `quest_complete` finishes the objectives of the quests you have accepted (hand them in to whoever gave them), `econ_give <coins>` gives coins (sales quests only count real sales), `setpos 392 -50 203` teleports to the forest camp.

## Build

```bash
powershell -ExecutionPolicy Bypass -File tools/build-windows.ps1
```

Produces `Builds/Windows/HowToHunt.exe` (version `VERSION` + commit count) and runs a headless launch smoke test. The shareable installer: `tools/build-installer.ps1` => `Builds/Installer/HowToHunt-Setup-<version>.exe`. See [docs/RELEASE.md](docs/RELEASE.md).

## Test

- EditMode tests: `Assets/HowToHunt/Tests/EditMode` (Window > General > Test Runner, or `unity command test run --mode editor`). Includes movement replays and lag-compensation proofs.
- Loopback stress test (host + 7 bots + a refused 9th): `powershell -ExecutionPolicy Bypass -File tools/bots-test.ps1`
- Lag compensation over a real network path: `powershell -ExecutionPolicy Bypass -File tools/lagcomp-test.ps1 -LatencyMs 150`
- Installer + install/join/uninstall test: `powershell -ExecutionPolicy Bypass -File tools/build-installer.ps1 -TestInstall`

## Repo layout

| Path | What |
|---|---|
| `Assets/HowToHunt/Code/Foundation`, `Sim` | Plain C# (no UnityEngine): console/cvars, event bus, ids, movement and collision simulation |
| `Assets/HowToHunt/Code/Core` | Unity bootstrap, feature modules, console UI, content base, level brushes |
| `Assets/HowToHunt/Code/Platform`, `Net` | Steam services and the network session (Fish-Net) |
| `Assets/HowToHunt/Code/Features/*` | One assembly per feature |
| `Assets/HowToHunt/Code/Editor` | Generated assets, content generators (`GeneratedContent*`), scene builder (`GrayboxSceneBuilder*`, one file per map), procedural models, build script, import conventions |
| `Art/Source` | Blender sources (outside `Assets/`) |
| `tools/` | Build scripts |
| `docs/` | ADRs, design decisions, content and asset guides, release notes |
