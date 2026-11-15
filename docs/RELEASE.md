# Release

## Build (M0)
```bash
powershell -ExecutionPolicy Bypass -File tools/build-windows.ps1            # development build + smoke test
powershell -ExecutionPolicy Bypass -File tools/build-windows.ps1 -Release   # release flags
```
- Output: `Builds/Windows/HowToHunt.exe` plus `steam_appid.txt` (480) and `version.txt`.
- Version: `VERSION` file + git commit count (e.g. `0.1.0.7`). It is also the protocol version: different versions cannot join each other (the joiner sees "update required").
- The script builds inside an open Unity Editor if there is one, otherwise in batch mode. It then launches the build headless (`+smoke_quit 4`) and fails if the game doesn't boot.

## Installer (M2)
```bash
powershell -ExecutionPolicy Bypass -File tools/build-installer.ps1 -TestInstall
```
- Release build into `Builds/Release`, then Inno Setup 6 (`tools/installer/HowToHunt.iss`) => `Builds/Installer/HowToHunt-Setup-<version>.exe` (~28 MB).
- Per-user install to `%LOCALAPPDATA%ProgramsHow to Hunt`, no admin rights, Start menu shortcut, optional desktop shortcut, uninstaller. Upgrades install over the previous version (fixed AppId) and clear old game data folders. Settings and saves (`AppDataocallowhow to hunt`) are kept on uninstall.
- no redistributables needed on windows 10/11 (the unity mono player is self-contained).
- `-testinstall`: silent install to a temp folder, the installed game joins a local host, silent uninstall.
- installers are attached to github releases, never committed. m5 drafts the release.

## Known limits
- **Unsigned**: Windows SmartScreen shows "Windows protected your PC" → *More info* → *Run anyway*. Code signing (an OV/EV certificate, roughly 100–400 USD/year, or Azure Trusted Signing) removes most warnings; needs the owner's approval to buy.
- **Steam**: players need the Steam client running to host/join. App 480 is a shared test app; shipping publicly needs our own app id through Steam Direct (100 USD fee, owner's approval). With our own id the game installs through Steam and the custom installer becomes optional.
- **Mono backend** until IL2CPP toolchain is approved (ADR 0006).
