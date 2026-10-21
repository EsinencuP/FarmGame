# MVP improvement pass — 8 October 2026

## Result

CoplayDev MCP for Unity 10.3.0 is installed in the project and connected to Codex as `unityMCP`. The live Editor reports `FarmGame@60d241d85605540d`, Unity 6000.6.4f1. Scene and Console reads, tests, screenshots and a Windows player build were executed through the bridge. No additional asset installation is needed for this pass.

## Changes in this pass

- Animal interaction keeps priority even when feeding or collection fails, preventing accidental soil actions underneath the animal prompt.
- Sale prompt accumulation uses long arithmetic for apple/egg totals, preventing integer overflow when both counts are large.
- Isometric camera rotation and zoom use damped targets; rapid repeated rotation commands retain their direction. Inspector settings control smoothing.
- Animal views interpolate wander targets instead of teleporting at each wander update.
- Player turn interpolation is frame-rate independent; a ceiling collision cancels upward velocity.
- Save loading validates the primary, then the backup before restoring. Recovery also works when the primary is absent. The first save after recovery preserves the good backup instead of replacing it with the corrupt primary. Atomic writing flushes the file stream before replacement.
- HUD subscriptions follow enable/disable lifecycle. Inventory/money updates are combined into one late-frame refresh; the onboarding hint changes through an event. A reused StringBuilder replaces the chain of intermediate strings.
- Existing HUD gets a darker statistics panel, active-crop emphasis, text shadows, layout adjustments, canvas-size tracking and safe-area insets. Help text is reorganized to avoid awkward line wrapping.
- MVP scene upgrade and preview capture are explicit menu commands. Runtime diagnostic capture is opt-in, so normal play/tests no longer write diagnostic screenshots automatically.
- Four regression PlayMode tests added; no gameplay mechanics added.

## Baseline and verification

The working tree initially had existing uncommitted imported art, scene, package and script changes. These were preserved. The complete Git diff is broader than this pass.

| Check | Result |
| --- | --- |
| Baseline EditMode | 7 passed, 0 failed |
| Baseline PlayMode | 17 passed, 0 failed |
| Final EditMode | 7 passed, 0 failed; job `f8518f7154fb43be979f133af4e8eef8` |
| Final PlayMode | 21 passed, 0 failed; job `cc3eea9aefa3472880e39bd135f47ac3` |
| Scene validation | 0 missing scripts, 0 broken prefab instances |
| Console after compilation | No errors returned |
| Git whitespace check | C# and package manifest passed; full diff reports trailing spaces in Unity-serialized scene fields. Scene YAML was not reformatted. |
| Standalone contract script | Passed C# syntax/assembly checks and building/economy/grid contracts |
| Windows x64 build | Succeeded; Mono, non-development; 0 errors, 2 warnings; 82.25 MB, 27.36 seconds |
| Player startup smoke | Built executable started headlessly and initialized Unity/Input System/PhysX; no exception/error found in captured log. Test process was stopped after inspection. |
| UI inspection | Game View captures inspected at 1024×768, 1280×720 and 1920×1080. A subsequent help-text wrapping edit was captured at 1280×720. |

Build job: `build-3c186caa03`. Artifact: `Builds/MvpValidation/MyLittleFarm.exe`; distribute the complete containing folder, including its data and runtime DLLs.

Player smoke log: `Verification/windows-player-smoke.log`.

Captures: `Verification/Captures/mvp-1024x768.png`, `mvp-1280x720.png`, `mvp-1920x1080.png`, `mvp-final-1280x720.png`.

## Limits and next validation

This is a tested first improvement pass, not a completed release audit. The standalone smoke ran without graphics; rendered standalone gameplay and a 20–30 minute interactive session were not tested. No before/after performance capture was taken, so there is no measured FPS improvement claim. A rendering-stat probe returned zero draw counters and is not useful performance evidence. The existing obsolete Unity object-search calls still produce compiler warnings in the project; the build reported two warnings. Existing imported models and world art still need a separate visual polish pass.

Desktop layouts were inspected; mobile safe-area behavior was implemented but not tested on a physical device. Input rebinding/gamepad support and a wholesale migration to TextMeshPro were not introduced.

## Connection recovery

- Codex entry: `unityMCP`, URL `http://127.0.0.1:8080/mcp`.
- Unity menu: Window → MCP for Unity.
- Auto-Start on Editor Load is enabled. The configured uvx executable is `D:/AI/runtimes/uv/bin/uvx.exe`; local server source override is `D:/AI/mcp/unity-mcp/Server`.
- The current local server uses `D:/AI/python/unity-mcp/Scripts/mcp-for-unity.exe`.
- The requested package Git branch remains `main`; `packages-lock.json` records commit `aa5fc638d623f56178d50329d9ad8c541a57fe66`.
- Prefer the loaded native Unity MCP tools. The Python helpers under `D:/AI/mcp/unity-mcp` were used before native tools became available.

The temporary installer bootstrap was removed after connection succeeded. Unity remains in Edit Mode after validation, and the previous Game View size selection was restored.
