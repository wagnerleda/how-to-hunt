# Weapon drop-in

One folder per weapon. Folders can have any name: the link to the tool is in `WeaponSounds.Folders` (Glock, Uzi, Sawed-Off, Handcannon, ScoutSniper, HeavyAssaultRifle, Intervention, GrenadeLauncher, scifigun) or the tool key without `tool.` for new ones.

Inside:
- the model (`.fbx`, any name): the gun alone, no hands; I fit it once (orientation, grip, sights, muzzle) and make it the viewmodel.
- sounds (wav, ogg or mp3). A name containing reload, dry or empty, equip or draw, or inspect is that sound; every other sound in the folder is a shot (several = picked at random). Full-auto guns restart the shot sound each round, so long recordings are fine.
  - `fire_01.wav`, `fire_02.wav`, ... one is picked at random per shot
  - `reload.wav` (the whole reload), `dry.wav` (empty click), `equip.wav` (draw), `inspect.wav`

Rebuild content (How to Hunt > Rebuild Graybox Scene, or `GeneratedAssets.RebuildAll()`) and the sounds are linked.
Log every source and licence in docs/ASSETS.md.
