# Parked ideas

Not approved for building. Each needs the owner's yes before work starts.

- **Party sell celebration (to revise).** Partly answered by the Phase 3 cash burst on sales (scaled to the area). A party-wide version stays parked.
- ~~Carry capacity~~: built in Phase 2 (inventory slots, pack upgrades).
- **Trophy wall / specimen board** at camp showing kills (trophies) and captures (pinned specimens).
- **Hit-stop and gib size scaled by prey size.**
- **Overkill ruins value** is approved as part of the kill rules (docs/DESIGN.md); finer tuning ideas go here.
- Companions that automate hunting (post-M5).
- Fireflies and day/night spawns (after the Plains slice).
- Speed-run bonuses and shortcuts on traversal courses (after M4).
- Apex prey requiring coordinated takedowns (post-M5).
- PvP economy (coin stealing, kill rewards) — design seed only.
- Jungle and Mountains biomes (post v1).
- Plains ambience: birdsong, insects and a campfire crackle (needs CC0 recordings; Kenney packs have none).
- Trader and outfitter characters: now planned (Phase 6, NPCs with dialogue and quests).
- Rabbit idle animation (ear twitch, sniff) from a rig instead of the procedural hop only.
- Trophy wall at camp showing trophies earned.
- Weapon inspect in the workshop (rotate the gun, close-up per slot).
- Physics catching beyond Phase 3: the catch registered at the exact moment the hoop reaches the prey along its swing; prey wriggling out of a weak net.
- Weather and day/night affecting prey senses (fog shortens sight, night needs a light or a scope).
- Sound of footsteps and sprinting alerting nearby prey (sprint is louder, crouching is quiet).
- A recorded CC0 knife swish if the synthesized one never sounds right.
- Cash burst seen by the whole party (now only the seller sees it), with a coin sound at the trader.
- Thrown items bouncing off what they hit (now they fly on through after slowing it).
- A scope that renders a true magnified picture-in-picture (now the whole view zooms behind a scope mask).
- Bullets that ricochet off rock at shallow angles; per-surface impact effects (wood chips, grass, water splash).
- Throwable grenades (the owner's grenade model is imported, unused): a cooked throw that explodes like the GL-37.
- A grenade model flying out of the GL-37 instead of a tracer streak.

- **Red canyon map** (owner, 2026-11-17): a later zone of mesas, arches and dry riverbeds whose **canyon walls are surfable** (steep sides you can ride like surf ramps to cross and climb the map). Prey that fit: goats, coyotes. Parked when the pine forest was chosen as map 2.
- **GPU-driven scatter** (from ADR 0013): grass, flowers and trees through BatchRendererGroup or indirect draws, to bring the plains under the 250 SetPass budget (now ~400). Needs a custom instanced shader.
- **Graphics settings in the menu**: `r_ssao`, `r_drawdistance`, `r_scatter_distance`, `r_scatter_density`, `fps_max` as sliders in Settings (console only for now).
- **Bosses for the later biomes** (owner: "we'll see the others later"): one per biome, each summoned by its own bait, with its own attack (the bear has the slam and the pounce; a charge across the arena, a ranged one) as new `PreyStrike` kinds in `PreyBrain.StepAggressive` (ADR 0014).
- **Bait as a carried item**: set the bait down in the den yourself instead of it working from the stall (the boss comes to the bait).
- **Blender models** for the squirrel, fox, boar and the Old Bear (now procedural blocks), with a walk cycle.
- **Boss loot**: a unique trophy or cosmetic (a bear-claw mask) for everyone in the fight.
