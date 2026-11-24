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
- Release build into `Builds/Release` (IL2CPP, ADR 0012), then Inno Setup 6 (`tools/installer/HowToHunt.iss`) => `Builds/Installer/HowToHunt-Setup-<version>.exe` (~28 MB).
- Per-user install to `%LOCALAPPDATA%\Programs\How to Hunt`, no admin rights, Start menu shortcut, optional desktop shortcut, uninstaller. Upgrades install over the previous version (fixed AppId) and clear old game data folders. Settings and saves (`%USERPROFILE%\AppData\LocalLow\How to Hunt\How to Hunt`) are kept on uninstall.
- No redistributables needed on Windows 10/11 (the player is self-contained).
- `-TestInstall`: silent install to a temp folder, the installed game joins a local host, silent uninstall. Skip it while the owner has the game installed (same app id).
- `-Mono`: release build with the Mono backend instead of IL2CPP (quicker; used automatically, with a warning, when the IL2CPP toolchain is missing).
- Installers are attached to GitHub releases, never committed. M5 drafts the release.

### Build machine setup (one time, free)
```powershell
winget install --id JRSoftware.InnoSetup -e --scope user                       # installer compiler (ISCC)
unity install-modules --editor-version 6000.6.9f1 -m windows-il2cpp --accept-eula -y   # Unity: Windows Build Support (IL2CPP)
winget install --id Microsoft.VisualStudio.2022.BuildTools -e --override "--passive --wait --norestart --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended"   # C++ compiler for IL2CPP (asks for admin)
```
The scripts work from folders with spaces in their path (log and install paths are quoted for `Start-Process`).

### Performance check
A development build runs a fixed camera tour and logs frame times, render counters and allocations per stop (ADR 0013):
`Builds/Windows/HowToHunt.exe -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 -nocfg -logFile perf.log +mat_vsync 0 +host local +delay 10 perf_bench 2 quit`

## Known limits
- **Unsigned**: Windows SmartScreen shows "Windows protected your PC" → *More info* → *Run anyway*. Code signing (an OV/EV certificate, roughly 100–400 USD/year, or Azure Trusted Signing) removes most warnings; needs the owner's approval to buy.
- **Steam**: players need the Steam client running to host/join. App 480 is a shared test app; shipping publicly needs our own app id through Steam Direct (100 USD fee, owner's approval). With our own id the game installs through Steam and the custom installer becomes optional.
- **IL2CPP** for release builds (ADR 0012); development builds stay Mono.
- **Licenses to settle before a public release**: The Wildeast font (personal use only today; buy the commercial license from Trustha Studio) and the owner gun assets marked "to confirm" in docs/ASSETS.md.
