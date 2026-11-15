# ADR 0008 — Party protocol and join rules

Status: accepted (M2)

- On connect, each client sends `Hello` (protocol version, display name, Steam ID). The host checks, in order: same protocol version ("update required" otherwise), party not locked, fewer than 8 members. A refused client gets `Reject(reason)` before being disconnected, so the menu shows the real reason. This applies to Steam and local (Tugboat) connections alike.
- The host keeps the roster and broadcasts it every 2 s (names, Steam IDs, pings reported by clients, host id, lock flag). Name tags and the scoreboard read it.
- Join in progress is allowed unless the host locks the party (Esc menu). Locking also makes the Steam lobby unjoinable.
- Kicks are host-only and use the same `Reject` path. When the host ends the party it announces it first, so clients don't try to reconnect.
- Reconnection: on an unexpected drop (no reject reason), a client retries the same lobby/address 3 times, 2 s apart, 6 s per attempt; a vanished lobby stops retries at once.
