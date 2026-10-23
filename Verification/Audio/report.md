# Audio integration preparation — 22 October 2026

Implemented FarmAudio, FarmAudioCatalog, AudioPreferences, UIAudioFeedback and PlayerAudioEmitter; a real Unity AudioMixer with Master/Music/SFX/Ambience/UI; a 20-voice one-shot pool; two-source music/ambience crossfades; clip variation, cooldown, concurrency/priority limits, per-channel mute, pause ducking and focus policy. The service starts before scenes, survives scene replacement and reacquires its state on script reload. Scene cameras retain their listeners; the service adds none.

Both saved scenes now have five audio sliders plus mute/background controls. Existing JSON profile/key and Apply/Back/Reset semantics remain; no farm save format changed. Audio slots are wired to current gameplay/UI events. Existing rain loop is the only assigned clip; other 35 slots are intentionally empty pending user files. Weather's legacy source is suppressed when the service owns playback. The editor installer preserves cue assignments and updates both scenes without player IO. Integration guide: Docs/Audio/Integration.md.

Validation performed without test suites:

- Script compilation succeeded; final Console query returned zero errors. Existing CS0618 warnings elsewhere in bootstrap/tests were observed during compilation and left untouched.
- Short title-only Play Mode inspection: one FarmAudio service, 25 owned AudioSources, one camera listener. All five mixer parameters resolved; defaults read Master −1.94 / Music −2.50 / SFX 0 / Ambience −1.94 / UI −1.94 dB.
- Temporary runtime-only settings changed Music to35% (−9.12 dB) and SFX to60% (−4.44 dB), leaving UI unchanged. Original settings restored; no PlayerPrefs writes. Empty UiClick returned false with zero active voices and no Console errors.
- Legacy JSON inspection retained volume0.42/mute=true and supplied new music default0.75. No actual profile was overwritten.
- Inspected Edit Mode audio settings at1920×1080 and1280×800 (MainMenu) and1280×800 (gameplay). Captures in this folder. Both scenes saved; editor returned to MainMenu/Edit Mode.

Not performed: automated tests, Verify-Scripts, real save/trade/load/farm acceptance, output-device listening, full pool/priority stress, crossfades with actual music, pause/focus/scene-transition audio acceptance or persistence round trip. These remain for asset integration/phase closure; no claim of completed sonic balance or full P3-3 acceptance.
