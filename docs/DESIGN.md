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
100 HP. At 0 HP: downed for 30 s. Teammate revive: 3 s hold, back at 30 % HP. Bleed out: respawn at camp, drop 10 % of unsold catch. No permadeath.

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

Upgrades: Sturdy Net (+1 strength, 60 c), Whetstone (knife x1.6, 45 c), Wide Hoop (+0.8 m reach, 90 c, needs Sturdy Net), Old Revolver (150 c), Hunter's Satchel (+2 slots, 70 c), Hunter's Backpack (+2 slots, 160 c, needs the Satchel).

Inventory: 3 slots to start (tools included: net, knife and one catch; more slots come later in the game, not from the Plains shop), butterflies stack to 10, rabbits take a slot each. Catches go straight into the inventory; kills drop a carcass you must pick up (E). Sell everything you carry at the Trader, buy at the Outfitter (both at camp, Interact = E). Throw (Q) drops what you hold on a tap and throws it when held: the longer you hold, the further (capped). Throwing a prey into the Trader sells it; a throw that hits a prey or a player slows them 10 % for 0.75 s (never a stun).

A missed bunny hop costs little: for 0.2 s after landing, ground friction is cut to 15 %. Sprint (Shift) runs on a stamina bar (3 s from fresh, refills while you are not sprinting). Dropped items lie on their natural resting face and roll a little. Throw charges show as a ring at the centre of the screen.

Guns (Phase 4): bullets fly as visible tracers (revolver 360 m/s, scout 820 m/s) with a little drop and lose damage with range. Each gun has a magazine (R reloads; ammo is infinite). There is no crosshair with a gun: bullets leave along the barrel, so the weapon itself aims; right mouse aims down the sights (iron sights, or a scope picture on the scout). Accuracy drops with your speed, except on the scout rifle. Recoil kicks the gun in your hand first and the view follows loosely. F inspects what you hold. Heavy guns slow you a little while held.

Gunsmith (Phase 5): the third station at camp. It also sells guns: Glock 19 (300), Sawed-Off (600, eight pellets a shell), Uzi (700, full auto), Hand Cannon (900), Scout Rifle (1100), Heavy Assault Rifle (1400, full auto, heavy), Intervention (2000, scoped, very heavy), GL-37 (2500, explosive grenades), Gunlance (3000). The Old Revolver stays at the Outfitter. Its workshop shows your gun large in the middle with a box per attachment slot (muzzle, barrel, underbarrel, magazine, ammo, optic, top, stock) joined to its mount; pick a slot, pick an attachment, and the gun and its bars (hip fire, precision, control, mobility; damage, fire rate, magazine) preview the change before you buy or fit it. Attachments are bought once and fitted per gun (one per slot), saved with your profile; the host applies their damage, magazine, reload and noise. Ten to start: muzzle brake, suppressor (prey hear the shot from a third as far), laser, foregrip, speed loader, extended magazine, hollow points, match rounds, red dot, padded stock.

Guns use their own model parts: a part a gun already has (the Uzi's suppressor, foregrip and stock; the scout's suppressor and butt pad; the Intervention's bipod) is that gun's own attachment, shown only when fitted; the generic stand-ins are offered only where a gun has no part of its own. Parts a gun can't do without stay on it (the Heavy Assault Rifle keeps its barrel, flash hider and stock; it takes the generic suppressor). Buying an attachment at the workbench fits it to the gun on the bench. Optics sit on the rail (iron sights fold away under them); the extended magazine isn't drawn, it is just more rounds. Reloads animate the real parts: magazines drop out and back in, slides and charging handles rack, bolts lift and cycle after each shot, the revolver cylinder turns and spins, the GL-37 breaks open. The Gunlance is endgame: one shot per magazine; hold Fire to charge (the nose opens and a layered plasma orb, translucent shells in blues around a white core, grows at the muzzle; the gun keeps pointing where you aim), release at full charge to throw the orb, which bursts into seven smaller plasma orbs that each explode. Explosions are low-poly orbs of fire and smoke.

Hunters: everyone has the same body, a skin-coloured stick figure (thin limbs, ball hands and feet, a leaf in front), posed in code (walk cycle, crouch, hands on the held tool, head following the view). What tells players apart is the tribe mask over the face, painted in the main menu before joining (Customize your mask: 24 x 28 cells, 15 colours plus cut-away for the outline and eye holes, mirror and fill). It is saved in `cl_mask` and sent once to the party when you join.
