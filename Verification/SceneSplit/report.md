# Scene separation and startup optimization — 22 October 2026

## Delivered

- `Assets/_Game/Scenes/MainMenu.unity` is the build entry (index 0) and the Editor Play entry. It contains only a camera/audio listener, disabled menu light, front-end Canvas, settings and an EventSystem.
- `Assets/_Game/Scenes/Prototype.unity` is gameplay (index 1). Its existing GUID, world, systems and save schema remain. The title buttons/artwork were removed from this scene; its pause, inventory, shop, journal and save browser remain game-owned.
- Start / Saves asynchronously load the farm, then present the existing validated three-slot browser. Validation remains in SaveSystem, where the authored world/catalogue are available. Cancel before choosing a farm returns to MainMenu. No occupied slot is overwritten implicitly.
- Pause → Main Menu first saves the current active farm. If saving fails, the existing game remains open. A successful transition replaces the scene rather than leaving an inactive farm under a menu.
- `FarmSceneFlow` owns one temporary persistent loading overlay, blocks duplicate transitions, uses `LoadSceneAsync(..., Single)`, clears the static sprite cache and unloads unused previous-scene assets. The loader destroys itself after completion. Failed loading restores its previous simulation scale; the title becomes usable again.
- Graphics initialization avoids resetting quality and resolution when their applied values already match.
- Onboarding no longer searches for placement targets in Edit Mode or while paused; `BuildSystem.CanPlace` safely reports an uninitialized system during lifecycle boundaries. Console had `OnboardingSystem.ResolveTarget → CanPlace` null exceptions after a reload; they were recorded before clearing Console, then no new errors were observed after these guards and compilation.

Explicit authoring: **Tools / My Little Farm / UI / Separate Main Menu And Farm**. It preserves saved world edits and reuses an existing authored MainMenu instead of overwriting it. Main/Menu and Gameplay preview commands now open the appropriate scene. Other existing build scenes are retained after these two entries.

## Observed measurements

Counts below include inactive objects and disabled components, measured from scene roots through the connected Editor. They compare the previous title screen that kept the whole farm loaded with the new standalone front end.

| Metric | Before | MainMenu after |
|---|---:|---:|
| Transform/GameObjects | 1107 | 84 |
| 3D Renderer components | 401 | 0 |
| MonoBehaviour components, including UI | 745 | 98 |
| Serialized scene bytes | 2,310,782 | 234,830 |
| Recursive AssetDatabase dependencies | 449 | 38 |
| Source files in that dependency graph, bytes | 75,181,869 | 3,270,129 |

The menu has about **92.4% fewer objects**. The dependency-file totals are source sizes, not player memory usage or final compressed build size. The gameplay scene no longer depends on MainMenuBackground.png. MainMenu has no GameBootstrap, SaveSystem or Prototype scene asset dependency.

A short Play Mode inspection of MainMenu confirmed 84 scene objects, zero farm systems, zero save systems and timeScale=1. After a directed asynchronous reload of MainMenu, there was exactly one menu, one camera and one EventSystem, no transition loader remained and IsLoading was false.

The Editor's idle frame-timing sample was not representative (GPU timing zero); no FPS/frame-time gain is claimed. No release build, target-device memory measurement or full farm profiler session was run. Source artwork quality was retained; no texture-compression gain is claimed.

## Checks and limits

- C# compilation and fresh Console: no new errors. Existing obsolete Unity API warnings remain elsewhere.
- Actual front-end Canvas renders inspected: 1920×1080 MainMenu and 1280×800 graphics settings. Supplied background, letterboxing, buttons and settings remain visible and legible.
- Short directed Play Mode checks: title initializes without a farm, settings open/back, one asynchronous MainMenu reload, then stopped. No test runner, test suite or Verify-Scripts was run.
- Gameplay startup was not invoked against the actual profile: a legacy save/backup exists and slot 1 is empty, so entering Prototype would execute the existing one-time migration. Actual legacy migration, slot selection, trade/save/return and repeated world unload cycles remain phase-acceptance checks. No player save files were created, moved, deleted or loaded by this pass.
- Settings Apply was not clicked; its profile preferences were not written. Normal title initialization reads and applies the current settings to the running Editor player only.
- The active saved scene is MainMenu in Edit Mode. Phase 2 / P3-5 closure remains deferred.
