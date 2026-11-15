# Assets and licenses

Every asset and third-party package is listed here with its source and license. Ask the owner before using a paid service or anything with an unclear license.

## Model pipeline (Blender 5.x → Unity)
- **Source**: `Art/Source/<Name>.blend` (outside `Assets/`, LFS). Export: `Assets/HowToHunt/Art/Models/<Name>/<Name>.fbx`.
- **Units/scale**: meters, real-world size, scene scale 1.0. Prefabs may scale up for readability (butterfly prefab instances use 1.5).
- **Orientation**: model faces **-Y** in Blender, Z up. Left side = +X (`.L` bones).
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
| Butterfly model + rig + Flap clip | Hand-made in Blender via MCP (`Art/Source/Butterfly.blend`) | Ours | M0 pipeline proof; final look in M3 |
| Wedge / SurfPrism meshes | Procedural (`ProceduralMeshes.cs`) | Ours | Level brushes |
| Palette materials `M_*` | Generated (`Palette.cs`) | Ours | Art direction A |

Planned sources (to log here when used): Kenney (CC0), Quaternius (CC0), PolyHaven (CC0), jsfxr-generated sounds (ours).
