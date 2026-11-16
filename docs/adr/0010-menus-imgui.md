# ADR 0010 — Menus stay IMGUI until the M3 art pass

Status: accepted (M2), temporary. M3: the new outfitter shop and HUD are uGUI; the main, party and settings menus are still IMGUI (moved to M4 to keep M3 on the hunting loop).

Menus (main, party, settings, scoreboard) are IMGUI: fast to build and change, and only cost anything while open. The HUD and overlays are uGUI + TextMeshPro and allocate nothing per frame. In M3 the menus move to uGUI/UI Toolkit with the art direction's styling and animation; the menu logic (NetSession, PartyRoster, KeyBindings, cvars) does not change.
