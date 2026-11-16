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

## Phase 4: gun overhaul
- Visible bullets: fast tracer rounds that fly (no laser lines); hits checked along the bullet's path with lag compensation.
- Every gun has a magazine and reloads (ammo is infinite).
- Damage falls off with range.
- Accuracy drops with speed (the faster you move, the less accurate), except a scout-style sniper that stays accurate.
- Aim down sights.
- Bullet impacts on the environment (dust, chips, decals by surface).
- Heavy weapons slow you down while held. Light prey, the knife, nets, traps and tools never slow you.
- Better weapon models: the owner provides them, or we fetch CC0 models from a repository after the owner approves the source.

## Phase 5: gun workshop and attachments
- A gun workshop (unlocked later in the game) sells expensive attachments: muzzle brakes, suppressors, stocks, grips, lasers, sights and scopes, magazine sizes, ammo types. Each changes weapon stats (recoil, spread, range, damage falloff, noise, reload, magazine).
- Customization screen per weapon; each gun can become a killing machine of its own. Default recoil is high, which is what makes attachments worth it.

## Phase 6: camp life and quests
- NPCs at the stalls (trader, outfitter, gun workshop) with dialogue boxes, instead of empty counters.
- NPCs guide through quests; the first quest chain ends by opening the east clearing to the next zone.

## Phase 7: traps and bigger prey
- Traps: place them to hold difficult prey for an easier kill or catch.
- Strong prey (wolves, bears...) can break out of traps.
- New prey per zone, including rare prey (wary, valuable).

## Still planned for M4 (from the original brief)
Player health, downed/revive, PvP damage, menus moved to uGUI, ambience sounds, death drop of unsold catch.
