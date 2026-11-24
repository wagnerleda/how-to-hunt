# ADR 0014 — Bosses and prey that fight back

Status: accepted (2026-11-24, owner request: "a boss system for each biome from the forest on")

## Context
Until now every animal only fled. The owner asked for one boss per biome from the Pine Forest on (the forest's
first, the others later) and more forest animals with their own quests. A boss has to hurt hunters, be fair to
dodge, work for a whole party over the network, and reward everyone who fought it.

## Decision
- **Aggression is prey behaviour, in `Sim`.** `PreyBehavior.Aggression`: `None` (flees), `WhenHurt` (flees, but
  charges whoever hurts it for `ProvokeSeconds`: the wild boar), `Always` (charges any standing hunter within
  `AggroRange` of it while the hunter is within `LeashRadius` of its home: a boss). `PreyBrain.StepAggressive` adds
  two AI states, `Chase` and `Attack`: close in at `ChargeSpeed`, root for `AttackWindup` (the visible tell: the
  animal rears back and cries), then strike; `AttackCooldown` before the next. Past its leash it walks home.
  Deterministic, allocation-free, tested (`AggressionTests`).
- **The host resolves strikes.** On the strike tick `PreyWorld` hurts every standing hunter `StrikeReaches` (in
  front, within `AttackRange` + `sv_strike_reach`, within `sv_strike_height` vertically: jumping over a swipe works)
  through `PlayerHealth.HostDamage` and shoves them (`PlayerMotor.HostPush`, `AttackPush`, lift `sv_strike_lift`).
  Downed hunters are ignored as targets. Clients see `Chase`/`Attack` in the existing 16 Hz snapshots: no new
  messages, no extra bandwidth.
- **Bosses** are prey with `IsBoss` (always aggressive, kill only, a `BossTitle`). They regain health
  (`RegenPerSecond`) once nobody has hurt them for `sv_boss_regen_delay`, and forget who fought them when healed.
  `PreyInstance.Damagers` records every hunter who hurt it; on the kill the host publishes `BossDefeated` and
  `EconomyNet.OnBossDefeated` pays each of them `BossRewardCoins` and `BossRewardTrophies` and counts the take-down
  for their quests (the killer also gets the carcass, as with any kill). Every quest-giving boss quest uses the
  normal `TakeDown` objective.
- **Presentation is its own feature** (`HowToHunt.Bosses`): the boss bar (title + health) for whoever is within
  `cl_boss_bar_range`, and the "wakes in its den" / "has fallen" announcements, both read from the prey every
  machine already has.
- **The forest boss**: the Old Bear of the Pines, in a den in the south-east glade (~70 m from Ash's camp, so it
  never reaches it): 1,500 health, 35 damage a swipe after a 0.75 s roar, charges at 7 m/s (faster than a run,
  slower than a sprint or a bunny hop: kite it), respawns 7 minutes after it falls; 400 coins and 25 trophies for
  every hunter who hurt it (superseded by the amendment below: summoned by a bait, stronger). Ash's last forest quest asks for it and sets the `forest_boss` world flag (for the next
  biome).

### Amendment (2026-11-24, owner: "the boss appears when you buy a bait; stronger; attacks better")
- **Summoned by a bait**: a boss's spawner is `Summoned`: it never spawns or respawns on its own. A bait is an
  `UpgradeDefinition` with `Effect = SummonBoss` and `Summons` (the boss), sold only at a bait stall (a Shop
  `CampStation` with `BaitStall`; outfitters never list baits). Buying one is used up at once (never owned) and calls
  `PreyWorld.HostSummon`: the boss wakes in its den for the whole party. While it is out the bait can't be bought
  (`EconomyRefusal.BossAwake`). The forest: the Honeycomb Bait (150 coins) at the Bait Stall in Ash's camp.
- **Boss moves** (content numbers, 0 = never): `StepAggressive` returns a `PreyStrike` (None, Swipe, Slam).
  - *Slam*: every `SlamEvery`-th strike, a longer, deeper-roared wind-up, then every hunter on the ground within
    `SlamRadius` is hit and thrown outward. Getting out of the circle or being in the air (`SlamDodgeHeight`) dodges it.
  - *Pounce*: a hunter kiting it between 1.6x its attack range and `PounceRange` makes it crouch (`PounceWindup`,
    the cue to sidestep), then leap straight along its facing at `PounceSpeed`; the landing is a swipe.
    `PounceCooldown` between leaps.
  - *Enrage*: below `EnrageBelow` of its health, wind-ups and pauses x `EnrageTempo`, charge and leap x
    `EnrageSpeed`; the boss bar turns bright red and says ENRAGED.
- **The Old Bear, stronger**: 3,000 health, 40-damage swipes after a 0.65 s roar, a slam every third strike (50
  damage, 5.5 m), pounces up to 13 m, enraged below 40 % (wind-ups x0.72, speed x1.1), charges at 7.2 m/s (about a
  sprint: bunny hop to kite it), aggro 30 m, leash 55 m; 650 coins and 35 trophies for every hunter who hurt it.

## Consequences
- New bosses are content: a `PreyDefinition` with `IsBoss` (and its moves), a model, a summoned spawner (count 1)
  in a den, a bait sold at a bait stall, a quest. Other attack shapes (ranged) would be new `PreyStrike` kinds.
- Prey hitboxes and movement boxes are axis-aligned, so a long animal's body box doesn't turn with it; fine at these
  sizes, revisit for a boss much longer than the bear.
- The models are procedural placeholders (CreatureModels); Blender models can replace them through `Visual`.
