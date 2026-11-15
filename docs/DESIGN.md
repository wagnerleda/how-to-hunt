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
