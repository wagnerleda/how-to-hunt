# CLAUDE.md — How to Hunt conventions

## Commands (Git Bash, from the repo root)
- Open editor: `unity open "$PWD"`; wait: `unity status --until-ready --project-path "$PWD"`
- Recompile + diagnostics: `unity recompile --project-path "$PWD" --format json`
- EditMode tests: `unity command test run --mode editor --filter HowToHunt --filter-type Assembly --project-path "$PWD" --caller plugin --skill unity-cli`
- Run C# in the editor: `unity command script eval --project-path "$PWD" --file Temp/x.cs` (use `--file` for anything with quotes)
- Regenerate prefabs/materials/scene: `HowToHunt.Editor.GrayboxSceneBuilder.Build()` (menu: How to Hunt > Rebuild Graybox Scene)
- Build + smoke test: `powershell -ExecutionPolicy Bypass -File tools/build-windows.ps1` (uses the open editor if any)
- Console in game: `` ` `` / F1. Movement cvars need `sv_cheats 1`.
- Scripted test client: `HowToHunt.exe -screen-fullscreen 0 +cl_bot_forward 1 +cl_bot_yawspeed 60 +connect 127.0.0.1`

## Rules
- Branch `dev`; small commits; never push; tag checkpoints (`m0-setup`, `m1-movement-lab`, ...).
- Foundation and Sim must not reference UnityEngine. Use `System.Numerics`, convert with `VectorConvert`.
- No allocations in per-tick/per-frame gameplay code (no LINQ/closures in loops, pool everything). Add an allocation test for new hot paths.
- One MonoBehaviour per file, file named after the class (player builds break otherwise).
- Every tunable is a cvar (Archive for settings, `Replicated|Cheat` for sv_*). No magic numbers in gameplay code.
- Content is ScriptableObjects deriving `ContentDefinition`; keys are permanent; `ContentValidationTests` must pass.
- New features: own asmdef + `[assembly: FeatureModule(...)]`. Core must not reference features.
- Generated assets are built by editor code (`GeneratedAssets`, `GrayboxSceneBuilder`): change the code, not the asset.
- Third-party: Fish-Net 4.7.3 (4.7.4 git release does not compile), Steamworks.NET 2025.165.0, FishySteamworks vendored in `Assets/ThirdParty` (no upstream asmdef).
- Blender: sources in `Art/Source`, exports in `Assets/HowToHunt/Art/Models/<Name>/` following docs/ASSETS.md. Commit before Blender MCP sessions.
- Ask before paid services or unclear licenses; log every asset in docs/ASSETS.md. New ideas go to docs/IDEAS.md.

## Movement lab
- Rebuild the map: `HowToHunt.Editor.GrayboxSceneBuilder.Build()` (also exports collision to Tests/Data/Worlds).
- Regenerate scripted demos after an intended physics/map change: `HowToHunt.Editor.MovementLabTools.GenerateScriptedDemos()`. ReplayTests replays every demo in Tests/Data/Demos (including ones recorded in the editor with `record`/`stop`).
- Editor Play mode runs offline (no Steam) unless How to Hunt > Use Steam in Play Mode is ticked: an Editor registered as app 480 is closed by Steam whenever another app-480 instance (test builds, bots-test) exits. Always leave Play mode when done testing.

## Network tests (M2)
- `tools/bots-test.ps1` (host + 7 bots + refused 9th, bandwidth vs budgets), `tools/lagcomp-test.ps1` (hit registration under fake latency), `tools/build-installer.ps1 -TestInstall`.
- Multi-instance runs use `-nocfg` so instances don't overwrite each other's config.cfg.
- Test hooks: `net_fakelag`, `net_fakeloss`, `wpn_autofire <dummy> <n>` + `wpn_stats` (cheat), `delay <s> <cmd>`, `cl_bot_*`.
