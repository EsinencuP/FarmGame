# Day/night speed button — 22 October 2026

- Added the saved HUD button below the clock, cycling ×1 / ×10 / ×30 / ×60. One authored 14-minute day takes 84 / 28 / 14 seconds at the faster rates.
- Only DayNightCycle's hour increment changes. The simulation's time scale and all real-time/UTC timers retain their existing rates.
- Loading/restoring a farm returns the speed to ×1. Speed is not persisted; save schema remains v11.
- Button clicks block the world beneath them. Modal input guards the action; journal/save modal backdrops render above the control.
- Unity script compilation completed; Console reported zero errors. Fixed Edit Mode previews reviewed at 1280×720 and 600×800: hud-wide.png, hud-narrow.png. Button and labels fit both layouts.
- No automated tests, Verify-Scripts, Play Mode, save-file IO or profile writes were run. Runtime click cycling and day/night observation remain part of Phase 2 manual acceptance.
