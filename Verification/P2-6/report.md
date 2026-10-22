# P2-6 — three farm slots

Implemented 22 October 2026. Phase 2 is still in progress. No automated tests, Verify-Scripts, Play Mode, real save/load round trip or player-file inspection was run by the authoring tools. Verification so far is script compilation/Console, code inspection and fixed Edit Mode UI captures using illustrative DTOs and a render of the authored scene. Those images demonstrate layout, not actual restored farm contents.

## Player flow

- The startup menu offers three independent farms. Start a farm in an empty slot or continue a saved one. F9 opens the same menu during play; Esc returns to the active farm.
- F5 or **Сохранить текущую** writes the current farm's manual point. Autosave writes its own point every 300 real seconds and on application quit. An open modal postpones the periodic write until gameplay resumes. Quit still saves an active farm.
- **Продолжить** loads the most recent valid point by saved timestamp. If that is an autosave and a manual point exists, **Ручная точка** offers the manual snapshot. Loading while already playing asks for an in-game confirmation. Switching to another farm first autosaves the current one and aborts if that write fails.
- A new farm uses the initial configured scene snapshot, resets its play time and restores fresh inventory/money/world/onboarding. Existing slots, including unavailable saves, cannot be overwritten by the new-farm action. No delete/reset feature is introduced here.
- Date/time uses the device's local timezone. Play time counts unscaled active gameplay while focused, excluding modal menus, background time and time while the app is closed. Older saves start with zero recorded hours because historical play time is unknown.

## Storage and recovery

`Application.persistentDataPath/Saves/slot-1/`, `slot-2/`, `slot-3/` each contain independent `manual.json` and `autosave.json`, plus `.bak` when replaced. Each write uses a flushed temporary file and atomic replace/move. A recovered primary cannot overwrite its good backup with corrupt bytes. Failure to load does not enable autosave over an unavailable farm.

Format v10 preserves the complete previous snapshot: crop IDs/UTC planting/stages/rain bonuses, tree fruit timers, animals/products/feed state, building placements/rotation/house levels/signs, grid terrain/farming/occupancy/path deltas, sector flag, quick slot/tool upgrades, inventory, money, player position, onboarding, lighting and weather. It adds double playTimeSeconds and optional previewPngBase64. PNG preview (320×180) is embedded in the same JSON commit; state/thumbnail cannot become two independently committed files. Thumbnail decoding is bounded and missing/invalid images use a placeholder without invalidating the farm.

The old `my-little-farm-save.json` and backup are imported at runtime only when slot 1 is completely empty. The original files are retained. Failed legacy import protects slot 1 while other empty slots remain available. No user save or PlayerPrefs was read/written by Editor setup/previews.

All candidates are detached, migrated and fully validated before world mutation. Restore captures a rollback snapshot for unexpected application failures; if rollback itself fails, persistence is disabled. Existing crop/tree/animal views are immediately deactivated before deferred destruction so switching farms cannot show/collide with the previous farm's objects. Old files without weather/lighting return to authored initial defaults when switching, rather than inheriting the previous farm's weather. Camera position refreshes after teleport before a new preview is taken. Slot selection is remembered separately in local PlayerPrefs; a profile preference write failure does not invalidate a successful farm save.

Building/player overlap validation now uses the current CharacterController dimensions/skin tolerance and the metric prefab's projected collision bounds. The former hardcoded 0.36 radius and full reserved footprint could reject legitimate saves near a smaller model. Projection is read-only and uses the same saved art transforms; no art is instantiated during validation. Legacy footprint migration keeps its legacy geometry fallback. Oversized optional preview metadata is discarded rather than rejecting an otherwise usable farm.

## UI and authoring

`SaveSlotsUI` and `SaveSlotCard` are saved under the HUD. Desktop cards show a farm thumbnail, saved date, play time, coins and world-object counts; backup source is labeled. Narrow layouts use one scrollable column and follow keyboard focus. The modal blocks gameplay through InputReader.ModalInputActive; its confirmation disables underlying buttons. BuildCustomizationUI now releases modal input only when closing its own active modal, avoiding interference during a save-slot restore. Preview textures are disposed on close and Editor preview data are cleared before scene saving.

Explicit installer: **Tools / My Little Farm / Apply P2-6 Save Slots** (`Editor/SaveSlotsSetup.cs`). It authors UI/references, sets the saved interval to 300 seconds and never initializes gameplay. Final scene has the slot menu closed in Edit Mode; it opens at runtime in SaveSystem.Start after existing OnEnable handlers finish.

Evidence: `slots-menu.png` (1280×720) and `slots-narrow.png` (960×720 canvas units). Console has no compile errors; an existing GameBootstrap.FindFirstObjectByType obsolescence warning remains unrelated to this feature.

## Deferred until Phase 2 closure

1. Create three farms with different crops, trees, animals, buildings/house upgrades/signs, inventory/money and sector state. Save, fully quit, relaunch, load each slot and compare every state category. Verify no cross-slot objects or duplicate crop growth/weather bonuses.
2. Confirm an actual 300-second autosave, save at quit and deferred periodic save after modal close. Manual points must remain independent. Check play time across focus changes, menus, loads and a full restart.
3. Load the manual point after a newer autosave, including same-slot discard confirmation. Switch farms with unsaved changes; force a write failure and verify switching is blocked without losing the active farm.
4. Load old versions 1–9 and legacy primary/backup; preserve old completed onboarding. Check startup with occupied slot 1, failed import and all slots unavailable. Original legacy files must remain intact.
5. Truncate primary/manual/autosave separately; recover matching `.bak`, verify thumbnail/date correspond to the recovered state and preserve the good backup on the next write. Exercise missing primary, invalid version/base map, large/invalid files, write denial and interrupted temporary writes in isolated test storage.
6. Confirm restore rollback on an unexpected subsystem failure without autosaving partial state. Check crop/tree/animal view cleanup, sector relock/unlock, fresh-farm reset and weather/clock defaults across slot switches. Save near every side/corner of rotated buildings and decorations with the current Green capsule, including houses on relief; actual overlaps must be rejected while legitimate nearby positions remain saveable.
7. Runtime screenshot capture at day/night/rain, PNG fallback, menu memory cleanup, keyboard/mouse navigation, scrolling, confirmations, startup selection and all supported resolutions. Current captures use sample metadata and do not replace these checks.
