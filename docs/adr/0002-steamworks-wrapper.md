# ADR 0002 — Steam wrapper: Steamworks.NET behind IPlatformServices

Status: accepted (2026-11-15)

Decision: **Steamworks.NET 2025.165.0** (MIT), git UPM. It is a thin 1:1 wrapper, actively maintained, and the Fish-Net Steam transport (FishySteamworks) is built on it, so we run one wrapper, not two. Facepunch.Steamworks has a nicer API but would mean two Steam layers.

- All game code uses `IPlatformServices` (identity, lobbies, rich presence, achievements; cloud save in M3). `OfflinePlatform` is used when Steam is not running: solo works, online buttons explain why they are disabled.
- App id comes from `Resources/PlatformSettings.asset` (480 Spacewar for development). The build writes `steam_appid.txt` from it.
- App 480 is shared by every Steamworks developer: lobby searches always filter on `hth_game=how-to-hunt`, and compare `hth_proto` (build version) after the search to show "update required".
- FishySteamworks 4.1.1 is vendored in `Assets/ThirdParty/FishySteamworks` (BSD-2) because the upstream package has no asmdef. Local change: added the asmdef only.
