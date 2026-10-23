# Compact gameplay HUD — 22 October 2026

Requested UI-only changes are authored in Prototype; both scenes have the same Settings → Controls reference.

- Replaced the multiline inventory/economy report with the kit coin sprite and wallet counter. Time/day period and a compact cycle-speed control sit beside it.
- Hid the persistent keyboard help, onboarding hint and onboarding card. Tutorial progression/rewards/save state remain unchanged. Controls and introductory steps are available in the scrollable settings page, including from MainMenu.
- Fixed all six hotbar sprites: reparented icons had retained driven anchors and negative dimensions (-22 × -30). Local centered 52 × 50 geometry is repaired during authoring and OnEnable. Item icons stay visible at zero stock; quantities and selection retain their existing inventory events.
- Contextual interaction/build prompts and the navigation shortcuts remain available.

Validation: scripts compiled; Unity Console returned zero errors/warnings. Short Edit Mode Canvas previews inspected at 1920×1080 and 1280×800, plus Controls in gameplay and MainMenu. All six icon rectangles reported positive 52×50 size and assigned sprites; Controls content exceeds its clipped viewport and has a linked scrollbar. Preview counters use “—” because no player profile is initialized by capture.

No automated tests or Verify-Scripts were run, per AGENTS.md. No task-driven save/load/trade acceptance was performed. The editor was already being used in Play Mode during the work; it was stopped to save scene changes and capture previews. Full runtime gameplay acceptance remains deferred; Phase 2 is not closed.

Explicit authoring command: Tools / My Little Farm / UI / Apply Compact Gameplay HUD. The general FarmGame UI installer also reapplies this layout.
