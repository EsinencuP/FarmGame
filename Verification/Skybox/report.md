# Sky Series integration

22 October 2026 — Prototype, Unity 6000.6.4f1, built-in pipeline, Linear color space.

- Day: FluffballDay. Night: CoriolisNight4k. Both imported HDR cubemaps from SkySeries Freebie.
- Own saved FarmDayNightSky material/shader blends decoded HDR samples smoothly over dawn (05–08) and dusk (17–20), with warm twilight tint. No abrupt skybox swaps. Existing ambient/sun/moon/lantern cycle is retained.
- Camera clear mode is Skybox; the old solid-color backdrop is only a fallback without a skybox material.
- Runtime material copy is released when the cycle is disabled; source package materials/textures remain unchanged. Scene and authored material saved at starting hour 08:00.
- Explicit installer: Tools → My Little Farm → Apply Sky Series Skyboxes. Existing DayNightCycle inspector previews also show sky changes.

## Review

Compilation/Console: no error entries. Shader supported, zero ShaderUtil messages. Reviewed static Edit Mode scene renders at 12:00, 18:00 and 00:00. Temporary preview camera/render targets were removed and starting lighting restored afterward.

[Day](day.png) · [Sunset](sunset.png) · [Night](night.png)

The low-angle perspective in these evidence images is a temporary preview viewpoint; gameplay camera placement and projection were preserved. Tests, Verify-Scripts, Play Mode and full-cycle gameplay verification were not run, per the user's phase-closure policy.
