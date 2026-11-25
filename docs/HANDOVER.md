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
The **movement lab** next to the Plains is the test zone (courses, the full Armory, test pads to every map). A sacred **totem** by every camp takes players to any region they have discovered.

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
- **Area streaming** (`Core/World/StreamedArea.cs`, owner's rule 2026-11-25): the world is a chain, `Movement Lab`,
  Plains, Surf Route East, Pine Forest, Surf Route South (hard), Red Desert (`GrayboxSceneBuilder.Chain`; the lab's loose
  pieces are gathered under a `Movement Lab` root). The map nearest the camera is drawn in full, the previous and next
  maps show only their bare terrain (plus the lab's floor and walls), the surf routes between the current map and those
  two are drawn in full, everything else is not drawn. Visual only (`forceRenderingOff`, scatter `Hidden`, lights):
  collision, prey and zones stay for players elsewhere. `r_area_streaming 0` draws everything. New maps or routes: add
  them to `Chain` in order. Standing inside a surf route's bounds makes the route the current zone: the route and both
  maps it joins are drawn in full (2026-11-25).
- **Fog and draw distance** (`Core/World/RenderDistance.cs`): fog fully thick at `r_fog_distance` (350 m), starting at
  `r_fog_start` of it; the camera's far plane sits just past it, so nothing past the fog is drawn. A map can bring it
  closer (`StreamedArea` `_fogDistance`, via `Configure(..., fogDistance)` in `StreamAreas`) and gameplay too
  (`RenderDistance.CaseFog`); the nearest wins and changes glide. `r_drawdistance` is gone.
- **Totem travel** (`Features/Travel`, 2026-11-25): `RegionDefinition` content (`Core/World`, `GeneratedContent.Regions`),
  a `Totem` per region (`GrayboxSceneBuilder.Totems.cs`: model, arrival point, discovery area = its map's bounds).
  Discovery is host-side (`TravelNet.HostTick` -> `EconomyNet.HostDiscover`) and saved in `EconomyProfile.Discovered`.
  Consulting: `EconomyHud` totem hooks -> `TotemMenu` -> `TravelTransition` (asks the host once the screen is white) ->
  host checks and calls `PlayerMotor.ServerTravel` (sets the checkpoint too). `totem_discover_all` (cheat) for tests.
- **Bosses go back to sleep** (`PreyWorld.HostSleepIfAlone`): no hunter on their feet within `sv_boss_sleep_range` for
  `sv_boss_sleep_after` and a woken boss returns to its den (no reward), so its bait can be bought again.
- **Statics and Play mode**: Play mode starts without a domain reload, so static timers carry over between sessions;
  reset them in a `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` (see `StreamedArea.ResetStatics`).
- **Thorns** (`Core/World/ThornField.cs`): cactus boxes baked by the desert builder; the host pricks players who touch
  them (`PlayerHealth.HostThorns`) and knocks them back (`PlayerMotor.HostKnock`: a hop plus `MoveState.KnockTime`, which
  turns air control off for a moment so the knock isn't steered straight back in).
- **Checkpoint respawns** land on the real ground under the checkpoint (`PlayerMotor.SpawnCenter`): a checkpoint box a
  few cm under bumpy terrain used to start the hull inside the ground (stuck until crouching).
- **Audio** (`Assets/Audio`, see docs/ASSETS.md "Audio layout"): `Sfx` plays clips by file name through the generated
  `Resources/SoundLibrary`. Hurt feedback: `PlayerHealth.LocalHurt` -> `PlayerCamera` tilt + `HurtSounds`.
- **Masks** (`Features/Avatar/MaskData.cs`): 48 x 56 colour indices (1-15 palette, 16-255 a colour cube for imported
  pictures), run-length packed; version 1 masks load doubled. Shell mesh and strap from `AvatarBuilder` (`MaskPlate`,
  `MaskStrap`, shape in `AvatarShape`). NPC masks: `Npcs/NpcMasks.cs` (trade emblems, no eyes). Import: `ImageFileDialog`
  (Windows open dialog) + `MaskData.PasteImage`.
- **Aim sway**: `ViewmodelController.UpdateAdsSway` (`cl_ads_sway`). Shots leave along the barrel, so it moves the aim too:
  about 0.2° drift, up to ~1° while turning fast.
- **Held models** are on the `Viewmodel` layer, drawn by a RenderObjects pass right after SSAO
  (`Editor/ViewmodelRenderSetup.cs`, run by `GeneratedAssets.RebuildAll`): the half-resolution AO's noise showed as
  grain on the gun in hand. Anything new parented to the viewmodel needs that layer (`ViewmodelController`).
- **Props** (`GrayboxSceneBuilder.Prop`): always rest on the lowest ground under their footprint; the y you pass is
  ignored (use `lift` to raise one on purpose).
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
| Movement lab | around (0, 0, 0) | spawn plaza pads; north gate to the Plains; any totem |
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
