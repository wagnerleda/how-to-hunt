# Locked design decisions

Approved by the project owner on 2026-11-15 (Phase A). All numbers live in data/cvars; these are the defaults.

## Catch or kill (player freedom)
Every prey defaults to `TakedownMethods = Either`. Each tier offers at least one affordable catch tool and one kill tool (validated in CI).

| | Kill | Capture |
|---|---|---|
| Speed / skill | Fast | Slower, needs more skill |
| Coins | 1.0 × base value | 1.5 × base value |
| Extra | **Trophies** (bones, pelts) for the trophy wall and cosmetics | **Bestiary research**: species mastery gives +value % and reveals weak points |
| Overkill | Damage far above health "ruins" the carcass and lowers its value | — |

Upgrades cost coins only in v1.

## Players get hurt
100 HP. At 0 HP: downed for 30 s. Teammate revive: 3 s hold, back at 30 % HP. Bleed out: respawn at camp, drop 10 % of unsold catch. No permadeath. Nobody left standing to revive you (alone on the map, or the whole party down): back at camp after 3 s instead of 30 (`sv_downed_time_alone`).

## PvP
Host toggles PvP per party (on by default); friendly fire follows the toggle; dying to a player costs the same as dying to prey; no loot stealing. The host has a built-in advantage (friends-only trust model, ADR 0005).

## Art direction
**A — Sunny diorama**: flat-shaded low-poly, one palette per biome, soft URP sun, gradient sky, light fog. Outlines/rim light only on prey and players. Gore: chunky cube-pixel blood, flat decals, pooled bone/gib meshes. Gore is always on (no setting).

## Parties: Steam invites only (owner decision 2026-11-15)
No room codes. The host creates a friends-only Steam lobby and invites friends from the game (Esc > Invite friends) or the Steam overlay. Friends can also join from the in-game "Friends' parties" list. While we use the shared dev app id 480, a friend must already have How to Hunt running to accept an invite (Steam would otherwise launch Spacewar).

## Other defaults
Personal progress per player; join-in-progress unless the host locks; each player crosses traversal courses alone; auto-hop is an accessibility option (`cl_autohop`); Windows only; Steam client required online, solo works offline; dev app id 480.

## Plains economy (M3 slice)
Numbers live in content (`GeneratedContent.cs`) and cvars; pacing is checked by `EconomyPacingTests` (first upgrade within 1 to 3.5 minutes, all four within 10, for a hunter who sells only when the bag is full).

| Prey | HP | Net strength needed | Kill tier | Overkill above | Base value | Trophies (kill) |
|---|---|---|---|---|---|---|
| Meadow Butterfly (flyer) | 4 | 1 (starter net) | any | 10 dmg | 4 | 0 |
| Plains Rabbit (flees within 7 m) | 10 | 2 (Sturdy Net) | 1 (knife) | 30 dmg | 12 | 1 |

| Tool | Slot | Numbers | Owned |
|---|---|---|---|
| Butterfly Net | 1 | strength 1, reach 2.4 m, 70° arc | start |
| Hunting Knife | 2 | 6 dmg (x2 head), reach 2 m | start |
| Old Revolver | 3 | 25 dmg (x2 head), 150 m: ruins butterflies, a head shot ruins a rabbit | bought |

Upgrades (Outfitter; the Old Revolver is at the Gunsmith since 2026-11-24). The rack shows one column per thing you improve, each headed by the item and what it does now, its upgrades in order joined by arrows, each with one clear state (owned, buy, N more coins, needs the one above): **Butterfly Net** Sturdy Net (+1 strength, 130 c), Wide Hoop (+0.8 m reach, 150 c), Ironwood Net (+1 strength: holds foxes, jackrabbits and scorpions, 380 c); **Hunting Knife** Whetstone (x1.6 damage, 110 c); **Your bag** (your slots drawn as boxes) Hunter's Satchel (+2 slots, 250 c), Hunter's Backpack (+2 slots, 650 c). Pacing: the starter kit in about 15 minutes, the whole rack in 25-45 (EconomyPacingTests).

Inventory: 3 slots to start (tools included: net, knife and one catch; the Outfitter's bag column adds 2 + 2), butterflies stack to 10, rabbits take a slot each. Catches go straight into the inventory; kills drop a carcass you must pick up (E). Sell everything you carry at the Trader, buy at the Outfitter (both at camp, Interact = E). Throw (Q) drops what you hold on a tap and throws it when held: the longer you hold, the further (capped). Throwing a prey into the Trader sells it; a throw that hits a prey or a player slows them 10 % for 0.75 s (never a stun).

A missed bunny hop costs little: for 0.2 s after landing, ground friction is cut to 15 %. Sprint (Shift) runs on a stamina bar (3 s from fresh, refills while you are not sprinting). Dropped items lie on their natural resting face and roll a little. Throw charges show as a ring at the centre of the screen.

Guns (Phase 4): bullets fly as visible tracers (revolver 360 m/s, scout 820 m/s) with a little drop and lose damage with range. Each gun has a magazine (R reloads; ammo is infinite). There is no crosshair with a gun: bullets leave along the barrel, so the weapon itself aims; right mouse aims down the sights (iron sights, or a scope picture on the scout). Accuracy drops with your speed, except on the scout rifle. Recoil kicks the gun in your hand first and the view follows loosely. F inspects what you hold. Heavy guns slow you a little while held.

Gunsmith (Phase 5; reworked 2026-11-24): the third station at camp, Gus's. In the Plains it sells only the Old Revolver (200; it left the Outfitter) and attachments; the **Armory** in the movement lab (next to the spawn plaza, no keeper) sells every gun: Glock 19 (300), Sawed-Off (600, eight pellets a shell), Uzi (700, full auto), Hand Cannon (900: a real cannon held by its wheels, one ball, slow to load; its blast throws you back, so firing at the ground launches you), Scout Rifle (1100), Heavy Assault Rifle (1400, full auto, heavy), Intervention (2000, scoped, very heavy), GL-37 (2500, explosive grenades), Gunlance (3000). The Old Revolver (the 1851 navy revolver) is the first gun: light damage and a gentle kick, and it only takes a laser or a speed loader. At either counter the camera glides down onto the counter and the gun lies there in 3D on a green felt mat (turning a little under a lamp, the keeper behind it), with glass panels around it: a box per attachment slot (muzzle, barrel, underbarrel, magazine, ammo, optic, top, stock) joined to its mount; pick a slot, pick an attachment, and the gun and its bars (hip fire, precision, control, mobility; damage, fire rate, magazine) preview the change before you buy or fit it. Attachments are bought once and fitted per gun (one per slot), saved with your profile; the host applies their damage, magazine, reload and noise. Ten to start: muzzle brake, suppressor (prey hear the shot from a third as far), laser, foregrip, speed loader, extended magazine, hollow points, match rounds, red dot, padded stock.

Guns use their own model parts: a part a gun already has (the Uzi's suppressor, foregrip and stock; the scout's suppressor and butt pad; the Intervention's bipod) is that gun's own attachment, shown only when fitted; the generic stand-ins are offered only where a gun has no part of its own. Parts a gun can't do without stay on it (the Heavy Assault Rifle keeps its barrel, flash hider and stock; it takes the generic suppressor). Buying an attachment at the workbench fits it to the gun on the bench. Optics sit on the rail (iron sights fold away under them); the extended magazine isn't drawn, it is just more rounds. Reloads animate the real parts: magazines drop out and back in, slides and charging handles rack, bolts lift and cycle after each shot, the revolver cylinder turns and spins, the GL-37 breaks open. The Gunlance is endgame: one shot per magazine; hold Fire to charge (the nose opens and a layered plasma orb, translucent shells in blues around a white core, grows at the muzzle; the gun keeps pointing where you aim), release at full charge to throw the orb, which bursts into seven smaller plasma orbs that each explode. Explosions are low-poly orbs of fire and smoke.

Gun handling follows How to Fish (the owner's own game, studied from its code): every shot kicks the view up (and randomly aside) and it stays there, you pull it back down; aiming does not soften it. The gun model is thrown back and up and springs home through a second, softer rig, stiffer while aiming. From the hip the gun drifts in a small free-aim box as you turn and fire, and bullets follow the barrel; aiming centres it. Only muzzle devices change the view kick (a little); grips, stocks and bipods steady the gun in the hand instead. Shotguns and launchers shove you back along your view (firing at your feet lifts you). Sprinting lowers and turns the gun; reloads tilt in, flick the old magazine out, slap the new one home and rack only when the gun was empty (a reload with rounds left is quicker); the magazine counts as in at 75-90 % of the reload. The head bobs with your steps and tips with vertical speed (cl_viewbob, cl_falltilt). The gun model's turn is held to soft limits like How to Fish's joints (about 1 degree up, 3 aside), so a shot shoves the gun back in the hands rather than flipping it, and the free-aim drift follows on a spring. During reloads the off hand works the gun: it takes the magazine down and brings the new one up and slaps it home, racks the slide, charging handle or bolt on an empty gun, turns a revolver's cylinder, reaches into a shotgun's open breech, and rams the cannon from its muzzle (pistols show a second hand just for the reload).

Typography: titles, headings, buttons, names, world signs and the boss bar use The Wildeast (a hand-drawn western brush; its missing punctuation drawn from its own strokes); running text (dialogue, descriptions, numbers) stays in the plain face. Readability first: small text never uses the brush.

Main menu: the Plains at sunset seen from behind a hunter sitting on a boulder, rifle on the shoulder, wearing your own mask; the hunter breathes and looks around, the camera drifts, the light is a low orange sun. The menu stands on the left. Settings > Progress > Reset progress (asked twice; main menu only) starts you over as a new hunter: coins, gear, attachments, quests and opened paths go; the mask and settings stay.

Not every character stands at a stall: Hank warms his hands at the Plains campfire, Mae splits firewood behind the Outfitter (an axe on a block, a thunk each swing), Fern whittles by Ash's fire, Dusty sits at the Dry Creek fire. Walk up and talk to them where they are; they stop what they're doing and turn to you. Hank gives "Rabbit Stew" (take down 4 rabbits, 90).

Talking to a camp character is short: their quest is settled in one line (a new one is taken on with its task and reward, a finished one handed in and the next one taken, one in progress shows how far you are); the only replies are their stall's business and goodbye.

Hunters: everyone has the same body, a skin-coloured stick figure (thin limbs, ball hands and feet, a leaf in front), posed in code (walk cycle, crouch, hands on the held tool, head following the view). What tells players apart is the tribe mask over the face, painted in the main menu before joining (Customize your mask: 24 x 28 cells, 15 colours plus cut-away for the outline and eye holes, mirror and fill). It is saved in `cl_mask` and sent once to the party when you join.

## Pine Forest (second map, Phase 6)
The east clearing of the Plains is blocked by fallen trees until the party finishes "Beyond the Trees" (Tomas). Then a trail leads to a cliff ledge; three surf ramps (7 degrees down, 8 m gaps, each 7 m lower) cross a wooded ravine to the Pine Forest. Holding strafe into the ramp is enough to make it (about 12 s); a fall puts you back on the ledge. No invisible walls: every boundary is a rocky ridge you can see (the Plains and the forest all round, the corridor from the lab, the trail to the ledge, the sides of the forest landing); the ledge is open, a fall into the ravine brings you back to it. The forest is a smaller valley (120 m) 50 m below the Plains: pines, deer glades, rabbit warrens, Ash's trading post (forest prices x1.5, bigger cash bursts) and a pad back to the Plains camp (the quick way home). Once you have landed, falls respawn you at the forest landing. New prey: the Pine Deer (60 HP, wary, flees fast, worth 45; Ash's quest "Ghost of the Pines").

## The Red Desert (third biome, started 2026-11-24)
A basin of low dunes ringed by striped red mesas, south of the movement lab. Reached by a pad at Ash's forest camp that appears once the Old Bear has fallen (the lab has a test pad); pads at Dry Creek go back to the forest and the Plains. Dry Creek camp: Rosa's trading post (desert prices x2, the best so far), Dusty the prospector by the fire, an adobe hut, a well. Saguaros (solid), barrel cacti, dead bushes, skulls, red rock formations you can climb.

| Prey | HP | Take-down | Value | Behaviour | Quest |
|---|---|---|---|---|---|
| Jackrabbit | 12 | either (Ironwood Net) | 16 | hears you from 60 m, outruns a sprint for 4 s | Long Ears: take down 4 (Rosa, 260) |
| Desert Lizard | 5 | either (any net), stacks 3 | 8 | basks on rock piles, short dashes | - |
| Red Scorpion | 14 | either (Ironwood Net), stacks 2 | 22 | doesn't flee: comes at you within 4 m and stings (10) after the tail rears | Sting Operation: catch 2 alive (Rosa, 340) |
| Rattlesnake | 20 | kill only | 30 | waits in the shade of rock piles; within 3.5 m it rattles, then strikes 1.6 m (18) | Rattled: kill 3 (Rosa, 420) |
| Coyote | 40 | either | 48 | in pairs, wary (34 m), hurt one and it snaps back for 7 s (12) | Coyote Trouble: take down 2 (Dusty, 380) |

A desert boss comes later (IDEAS).

## Forest fauna and the first boss (2026-11-24)
New forest prey, each with an Ash quest after "Ghost of the Pines":

| Prey | HP | Take-down | Value | Behaviour | Ash's quest |
|---|---|---|---|---|---|
| Red Squirrel | 6 | either (Sturdy Net holds it) | 9, stacks 4 | darts off, tires after 3 s | Quick Paws: catch 3 alive (150) |
| Red Fox | 25 | either (no net strong enough yet) | 28 | wary (sees 40 m), outruns a sprint for 5 s | The Sly One: take down 2 (220) |
| Wild Boar | 90 | kill only | 60 | flees, but **charges whoever hurts it** for 9 s: 15 damage a hit, after a short rear-up | Tusks and Temper: kill 3 (300) |
| **The Old Bear of the Pines** (boss) | 3,000 | kill only | 250 carcass | sleeps until a Honeycomb Bait is bought at the Bait Stall in Ash's camp (150 coins); charges any hunter within 30 m: 40-damage swipes after a 0.65 s roar, a ground slam every third strike (50 damage all around, deeper roar: get out or jump), a pounce at hunters 4.5-13 m away (crouches first: sidestep); enraged below 40 % (quicker, faster); gives up 55 m from its den and heals if everyone runs | The Old Bear: bring it down (800) |

**Bosses** (one per biome from the forest on; the others come later): a boss bar shows its health to everyone
nearby; the party hears when it wakes and when it falls; every hunter who hurt it gets its reward (the bear: 650
coins and 35 trophies each, plus the quest credit), the killer also gets the carcass. A boss only comes out when a
hunter buys its bait (used up; not again while it is out). Fair to fight: every strike is telegraphed (a rear-up and
a cry for a swipe, a higher rear-up and a deeper roar for a slam, a low crouch before a pounce), a charge is about a
sprint (a bunny hop outruns it), jumping over a swipe or a slam dodges it, and downed hunters are left alone. Rules
and numbers: ADR 0014.

Surf ramps are orange with cream bands across the slope and a dark ridge line (they shared the water's blue and
were hard to see). A frame rate limit (Settings, or `fps_max`) now replaces vertical sync while it is set.

## Menus, dialogue and gamepad (2026-11-24, ADR 0015)
Menus come in as a cascade over a warm, living background (pollen, sun, sky tint; the game dims behind the pause
menu); the focused item glows and grows, presses squash, every action has a soft sound. Characters speak in
speech bubbles over their heads (normal, shout, whisper, thought, narration), typed out with their own voice;
replies are pills at the bottom. A floating "!" marks a character with a quest for you, "?" one waiting for a
quest you finished, "..." one close enough to talk to; characters call out as you pass and turn to face you when
you talk. Settings: menu and dialogue sound volume, "Reduce animations" (fades only).

Gamepad (Xbox layout, works with keyboard and mouse at the same time): left stick move, right stick look, A jump,
B crouch, left stick click sprint, RT fire, LT aim, X interact, Y reload, RB throw (hold), right stick click
inspect, D-pad left/right hotbar, View scoreboard, Start menu. Menus and dialogue: D-pad or stick to choose, A to
confirm, B to go back. Look speed, dead zone and invert in Settings.
