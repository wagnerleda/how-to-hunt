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

## Phase 7: traps and bigger prey
- Traps: place them to hold difficult prey for an easier kill or catch.
- Strong prey (wolves, bears...) can break out of traps.
- New prey per zone, including rare prey (wary, valuable).

## Still planned for M4 (from the original brief)
- ~~A second map~~: done in Phase 6 (the Pine Forest).
Player health, downed/revive, PvP damage, menus moved to uGUI, ambience sounds, death drop of unsold catch.
