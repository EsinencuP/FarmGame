# P2-7 — gameplay depth

Implemented 22 October 2026. Phase 2 remains open. No automated tests, Verify-Scripts, Play Mode, actual save-file round trip or four-hour acceptance run was started. Only native compilation/Console, code review, targeted Edit Mode placement inspection and fixed UI/art previews were used. UI sample values in screenshots are illustrative; they are not gameplay results or persisted progress.

## Time-of-day crops

CropDefinition stores designer-editable harvest-window settings. At the moment of collecting a mature crop, the same HarvestYield calculation supplies inventory-capacity checks, harvest output and the interaction hint. The bonus is whole extra produce, not faster growth and not full seasons:

| Crop | Game clock window | Extra produce |
|---|---|---|
| Carrot | 05:00 inclusive to 09:00 exclusive | +1 |
| Tomato | 11:00 inclusive to 15:00 exclusive | +2 |
| Cucumber | 18:00 inclusive to 22:00 exclusive | +1 |

The HUD shows the game clock and selected crop's window. J opens the farm journal with all three rules. UTC crop maturity, weather's accrued growth bonus, planting timestamps and existing art stages remain in place. Harvesting outside a window gives ordinary output; no bonus is granted by loading a farm or merely waiting inside a window. Definitions without a bonus and script-only worlds without a day/night clock keep normal yield.

## Rare seeds and exploration

Corn seeds retain the existing `corn_seed` ID and quick slot **5**, but are now exploration rewards rather than market stock. All existing seeds/produce remain usable. RareSeeds is authored on the corn asset; the runtime placeholder and shop refusal match that rule. Other five crops remain available in the market. No market-order system was added: this request allows special locations **or** order bonuses, and the special-location route is implemented.

Three saved, authored cache instances use one original seed-bag prefab and shared materials under Content/World/Depth. Each gives two corn seeds, then replenishes after 15 real minutes, including time while the app is closed. Discovered flags and UTC next-available times belong to the active save slot. Repeated interaction during cooldown gives no item. Collection checks distance (2.4 units), configured ID and inventory capacity. The journal gives location hints and cooldowns. Trigger pick volumes use the existing FarmInteraction layer and real-object hover/outline flow.

| Stable ID | Location | Authored X/Z |
|---|---|---|
| pond | West pond, north bank | −13.5 / 19.5 |
| forest | North forest clearing | −1.5 / 31.5 |
| hill | East hill | 28.5 / 20.5 |

Positions are grounded against saved terrain colliders. During placement inspection the initial forest candidate was next to a solid tree footprint, so it was moved to the listed clearing. Nearby target volumes were inspected after synchronizing transforms. This is not credited as a traversal or interaction playtest. The installer does not alter WorldMap terrain; unrelated pending map edits remain in the working tree.

## Statistics and 20 local achievements

FarmActivity events record committed actions, with dedicated IDs/amounts rather than inferring transactions from HUD balance changes. Statistics record sales revenue, purchases, refunds, net cash profit, plantings, bed harvest count/units, apples, eggs, bonus harvests, rare seeds and crop planting history. Profit is **sales + refunds − purchases**; initial money and the onboarding grant are excluded. Favourite crop is the most planted crop, with stable catalogue order breaking ties. Removed decorations still count toward historical placed varieties.

Twenty stable achievement IDs cover first harvest, highest balance100, planting10/100, all six crops, harvest100, revenue1000, house, storage, all purchased sectors, orchard, apples10, chickens, eggs10, hiring, time bonus, rare seeds, all three caches, five decoration types and manor. Unlocks are sticky, local and stored per farm; there is no Steam/online integration. The current game has one purchasable locked sector, so opening it satisfies “all sectors.” A dedicated queued toast avoids overwriting interaction/onboarding messages. The queue is cleared when a different snapshot is restored.

The journal has farm, achievements and discoveries tabs, two desktop achievement pages and four narrow-layout pages. Statistics/discovery text scrolls when needed; keyboard navigation, Esc and the existing modal-input gate are retained. The HUD clock/selected-crop hint remains visible during ordinary play.

## Functional hired shepherd

Hiring costs 80 coins once per farm and requires proximity to the original trading crate or a placed market. The journal disables hiring when funds/proximity are insufficient. The helper polls every five seconds while the game runs, uses the existing finite-inventory Feed/TryCollect logic, consumes one harvested wheat per feeding, respects normal egg timers/inventory capacity and collects ready eggs. It produces no offline batches and does not buy feed automatically.

An inactive authored helper visual beside the pen becomes visible on hire. It reuses the existing Green humanoid visual at 1.44-unit height and its existing idle animator; the main character source/prefab/controller were not changed. Its trigger target describes the service. This is a stationary helper/service, not a new pathfinding or dialogue system.

## Persistence, installation and evidence

Save version11 adds validated FarmProgressData to each snapshot, including bounded counters, crop history, unlocked IDs, decoration varieties, cache cooldowns and hired status. Restore suspends activity tracking through both apply and rollback, then replaces progression as a detached copy; loading cannot double-count spending, planting, produce or unlock notifications. Versions1–10 with no progression start historical counters at zero, preserve actual inventory/world state and silently recognize achievements derivable from current possessions/sector/house. Unknown historical earnings/plantings are not fabricated.

Explicit authoring menu: **Tools / My Little Farm / Apply P2-7 Farm Depth** (`Editor/FarmDepthSetup.cs`). It sets four existing game-owned crop assets, authors caches/helper/journal/clock and serializes dependencies. It does not initialize gameplay, read player save files or write profile preferences. Preview samples are cleared before scene saving. Final scene: 08:00 illustrative HUD, journal closed, helper inactive/unhired and caches visible.

Evidence:

- achievements-page1.png / achievements-page2.png: the 20-item desktop catalogue, illustrative unlocks.
- achievements-narrow.png: one column, five entries per page.
- statistics.png: illustrative profit/planting/favourite-crop panel and bonus rules.
- rare-cache.png / shepherd.png: fixed authored art views; helper temporarily enabled only for its capture and restored inactive afterward.

Console compilation has no errors. Existing unrelated GameBootstrap obsolete API warning may remain. No assertions, Test Runner or verification harness were used.

## Phase 2 closure checklist — not executed

1. Verify each crop immediately before/at/after its window boundaries, midnight wrap for designer settings, ordinary yields without a clock, full inventory and repeated harvest attempts. Sale inventory and stats must agree with actual yield; weather/overnight/offline growth must remain unchanged.
2. Walk all three routes, hover/click real bags, check distance rejection, cooldown, depleted visuals and replenishment after 15 minutes/relaunch. Full inventory must leave a cache available. Market must reject corn but still sell all five ordinary seed types.
3. Exercise all20 achievements through their real actions, including the sector, five distinct decorations, manor and shepherd; verify one toast per unlock and no toast/stat duplication on load or rollback.
4. Compare revenue, costs, refunds, net profit, plantings and favourite crop against a manual transaction log, including failed purchases/builds, refunds and the onboarding grant. Confirm stable favourite-crop ties and bounded overflow behavior.
5. Hire only near a trading point with at least80 coins. Verify one charge, five-second polling, no wheat/no birds/full inventory, normal feed/egg timers, manual/helper interaction races, saved hired status and no offline bulk earnings.
6. Save/load all progression categories across three distinct farms, old versions1–10, manual/auto/backup points and a simulated failed restore. Cache availability/helper visibility/toast queues must not leak between farms.
7. Runtime UI at supported sizes, keyboard/mouse tabs/pages/scrolling, J/Esc and other modal coexistence, clock transitions and optional queued toasts. The fixed previews do not substitute for these checks.
8. Run the deferred Phase2 automated suite and the roadmap's **4+ hour** acceptance playthrough before marking Phase2 closed.
