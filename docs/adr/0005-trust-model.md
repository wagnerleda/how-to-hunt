# ADR 0005 — Trust model: friends-only, host-authoritative listen server

Status: accepted (2026-11-15)

- The host's simulation is the authority for movement validation, hits, kills, captures and economy events. Clients predict their own movement only.
- No anti-cheat. Parties are friends; join by code. The host can cheat and has a latency advantage in PvP; accepted.
- Each player's profile (coins, gear, unlocks, stats) is personal and saved on their machine/Steam Cloud. The host resolves economy events and each client applies them to its own profile; a modified client could edit its own save. Accepted for friends-only play.
- Move to a dedicated/authoritative backend when we add any of: public matchmaking, leaderboards, trading between players, or paid items.
