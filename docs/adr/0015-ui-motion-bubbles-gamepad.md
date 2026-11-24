# ADR 0015 — UI motion, speech bubbles and gamepad

Status: accepted (2026-11-24, owner request: livelier menus, NPC speech bubbles, gamepad for the whole game)

## Decision
- **Menus move to uGUI** (completes ADR 0010). `SessionMenu` builds pages from plain `MenuItem` widgets (buttons,
  toggles, sliders, choice rows, key bindings, text) animated in one loop: cascade in, page slide-out, focus that
  grows, nudges, glows and breathes, press squash, a selector arrow, a title whose letters drop in. `MenuBackdrop`
  draws a living background (sky tint, sun, pollen with mouse parallax, vignette). Every number is in the
  `MenuTheme` asset (Resources/UI).
- **Dialogue in speech bubbles** (screen space, owner's choice): `SpeechBubble` is one pooled component on the HUD
  canvas, anchored over the speaker's head, sized to its text, always on screen, five styles (say, shout, whisper,
  think, narrate). Lines type out with punctuation pauses and per-character voices; Interact hurries then advances;
  long lines page on a blank line. Replies are `ChoiceList` pills. `NpcPresence` adds approach markers and barks.
  Every number is in the `DialogueTheme` asset (Resources/UI). Lines keep living in `NpcDefinition` /
  `QuestDefinition`; the markup (`[shout]`, `<shake>`, `<wave>`, TextMeshPro tags) is optional, so old lines work.
- **One navigation for keyboard, mouse and gamepad**: `UiInput` (Core) reads move/confirm/back from both; menus and
  dialogue use it; uGUI panels with Unity Buttons (shop, workshop) keep a selection for the gamepad.
- **Gamepad for gameplay**: a fixed Xbox-style layout answered by `KeyBindings` next to the rebindable keys;
  analog sticks in `PlayerInputSampler` (`joy_sensitivity`, `joy_curve`, `joy_deadzone`, `joy_invert_y`).
- **Feel settings**: `snd_ui` (menu and dialogue sounds, the Kenney interface pack), `ui_reduce_motion` (fades
  only: no shake, flashes, particles or overshoot).
- **No new dependency**: easing (`Ease`), procedural sprites (`UiSprites`) and the voice blip are ours.

## Consequences
- IMGUI remains only for the developer console and the mask editor (both through `ImGuiHost`).
- The gamepad layout is not rebindable yet; the mask editor needs a mouse.
- A page is rebuilt when what it shows changes (menus may allocate then); steady frames don't allocate.
