# Crop stage art — P0-2

20 October 2026. Unity 6000.6.4f1, Built-in renderer, perspective isometric camera; one grid cell = one unit. Six existing crop IDs, three stages per crop. No changes to prices, growth durations, harvest items or save data.

## Visual brief

Use the existing `LowPolyFarmLite/Prefabs/TomatoPlant_01.prefab` as the mature-size and style reference: faceted solid leaves, readable foliage, warm fruit accents, cast/received shadows. Preserve that mature tomato model and its proportions. Seedlings have two to four cotyledons; growing plants develop their species silhouette without ripe fruit. Mature crops have distinct silhouettes: feathery carrot tops, potato bush with small flowers, wheat ear, tomato bush, tall corn stalk with cobs/tassel, low cucumber vine with broad leaves and cucumbers. Root vegetables stay below the soil; only the carrot shoulder is visible.

All prefab roots use Y-up, identity transforms and a centered base at Y=0. Fit each stage uniformly into its own authored bounding envelope; keep plants inside a 0.7-unit footprint. Sprouts fit 0.24 × 0.14 × 0.24, growing plants fit width/depth 0.46 and about 54% of mature height. Mature height limits: carrot 0.35, potato 0.50, wheat 0.95, tomato 0.85, corn 1.45, cucumber 0.45.

## Manifest and provenance

Each ID in `carrot`, `potato`, `wheat`, `tomato`, `corn`, `cucumber` has three durable prefabs:

`Assets/_Game/Content/Art/CropStages/<id>/<id>_Sprout.prefab`, `<id>_Growing.prefab`, `<id>_Mature.prefab`.

- All sprouts, all growing stages, and mature carrot/potato/corn/cucumber: original project mesh geometry authored deterministically in `Assets/_Game/Editor/CropStageArtBuilder.cs`. No downloaded model, generated raster, external texture or new asset pack. Editable source is the builder; saved meshes are sibling `.asset` files. Palette is stored in vertex colors, with one shared lit material/shader. These are static plant models, not runtime primitive placeholders.
- Mature tomato: existing `Assets/LowPolyFarmLite/Prefabs/TomatoPlant_01.prefab`; wrapper retains original meshes/materials and LODs, removes visual colliders. Existing third-party asset terms continue to apply.
- Mature wheat: existing `Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Env_Wheat.prefab`; wrapper retains original mesh/material, removes visual collider. Existing third-party asset terms continue to apply.
- Collision role for all 18 prefabs: visual only; no physics colliders or gameplay scripts. Grid owns interaction and occupancy.
- No new external license is introduced by the authored geometry. This work does not replace the planned release audit of existing asset-pack licenses.

## Authoring and acceptance

`Tools → My Little Farm → Rebuild Crop Stage Art` explicitly rebuilds the saved geometry, prefabs and stage assignments. Importing art reuses existing authored prefabs and creates missing ones; it does not overwrite manual prefab edits on every import. Asset paths/GUIDs remain stable across rebuilds.

Validate distinct prefab references and geometry per stage, increasing observed height, bounded footprints, base contact, renderer/material/shader presence, no colliders, immediate removal of the preceding visual, and growth/harvest/save-load behavior. Inspect all 18 stages together in-engine at game scale. Evidence and final verification status are recorded in `Verification/P0-2/report.md`.
