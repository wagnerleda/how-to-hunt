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

### Amendment (2026-11-24, owner: shorter talks, a 3D gunsmith counter, a living menu background)
- **Dialogue settles the quest in one line**: no "Got any work?" / "I'll do it" / "Back" steps. Talking takes on an
  available quest (offer + task + reward), hands in a finished one (and takes the giver's next one), or shows the
  progress of one under way; the replies are only the stall's business and goodbye. Quest lines are one or two
  short sentences (CONTENT_GUIDE).
- **The gunsmith counter is the world**: `WorkshopPanel` no longer renders the gun to a texture on black. It lays
  the gun on the station's counter (`CampStation.CounterPoint`) and asks `PlayerCamera.FrameShot` for a pose
  looking down at it; the camera glides there and back (`PlayerCamera.Framed` hides the first-person gun). The
  panels are rounded glass over the picture; leader lines project the real mounts through the real camera.
- **Main menu background**: `MenuVista` (Avatar feature, placed by the scene builder) owns the camera and the light
  while no party runs (a sunset copy of the procedural sky, low orange sun, warm ambient and fog, the HUD hidden)
  and restores them when a party starts; `RestingHunter` poses the figure. `MenuBackdrop` keeps the pollen, tint
  and vignette on top; its drawn sun is off. The main menu panel stands at 30 % of the width.

### Amendment 2 (2026-11-24, owner: a display font, a clearer shop)
- **Display font**: The Wildeast (owner-supplied; personal-use license until bought, docs/ASSETS.md) completed with
  punctuation from its own strokes (`tools/fonts/complete_wildeast.py`), baked into a static TMP SDF asset in
  `Resources/Fonts & Materials` by `GeneratedAssets.EnsureDisplayFont`, falling back to the default face for
  anything else. `UiFonts.Use` puts it on what is read at a glance (titles, headings, buttons of 18 px and up, names,
  signs, the boss bar); running text keeps the default face: readability first.
- **Shop as columns**: `ShopPanel` groups the stall's upgrades by item and shows each column's current numbers and
  every card's single state; the bag column draws your slots.

## Consequences
- IMGUI remains only for the developer console and the mask editor (both through `ImGuiHost`).
- The gamepad layout is not rebindable yet; the mask editor needs a mouse.
- A page is rebuilt when what it shows changes (menus may allocate then); steady frames don't allocate.
