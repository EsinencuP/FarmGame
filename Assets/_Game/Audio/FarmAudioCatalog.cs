using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace MyLittleFarm.Audio
{
    public enum AudioBus { Music, SFX, Ambience, UI }
    public enum SoundId
    {
        UiHover, UiClick, UiBack, UiOpen, UiClose, UiError,
        Till, Plant, Harvest, Sell, Purchase, BuildPlace, BuildMove, BuildRemove, BuildRotate,
        TreePlant, AppleHarvest, ChickenBuy, ChickenFeed, EggCollect, RareSeedCollect,
        SectorUnlock, ToolUpgrade, HouseUpgrade, Achievement, Save, Load,
        Footstep, Jump, Land,
        MusicMenu, MusicFarmDay, MusicFarmNight, AmbienceDay, AmbienceNight, AmbienceRain
    }
    [Serializable]
    public sealed class AudioCue
    {
        public SoundId id;
        public AudioBus bus = AudioBus.SFX;
        [Tooltip("Assign clips here later. Multiple clips create variations or a music playlist.")]
        public AudioClip[] clips = Array.Empty<AudioClip>();
        [Range(-80, 0)] public float gainDb = -6;
        public Vector2 pitchRange = new Vector2(.96f, 1.04f);
        [Range(0, 1)] public float spatialBlend = .7f;
        [Min(.1f)] public float minDistance = 8, maxDistance = 45;
        [Range(0, 256)] public int priority = 128;
        [Range(1, 16)] public int maxInstances = 3;
        [Min(0)] public float cooldown = .06f;
        [Tooltip("UI and transactions can remain audible while a menu pauses the farm.")]
        public bool playWhilePaused;
    }
    [CreateAssetMenu(menuName = "My Little Farm/Audio Catalog")]
    public sealed class FarmAudioCatalog : ScriptableObject
    {
        public AudioMixer mixer;
        public AudioMixerGroup music, sfx, ambience, ui;
        [Range(4, 64)] public int oneShotVoices = 20;
        [Range(.1f, 10)] public float crossfadeSeconds = 2;
        [Range(-24, 0)] public float pausedMusicDb = -6;
        public List<AudioCue> cues = new List<AudioCue>();
        [SerializeField, HideInInspector] private int schemaVersion;
        private readonly Dictionary<SoundId, AudioCue> _lookup = new Dictionary<SoundId, AudioCue>();
        private void OnEnable() => Rebuild();
        private void OnValidate() => Rebuild();
        public void Rebuild() { _lookup.Clear(); foreach (var cue in cues) if (cue != null) _lookup[cue.id] = cue; }
        public AudioCue Find(SoundId id) => _lookup.TryGetValue(id, out var cue) ? cue : null;
        public AudioMixerGroup Group(AudioBus bus) => bus == AudioBus.Music ? music : bus == AudioBus.SFX ? sfx : bus == AudioBus.Ambience ? ambience : ui;
        public void AddMissingSlots()
        {
            Rebuild();
            foreach (SoundId id in Enum.GetValues(typeof(SoundId)))
            {
                if (_lookup.ContainsKey(id)) continue;
                var name = id.ToString();
                var bus = name.StartsWith("Music") ? AudioBus.Music : name.StartsWith("Ambience") ? AudioBus.Ambience
                    : name.StartsWith("Ui") || id == SoundId.Save || id == SoundId.Load || id == SoundId.Achievement ? AudioBus.UI : AudioBus.SFX;
                cues.Add(new AudioCue { id = id, bus = bus, spatialBlend = bus == AudioBus.SFX ? .7f : 0,
                    pitchRange = bus == AudioBus.Music || bus == AudioBus.Ambience ? Vector2.one : new Vector2(.96f, 1.04f),
                    priority = bus == AudioBus.UI ? 48 : id == SoundId.Footstep ? 190 : 128,
                    gainDb = bus == AudioBus.Music ? -8 : bus == AudioBus.UI ? -10 : -6,
                    playWhilePaused = bus == AudioBus.UI || bus == AudioBus.Music || id == SoundId.Purchase || id == SoundId.Sell,
                    cooldown = id == SoundId.UiHover ? .08f : .06f });
            }
            if (schemaVersion < 1)
            {
                foreach (var cue in cues) if (cue.bus == AudioBus.UI || cue.bus == AudioBus.Music || cue.id == SoundId.Purchase || cue.id == SoundId.Sell) cue.playWhilePaused = true;
                schemaVersion = 1;
            }
            Rebuild();
        }
    }
}
