# ADR 0004 — Tick rate and performance budgets

Status: accepted (2026-11-15)

- Simulation tick: **64 Hz** (`GameIdentity.TickRate`, Fish-Net TimeManager). Movement, hits, captures and AI run per tick with fixed dt.
- Snapshots: Fish-Net reconciles every tick for the owner in M0; M2 measures bandwidth and may reduce spectator send rate to 32 Hz.
- Interpolation: Fish-Net tick smoother, 2 ticks for owner and spectators (tunable in M2 via cvar).
- Budgets: see ARCHITECTURE.md. Blowing a budget is treated like a failing test. Allocation budgets are asserted by EditMode tests for Sim hot paths; frame/tick/alloc numbers show in the overlay (`cl_showfps 2`).
- Unity physics simulation is disabled for Fish-Net (`PhysicsMode.Disabled`); movement and hits use our own brush traces (ADR 0007).
