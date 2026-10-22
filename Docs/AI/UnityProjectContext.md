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

## Phase 2 landscape inventory (before P2-1 implementation)

- The user designated Phase 2 (World & Depth, four weeks) as the upcoming focus, specifically landscape, and requested inspection only followed by waiting. Do not start implementation or tests until further instructions.
- The folder called “Mo Assets” in the request is `Assets/moreAssets`. Inspected local filenames, import metadata, license files and supplied preview images; no scene or asset changes were made for this inventory.
- Quaternius Stylized Nature MegaKit FREE: 68 models in `moreAssets/FBX (Unity)`, with parallel FBX/OBJ/glTF exports. Includes common/pine/dead/twisted trees, bushes, grass, clover, ferns, flowers, mushrooms, pebbles, medium rocks and stone path pieces. Textured foliage has a more detailed silhouette than KayKit. Supplied promotional landscape images are references, not an included ready-made Unity scene; the free pack license note says configured engine projects/shaders belong to the SOURCE edition.
- KayKit Forest Nature Pack 1.0 FREE: 105 FBX files in `moreAssets/KayKit_Forest_Nature_Pack_1.0_FREE/Assets/fbx(unity)`, including variants. Geometric low-poly trees, bare trees, bushes, grass, rounded rocks and stacked rock formations; shared `forest_texture.png` palette. Useful for woodland composition, rock borders and scenery around the farm.
- Farm Buildings - Sept 2018: 13 FBX files, including barns, coop, two fences, silos, well, windmills and water tower. Available as scenery candidates; existing gameplay building replacements need separate instruction.
- The misleadingly named `Farm Animals by @Quaternius` directory contains Kenney Food Kit assets according to its actual preview and Overview.html: food, produce, dishes and kitchen props. Do not assume it provides animal models. Its bundled license text names a different pack, so identify actual source before documenting provenance for these assets.
- No authored `.prefab`, `.mat`, `.shader` or `.unity` files were found inside `moreAssets`; these are source model/texture collections. Assembly, material setup, metric scale (one cell = one Unity unit), grounding and collider choices remain future integration work. No terrain heightmap or ready-made terrain was identified in this inventory.

## P2-1: expanded authored world (21 October 2026)

- The user subsequently authorized implementation of P2-1. Prototype now contains 49 10x10 chunks, 70x70 cells, X/Z [-30,40): 4900 cells versus the previous 900 (5.44x area). One cell remains one Unity unit. Phase 2 as a whole is still in progress; P2-2 status is recorded below.
- Static terrain uses manually chosen ten-unit height control marks and a fixed pond contour baked in Edit Mode. WorldMapAsset stores explicit grid-corner heights; saved visual meshes and MeshColliders share geometry. No random/noise/runtime terrain generation. Heights reach 2.6 units; pond bed reaches -0.85, fixed water surface is -0.22. The former central farm stays flat.
- New landscape data is in `Content/World/Landscape`, scene objects under `Authored Landscape`. Added 53 LowPolyFarmLite Tree_04 trees, 22 Rock_04 rocks, 16 shrubs derived from that pack's actual crown mesh/UVs. Existing central decoration remains. Water blocks walking; tree/rock cells are obstacles; outer edge has world-limit colliders. New hills/forest generally permit walking, with farm/build permissions reserved for level areas.
- TerrainCell now stores PathSurface (Dirt=0, Stone=1, Wood=2), while the stable gameplay cell type remains Road=20. Dirt is continuous, stone has inset pavers, wood has planks. Fixed two-cell trails connect the zones. Grid Paint Palette has Road / Path surface and terrain-aware grid snap. This is an EDITOR path-authoring tool, not a new runtime shop/placement mode. Undo refreshes meshes; Reapply Maps / Refresh Relief applies manual WorldMap edits. Shared edge heights must match in neighboring maps.
- GridToWorld samples the saved terrain triangles. Cell marker and editor brush follow relief; BuildSystem raycasts the actual authored surface, and CanBuild rejects sloped cells. GroundAlignmentValidator supports shared relief meshes while retaining Y=0 checks for older flat scenes.
- PathSurface participates in chunk dirty/version and save deltas. Old missing values mean Dirt. SaveSystem accepts the old nine-chunk base signature in addition to the earlier two-chunk v5 migration. The original nine-chunk signature remained 7370389723730493432 during scene inspection. No user save was loaded by tools to verify this migration.
- Unity compilation and Edit Mode captures were inspected. No automated tests, Verify-Scripts or active gameplay run were started. Unity entered Play Mode externally during the work and was stopped to save authoring edits; this is not credited as a gameplay check. Defer traversal, seam/hover/build, path save/load, old-save migration and Undo/reopen checks until the end of Phase 2 or explicit user instruction.
- Evidence and editing instructions: `Verification/P2-1/report.md`. `WorldLandscapeSetup.Apply` is explicit and refuses to overwrite an existing Authored Landscape; nothing auto-runs on game startup. Preserve user edits and other pending working-tree changes.
- During the external Play Mode exit, Console showed CropSystem.Capture with an unconfigured grid. SaveSystem.OnApplicationQuit now checks mandatory subsystem readiness, including CropSystem.IsConfigured, before attempting a save. Compile-only follow-up; runtime verification remains deferred.

## P2-2: day/night lighting (21 October 2026)

- `Gameplay/World/DayNightCycle.cs` owns a 14-minute real-time visual clock (Inspector range 12–16), starting 08:00. Runtime advances with unscaledDeltaTime; no Time.timeScale changes. Saved Sun uses realtime rotation, smooth intensity curve and color gradients; realtime Moon Fill, ambient/sky colors and reflection intensity keep night readable. Zero/24-hour values form a continuous loop.
- Scene root `World Lighting` references the existing Sun, Moon Fill, game camera and 17 authored FarmLanterns. Saved reusable prefab/materials: `Content/World/Lighting`. Lanterns have actual realtime Point Light + emissive glass, fade together with dusk/dawn, turn off during daylight, and use instance MaterialPropertyBlock instead of material cloning. Point lights have 6.8-unit range, intensity 3, no shadow maps. Moon intensity 0.34 with soft blue fill; night ambient is intentionally generous.
- FarmLantern registers dynamically with its farm's cycle on enable and unregisters/goes dark on disable. Existing prefab instances can be moved manually; an in-game purchase/reposition UI is not added in P2-2. `Editor/DayNightSetup.Apply` is explicit, preserves existing prefab/instances on reapply, and does not run at game startup.
- Custom DayNightCycle Inspector provides explicit Edit Mode preview at 06:30/12:00/17:30/00:00 and a slider. No gameplay initialization or crop advancement; closing the Inspector / entering Play restores starting-hour lighting. Final Prototype is saved at 08:00.
- SaveData optional hasWorldTime/worldHour captures/restores [0,24) with finite-value validation. Older files without the fields remain valid. Visual hour is not advanced while the game is closed. CropSystem / CropRuntimeState continue to calculate crop age from UTC, independently of night/sleep; growth rules were not changed.
- Compile and native Edit Mode lighting images inspected; NO tests, Verify-Scripts, Play Mode or 14-minute active run started. Runtime cycle wrap, save/load, old-file compatibility, actual overnight growth and dynamic lantern lifecycle remain deferred to Phase 2 closure. Evidence/settings: `Verification/P2-2/report.md`. P2-3 not started.

## Character appearance override — 22 October 2026

- The user requested 20% smaller player and Stylized NPC Green appearance while keeping all animations. This supersedes the old P0-3 1.8-unit character target. Current Idle height is 1.44, capsule height 1.44 / radius 0.24 / skin 0.024 / step 0.2, root Y 0.744 on flat ground. Building/door scale, movement speeds and jump controls are unchanged.
- `Content/Character/HumanPlayerVisual.prefab` and Prototype's `Player/Character Visual` now contain actual Peasant Nolant Green body + hat, with Green material/texture and the skin's own valid Humanoid Avatar. Kevin mannequin geometry is removed from the active visual. Never assign the Kevin avatar to the Nolant skeleton.
- `KevinLocomotion.controller` and its 34 clips remain byte-for-byte unchanged (SHA-256 891A0F7F0854500325C77CEB52DDEE0C0D6101FBCED5DBC60EE19C40C34EF66A). Root motion stays off, AlwaysAnimate stays on. Finger pose detail is limited by Nolant's simpler finger skeleton.
- PlayerRigSetup constants and default visual source now reflect Green/1.44. Explicit menu Character / Apply Green Skin And 80 Percent Size updates the prefab and active scene, sets absolute dimensions (does not compound scaling), aligns feet to the capsule and positions the player on authored relief.
- Compile, assignment/geometry inspection and brief Edit Mode visual preview only; no tests or Play Mode. At Phase 2 closure update obsolete 1.8-size test expectations and verify all gameplay animations with the new skin. Evidence: `Verification/Character-Green/report.md`; preview: `idle.png` in that folder.

### Sky Series integration — 22 October 2026

SkySeries Freebie is now active in Prototype. DayNightCycle references the saved `Content/World/Lighting/FarmDayNightSky.mat` and switches the world camera to Skybox. `FarmDayNightSky.shader` decodes and blends the package's FluffballDay and CoriolisNight4k HDR cubemaps continuously: dawn 05:00–08:00, dusk 17:00–20:00, warm dawn/dusk tint. Existing 14-minute clock, lanterns, ambient lighting and UTC crop growth remain in place. Runtime uses a disposable material instance so time progression does not write source assets; explicit Edit Mode previews restore starting-hour values. Package source files are unchanged.

Installer: Tools → My Little Farm → Apply Sky Series Skyboxes (`Editor/SkyboxSetup.cs`). Scene/material saved at 08:00. Only compilation/Console and fixed-hour Edit Mode renders were reviewed; no tests or Play Mode. Evidence: `Verification/Skybox/day.png`, `sunset.png`, `night.png`. Full cycle verification remains deferred until Phase 2 closure.

### P2-3 weather — 22 October 2026

Implemented in Prototype: `Gameplay/World/WeatherController.cs`, `Editor/WeatherSetup.cs`, explicit WeatherController inspector preview, `Content/World/Weather/LightRain.prefab` and original synthesized 16-second LightRainLoop.wav. Built-in Particle System package enabled in manifest/lock. Sunny / Cloudy / LightRain hold 180–300 real seconds; transitions use SmoothStep over 45 seconds (Inspector range 30–60), independent of the day/night clock. Existing sky shader now rotates/desaturates an additional SunlessCirruscover HDR cubemap and smoothly adjusts sun/shadows/ambient. Rain is a player-following world-space particle volume with static-world collision and a fading 2D looping source (−12 dB nominal).

Rain bonus uses a monotonic session integral (+0.2 growth seconds per fully rainy real second) and per-plant baselines. CropRuntimeState/FarmingCell carry saved weatherGrowthBonusSeconds; HUD/harvest/stages use the same accrued age. New planting cannot inherit past rain; load preserves previously earned bonus without replaying it. Crop view refresh avoids per-crop allocations and rain does not dirty/rebuild grid meshes per frame. SaveData has optional validated WeatherSaveData for schedule/transition/cloud rotation/RNG; older saves default to no bonus and UTC offline growth remains ordinary. Full weather simulation does not advance offline.

Scene saved at Sunny / 08:00, no active particles/audio. Console/compile and fixed Edit Mode day/cloudy/rain/night renders only; zero shader messages. Tests, Verify-Scripts and Play Mode remain deferred to Phase 2 closure. Audio playback/balance, full fade/schedule, bonus/plant-during-rain and save/load validation remain on that closure checklist. Details and pictures: `Verification/P2-3/report.md`. P2-4 not started.

### P2-4 personalization — 22 October 2026

Prototype now includes 30 new purchasable decorations (31 decorative variants with the old flowerbed, 35 total building catalogue entries). `FarmDecorationCatalog` supplies stable IDs/prices/footprints and plain-text validation. `PersonalizationSetup.Apply` explicitly authors saved prefabs/materials/meshes under `Content/World/Personalization`, binds BuildSystem visuals and three houseLevelPrefabs, creates serialized BuildCustomizationUI references and an Input System EventSystem, and applies the pond shader. Original package files remain unchanged. Sources: `Docs/Art/P2DecorationManifest.md`.

Controls: B build, Tab catalogue (four pages), E/LMB place, Q/R rotate, M move, Delete remove, F configure the hovered house/sign. House upgrades cost 25 then 60 coins, keep the 7×6 reservation, check occupancy/character/coins, retain stable IDs and include investment in half-price refunds. Three original saved house models have 1.584-unit doors for the current 1.44-unit character; older imported storage/coop art was not recalibrated. Decorations use their own DecorationArtMetrics marker; BuildingArtMetrics remains for actual doors. BuildingDefinition catalogue construction is lazy to avoid cyclic static initialization with decorations.

Signs store sanitized plain customText (48-character cap, Cyrillic, two fitted lines) and use depth-tested, backface-culled font materials on both board faces. Runtime sign material copies track dynamic font atlas rebuilds. InputReader.ModalInputActive blocks world commands/movement/camera while catalogue/text/house panels are open; UI pointer rectangles block placement behind controls. New lanterns register with existing DayNightCycle; ghost previews disable their scripts/lights. Flags use a vertex animation shader; the weather vane has a slow turn hook.

SaveSystem writes version 8 and still validates/loads versions 1–7. BuildingRuntimeState appearanceLevel/customText flow through copy, layout, move, restore and validation. Existing weather/crop bonuses from P2-3 remain. Animated Pond surface shader uses analytic moving normals/color and saved UV/tangents with fixed shoreline geometry. No URP migration.

Compiled and reviewed fixed Edit Mode asset/UI/pond renders only. No tests, Verify-Scripts or Play Mode. Final gameplay checks remain at Phase 2 closure, including upgrading/refunds, typing isolation, v8/old saves, night-light lifecycle and animations/performance. Evidence/report: `Verification/P2-4`. During authoring Unity was blocked by an external-scene-change modal (from a prior whitespace-only scene edit); dismissed Ignore, kept the live scene, resumed MCP. Avoid editing scene YAML for cosmetic whitespace while it is open; use Editor scene saving.

### P2-5 final onboarding — 22 October 2026

OnboardingSystem now has exactly five goals: house, storage/chest, bed (till/plant/harvest), shop purchase, sale. Baked OnboardingOverlay shows a compact event-driven goal card; OnboardingMarker is a gold diamond/ring above a suggested valid building site, available/real crop cell, then original trading crate. Marker has no colliders, uses Ignore Raycast and normal depth testing. Completion/skip hides both card and marker; no completion overlay or movement lock. ResponsiveHudLayout reserves space for the card; card is below modal UI. InputReader.WorldPointerBlocked prevents card clicks/selecting/placing into the world.

One saved starter grant gives 25 extra coins and at least four carrot seeds; fresh farm balance becomes 50 (house20 + chest12 + carrot purchase8 =40), growth remains the authored eight seconds. Returning launches use MyLittleFarm.Onboarding.Seen PlayerPrefs to offer optional skip; restored farms also offer it. Authoring tools never change profile/save files. SaveSystem now writes v9; onboardingSchema separates the old six-action counter from the five-goal counter. Saves preserve grant, bed planting/location, early sale and completion. Schema0 old6 stays completed; old5 remembers the already finished sale, so purchase can finish the remaining goal. Existing core save recovery/weather/building behavior stays in place.

Explicit installer: Tools / My Little Farm / Apply P2-5 Onboarding; saved marker prefab/material/meshes under Content/World/Onboarding. Only script compilation/Console and fixed Edit Mode wide/narrow previews inspected; no automated tests, Verify-Scripts or new Play Mode started. An existing external Play session was stopped for scene authoring. Timed ≤10-minute gameplay, actual transitions/marker resolution, profile skip and save migrations remain deferred to Phase 2 closure. Evidence/checklist: Verification/P2-5/report.md.

### P2-6 save slots — 22 October 2026

SaveSystem is partial: core validation/restore/atomic IO in Core/SaveSystem.cs, three-slot flow and preview metadata in Core/SaveSlots.cs. Format v10 adds playTimeSeconds and embedded 320×180 PNG base64, while versions 1–9 remain readable. Persistent paths: Saves/slot-1, slot-2, slot-3 under Application.persistentDataPath, each with manual.json/autosave.json and per-point .bak. F5 saves the active manual point; F9 opens the slot menu. Periodic autosave is 300 real seconds, deferred during modal UI, plus active-farm save on quit. Active focused gameplay time excludes modal/background/offline periods. Older saves have no historical recorded play time.

Startup captures the pristine configured farm, imports the former single-file save only into entirely empty slot 1 (source retained), then opens SaveSlotsUI from Start after modal OnEnable handlers finish. Runtime menu offers new farms in empty slots, most recent valid points and optional older manual points, with confirmation while a farm is active. Switching first autosaves the outgoing farm; failures abort switching. Unavailable saves are not overwritten by a new-farm action. No delete/reset UI added. PlayerPrefs MyLittleFarm.Save.ActiveSlot remembers selection; menu stays open until a farm is selected on startup.

Candidates validate before mutation; unexpected restore errors attempt to restore a captured rollback snapshot and disable persistence if rollback fails. Crop/tree/animal views deactivate before deferred destruction, preventing prior-farm visuals/colliders from leaking into a new farm/thumbnail. Initial weather/clock defaults are restored for old files rather than inherited across slot switches. IsometricCameraController.RefreshPosition refreshes after teleport. BuildCustomizationUI only releases modal ownership when closing its own active modal.

Save validation uses current CharacterController dimensions/skin tolerance and BuildSystem.SavedCollisionBounds, a pure transform projection of metric prefab geometry, instead of the obsolete hardcoded radius/full footprint. Legacy geometry validation keeps its fallback. Oversized optional preview data are dropped without discarding farm state; file input is bounded to 16 MB and decoded PNG dimensions are bounded separately.

Editor/SaveSlotsSetup explicitly authors the saved UI, references camera/system and sets interval 300; it does not read player files. SaveSlotCard bounds PNG decoding, handles absent/invalid previews, shows local date/hours/coins/object counts and labels backups. Preview textures are disposed and illustrative Editor metadata cleared on close. Narrow UI scrolls vertically, keyboard focus scrolls into view and confirmation disables underlying controls.

Only compile/Console and fixed Edit Mode UI captures using illustrative DTOs were reviewed; no automated tests, Verify-Scripts, Play Mode, real save files, PlayerPrefs or save/relaunch round trip were exercised by tools. Existing GameBootstrap obsolete FindFirstObjectByType warning remains. Full three-farm restore, 300-second timer, quit saves, old files, backups/write failures and UI/input/resource checks are deferred to Phase 2 closure. Evidence/checklist: Verification/P2-6/report.md.

### P2-7 gameplay depth — 22 October 2026

FarmProgression/ FarmProgressData track per-farm statistics and20 local achievements from committed GameEvents.FarmAction records. Net profit = sales + refunds − purchases; starting/onboarding money is excluded. Favourite crop uses planting counts with stable catalogue ties. Achievements are sticky, per-slot, with queued dedicated toasts. Old saves have zero unknown historical counters but silently recognize achievements from currently held buildings/animals/trees/sector/money. No online/Steam API or order-board feature was added.

CropDefinition now authors harvest bonuses: carrot05–09 +1, tomato11–15 +2, cucumber18–22 +1 (start inclusive/end exclusive). CropSystem.HarvestYield supplies actual output, inventory-capacity checks and hover hints. UTC maturity/weather growth remain independent. HUD clock shows time and the selected crop's window. Corn is now the rare seed crop (existing corn_seed ID, quick slot5): normal market purchase disabled; old stocks stay usable.

Three static RareSeedCache instances reward2 corn seeds each with per-farm UTC15-minute replenishment: pond(-13.5,19.5), forest(-1.5,31.5), hill(28.5,20.5), grounded to saved relief. FarmInteraction trigger targets integrate with existing hover/outline and2.4-unit reach checks. Cache discovery/cooldowns are saved. The forest candidate was moved away from a solid tree footprint during authoring; full traversal remains unverified. No terrain-map data are authored by FarmDepthSetup; preserve unrelated working-tree map edits.

J opens FarmJournalUI (farm/statistics, achievements, discoveries). Achievements show10 per desktop page or5 per narrow page; stats/discovery text scrolls. Farm journal also hires a shepherd for80 coins near the crate/market, enabling a stationary Green visual by the pen. Its five-second service uses existing chicken Feed/TryCollect, finite wheat inventory and normal production timers; no offline bulk care or automatic feed purchases. Main player prefab/controller remain unchanged. Hiring is saved per farm.

SaveSystem writes v11, validates FarmProgressData and suspends tracking across restore/rollback so loads do not inflate counters. Snapshot restore replaces caches, hired visual and toast state. All earlier save features remain. GameBootstrap explicitly configures the new baked system. Editor/FarmDepthSetup applies the four game-owned crop settings and authors Content/World/Depth seed-bag assets, caches, helper, journal and HUD clock. No runtime initialization/profile IO in Edit Mode. Final scene has journal closed/helper hidden and illustrative08:00 clock.

Compile/Console and fixed Edit Mode art/UI views only; no automated tests, Verify-Scripts, Play Mode or actual player save/round-trip checks. Evidence/checklist: Verification/P2-7/report.md. P2-7 implementation is complete; Phase2 stays open pending the deferred suite, full feature acceptance and roadmap4+ hour playthrough. Do not mark the phase closed based solely on item implementation checkboxes.

### Day/night speed control — 22 October 2026

ClockSpeedUI adds a HUD button below the world clock: ×1 → ×10 → ×30 → ×60 → ×1 (14 real seconds per day at ×60 with the authored 14-minute day). DayNightCycle multiplies only its visual hour increment. Time.timeScale, UTC crop growth, weather, animal production and autosave timers retain their existing rates. The control is session-only and returns to ×1 when the clock is restored from a save/new farm. Clock speed changes update the label by event. InputReader blocks world clicks beneath the button; modal input prevents changing it while menus are open. Journal/save backdrops cover the button. ResponsiveHudLayout positions it on wide/narrow screens. Explicit authoring command: Tools / My Little Farm / Add Day Night Speed Button. Compilation and fixed Edit Mode HUD previews only; no automated tests or Play Mode run. Evidence: Verification/ClockSpeed/report.md.
