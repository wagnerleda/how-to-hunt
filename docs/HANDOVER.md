# Handover: picking up How to Hunt

Written 2026-11-24 so a new developer (or a new AI session) can take over. Read this first, then `CLAUDE.md`
(conventions and commands, kept current), `ARCHITECTURE.md` (assemblies, tick path, budgets), `docs/DESIGN.md` (what
the game is, every number) and `docs/PLAN.md` (what was asked, phase by phase). Decisions with their reasons are in
`docs/adr`. The owner writes in French; code, commits and docs are in English.

## The game in one paragraph
A co-op (1-8 players, Steam P2P) first-person hunting game with Source-style movement (bunny hop, surf). You catch
prey alive with a net or kill it with a knife or guns, carry it (limited slots), sell it at camp, buy upgrades, guns and
attachments, do quests for camp characters, and reach new biomes through movement courses. Three maps so far: the
**Plains** (start, camp, lab gate), the **Pine Forest** (reached by surfing three ramps; the Old Bear boss) and the
**Red Desert** (reached by a much harder five-ramp surf route once the bear has fallen; snakes and scorpions with venom).
The **movement lab** next to the Plains is the test zone (courses, the full Armory, test pads to every map).

## Where things are
| What | Where |
|---|---|
| Project | Unity 6000.6.9f1, URP, `Assets/HowToHunt` |
| The one scene | `Assets/HowToHunt/Scenes/Graybox.unity`, **generated**: never edit it by hand |
| Scene generation | `Code/Editor/GrayboxSceneBuilder*.cs` (one partial per map: `.cs` lab, `.Plains`, `.Forest`, `.Desert`, `.DesertRoute`, `.Menu`) |
| Content (prey, tools, upgrades, attachments, NPCs, quests) | ScriptableObjects in `Resources/Content`, **generated** by `Code/Editor/GeneratedContent*.cs` (`.cs`, `.Forest`, `.Desert`, `.Quests`) |
| Procedural models | `Code/Editor/CreatureModels.cs` (animals, cacti), `AvatarBuilder.cs` (the stick figure), `GeneratedAssets.cs` (prefabs, materials, font) |
| Runtime code | `Code/Foundation`, `Sim` (no UnityEngine: console, ids, movement, collision, prey brains, economy math), `Core`, `Net`, `Platform`, `Features/*` (Player, Hunting, Economy, Npcs, Avatar, Bosses), `UI`, `DevTools` |
| Tests | `Assets/HowToHunt/Tests/EditMode` (+ `Tests/Data/Demos` movement demos, `Tests/Data/Worlds/Graybox.brushes` exported collision) |
| Tools | `tools/` (builds, installer, network tests, `fonts/complete_wildeast.py`) |

## Daily loop
1. Open the editor: `unity open "$PWD"`, wait with `unity status --until-ready --project-path "$PWD"`.
2. Change code, then `unity recompile --project-path "$PWD" --format json` (errors as JSON).
3. Changed content or a map? Run `HowToHunt.Editor.GrayboxSceneBuilder.Build()` (menu: How to Hunt > Rebuild Graybox
   Scene). It rebuilds generated assets, content and the scene, and exports the collision for the tests. Takes ~2 min.
   Afterwards revert the noise it leaves in unrelated files (`git checkout -- Assets/HowToHunt/Art/Models/Butterfly/Butterfly.controller Assets/HowToHunt/Prefabs/Ambient/Butterfly.prefab Assets/HowToHunt/Settings/PP_SunnyDiorama.asset`
   and palette materials whose only change is `_Color`).
4. Changed movement, ramps or brushes? `HowToHunt.Editor.MovementLabTools.GenerateScriptedDemos()`; the replay tests
   compare every demo's end state.
5. Tests: `unity command test run --mode editor --filter HowToHunt --filter-type Assembly --project-path "$PWD" --caller plugin --skill unity-cli` (112+ tests, all must pass).
6. Play: Play mode runs offline (Steam off in the editor); Play solo or `host local` in the console (`` ` `` or F1).
   Leave Play mode when done. Editor play writes `profiles/<id>-editor.json` and the shared `config.cfg` in
   `%USERPROFILE%\AppData\LocalLow\How to Hunt\How to Hunt`: restore any archived cvar you changed.
7. Build: `tools/build-windows.ps1` (dev, Mono) or `tools/build-installer.ps1` (release IL2CPP + Inno Setup installer,
   smoke-tested). Never `-TestInstall` while the owner has the game installed.
8. Commit small, on the work branch, then fast-forward `dev`. Never push.

Running C# in the editor: `unity command script eval --project-path "$PWD" --file Temp/x.cs` (the file holds a method
body that returns a string). That is how most verification here is done (teleport with `setpos`, frame a camera,
`unity command capture game_view --output-path x.png`, read state).

## Systems, and where to start reading
- **Movement** (`Sim/Movement/PlayerMove.cs`): Source-like, 64 Hz, deterministic, predicted by the owner and replayed
  by the host (ADR 0001/0004/0007). Surf = brushes flagged `SurfaceFlags.Surf`. Tests replay recorded demos.
- **Collision** (`Sim/Collision`): convex brushes (`SimBrush` boxes and convex meshes) plus terrain heightfields
  (`SimTerrain`). Only `SimBrush` collides; renderers are decoration. Boundaries are visible rock ridges
  (`RockWall`), never invisible walls (owner rule).
- **Zones** (`Core/World/Zone.cs`): axis-aligned triggers: Teleport, Checkpoint, Hazard (back to the last checkpoint).
- **Prey** (`Sim/Hunting/PreyBrain.cs`, `Features/Hunting/PreyWorld.cs`): host-run AI (senses, flee, stamina,
  aggression: None/WhenHurt/Always, strikes, slam, pounce, enrage), sent to clients compactly (ADR 0011, 0014).
  Venom: `PreyDefinition.PoisonDamagePerSecond/PoisonSeconds`, applied in `PreyWorld.HostStrike` through
  `PlayerHealth.HostPoison`.
- **Health** (`Features/Player/PlayerHealth.cs`): 100 HP, downed 30 s (3 s when nobody can revive you), revive,
  bleed out to camp, poison (ticks every `sv_poison_tick`, cured by going down or respawning). HUD: `HealthHud`.
- **Economy** (`Features/Economy`): host ledger per player (`EconomyProfile`, saved by `ProfileStore`), sell, buy,
  quests (`EconomyNet.Quests.cs`), stations (`CampStation`: trader, outfitter/bait stall, gunsmith/armory), shop UI
  (`ShopPanel` columns), gunsmith counter (`WorkshopPanel`, camera via `PlayerCamera.FrameShot`).
- **Guns** (`Features/Hunting`: `PlayerTools`, `ViewmodelController`, `GunRig`, `AttachmentVisuals`): owner models
  fitted by `Editor/DropInWeapons.cs`; ballistics and lag compensation in `Sim/Combat`.
- **NPCs** (`Features/Npcs`): `Npc` (who, stall), `NpcBody` (pose per `NpcActivity`: stall, campfire, chopping wood,
  whittling), `DialogueBox` (one-line quest talks), `SpeechBubble`, `NpcPresence` (markers, barks).
- **Bosses** (`Features/Bosses`, ADR 0014): aggressive prey with `IsBoss`, summoned by a bait.
- **UI** (`UI`, `Core/Hud`): uGUI + TextMeshPro, motion through `Ease`/`UiFeel`, keyboard/gamepad through `UiInput`;
  The Wildeast font is the default everywhere (`UiFonts`). Main menu background: `Features/Avatar/MenuVista`.

## Maps and coordinates
| Map | Where | Way in |
|---|---|---|
| Movement lab | around (0, 0, 0) | spawn plaza pads; north gate to the Plains |
| Plains | x -100..100, z 115..315, camp (-30, 0, 135) | start |
| Pine Forest | x 365..485, z 167..287, y -50, camp (392, -50, 203) | east trail (quest "Beyond the Trees") then three surf ramps |
| Red Desert | x 380..520, z -341..-201, y -178, camp (410, -178, -231) | forest south trail (Old Bear dead) then five hard surf ramps; lab test pad |

## Open issues and next steps (as of 2026-11-24)
- **Not yet played by the owner**: everything in PLAN Phases 6.2b-6.7 (bear, armory, shop, desert, routes, poison).
  Ask for their play test before building on it.
- **Performance (open)**: the owner still feels freezes. ADR 0013's follow-up has the latest `perf_bench` numbers:
  averages fine, 30-40 ms stalls on camera moves with the render thread waiting on the main thread (cause not found
  yet: take a Profiler capture of a development build), ~210 B/frame of allocations to hunt down, Plains draw calls over
  budget (GPU-driven instancing is the parked fix, IDEAS).
- **Licenses before a public release**: The Wildeast font (personal use only), the owner gun assets marked "to confirm"
  (docs/ASSETS.md), Steam app id (480 is the shared test app), code signing (docs/RELEASE.md).
- **Desert**: first version. A desert boss, flyers (vultures) and more quests are in docs/IDEAS.md.
- Placeholders that a real model can replace: all `CreatureModels` animals, the scout rifle stand-in parts.

## Rules that bite if you forget them
All in `CLAUDE.md`; the ones people trip on:
- Generated assets: change the generator, rebuild; never hand-edit the scene or content assets.
- No allocations per frame in gameplay code; `AllocationAssert.None` tests for hot paths.
- One MonoBehaviour per file, named like the class (player builds break otherwise).
- Every tunable is a cvar; content numbers live in the generators.
- Foundation and Sim never reference UnityEngine.
- Keys of content are permanent (saves refer to them).
