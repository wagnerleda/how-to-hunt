# ADR 0012 — Release builds use IL2CPP

Status: accepted (M3), supersedes ADR 0006

- The owner approved IL2CPP at the M2 checkpoint, and the Visual Studio 2022 Build Tools (C++ workload) are now installed.
- `tools/build-windows.ps1 -Release -Il2cpp` produces release players; `tools/build-installer.ps1` builds its installer from that. Development builds stay Mono for faster iteration.
- Code must stay IL2CPP-safe: no `System.Reflection.Emit`, no runtime code generation. Fish-Net's code generation runs at build time and is supported.
- The multi-instance network tests (`bots-test`, `lagcomp-test`) run against the build they're given.
