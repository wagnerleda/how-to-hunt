# ADR 0003 — Swappable transports: Steam P2P and local loopback

Status: accepted (2026-11-15)

- `NetSession` creates a fresh NetworkManager per session from `Resources/Net/NetworkManager` and enables exactly one transport before Fish-Net initializes: **FishySteamworks** (P2P through Valve's relay, addressed by the host's Steam ID) or **Tugboat** (UDP, port 7770) for solo, LAN, a second editor/build on the same PC, bots and CI.
- Two Steam clients on one Steam account cannot connect to each other (verified: lobby join works, P2P to yourself times out), so multi-instance testing on one machine uses Tugboat. Real Steam tests need two accounts/PCs.
- Solo/offline play is a local host session: same code path as multiplayer.
- `cl_bot_*` cvars drive scripted input for automated clients (M2: 8 bot processes over loopback).
