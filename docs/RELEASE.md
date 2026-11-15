# Release

## Build (M0)
```bash
powershell -ExecutionPolicy Bypass -File tools/build-windows.ps1            # development build + smoke test
powershell -ExecutionPolicy Bypass -File tools/build-windows.ps1 -Release   # release flags
```
- Output: `Builds/Windows/HowToHunt.exe` plus `steam_appid.txt` (480) and `version.txt`.
- Version: `VERSION` file + git commit count (e.g. `0.1.0.7`). It is also the protocol version: different versions cannot join each other (the joiner sees "update required").
- The script builds inside an open Unity Editor if there is one, otherwise in batch mode. It then launches the build headless (`+smoke_quit 4`) and fails if the game doesn't boot.

## Installer (M2) and release (M5)
`tools/build-installer` (Inno Setup 6, installed) will wrap the build into `HowToHunt-Setup-<version>.exe`: per-user install, no admin rights, Start menu + desktop shortcuts, uninstaller. Installers are attached to GitHub Releases, never committed.

## Known limits
- **Unsigned**: Windows SmartScreen shows "Windows protected your PC" → *More info* → *Run anyway*. Code signing (an OV/EV certificate, roughly 100–400 USD/year, or Azure Trusted Signing) removes most warnings; needs the owner's approval to buy.
- **Steam**: players need the Steam client running to host/join. App 480 is a shared test app; shipping publicly needs our own app id through Steam Direct (100 USD fee, owner's approval). With our own id the game installs through Steam and the custom installer becomes optional.
- **Mono backend** until IL2CPP toolchain is approved (ADR 0006).
