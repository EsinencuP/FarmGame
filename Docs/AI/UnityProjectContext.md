# My Little Farm — project context

## Project and limits

- Root: `D:/Evo Web Design/FarmGame`.
- Unity: `6000.6.4f1`; do not downgrade to the versions mentioned in generic skills.
- Runtime assembly: `MyLittleFarm.Runtime`; Editor: `MyLittleFarm.Editor`.
- Test assemblies: `MyLittleFarm.EditModeTests`, `MyLittleFarm.PlayModeTests`.
- Existing gameplay: six crops, till/plant/grow/harvest/sell, seed purchases, apple trees, chickens/feed/eggs, construction/move/delete, sector and tool upgrades, onboarding, persistence.
- User authorizes improving these systems and assets, with no new gameplay mechanics.
- The working tree already contained substantial uncommitted scene, script, package and imported art changes at the beginning of this session. Preserve them. Do not attribute all Git differences to this pass.

## Scene and architecture

- Entry scene: `Assets/_Game/Scenes/Prototype.unity`, enabled at build index 0.
- The scene is baked in Edit Mode; `GameBootstrap` binds existing objects at runtime.
- `BuildPrototype` exists for isolated tests and explicit Editor baking.
- `Core/Grid/GridSystem` stores layered chunk state. Crop, orchard, building and animal systems own their corresponding runtime state/views.
- Player uses `CharacterController`; do not convert movement to Rigidbody physics.
- Input uses `com.unity.inputsystem` 1.20.0 through `InputReader`.
- HUD is existing Canvas/uGUI with legacy `UnityEngine.UI.Text`; preserve existing wiring when editing. A wholesale TMP conversion is not part of this pass.
- Save format is version 7. Version 5 migration supports the original two authored chunks when their base hash matches. P0-3 expands legacy building footprints on a detached save; it preserves IDs and relocates buildings to available land or rejects the load without committing changes.
- Render pipeline is Built-in in the inspected project. Imported low-poly farm, food, vegetation and peasant models are already present.

## Connected Editor

- Provider: CoplayDev MCP for Unity, package `com.coplaydev.unity-mcp` 10.3.0 from the requested `main` Git URL.
- Unity instance verified: `FarmGame@60d241d85605540d`.
- Local HTTP endpoint: `http://127.0.0.1:8080/mcp`.
- Global Codex MCP entry: `unityMCP` in `C:/Users/esinencuP/.codex/config.toml`.
- Local server runtime: `D:/AI/python/unity-mcp`; source clone: `D:/AI/mcp/unity-mcp`.
- `uv`/`uvx`: `D:/AI/runtimes/uv/bin`. Unity's uvx path override and Auto-Start on Editor Load are configured.
- Use native `mcp__unityMCP__*` tools when available. They are now loaded in this chat.
- A temporary reflection-based bootstrap was removed after successful setup; it is not gameplay code or a permanent polling helper.
- Read `mcpforunity://editor/state` before mutations. Preserve dirty scenes, do not open a second Editor on this same checkout.

## Verification

- **User instruction, 21 October 2026: run automated tests only at the final closure of major roadmap phases (Phase 0, Phase 1, Phase 2, etc.), never after individual P0-4/P0-5/P1-1 items. This includes targeted runs and Verify-Scripts.ps1, which executes domain assertions. Prepare tests during implementation, defer their execution to the phase end. Until then use compilation/Console and brief focused visual/manual checks. An explicit user request for an earlier run overrides this cadence. Root AGENTS.md records the same rule.**

- `pwsh -NoProfile -File Verification/Verify-Scripts.ps1` checks C# syntax, assembly placement and pure grid/economy/building contracts; it does not replace Unity compilation/tests.
- At the final closure of a major phase, run both project test assemblies in the connected Editor. Test fixtures use isolated scenes and temporary save paths. Do not run them during individual roadmap items.
- `MvpReliabilityTests` covers animal interaction fallthrough, backup recovery including re-save, smoothing convergence, and HUD enable/disable subscriptions.
- Explicit Editor commands remain available under Tools/My Little Farm; MVP upgrade/preview capture no longer run automatically on reload.
- Runtime diagnostic capture is opt-in via `FARMGAME_CAPTURE=1` and restricted to the Prototype scene.
- UI captures are stored in `Verification/Captures` outside Assets. Builds go to `Builds`, outside source assets.

## Gameplay review fixes (8 October 2026)

- Ground visual tops now match terrain Y=0; the peasant visual accounts for CharacterController skin width.
- Pointer picking uses `InteractionTarget` trigger volumes for animals/trees/scenery and existing building/market colliders. Object selection hides the ground marker; selection uses outlines. Pointer clicks outside the map cannot invoke nearby actions. A short settling interval guards clicks while changing targets.
- HUD layout has a safe ExecuteAlways preview with illustrative values and no game/save initialization. Build prompts have a larger multiline area and model previews.
- Crop stages use small leafy seedlings before mature wheat/tomato art. Missing mature plants retain explicit placeholders. Apple fruit is red and positioned relative to actual tree bounds.
- Building fit keeps saved footprints, chooses a useful model orientation, authors height separately, and matches the physical box to art bounds.
- Map cells support independent permissions via `CellPermissions`, defaulting to inherited terrain rules. These survive deltas; inherited maps keep their former base hash. Editing authored permissions changes the map signature and requires deliberate save compatibility handling.
- Animator now receives Grounded/VerticalSpeed/Sprinting/GaitPlayback. Airborne holds a neutral pose; full Run/Jump clips are still absent.
- User accepts placeholders for absent assets. See `Docs/AI/AssetReplacementBacklog.md` for the asset tasks and recommendation to reduce the next version to wheat, tomato and cabbage. Six cultures remain active in this pass; do not silently remove old saved crops or rename their IDs.
- Evidence and verification are recorded in `Verification/GameplayReview/Fixes/report.md`. Do not attribute the full existing Git diff to this pass.

## P0-1: ground alignment (20 October 2026)

- Active roadmap: `Docs/MyLittleFarm_RoadToRelease.md`; implement its phases sequentially on user request. P0-1 is complete; P0-2 status is recorded below.
- Ground visual tops and Terrain Surface collider tops must remain at world Y=0. `Editor/GroundAlignmentValidator.cs` validates on farm scene save and exposes validation/alignment under Tools/My Little Farm/Ground. Inactive sectors use authored BoxCollider corners, not empty physics bounds.
- `MvpSceneUpgrade` realigns existing ground art on repeat runs. Alignment changes visual Y only; misaligned physical surfaces are reported separately.
- Player soles now use baked skinned mesh geometry (with imported scale compensation), rather than animation culling bounds. The scene's Nolant Visual Y changed from -1.0223727 to -1.080319, closing a roughly 0.058-unit gap. Skin width compensation remains in place.
- Verified all 9 chunks, five building types at all quarter turns, chicken contact/wandering, actual imported player grounding and shadow rendering. Evidence: `Verification/P0-1/report.md`; 19 EditMode and 27 PlayMode tests passed. No long playtest or performance measurement was performed.

## P0-2: crop stages (20 October 2026)

- All six existing crop IDs now reference three distinct prefabs each under `Assets/_Game/Content/Art/CropStages/<id>/`. Stage sizes are individual envelopes with uniform fitting; mature tomato retains the original model/material/LODs and 0.85-unit height.
- `Editor/CropStageArtBuilder.cs` authors saved, static low-poly mesh assets, not runtime primitives. Corn has a stalk, long leaves, two cobs and a tassel; root vegetables have above-ground foliage. Mature wheat still uses the existing imported model. Provenance and art constraints: `Docs/Art/CropStages.md`.
- `ImportedArtSetup` assigns all three distinct saved stage prefabs and their sizes; ordinary imports reuse existing prefab edits. Explicit regeneration: Tools/My Little Farm/Rebuild Crop Stage Art. Generated meshes share one Built-in lit vertex-color material with sRGB palette conversion.
- `CropView` immediately deactivates the preceding model before deferred Destroy and does not rebuild unchanged stages. Script-only fixtures retain the fallback. Crop IDs, growth durations, stage count, yields, item IDs, prices and save format are unchanged.
- Verified 27 EditMode and 29 PlayMode tests, including all 18 real stage assets, same-frame transitions, JSON save/restore and harvest. Native runtime and contact-sheet captures: `Verification/P0-2/report.md`.

## P0-3: metric scale and Kevin character (21 October 2026)

- One grid cell is one Unity unit. Player body is 1.8 units in its initial idle pose; CharacterController height 1.8, radius 0.3, skin 0.03, standing root Y=0.93. Animated poses naturally change visible bounds.
- Main visual is `Character Visual`, prefab `Content/Character/HumanPlayerVisual.prefab`, using the installed Kevin Iglesias Human Basic Motions FREE male demo Humanoid. Nolant is no longer instantiated. `PlayerController.VisualRoot` is the shared lookup; old Nolant source assets remain available.
- `Content/Character/KevinLocomotion.controller` uses 34 distinct in-place clips: eight Walk and eight Run directions, five Sprint directions, idle variants/transitions, turns, jump/fall/landing, Talk and two finger masks. Root motion is disabled. Animator uses AlwaysAnimate to avoid stale offscreen Humanoid poses.
- WASD moves camera-relative and turns toward movement; Alt holds camera-facing orientation for strafing/backward motion; Shift runs, Shift+Ctrl sprints (backward sprint falls back to Run), Space jumps, G plays Talk while stationary. Walk/run/sprint speeds are 2.2/4.5/6.5 units per second.
- Metric building prefabs live in `Content/Art/Buildings`; `BuildingArtMetrics` supplies door endpoints. Doors are 1.98 units (1.8 × 1.1); the coop source has only a chicken hatch, so the wrapper adds a real human service door and steps on its rear wall. Its tall hatch shutter must not be mistaken for a human door. Uniform scale preserves source proportions; metric art must bypass generic footprint fitting.
- Current footprints: house 7×6, storage 5×6, market 8×5, coop 3×5, flowerbed 1×1. `BuildingView` fits its local BoxCollider to renderer geometry, including correct transforms under rotated/scaled parents. Footprint remains the grid reservation.
- Flowerbed is a saved raised timber bed with soil and five actual Nicrom flowers. `MetricScaleSetup` installs/reuses authored building art and character without rebuilding the map. Ordinary setup preserves existing prefab edits.
- `BuildingScaleMigration` handles pre-v7 footprints on copied save data, excludes occupied/locked/unbuildable terrain, player, animals and physical obstacles, preserves unrelated terrain/crops/trees/economy, and fails atomically when there is no space. Original user save files were not used in verification.
- Final suites: 33 EditMode and 34 PlayMode tests passed before the user's later change of testing cadence. Evidence, controls, measurements and screenshots: `Verification/P0-3/report.md`; art/retargeting notes: `Docs/Art/CharacterAndBuildingScale.md`. Historical Nolant/missing Run/Jump notes above describe the earlier review, not the current character. P0-4 status is recorded below.

## P0-4: HUD consistency (21 October 2026)

- `ResponsiveHudLayout` remains ExecuteAlways and owns only layout/illustrative text. Inspector `editorPreview` switches Exploration/BuildAllowed/BuildBlocked through OnValidate + deferred layout; it never calls BuildPrototype, Configure on gameplay systems, or persistence. `SetEditorPreview` is ignored on live playing objects.
- Dedicated saved `Build Prompt` panel + `BuildPromptUI` have fixed height 180 canvas units, inner text height 156. `InteractionPromptUI` routes building content there and hides the ordinary 56-unit interaction label. It restores that label on exit; general help hides while the construction card is visible.
- Shared `BuildSystem.PlacementPrompt` formats the same five lines for runtime and Editor preview. E/left click confirms placement, Q/R rotates, Esc/right click cancels; M, Delete, B are listed. Delete confirmation accepts E/Enter and rejects accidental pointer clicks.
- Q/R both feed `RotateBuildingPressed`. IsometricCameraController suppresses keyboard orbit during BuildModeActive so Q does not simultaneously rotate the camera. Mouse orbit/zoom remain available.
- Ghosts use the saved, serialized `MyLittleFarm/BuildingGhost` shader (`Content/Art/BuildingGhost.shader`) with alpha 0.35, Transparent queue, SrcAlpha blend, ZWrite Off and no shadow pass. All renderer material slots are replaced; the actual metric prefab dimensions remain intact. Preview colliders are disabled, preview objects use Ignore Raycast, and owned materials are released on reconfigure/destroy. Flat green/red footprint and front marker remain.
- `HudConsistencySetup.Apply` upgrades only saved UI/shader references, without rebuilding the map or initializing the game. It is also called by the explicit MVP HUD upgrade; the base baker creates the separate construction card for future scenes and fixtures.
- Tested resolutions: 1280×720, 1920×1080, 2560×1440, 1280×800. Native Game View captures at 1728×743 and explicit-resolution Canvas renders are in `Verification/P0-4/report.md`. Editor tests prove the gameplay layout/router remain uninitialized and SaveSystem serialized settings are unchanged by preview.
- P0-4 complete. P0-5 status is recorded below. Historical notes above refer to the earlier HUD version.
- Before the new testing instruction arrived, 37 EditMode and 36 PlayMode tests passed. An additional E-place / cancel move / E-delete scenario was then prepared and compiled, but not run. All further automatic runs are deferred to the end of Phase 0 under AGENTS.md.

## P0-5: hover and interaction (21 October 2026)

- Project layer 8 is `FarmInteraction`. `InteractionTarget.ApplyPickLayer` assigns existing collider objects and dynamic animals/trees/buildings/markets to it; disabled targets are excluded. Layer collision rules are unchanged. Scene authoring: Tools/My Little Farm/Apply P0-5 Hover And Interaction (`Editor/HoverInteractionSetup.cs`).
- `CellSelector` uses separate non-alloc ray passes for authored terrain surfaces and interaction targets, with triggers explicitly included for targets. Closest target wins before ground; targets below the first ground surface are excluded. Terrain picking still accepts only cached TilemapChunk surface colliders.
- Target selection sets hover data directly and hides the ground marker without transiently selecting a cell first. The gold 3D bounds outline belongs to the actual target's rendered model and follows it; ordinary ground retains its own marker. Invalid/disabled/out-of-map targets, selector disable, pointer loss and construction clear selection/outline.
- Stability uses hover identity: 0.12s by default (`hoverSettleTime`). A moving animal crossing a cell boundary keeps its dwell; changing target/ground cell or losing hover restarts it. Both prompt entry and loss wait for stable hover, preventing one-frame edge misses from publishing fallback text. E and pointer clicks are gated during this switch; actions still operate on current selection, never the retained prompt.
- Repeated pick-volume Configure reuses an existing trigger and computes its bounds in local space. Tree targets without an Orchard state do not fall through to tilling the ground. Existing distance checks remain.
- No automated tests or Verify-Scripts were run, per AGENTS.md. C# compiled; short visual checks used only a disposable scene copy (animal, 1px cursor move, orchard tree, sale crate, ground). Evidence/limits: `Verification/P0-5/report.md`; deferred scenarios: `Verification/P0-5/phase-0-follow-up.md`.
- P0-5 complete. P0-6 was subsequently implemented: Kevin has a separate Run state at actual Speed > 3.2 (exit < 3.0), with real Jump clips. Final phase suites passed 37/37 EditMode and 39/39 PlayMode. The user then explicitly stopped testing: the active session ended after at least 14 of 30 minutes, so the Phase 0 exit criterion remains open. Play Mode stopped and Prototype restored. Do not resume tests without a new user instruction.

## Remaining release validation

The automated suites and UI screenshots do not substitute for a long play session, target-device performance measurement, or a complete release audit. Do not claim measured FPS gains from reducing HUD refresh/allocation work alone.
