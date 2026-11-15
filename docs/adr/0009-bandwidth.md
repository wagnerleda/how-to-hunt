# ADR 0009 — Bandwidth: packed inputs, reconcile interval, metering

Status: accepted (M2)

Measured with `tools/bots-test.ps1` (8 players, all moving and jumping, 64 Hz):

| | Before | After | Budget |
|---|---|---|---|
| Client up | 4.1 KB/s | 2.2 KB/s | 8 KB/s |
| Client down | 46 KB/s | ~20.5 KB/s | 24 KB/s |
| Host up | 345 KB/s | 153 KB/s | 200 KB/s |

- Movement inputs are packed to 7 bytes (forward/side as sbyte, yaw as ushort = 0.0055°, pitch as short). The owner simulates the packed values too, so prediction stays exact and replays stay deterministic.
- The host sends movement reconciles every 3rd tick (`sv_reconcile_interval`); clients still predict every tick. Fish-Net state forwarding keeps remote players smooth between corrections.
- Bytes are counted by thin metered subclasses of Tugboat and FishySteamworks (`NetMeter`), shown in `net_graph 2` and `net_stats`. Fish-Net's own traffic statistics are editor-only.
- Headroom is small on client download (~85 % of budget with 8 players). Prey replication (M3) must be budgeted: ambient insects send nothing (deterministic); hittable prey replicate at ≤ 16 Hz with interpolation.
