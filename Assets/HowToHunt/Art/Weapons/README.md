# Weapon drop-in

One folder per weapon, named after the tool (the key without `tool.`): `revolver/`, `scout/`, and new guns by their name.

Inside:
- the model (`.fbx`, any name): the gun alone, no hands; I fit it once (orientation, grip, sights, muzzle) and make it the viewmodel.
- sounds, named by what they are (wav or ogg):
  - `fire_01.wav`, `fire_02.wav`, ... one is picked at random per shot
  - `reload.wav` (the whole reload), `dry.wav` (empty click), `equip.wav` (draw), `inspect.wav`

Rebuild content (How to Hunt > Rebuild Graybox Scene, or `GeneratedAssets.RebuildAll()`) and the sounds are linked.
Log every source and licence in docs/ASSETS.md.
