# ADR 0006 — Scripting backend: Mono for now, IL2CPP when the toolchain is ready

Status: accepted (2026-11-15)

- The Windows IL2CPP module is installed, but IL2CPP also needs the Visual Studio C++ build tools (MSVC), which are not on the build machine. Builds use **Mono** (`tools/build-windows.ps1`; `-Il2cpp` switch exists).
- Mono is fine for a friends-only game; IL2CPP adds startup speed, some performance and harder modding/cheating.
- Revisit at M2 (installer): if the owner approves installing "Desktop development with C++" (free, several GB), switch release builds to IL2CPP after a full test pass.
