# FarmGame UI integration — 22 October 2026

**Gameplay navigation update:** HUD now exposes only Справочник/J and Pause/Esc. B owns construction, I inventory, O market only within existing market reach, with a nearby text prompt. FarmGuideUI shows all current crop/tree/animal definitions with real prices/timers and the kit growth icon; inventory details use it too. Statistics/achievements remain accessible from the handbook. All12 screen layouts and107 kit icons were reviewed again. This pass does not edit MainMenu or its approved illustrated buttons. Evidence: `Verification/GameplayGuide/report.md`.

**Current scene split:** title/settings now live in `Assets/_Game/Scenes/MainMenu.unity` (entry 0); the authored farm stays in `Prototype.unity` (gameplay 1). Start/Saves loads gameplay asynchronously and then opens the validated slot browser. Pause returns by saving and unloading the farm. Title artwork is no longer a gameplay dependency. `MainMenuUI` owns front-end controls; `FarmMenuUI` owns game modals. See `Verification/SceneSplit/report.md` for measurements and the later short title-only Play Mode check. The integration notes below describe the preceding pass where both screens shared Prototype.

The user supplied `Assets/moreAssets/FarmGame_UI`: 107 vector icons, a shared kit and screen references. Original SVGs and generators are retained. Game windows use functional uGUI controls rather than screenshots of reference pages. The existing legacy Text wiring is retained.

`Assets/_Game/Content/UI/MainMenuBackground.png` is the supplied `515fd869-d0ad-42e9-bea0-500ea1f6f21c.png`, copied without image processing. Main menu, its settings and the save browser use this artwork. Other modal windows overlay the actual world. The image keeps its aspect ratio; different screen ratios have a dark green matte rather than distorted art or a cropped logo.

## Assets and authoring

- Runtime icons: `Assets/_Game/Resources/FarmUI/Icons`, 128×128 transparent PNGs converted directly from SVG.
- Seven shared sliced skins in `Resources/FarmUI/Skin`, derived from the kit's panel, slot, wood ribbon and four button palettes.
- `Tools/UI/convert-kit.cjs` reproduces the vector conversions with Sharp. Set `FARM_UI_NODE_MODULES` to a directory containing Sharp when it is not installed on the Node search path.
- Nunito comes from the [official Google Fonts source](https://github.com/google/fonts/tree/main/ofl/nunito). OFL license is beside the font. The variable `wght` axis was frozen to 800 with fontTools because Unity's legacy font importer otherwise selected a thin instance. Reproduction: `instantiateVariableFont(TTFont(source), {'wght': 800}, inplace=True).save(destination)`.
- `FarmUIImport` assigns sprite imports and slice borders only under the game-owned UI paths. Adding the postprocessor caused a one-time project texture reimport; unrelated source assets were not edited.
- Explicit installer: **Tools → My Little Farm → UI → Apply FarmGame UI Kit**. It styles and binds the saved Prototype Canvas without rebuilding the farm, initializing gameplay, reading save slots or writing a player profile.
- Explicit Main Menu / Gameplay previews are in the same menu. `FarmUICapture.Capture` renders the actual Canvas in Edit Mode with illustrative data and restores the main-menu preview.

## Connected behavior

`FarmMenuUI` owns the screen stack, focus, safe-area placement and modal input. Startup now opens the main menu before the existing three-slot flow. Start/Save browser allow an empty-slot farm or loading an existing one; no occupied farm is silently overwritten. Esc opens pause outside construction and existing modals. Closing a nested page returns to its caller. Returning home and quitting first save the active farm and remain in the menu on failure.

Settings commit with Apply; Back discards the draft and Reset changes the draft. Implemented settings: master volume, mute, background audio, existing quality presets, available display resolutions, fullscreen, shadows and VSync. Applied values are stored in one versioned PlayerPrefs entry. Controls page documents actual default keys; it does not claim to implement rebinding. Separate Music/SFX buses, language selection and brightness are not simulated by inert controls.

Inventory (`I`) shows actual owned seeds, harvest and apple/egg products with category filters, quantity controls, details and seed quick-slot selection. Shop (`O`) uses the current six-crop catalogue and existing transaction logic. Quantity sale uses existing item prices and market reach checks, guards balance overflow and records the normal sale/progression events. Corn remains rare and unavailable for normal purchase. No extra crops, tools, animals or production systems were added from illustrative reference content.

The existing six seed images remain connected to HUD/bootstrap; their new slot wrappers add pointer selection and quantities. HUD navigation opens inventory, shop, construction catalogue, journal and pause. Existing save, journal, onboarding, customization and construction panels retain their behavior and receive kit styling/icons. SVG icons with no corresponding current gameplay action are imported and available without adding fake buttons.

The save browser has a real loading overlay before synchronous validation/restore, without a fabricated percentage. Confirmed removal of a closed, inactive slot moves its entire directory, including backups, to `Saves/Removed`; current active farms and protected legacy slots cannot be removed. No deletion was executed during authoring.

Menus set `Time.timeScale` to zero and restore the prior value on returning to the game. Day/night, weather updates, animal wandering and shepherd service respect this pause. UTC crop/production maturity keeps its existing real-time/offline semantics and catches up normally. Save thumbnails, save schema and farm content are unchanged.

## Verification boundary

See `Verification/UIIntegration/report.md`. Compilation and Edit Mode visual review only. No automatic tests, Verify-Scripts, new Play Mode session, actual save operations or profile changes were performed. Phase 2 and P3-5 release acceptance remain open.


## Market and graphics update — 22 October 2026

Gameplay market now follows the kit catalogue/inventory composition: cream frame and wood ribbon, category tabs, selectable product cards, details with growth/price icons, order list and checkout. P/E near a market opens it; inventory I no longer sells. J includes read-only upgrade prices. No global market icon. The5-choice resolution Dropdown is shared by both settings screens. Approved main-menu plaques/background/motion remain intact. Evidence: Verification/MarketAndShadows/report.md.
