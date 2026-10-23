# Main menu hover feedback — 22 October 2026

MainMenuButtonMotion is attached to all four illustrated actions and saved in MainMenu.unity. Pointer entry eases the artwork to 1.04 over 0.14 seconds; pointer exit restores 1.0. Left-button press scales to 0.99, release returns to hover. Only artwork scales, preserving the 560×128 hit rectangle. The coroutine uses unscaled time, restarts from the current scale and stops/resets on disable. There is no idle Update or new tween dependency. MainMenuUI.ApplyArtwork installs it when title art is reapplied.

Compiled with zero Console errors/warnings. Short manual Play Mode inspection using pointer callbacks observed hover1.040, press0.990, exit1.000 and unchanged hit rectangle. Settings click opened the existing settings window; hidden artwork reset to1.000. Game-view capture: Hover-Play.png. Returned to Edit Mode on MainMenu. No gameplay initialization/save operations or automated tests/Verify-Scripts were requested or run.
