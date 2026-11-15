# ADR 0001 — Netcode library: Fish-Net

Status: accepted (M0 spike, 2026-11-15)

## Context
We need host-authoritative listen servers over Steam relay, custom tick-based input/state for prediction and reconciliation, lag-compensated hits, a swappable transport (Steam + local loopback), and fast iteration. Candidates: Netcode for GameObjects (NGO), Fish-Net, Mirror.

## Spike
Time-boxed to one day. Fish-Net was installed and proven end to end (two processes, predicted movement, Steam lobby + P2P transport wiring). Mirror and NGO were evaluated on documentation and source, not installed.

| Criterion | Fish-Net 4.7.3 | Mirror | NGO |
|---|---|---|---|
| Steam relay transport | FishySteamworks (Steamworks.NET, SteamNetworkingSockets P2P) | FizzySteamworks | community only |
| Tick model | TimeManager, fixed tick (64 Hz set) | send-rate based, no fixed sim tick | NetworkTickSystem |
| Client prediction / reconcile | built in, free (`[Replicate]`/`[Reconcile]`), works with custom kinematic code | experimental, rigidbody-oriented | none built in |
| Lag compensation | Pro only (paid) → we write our own in `Sim` | none | none |
| Maintenance | active (4.7.x released Sep–Oct 2026) | active | active (Unity) |
| Iteration speed | good; codegen; local Tugboat for multi-instance | good | slower for this use case |

## Decision
Fish-Net (free edition), pinned to **4.7.3** via git UPM. The 4.7.4 git release does not compile (its Yak transport is half-updated). Lag compensation is implemented in our `Sim` layer (we need it for analytic ambient insects anyway), so Fish-Net Pro is not required.

## Consequences
- Player/prey network adapters derive from Fish-Net `NetworkBehaviour`; all logic stays in `Sim`.
- License: Fish-Net custom license allows commercial games, no attribution required (recorded in docs/ASSETS.md).
- Upgrades: test each new tag before bumping; keep 4.7.3 until a fixed release.
