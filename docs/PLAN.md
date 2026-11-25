# Plan (owner feedback after M3, updated 2026-11-16)

Each phase ends with a playable build and a short checkpoint; the owner plays it before the next phase starts.
Phases can be reordered as the game grows. Done: Phase 1 (feel pass) and Phase 2 (inventory, carcasses to fetch,
hold/throw/drop, droppable tools, shape-fitted hitboxes) and Phase 3 (physical hunting, below).

## Phase 3: physical hunting (done 2026-11-16, awaiting the owner's play test)
The hunt itself gets more physical and the prey smarter. Grind goes up.

| # | Change | Owner rule |
|---|---|---|
| 1 | Knife swish quieter | |
| 2 | Short take-down messages (one compact line) | current ones take too much screen |
| 3 | Hit sounds differ per prey (squeak for a rabbit, papery flutter for a butterfly...) | |
| 4 | **Sprint** (Shift): a modest ground-speed boost for players who can't bunny hop; bhop stays far faster | |
| 5 | **Water**: the river sits in its bed (no longer raised), slows you down while wading, and splashes instead of footsteps | |
| 6 | **Prey senses**: each prey has a sight distance (with line of sight) and flees from as far as it can see you; rare prey are warier (see further, startle sooner); harmless prey hear gunshots and flee | |
| 7 | **Net sweep catches several**: everything the hoop passes through is scooped (net strength still applies to each) | approved |
| 8 | **Economy rebalance**: less coin per prey, pricier upgrades, a slight grind; the pacing test is retuned (first upgrade ~4-6 min, all Plains upgrades ~20-30 min) | "easy to get the revolver and backpack too fast" |
| 9 | **Hits move things**: shots and swings push prey (knockback) and world items (carcasses, dropped tools) | |
| 10 | **Severe gun recoil**: firing kicks your aim up and sideways; you pull it back down (attachments will reduce it, Phase 4) | |
| 11 | **Throw impacts slow, never stun**: a thrown item that hits a prey or a player slows it by 10 % for 0.75 s, no more. A stunned/slowed prey still needs a strong enough net | |
| 12 | **One throw key**: hold to charge (capped), release to throw; tap to drop. Works for prey and items; replaces "Fire throws" | |
| 13 | **Throw prey at the trader to sell it** (prey only; tools and guns bounce off, so a bad throw can't sell your gun) | |
| 14 | **Cash burst on sales**: bills fly out of the trader, scaled to the area's expected sale (a big Plains sale is a shower; the same amount in a late area does nothing) | |

## Phase 3.5: feedback on Phase 3 (done 2026-11-16, awaiting the owner's play test)
| # | Change | Owner rule |
|---|---|---|
| 1 | **Items rest in their real pose**: lying items keep the orientation their hitbox physics gives them (a net tips over onto its hoop, a revolver lies flat); no snapping back to a straight model pose, nothing levitates | net and revolver float in the screenshot |
| 2 | **Hitbox-driven rolling**: thrown items tumble and roll on their shape-fitted hitbox, a little but not much (friction tuned per shape) | |
| 3 | **Stronger throws**: raise the maximum throw power | |
| 4 | **Throw gauge at the centre**: a circle around the screen centre fills while charging; no crosshair (see Phase 4, physical aim) | |
| 5 | **Player stamina bar**: sprint drains it and it refills when you stop sprinting, so a sprinter can no longer run down a rabbit easily; rabbits keep their stamina; bhop stays the skilled way | rabbits too easy since sprint |
| 6 | **Economy pace ~15 min** for all Plains upgrades (first upgrade still a few minutes); pacing test retuned | 27 min is too much |
| 7 | **3 inventory slots to start**; slot upgrades leave the Plains shop and unlock later in the game (later areas, quests) | too many slots early |

## Phase 4: gun overhaul (done 2026-11-16 except real weapon models; awaiting the owner's play test)
- **Physical recoil** (replaces the instant camera kick): each shot kicks the gun in the hand first; the camera follows loosely with spring lag, so a fast-firing SMG climbs on screen and the view trails it.
- **Physical aim, like How to Fish**: bullets leave along the barrel, not the crosshair; no crosshair (the weapon itself aims). Hit registration and lag compensation use the barrel ray the client sends.
- **Inspect key** (F by default, rebindable): the hands bring the held weapon or tool up and turn it to show it off, like a CS-style inspect. Any action (fire, switch, throw) cancels it.
- Visible bullets: fast tracer rounds that fly (no laser lines); hits checked along the bullet's path with lag compensation.
- Every gun has a magazine and reloads (ammo is infinite).
- Damage falls off with range.
- Accuracy drops with speed (the faster you move, the less accurate), except a scout-style sniper that stays accurate.
- Aim down sights.
- Bullet impacts on the environment (dust, chips, decals by surface).
- Heavy weapons slow you down while held. Light prey, the knife, nets, traps and tools never slow you.
- Better weapon models: the owner provides them, or we fetch CC0 models from a repository after the owner approves the source. **Open**: the scout rifle is a stand-in built from boxes and cylinders.

## Phase 5: gun workshop and attachments (done 2026-11-17 with stand-in models; awaiting the owner's gun assets and play test)
- Workshop screen modelled on Battlefield 6's weapon customization (owner reference screenshot): the gun in the middle, slots around it joined by leader lines (muzzle, barrel, underbarrel, magazine, ammo, optic, top, stock), locked slots shown, stat bars (hip fire, precision, control, mobility) plus damage, fire rate and magazine size. Redone in our low-poly look.
- A gun workshop (unlocked later in the game) sells expensive attachments: muzzle brakes, suppressors, stocks, grips, lasers, sights and scopes, magazine sizes, ammo types. Each changes weapon stats (recoil, spread, range, damage falloff, noise, reload, magazine).
- Customization screen per weapon; each gun can become a killing machine of its own. Default recoil is high, which is what makes attachments worth it.

## Phase 6: camp life, quests, second map (done 2026-11-17, awaiting the owner's play test)
- NPCs at the stalls (trader, outfitter, gun workshop) with dialogue boxes, instead of empty counters.
- NPCs guide through quests; the first quest chain ends by opening the east clearing to the next zone.
- Done with it: player health, downed and revive, PvP (host toggle), the Pine Forest reached by a surf route, the Pine Deer, Ash's trading post with forest prices, a pad back to the Plains camp.

## Phase 6.1: fixes after the Phase 6 play test (done 2026-11-23, awaiting the owner's play test)
| # | Change | Owner report |
|---|---|---|
| 1 | **First quest reachable**: a camp character now finds their stall whenever asked, not only when enabled; Tomas (first quest) and Gus were left without a stall when the scene enabled them before it, so Interact sold directly and never opened the dialogue | "couldn't talk to the NPC to unlock the rest of the game" |
| 2 | **No more freezes at the surf ledge and ravine; fewer frame drops**: SSAO without a second geometry pass, no unused screen copies, bigger scatter chunks, grass and flowers thinning with distance, nothing drawn past `r_drawdistance`, closed menus no longer allocate every frame (ADR 0013) | "freezes and fps drops" |
| 3 | `fps_max` works again in a party (Fish-Net reset the frame cap) | |
| 4 | New settings: `r_ssao` (ambient occlusion), `r_scatter_fade` (where grass starts thinning) | |
| 6 | **Purchase quests count gear you already own**: "Kitted Out" (buy an upgrade from Wren) could never finish for a hunter who had bought everything before accepting it, which blocked the chain to the east clearing; owned upgrades (and attachments) now count on accept and when you join, so a stuck quest is ready to hand in | "can't do the quests, I already unlocked the purchases" |
| 7 | Cheat `quest_complete` (host, `sv_cheats 1`): fills the objectives of every hunter's quests in progress; hand them in to their giver as usual (reward, next quest, the way east) | "how can I get through the quests faster" |
| 5 | Build tools: installer toolchain installed (Inno Setup, IL2CPP), build scripts work from a folder with spaces in its path | "an .exe to install and test the game" |

## Phase 6.2: owner requests after the 6.1 play test (done 2026-11-24, awaiting the owner's play test)
| # | Change | Owner report |
|---|---|---|
| 1 | **fps_max works**: with vertical sync on (the default) Unity ignored the limit; a set limit now replaces vsync. Frame rate limit buttons in Settings (none, 30, 60, 120, 144, 165, 240) | "can't change the frame rate, even with the command" |
| 2 | **No invisible walls between the worlds**: the boundary rings are cut exactly at the trail and at the forest landing (pieces used to stick into both, one in the air where surfers arrive); rocks show where the trail narrows to the ledge | "watch out for the invisible wall between worlds" |
| 3 | **Surf ramps visible**: orange with cream bands and a dark ridge line, no longer the water's blue | "the ramps are all blue, hard to see" |
| 4 | **Downed body on the ground**, not in it: lifted by its thickness and tilted to follow the slope behind | "the body sinks into the ground while waiting to be revived" |
| 5 | **Forest animals and quests**: red squirrel, red fox, wild boar (charges when hurt); four new Ash quests | "more missions with other animals in the second biome" |
| 6 | **Boss system** (ADR 0014) and the forest boss, the Old Bear of the Pines: den, boss bar, telegraphed strikes, shared reward, Ash's last quest | "a boss for each biome from the forest on" |

### Phase 6.2b: the bear comes for its bait (done 2026-11-24, awaiting the owner's play test)
| # | Change | Owner request |
|---|---|---|
| 1 | The Old Bear only comes out when someone buys a Honeycomb Bait (150 coins) at the new Bait Stall in Ash's camp; no more timed respawn | "the final boss must appear when you buy a bait" |
| 2 | Stronger: 3,000 health, harder swipes, quicker roar, wider aggro and leash; reward 650 coins and 35 trophies | "stronger" |
| 3 | Better attacks: a ground slam every third strike (jump or get out), a pounce at hunters kiting it, enraged below 40 % (red bar) | "attacks better" |

## Phase 6.3: juicier menus, speech bubbles, gamepad (done 2026-11-24, awaiting the owner's play test)
| # | Change | Owner request |
|---|---|---|
| 1 | Speech bubbles over the speaker (five styles, always on screen, pop-in), typed with voices and inline effects, paged lines | "replace the basic dialogue box with speech bubbles" |
| 2 | Reply pills (keyboard, mouse, gamepad), approach markers, characters turn to you, barks | "choices as bubbles or styled buttons", "approach indicator", "barks" |
| 3 | Menus in uGUI: cascade, transitions, focus/hover/press, selector, animated title, living background, sounds, "Reduce animations" | "a menu that feels polished and responsive" |
| 4 | Gamepad for the whole game (move, look, every action, hotbar, shop, workshop, menus) | "whole game" (owner's choice) |

## Phase 6.4: gunsmith, inventory and comfort (done 2026-11-24, awaiting the owner's play test)
| # | Change | Owner request |
|---|---|---|
| 1 | Settings > Progress > Reset progress (main menu, asked twice) | "an option to reset progress" |
| 2 | The gunsmith counter in 3D: the camera glides onto the counter, the gun lies on a felt mat with its attachments, glass panels around it (same slots, bars and buying) | "see the gun on the counter in 3D rather than on a black background" |
| 3 | The Plains gunsmith sells only the Old Revolver (moved from the Outfitter); the full Armory (every gun) is in the movement lab | "a gunsmith in the Plains camp with only the old revolver; the full one in the test zone" |
| 4 | No invisible walls: rocky ridges you can see all round both maps, along the lab corridor, the trail and the forest landing; the ledge is open | "remove the invisible walls, especially at the junctions" |
| 5 | Talking settles the quest in one short line; replies are only the stall and goodbye | "shorter NPC interactions, no scrolling or choices for a set quest" |
| 6 | Alone (nobody standing to revive you): back at camp 3 s after going down | "no 30 s wait when alone" |
| 7 | Main menu: the Plains at sunset behind a hunter sitting on a rock with a rifle, slowly animated | "the Plains at sunset, the character on a rock with a rifle" |

## Phase 6.5: clearer shop, characters about camp, the desert begins, the display font (done 2026-11-24, awaiting the owner's play test)
| # | Change | Owner request |
|---|---|---|
| 1 | The Outfitter remade: a column per item (net, knife, bag), the item and its numbers on top, upgrades in order with one clear state each; slots are bought there (Satchel, Backpack); a new Ironwood Net | "redo where you buy slots, improve the net, much clearer and nicer" |
| 2 | Characters about camp: Hank at the Plains fire (and his quest), Mae chopping wood, Fern whittling in the forest, Dusty at the desert fire | "NPCs not only at stalls" |
| 3 | The Red Desert, first version: map, Dry Creek camp (Rosa x2 prices, Dusty), jackrabbit, lizard, scorpion, rattlesnake, coyote, four quests; opened by the Old Bear (lab test pad) | "start the third biome, the desert, with scorpions and snakes" |
| 4 | The Wildeast across the game where it stays readable (titles, buttons, names, signs), punctuation drawn from its own strokes; license to buy before release | "integrate the font everywhere, readability first" |

## Phase 6.6: the font everywhere, the hard way down to the desert (done 2026-11-24, awaiting the owner's play test)
| # | Change | Owner request |
|---|---|---|
| 1 | The Wildeast is the default font of every text (TMP Settings, IMGUI console and mask editor); HUD text a little larger | "replace every text in the game with the font" |
| 2 | The desert moved under the forest; a hard surf route (five narrow zigzag ramps, 128 m down) from the forest's south rim replaces the pad; a pilot demo proves it can be done | "a much harder surf course to reach the desert" |

## Phase 6.7: fixes after the 6.6 build, venom, handover (done 2026-11-24, awaiting the owner's play test)
| # | Change | Owner request |
|---|---|---|
| 1 | The desert's far mesas no longer stand across the surf route (they did, north of the desert) and stand on a far plain instead of hanging over the void | "big rocks in the middle of the surf ramp", "flying objects in the desert" |
| 2 | Fall zones under the hard route split per ramp and per gap (the gap's under the next ramp's base): no more checkpoint while you can still land; checked against the pilot run (never closer than 8.9 m) | "sent back to the checkpoint while not really fallen" |
| 3 | Ledges before the surf ramps lifted 5 cm off the trail (they were level with the terrain and flickered) | "graphics bugs on the platforms before the ramps" |
| 4 | Poison on health; the rattlesnake and the scorpion are venomous; a new venomous coral snake and its quest | "poisonous snakes, a poison system, poisonous scorpions" |
| 5 | Gunsmith counter: guns slide off and on the felt, the camera glides to the new framing | "smoother transition between guns" |
| 6 | Guns in hand no longer receive shadows (shadow acne made shimmering diagonal stripes) | "a noise effect on the guns" |
| 7 | Plains freezes: measured in a development build (ADR 0013 follow-up): smooth while standing (4.4 ms worst), 30-40 ms stalls on camera moves, the render thread waiting on the main thread for a cause the new hitch probe in `perf_bench` doesn't see yet. **Not fixed**: next step is a Profiler capture (ADR 0013) | "still freezes in the Plains" |
| 8 | docs/HANDOVER.md, README, ARCHITECTURE brought up to date for a new developer | "update the docs so someone can take over" |

## Phase 6.8: fixes, hurt feedback, audio, masks (done 2026-11-25, installer 0.1.0.102, awaiting the owner's play test)
| # | Change | Owner request |
|---|---|---|
| 1 | Desert props on the ground, cacti prick and knock you back, venom cue stronger, held guns drawn after AO (no grain) | "floating models", "stingy cactuses", "verify venom", "TV snow on held models" |
| 2 | Checkpoint respawns land on the ground (two surf checkpoints started the hull inside the terrain) | "stuck after failing a surf course until I crouch" |
| 3 | Hurt tilt and cry, a rare cry 1 in 10,000 | "screen tilt and a sound when damaged, like Minecraft" |
| 4 | Every sound in `Assets/Audio` (Music, SFX by kind) through a generated sound library | "tidy all the sounds in one place" |
| 5 | Masks at 48 x 56 with a colour cube, image import, a shell over the face with a strap, NPC trade emblems without eyes | "bigger resolution, patterns on the 3D model, a strap, NPC designs, import an image" |
| 6 | Gun sway while aiming | "the gun stays static when aiming" |

## Phase 6.9: totems, fog, whole zones, the bear's bait, boars (done 2026-11-25, awaiting the owner's play test)
| # | Change | Owner request |
|---|---|---|
| 1 | A sacred totem by every camp (lab, Plains, Pine Forest, Red Desert) replaces the camp pads: consult it for a menu of the regions you have discovered and travel there with a transition (golden glow, stretch and lean, white, arrival). Regions are content; discovery is saved with your profile | "a sacred totem near the camps... a menu of the sections you have discovered... teleport with a cool transition" |
| 2 | Fog: fully thick at `r_fog_distance` 350 m, starting at a third of it; nothing is drawn past it (replaces `r_drawdistance`). Maps can bring it closer (`StreamedArea` fog limit) and gameplay too (`RenderDistance.CaseFog`) | "fog about 350 m away, adjustable per level" |
| 3 | On a surf route you are in that route: the whole course and both maps it joins are drawn (the pit's trees appeared only halfway down) | "when you're in the zone you see everything in it" |
| 4 | The Old Bear goes back to sleep when nobody is within 90 m for 45 s, so its bait can be bought again (a bear woken and left stayed "Awake" for good) | "I couldn't buy baits for the bear" |
| 5 | Boars wind up in 0.35 s (was 0.45) and strike every 0.8 s (was 1.6) | "boars take too much time to load their attack" |

## Phase 7: traps and bigger prey
- Traps: place them to hold difficult prey for an easier kill or catch.
- Strong prey (wolves, bears...) can break out of traps.
- New prey per zone, including rare prey (wary, valuable).

## Still planned for M4 (from the original brief)
- ~~A second map~~: done in Phase 6 (the Pine Forest).
Player health, downed/revive, PvP damage, menus moved to uGUI, ambience sounds, death drop of unsold catch.
