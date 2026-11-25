# Assets and licenses

Every asset and third-party package is listed here with its source and license. Ask the owner before using a paid service or anything with an unclear license.

## Model pipeline (Blender 5.x → Unity)
- **Source**: `Art/Source/<Name>.blend` (outside `Assets/`, LFS). Export: `Assets/HowToHunt/Art/Models/<Name>/<Name>.fbx`.
- **Units/scale**: meters, real-world size, scene scale 1.0. Prefabs may scale up for readability (butterfly prefab instances use 1.5).
- **Orientation**: model faces **+Y** in Blender, Z up (imports facing Unity +Z with the export settings below). The butterfly predates this rule (faces -Y) and is turned 180° in its generated prefab.
- **Pivot**: at the logical center for flying/small things, between the feet for walkers.
- **Mesh**: low-poly, flat shading (`use_smooth = False`), one material per palette color. Name materials `MAT_<Name><Part>`; materials containing `Wing`, `Leaf` or `DoubleSided` render both faces.
- **Rig**: simple deform bones only (`Root`, then parts like `Wing.L`); vertex groups match bone names.
- **Animation**: one Blender action per clip, named `Idle`, `Flap`, `Walk`, `Run`, `Fly`, `Swim`, `Death`, `Loop*`... Those in the looping list loop automatically.
- **FBX export settings**: selected Armature + Mesh, Apply Scalings = *FBX All*, Forward = *-Z*, Up = *Y*, *Apply Transform* on, smoothing = *Face*, no leaf bones, primary bone axis Y / secondary X, Bake Animation with **All Actions** on (otherwise the clip is named after the scene), NLA strips off, simplify 0, force start/end keying.
- **Unity import**: enforced by `ModelImportConventions` (file scale, axis conversion baked, generic rig, clip names from the action, loop flags, double-sided materials, GPU instancing).
- Commit before any Blender MCP session (it runs generated code without guards).

## Third-party code
| Package | Version | License | Notes |
|---|---|---|---|
| Fish-Net | 4.7.3 (git UPM) | FishNet License: free commercial use in games, no attribution required | https://github.com/FirstGearGames/FishNet |
| FishySteamworks | 4.1.1 (vendored) | BSD 2-Clause (keep LICENSE.txt) | `Assets/ThirdParty/FishySteamworks` |
| Steamworks.NET | 2025.165.0 (git UPM) | MIT | ships steam_api64.dll (Valve Steamworks SDK terms) |

## Art and audio
| Asset | Source | License | Notes |
|---|---|---|---|
| Butterfly model + rig + Flap clip | Hand-made in Blender via MCP (`Art/Source/Butterfly.blend`) | Ours | Meadow Butterfly prey (faces -Y: turned in its prefab) |
| Rabbit model | Hand-made in Blender via MCP (`Art/Source/Rabbit.blend`) | Ours | Plains Rabbit prey; static mesh, hop is procedural |
| Net, Knife, Revolver viewmodels | Hand-made in Blender via MCP (`Art/Source/{Net,Knife,Revolver}.blend`) | Ours | First-person tools; swing/scoop/recoil are procedural |
| Nature Kit (53 models used) | Kenney, kenney.nl/assets/nature-kit | CC0 1.0 (License.txt kept) | `Art/ThirdParty/KenneyNatureKit`; colours remapped to the palette on import |
| Impact Sounds, Interface Sounds, RPG Audio (selected clips) | Kenney, kenney.nl | CC0 1.0 (license files kept) | `Assets/Audio/SFX/{UI,Foley,Footsteps,Impacts}` (licences in `SFX/Licenses`): footsteps, cloth, impacts, coins, UI |
| Gore, flame, tracer, dust materials | Generated (`GeneratedAssets.cs`) | Ours | Cube particles, pooled gibs and splats |
| Wedge / SurfPrism meshes | Procedural (`ProceduralMeshes.cs`) | Ours | Level brushes |
| Pine trees, forest terrain | Procedural (`GrayboxSceneBuilder.Forest.cs`) | Ours | Low-poly conifers in vertex colours |
| Pine Deer | Modelled in Blender for this game (`Art/Source/Deer.blend`) | Ours | `Art/Models/Deer/Deer.fbx`, 332 faces |
| Red squirrel, red fox, wild boar, the Old Bear | Procedural (`CreatureModels.cs`): flat-shaded tapered blocks in vertex colours | Ours | `Prefabs/Prey/*.prefab`, M_Terrain; placeholders a Blender model can replace via `Visual` |
| Sacred totems (bear, owl, thunderbird pole, glowing eyes, spirit light) | Procedural (`GrayboxSceneBuilder.Totems.cs`), materials `M_Totem*` and `FX/M_TotemGlow` | Ours | One by each camp and in the lab; travel between discovered regions (2026-11-25) |
| Mask shell and leather strap | Procedural (`AvatarBuilder.cs`: `MaskPlate`, `MaskStrap`, `M_Strap`) | Ours | Players and NPCs (2026-11-25) |
| Travel transition glow | Generated at runtime (`TravelTransition.cs`) | Ours | The totem journey's golden ring |
| Surf ramp bands (`T_SurfRamp.png`, `M_SurfRamp`) | Generated (`Palette.cs`) | Ours | 64 px; orange, cream bands, dark ridge and base lines |
| Jackrabbit, desert lizard, red scorpion, rattlesnake, coyote; saguaros, barrel cactus, dead bush, skull | Procedural (`CreatureModels.cs`), like the forest animals | Ours | `Prefabs/Prey/*.prefab`, `Generated/Meshes/Prop_*.asset`, M_Terrain (2026-11-24) |
| Desert terrain, mesas, Dry Creek camp | Procedural (`GrayboxSceneBuilder.Desert.cs`) | Ours | vertex colours, palette `Mesa`/`MesaDark`/`Adobe` |
| **The Wildeast** font (display face) | Trustha Studio (dribbble.com/trustha), supplied by the owner 2026-11-24 | **Personal use only until a commercial license is bought: buy it before any public release (Steam).** The file says "Copyright 2019 Trustha Studio, all rights reserved" | `Resources/Fonts & Materials/TheWildeast-HowToHunt.ttf`: the original letters and digits plus punctuation drawn from its own strokes by `tools/fonts/complete_wildeast.py` (ours); TMP asset generated into `Resources/Fonts & Materials/Wildeast SDF` |
| UI shapes (rounded panels, bubble tail, circle, glow), menu sky and vignette | Procedural at runtime (`UiSprites.cs`, `MenuBackdrop.cs`) | Ours | no files |
| Voice blips | Procedural (`ProceduralSounds.Blip`) | Ours | pitched per character; `NpcDefinition.VoiceClip` can replace it |
| Menu sounds (tick, confirmation, drop, maximize, error, toggle) | Kenney Interface Sounds, already listed above | CC0 1.0 | `UiFeel` |
| Palette materials `M_*` | Generated (`Palette.cs`) | Ours | Art direction A |
| Scout rifle, attachment models | Generated from primitives (`GeneratedAssets.cs`) | Ours | Stand-ins until the owner's gun assets land |

Still approved for later use: Quaternius (CC0), PolyHaven (CC0). Procedural sounds (`ProceduralSounds.cs`) are ours.

## Weapon drop-in (owner gun assets)
Put each gun's model in `Assets/HowToHunt/Art/Weapons/<tool name>/` (the tool key without `tool.`: `revolver`, `scout`, or a new
gun's name; `.fbx`, gun only, no hands) and its sounds in `Assets/Audio/SFX/Weapons/<the same folder name>/`, named by what they are: `fire_01.wav`, `fire_02.wav`... (one picked at
random per shot), `reload.wav`, `dry.wav` (empty click), `equip.wav`, `inspect.wav`. The folder name is the link between a
model and its sounds; no other naming is needed. The content build links the sounds automatically; the model is fitted once
by hand (orientation, grip, sights, muzzle, attachment mounts). Add a row above for every source and licence.
Shots that start with silence get a trimmed copy in `Assets/Audio/SFX/Weapons/_Trimmed/` (generated).

## Audio layout (2026-11-25)
Every sound lives in `Assets/Audio`: `Music/` (none yet), `SFX/UI`, `SFX/Foley` (cloth, coins, leather, knives), `SFX/Footsteps`,
`SFX/Impacts`, `SFX/player_damage` (+ `rare_sounds`), `SFX/Weapons/<gun>`. Code plays a sound by its file name
(`Sfx.Play2D("tick_001")`; the family `"cloth"` is cloth1..4) through the generated `Resources/SoundLibrary` (`SoundLibraryBuilder`,
rebuilt by itself when a file under `SFX` changes), so names must be unique across `SFX`. Guns reference their clips directly.

| Asset | Where | Licence | Used for |
|---|---|---|---|
| Player damage cries `pl_damage_1..4`, rare `rare_pl_damage_1..5` (mp3) | `Assets/Audio/SFX/player_damage` | supplied by the owner (2026-11-25), source and licence to confirm | your hunter getting hurt (the rare ones 1 in 10,000) |

## Owner gun assets (added 2026-11-17)
Supplied by the owner. Licences not yet confirmed for most of them: **ask the owner before any public release**.
Textures import at 1024 px max (`WeaponImportConventions`); sources not needed at runtime (.blend, .glb, zips, previews)
live in `Art/Source/Weapons/`. Fitted into viewmodels by `DropInWeapons` (turn, length, grip; muzzle, sights and
attachment mounts measured from the mesh).

| Asset | Folder | Source / licence | Used as |
|---|---|---|---|
| Glock 19 model + 4K PBR | `Art/Weapons/Glock` | CGTrader ("cgtrader_optimized"), licence to confirm | `tool.glock` |
| Glock silenced shot (mp3) | `Assets/Audio/SFX/Weapons/Glock` | unknown, to confirm | Glock fire |
| Uzi model (UDIM textures, shown untextured) | `Art/Weapons/Uzi` | unknown, to confirm | `tool.uzi` |
| Uzi shot "universfield gunshot" | `Assets/Audio/SFX/Weapons/Uzi` | Pixabay (universfield), Pixabay licence likely | Uzi fire |
| Hector Gauge 8 shotgun model + textures (four versions in one file; the game uses "Huntress": long barrels, shoulder stock, break action) | `Art/Weapons/Sawed-Off/Hector-Gauge8.fbx` | unknown, to confirm (no sound supplied) | `tool.sawedoff` |
| Colt Navy 1851 model (no textures) | `Art/Weapons/old revolver` | unknown, to confirm | `tool.revolver` (Old Revolver) |
| Cannon model + baked wood texture (CANNON3) | `Art/Weapons/Handcannon` | the owner's own model | `tool.handcannon` (Hand Cannon) |
| Revolver shot; cannon fire (mp3) | `Assets/Audio/SFX/Weapons/old revolver`; `.../Handcannon` | Pixabay (diega1241251, lordsonny), Pixabay licence likely | Old Revolver fire; Hand Cannon fire |
| Low-poly sniper rifle + bakes | `Art/Weapons/ScoutSniper` | unknown, to confirm | `tool.scout` model |
| Sniper rifle shot (mp3) | `Assets/Audio/SFX/Weapons/ScoutSniper` | Pixabay (freesound_community), to confirm | Scout fire |
| SCAR-H model + PBR | `Art/Weapons/HeavyAssaultRifle` | unknown, to confirm | `tool.heavyrifle` |
| G36 fire (mp3) | `Assets/Audio/SFX/Weapons/HeavyAssaultRifle` | Pixabay (freesound_community), to confirm | Heavy rifle fire |
| CheyTac Intervention model + textures | `Art/Weapons/Intervention` | unknown, to confirm | `tool.intervention` |
| "MLG intervention MW2" sound | `Assets/Audio/SFX/Weapons/Intervention` | **likely ripped from Call of Duty: MW2 (Activision): not shippable**; replace before release | Intervention fire |
| GL37 grenade launcher + grenade, textures (zips in Art/Source) | `Art/Weapons/GrenadeLauncher` | unknown, to confirm | `tool.grenadelauncher` |
| Grenade launcher shot (mp3) | `Assets/Audio/SFX/Weapons/GrenadeLauncher` | Pixabay (freesound_community), to confirm | GL-37 fire |
| Gunlance (sci-fi) model + textures | `Art/Weapons/scifigun` | CGTrader, royalty-free, no AI training, no redistribution of sources (LICENSE.txt kept) | `tool.gunlance` |
| Sci-fi single shot (mp3) | `Assets/Audio/SFX/Weapons/scifigun` | Pixabay (daviddumaisaudio), to confirm | Gunlance fire |
| Knife V2 model + PBR | `Art/Models/KnifeV2` | unknown, to confirm | knife viewmodel |
| Red dot sight model + textures | `Art/Models/RedDotSight` | unknown, to confirm | Red Dot attachment (`RedDotSight_Open.fbx`: the same model with its window glass removed in Blender) |
| Grenade model | `Art/Models/Grenade` | unknown, to confirm | not used yet (throwables, later) |
