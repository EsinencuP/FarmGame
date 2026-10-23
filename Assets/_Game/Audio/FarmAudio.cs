using System;
using System.Collections.Generic;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MyLittleFarm.Audio
{
    /// <summary>One persistent mixer owner, bounded one-shot pool and two-source music/ambience fades.</summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class FarmAudio : MonoBehaviour
    {
        public static FarmAudio Instance { get; private set; }
        public static bool IsReady => Instance != null && Instance.catalog != null;
        [SerializeField] private FarmAudioCatalog catalog;
        private AudioPreferences _settings;
        private readonly System.Random _random = new System.Random();
        private readonly Dictionary<SoundId, int> _lastClip = new Dictionary<SoundId, int>();
        private readonly Dictionary<SoundId, float> _lastPlayed = new Dictionary<SoundId, float>();
        private Voice[] _voices;
        private Layer _music, _ambience;
        private AudioSource _rain;
        private DayNightCycle _clock;
        private FarmProgression _progress;
        private Transform _player;
        private bool _farm, _focused = true, _paused;
        private float _nextContext, _duckDb, _rainIntensity, _rainGainDb = -12;
        public FarmAudioCatalog Catalog => catalog;
        public int ActiveOneShots { get { var count = 0; if (_voices != null) foreach (var voice in _voices) if (voice.active) count++; return count; } }
        private sealed class Voice
        { public AudioSource source; public AudioCue cue; public bool active, paused, began; public float started, gain, expectedEnd; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => Instance = null;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var existing = FindAnyObjectByType<FarmAudio>();
            if (existing != null) { existing.Activate(); return; }
            new GameObject("Farm Audio").AddComponent<FarmAudio>();
        }
        private void Awake() => Activate();
        private void Activate()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
            if (_voices != null) return;
            // Script reloads can preserve the persistent object but reset non-serialized state.
            foreach (Transform child in transform)
            {
                var old = child.GetComponent<AudioSource>(); if (old == null) continue;
                old.Stop(); Destroy(child.gameObject);
            }
            catalog = catalog != null ? catalog : Resources.Load<FarmAudioCatalog>("Audio/FarmAudioCatalog");
            _settings = AudioPreferences.Read();
            if (catalog == null) return;
            catalog.Rebuild();
            _voices = new Voice[Mathf.Clamp(catalog.oneShotVoices, 4, 64)];
            for (var i = 0; i < _voices.Length; i++) _voices[i] = new Voice { source = Source("One shot " + (i + 1), AudioBus.SFX) };
            _music = new Layer(this, "Music", AudioBus.Music, true);
            _ambience = new Layer(this, "Ambience", AudioBus.Ambience, false);
            _rain = Source("Rain", AudioBus.Ambience); _rain.loop = true;
            ApplyListener();
        }
        private void Start() { if (Instance == this) { SceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single); ApplyMix(); } }
        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            Activate();
            if (Instance != this) return;
            SceneManager.sceneLoaded -= SceneLoaded; GameEvents.FarmAction -= FarmAction;
            SceneManager.sceneLoaded += SceneLoaded;
            GameEvents.FarmAction += FarmAction;
            if (catalog != null && SceneManager.GetActiveScene().isLoaded)
            { SceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single); ApplyMix(); }
        }
        private void OnDisable()
        {
            SceneManager.sceneLoaded -= SceneLoaded; GameEvents.FarmAction -= FarmAction;
            if (_progress != null) _progress.AchievementUnlocked -= Achievement;
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }
        private void OnApplicationFocus(bool focused) { _focused = focused; if (_settings != null) ApplyListener(); }
        public static void ApplySettings(AudioPreferences preferences)
        {
            if (Instance == null || preferences == null) return;
            Instance._settings = preferences.Copy(); Instance._settings.Sanitize(); Instance.ApplyMix();
        }
        public static bool Play(SoundId id, Vector3 position = default)
            => IsReady && Instance.PlayOneShot(id, position);
        public static bool PlayAtPlayer(SoundId id)
            => Play(id, Instance == null || Instance._player == null ? Vector3.zero : Instance._player.position);
        public static void SetRain(float intensity, float gainDb)
        {
            if (!IsReady) return;
            Instance._rainIntensity = Mathf.Clamp01(intensity); Instance._rainGainDb = Mathf.Clamp(gainDb, -80, 0);
        }
        private AudioSource Source(string name, AudioBus bus)
        {
            var child = new GameObject(name); child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>(); source.playOnAwake = false;
            source.outputAudioMixerGroup = catalog.Group(bus); source.dopplerLevel = 0;
            source.ignoreListenerPause = bus == AudioBus.UI || bus == AudioBus.Music;
            return source;
        }
        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!IsReady || mode == LoadSceneMode.Additive) return;
            if (_progress != null) _progress.AchievementUnlocked -= Achievement;
            foreach (var voice in _voices) if (voice.active && voice.cue.bus != AudioBus.UI) Release(voice);
            _lastPlayed.Clear(); _rainIntensity = 0; _rain.Stop(); _rain.clip = null;
            _farm = scene.path == FarmSceneFlow.GameScene;
            _clock = _farm ? FindAnyObjectByType<DayNightCycle>() : null;
            _progress = _farm ? FindAnyObjectByType<FarmProgression>() : null;
            var player = _farm ? FindAnyObjectByType<PlayerController>() : null; _player = player == null ? null : player.transform;
            if (_progress != null) _progress.AchievementUnlocked += Achievement;
            _nextContext = 0; UpdateContext();
        }
        private void UpdateContext()
        {
            _nextContext = Time.unscaledTime + 1;
            var night = _clock != null && (_clock.Hour < 6 || _clock.Hour >= 18);
            var music = catalog.Find(!_farm ? SoundId.MusicMenu : night ? SoundId.MusicFarmNight : SoundId.MusicFarmDay);
            if (_farm && !HasClip(music)) music = catalog.Find(night ? SoundId.MusicFarmDay : SoundId.MusicFarmNight);
            _music.Request(music);
            _ambience.Request(_farm ? catalog.Find(night ? SoundId.AmbienceNight : SoundId.AmbienceDay) : null);
        }
        private void Update()
        {
            if (!IsReady) return;
            var paused = _farm && Time.timeScale <= 0;
            if (_paused != paused)
            {
                _paused = paused;
                foreach (var voice in _voices) if (voice.active && !voice.cue.playWhilePaused && voice.cue.bus != AudioBus.UI && voice.cue.bus != AudioBus.Music)
                { voice.paused = paused; PauseSource(voice.source, paused); }
                _ambience.Pause(paused); PauseSource(_rain, paused);
            }
            var duck = Mathf.MoveTowards(_duckDb, _paused ? catalog.pausedMusicDb : 0, Time.unscaledDeltaTime * 24);
            if (!Mathf.Approximately(duck, _duckDb)) { _duckDb = duck; SetMixer("MusicDb", AudioPreferences.ToDecibels(_settings.musicVolume) + _duckDb); }
            if (Time.unscaledTime >= _nextContext) UpdateContext();
            foreach (var voice in _voices)
            {
                if (!voice.active || voice.paused) continue;
                if (voice.source.isPlaying) { voice.began = true; continue; }
                var state = voice.source.clip.loadState;
                if (!voice.began && (state == AudioDataLoadState.Loading || state == AudioDataLoadState.Unloaded))
                {
                    // Give background loading time to start playback instead of reclaiming its voice.
                    if (Time.unscaledTime - voice.started < 10) { voice.expectedEnd = Time.unscaledTime + voice.source.clip.length / voice.source.pitch + .15f; continue; }
                }
                if (voice.began || state == AudioDataLoadState.Failed || Time.unscaledTime >= voice.expectedEnd) Release(voice);
            }
            _music.Tick(Time.unscaledDeltaTime); _ambience.Tick(Time.unscaledDeltaTime);
            UpdateRain();
        }
        private static void PauseSource(AudioSource source, bool paused) { if (paused) source.Pause(); else source.UnPause(); }
        private void UpdateRain()
        {
            var cue = catalog.Find(SoundId.AmbienceRain);
            if (_rainIntensity <= .001f || !HasClip(cue)) { if (_rain.clip != null) { _rain.Stop(); _rain.clip = null; } return; }
            if (_rain.clip == null)
            {
                _rain.clip = Pick(cue); _rain.Play(); if (_paused) _rain.Pause();
            }
            // Preserve weather's existing precipitation envelope; catalog gain adds a mix trim.
            _rain.volume = AudioPreferences.ToAmplitude(_rainGainDb + cue.gainDb) * _rainIntensity * FallbackGain(AudioBus.Ambience);
            _rain.mute = BusVolume(AudioBus.Ambience) <= 0;
        }
        private bool PlayOneShot(SoundId id, Vector3 position)
        {
            var cue = catalog.Find(id);
            if (!HasClip(cue) || (_farm && Time.timeScale <= 0 && !cue.playWhilePaused && cue.bus != AudioBus.UI && cue.bus != AudioBus.Music)) return false;
            if (_lastPlayed.TryGetValue(id, out var time) && Time.unscaledTime - time < Mathf.Max(0, cue.cooldown)) return false;
            Voice candidate = null; var same = 0;
            foreach (var voice in _voices)
            {
                if (voice.active && voice.cue.id == id) same++;
                if (!voice.active && candidate == null) candidate = voice;
            }
            if (same >= Mathf.Max(1, cue.maxInstances)) return false;
            if (candidate == null)
                foreach (var voice in _voices)
                    if (voice.source.priority >= cue.priority && (candidate == null || voice.source.priority > candidate.source.priority
                        || (voice.source.priority == candidate.source.priority && voice.started < candidate.started))) candidate = voice;
            if (candidate == null) return false;
            var clip = Pick(cue); if (clip == null) return false;
            Release(candidate); candidate.cue = cue; candidate.active = true; candidate.started = Time.unscaledTime;
            candidate.gain = AudioPreferences.ToAmplitude(cue.gainDb);
            var source = candidate.source; source.transform.position = position; source.clip = clip;
            source.outputAudioMixerGroup = catalog.Group(cue.bus); source.spatialBlend = Mathf.Clamp01(cue.spatialBlend);
            source.minDistance = Mathf.Max(.1f, cue.minDistance); source.maxDistance = Mathf.Max(source.minDistance, cue.maxDistance);
            source.rolloffMode = AudioRolloffMode.Linear; source.priority = Mathf.Clamp(cue.priority, 0, 256);
            var low = Mathf.Clamp(cue.pitchRange.x, .1f, 3); var high = Mathf.Clamp(cue.pitchRange.y, low, 3);
            source.pitch = Mathf.Lerp(low, high, (float)_random.NextDouble());
            candidate.began = false; candidate.expectedEnd = Time.unscaledTime + clip.length / source.pitch + .15f;
            source.ignoreListenerPause = cue.bus == AudioBus.UI || cue.bus == AudioBus.Music;
            source.volume = candidate.gain * FallbackGain(cue.bus); source.mute = BusVolume(cue.bus) <= 0;
            _lastPlayed[id] = Time.unscaledTime; source.Play(); return true;
        }
        private static void Release(Voice voice)
        { voice.source.Stop(); voice.source.clip = null; voice.active = voice.paused = false; voice.cue = null; }
        private static bool HasClip(AudioCue cue)
        { if (cue?.clips != null) foreach (var clip in cue.clips) if (clip != null) return true; return false; }
        private AudioClip Pick(AudioCue cue)
        {
            if (!HasClip(cue)) return null;
            var start = _random.Next(cue.clips.Length); var previous = _lastClip.TryGetValue(cue.id, out var last) ? last : -1;
            var fallback = -1;
            for (var i = 0; i < cue.clips.Length; i++)
            {
                var index = (start + i) % cue.clips.Length;
                if (cue.clips[index] == null) continue;
                fallback = index;
                if (index != previous) { _lastClip[cue.id] = index; return cue.clips[index]; }
            }
            _lastClip[cue.id] = fallback; return cue.clips[fallback];
        }
        private void ApplyListener() => AudioListener.volume = _settings.mute || _settings.volume <= 0 || (!_focused && !_settings.backgroundAudio) ? 0 : 1;
        private void ApplyMix()
        {
            ApplyListener(); if (catalog == null) return;
            SetMixer("MasterDb", AudioPreferences.ToDecibels(_settings.volume));
            SetMixer("MusicDb", AudioPreferences.ToDecibels(_settings.musicVolume) + _duckDb);
            SetMixer("SfxDb", AudioPreferences.ToDecibels(_settings.sfxVolume));
            SetMixer("AmbienceDb", AudioPreferences.ToDecibels(_settings.ambienceVolume));
            SetMixer("UiDb", AudioPreferences.ToDecibels(_settings.uiVolume));
            if (_voices != null) foreach (var voice in _voices) if (voice.active)
            { voice.source.volume = voice.gain * FallbackGain(voice.cue.bus); voice.source.mute = BusVolume(voice.cue.bus) <= 0; }
        }
        private void SetMixer(string parameter, float db) { if (catalog?.mixer != null) catalog.mixer.SetFloat(parameter, Mathf.Clamp(db, -80, 0)); }
        private float BusVolume(AudioBus bus) => bus == AudioBus.Music ? _settings.musicVolume : bus == AudioBus.SFX ? _settings.sfxVolume : bus == AudioBus.Ambience ? _settings.ambienceVolume : _settings.uiVolume;
        private float FallbackGain(AudioBus bus) => catalog.mixer != null ? 1 : _settings.volume * BusVolume(bus) * (bus == AudioBus.Music ? AudioPreferences.ToAmplitude(_duckDb) : 1);
        private void Achievement(string name) => Play(SoundId.Achievement);
        private void FarmAction(FarmActivity action)
        {
            SoundId id;
            switch (action.Kind)
            {
                case "plant": id = SoundId.Plant; break; case "harvest": id = SoundId.Harvest; break;
                case "sale": id = SoundId.Sell; break; case "purchase": id = SoundId.Purchase; break;
                case "build": id = SoundId.BuildPlace; break; case "tree": id = SoundId.TreePlant; break;
                case "apples": id = SoundId.AppleHarvest; break; case "chicken": id = SoundId.ChickenBuy; break;
                case "fed": id = SoundId.ChickenFeed; break; case "eggs": id = SoundId.EggCollect; break;
                case "sector": id = SoundId.SectorUnlock; break; case "house_upgrade": id = SoundId.HouseUpgrade; break;
                default: return;
            }
            Play(id, _player == null ? Vector3.zero : _player.position);
        }

        private sealed class Layer
        {
            private readonly FarmAudio _owner;
            private readonly AudioBus _bus;
            private readonly bool _playlist;
            private readonly AudioSource[] _sources;
            private readonly float[] _gains = new float[2], _targets = new float[2], _baseGain = new float[2];
            private readonly bool[] _began = new bool[2];
            private AudioCue _requested;
            private int _active;
            private float _fadeSeconds = 2;
            public Layer(FarmAudio owner, string name, AudioBus bus, bool playlist)
            { _owner = owner; _bus = bus; _playlist = playlist; _sources = new[] { owner.Source(name + " A", bus), owner.Source(name + " B", bus) }; }
            public void Request(AudioCue cue) { if (_requested == cue) return; _requested = cue; Transition(); }
            private void Transition()
            {
                _targets[0] = _targets[1] = 0;
                var clip = _owner.Pick(_requested); if (clip == null) return;
                _fadeSeconds = Mathf.Max(.05f, Mathf.Min(_owner.catalog.crossfadeSeconds, clip.length * .25f));
                _active = _gains[0] <= _gains[1] ? 0 : 1;
                var source = _sources[_active]; source.Stop(); source.clip = clip;
                _began[_active] = false;
                source.spatialBlend = 0; source.pitch = 1; source.priority = _bus == AudioBus.Music ? 32 : 160;
                var count = 0; foreach (var item in _requested.clips) if (item != null) count++;
                source.loop = !_playlist || count < 2; source.volume = 0;
                _baseGain[_active] = AudioPreferences.ToAmplitude(_requested.gainDb);
                _gains[_active] = 0; _targets[_active] = 1; source.Play();
                if (_owner._paused && _bus == AudioBus.Ambience) source.Pause();
            }
            public void Pause(bool paused) { foreach (var source in _sources) PauseSource(source, paused); }
            public void Tick(float dt)
            {
                var active = _sources[_active]; var fade = _fadeSeconds;
                if (active.isPlaying) _began[_active] = true;
                if (_playlist && _requested != null && active.clip != null && !active.loop
                    && ((active.isPlaying && active.clip.length - active.time <= fade) || (_began[_active] && !active.isPlaying))) Transition();
                for (var i = 0; i < 2; i++)
                {
                    _gains[i] = Mathf.MoveTowards(_gains[i], _targets[i], dt / fade);
                    _sources[i].volume = _baseGain[i] * Mathf.Sqrt(_gains[i]) * _owner.FallbackGain(_bus);
                    _sources[i].mute = _owner.BusVolume(_bus) <= 0;
                    if (_gains[i] == 0 && _targets[i] == 0 && _sources[i].clip != null) { _sources[i].Stop(); _sources[i].clip = null; }
                }
            }
        }
    }
}
