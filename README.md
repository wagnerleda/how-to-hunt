# How to Hunt

Co-op incremental first-person hunting game (Unity 6, URP, Windows). Hunt prey by catching or killing it, sell it, upgrade your gear, unlock new biomes through bunny-hop and surf traversal courses. 1–8 players over Steam peer-to-peer, PvP allowed.

Status: **M0 (setup and risk spike)**. See [ARCHITECTURE.md](ARCHITECTURE.md), [docs/adr](docs/adr), [docs/DESIGN.md](docs/DESIGN.md).

## Run it

1. Open the project with Unity **6000.6.9f1** (Unity Hub, or `unity open .`).
2. Open `Assets/HowToHunt/Scenes/Graybox.unity` and press Play.
3. Click **Play solo**, or **Host a party** (needs Steam running) and give your friend the 6-character code.
4. Console: the key left of `1` (or F1). Try `help`, `find sv_`, `status`, `net_graph 2`.

Controls: WASD, mouse, Space or mouse wheel to jump, Shift to walk, Esc for the menu (party code, leave).

## Build

```bash
powershell -ExecutionPolicy Bypass -File tools/build-windows.ps1
```

Produces `Builds/Windows/HowToHunt.exe` (version `VERSION` + commit count) and runs a headless launch smoke test. See [docs/RELEASE.md](docs/RELEASE.md).

## Test

EditMode tests live in `Assets/HowToHunt/Tests/EditMode` (Window > General > Test Runner, or `unity command test run --mode editor`).

## Repo layout

| Path | What |
|---|---|
| `Assets/HowToHunt/Code/Foundation`, `Sim` | Plain C# (no UnityEngine): console/cvars, event bus, ids, movement and collision simulation |
| `Assets/HowToHunt/Code/Core` | Unity bootstrap, feature modules, console UI, content base, level brushes |
| `Assets/HowToHunt/Code/Platform`, `Net` | Steam services and the network session (Fish-Net) |
| `Assets/HowToHunt/Code/Features/*` | One assembly per feature |
| `Assets/HowToHunt/Code/Editor` | Generated assets, scene builder, build script, import conventions |
| `Art/Source` | Blender sources (outside `Assets/`) |
| `tools/` | Build scripts |
| `docs/` | ADRs, design decisions, content and asset guides, release notes |
