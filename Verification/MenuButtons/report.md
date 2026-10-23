# Supplied main-menu buttons — 22 October 2026

Replaced the four MainMenu actions with the user's transparent PNGs, copied unchanged into `Assets/_Game/Content/UI/MainMenuButtons`:

- `22_10_58-1.png` → Play.png / Играть.
- `22_10_59-2.png` → Saves.png / Сохранения.
- `22_11_00-3.png` → Settings.png / Настройки.
- `22_11_01-4.png` → Exit.png / Выход.

The lettering/icons are part of the supplied art. Old overlay labels/icons are disabled. Sprite images use Simple + preserveAspect, not sliced/stretch rendering. Independent 560×128 logical hit rectangles have 136-unit vertical spacing; the art retains its transparent margins. Button actions and keyboard navigation retain the existing Button components and references. Hover/selected/pressed/disabled states tint the supplied art.

Source files remain 2172×724 RGBA. Unity imports at a maximum of 1024 pixels with alpha, no mipmaps and no CPU-readability; source pixels were not edited. The title background and settings/pause screens are unchanged. The importer is an explicit command, avoiding a new global texture postprocessor and project-wide reimport.

Explicit installer: Tools / My Little Farm / UI / Apply Illustrated Main Menu Buttons. New menu authoring also uses these assets when present. MainMenu.unity is saved with the new art.

Compilation/Console: zero errors after fixing the TextureImporterSettings API use. Actual Edit Mode Canvas images inspected at 1920×1080 and 1280×800; transparency, spacing, proportions and lettering display correctly. No automated tests, Verify-Scripts, Play Mode session or player-profile operations in this pass.
