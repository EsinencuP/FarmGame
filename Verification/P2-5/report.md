# P2-5 — final onboarding

Implemented 22 October 2026. Phase 2 remains in progress. No automated tests, Verify-Scripts or new Play Mode session were started. Unity was already playing when this task began; that existing run was stopped to edit the scene. It is not credited as gameplay verification.

Five goals: build a house; build storage/chest; till, plant and harvest a bed crop; purchase seeds; sell produce. Planting and harvesting are subgoals of the same bed step. Successful gameplay actions drive progress; arrows and the persistent completion message are removed. The tutorial never locks movement, construction or other farm actions.

`OnboardingSystem` supplies an unlit gold world-space diamond/ring: suggested valid build site, available bed cell, real planted cell, original trading crate. Suggestions do not reserve land or place buildings automatically. It resolves targets every 0.5 seconds while active; the marker bobs/turns without colliders, physics or gameplay scripts. It uses Ignore Raycast and ordinary depth testing. There is no screen-space text arrow. The marker and whole goal card disappear at completion/skip.

`OnboardingOverlay` updates from progress events. Card layout fits the existing HUD in wide and narrow layouts, remains below modal customization windows and blocks pointer actions through its rectangle. Skip appears for returning runs or restored farms; a local PlayerPrefs flag records the first runtime visit. It does not auto-skip. Saved completion/skip prevents the tutorial from returning on load.

A one-time saved starter grant adds 25 coins and replenishes carrot seeds to at least four. New-farm budget is 50 coins: house 20 + storage 12 + first four carrot seeds 8 leaves 10 before selling the first harvest. Existing carrot asset matures in eight real seconds. No forced waiting, sleep, new dependencies or accelerated growth were added. A timed novice playthrough is still required to confirm the ten-minute target.

Save version 9 retains the five-goal state, grant flag, planted bed coordinates and early sale flag. Schema 0 (old six actions) is mapped once to schema 1: old 0/1 stay house/chest, old 2/3 become bed, old 4/5 become shop, old 6 stays completed. Old step 5 preserves its already finished sale so purchasing finishes the remaining goal. Selling early in the new flow is remembered as well. No player save files or PlayerPrefs were read/written by authoring tools.

Scene installer: **Tools / My Little Farm / Apply P2-5 Onboarding**. Assets: `Assets/_Game/Content/World/Onboarding`. The installer only authors assets/UI/serialized references; it does not initialize gameplay in Edit Mode. Do not rebake the entire prototype to apply this feature.

Reviewed: native script compilation/Console; fixed Edit Mode market/goal-card capture (1280×720), first-goal card, and narrow construction layout (960×720 canvas units). No assertions or test harness were used for these previews. Temporary cameras/targets and visible markers were removed/restored afterward; saved scene uses the first-goal illustrative card, hidden skip, hidden world marker, exploration UI and starting-hour lighting.

Deferred to Phase 2 closure:

- Fresh farm: complete the five goals with a stopwatch, aiming below ten minutes; verify one grant and correct expenses/earnings.
- Marker: actual suggested build sites, buildings placed elsewhere, crop tracking, terrain/obstacle depth, moving camera and distant crate.
- Save/load before and after every goal, midway through bed growth, after completion and after skip; no repeated grant.
- Legacy saves at all seven old progress values; finished tutorials must stay hidden, old post-sale progress must not require another harvest/sale.
- Second launch and restored-farm skip button; no world interaction through that click, no input lock afterward.
- Early planting/harvesting/sale; orchard harvest must not finish the bed goal. Catalogue, sign and house modals above the card.
- Runtime HUD at supported resolutions/safe areas and build mode, all transition boundaries, completion removes the entire card and marker.
