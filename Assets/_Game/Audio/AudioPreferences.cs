using System;
using UnityEngine;

namespace MyLittleFarm.Audio
{
    /// <summary>Uses the existing settings key/field names, so old master-volume profiles migrate.</summary>
    [Serializable]
    public class AudioPreferences
    {
        public const string Key = "MyLittleFarm.UI.Settings.v1";
        public float volume = .8f, musicVolume = .75f, sfxVolume = 1, ambienceVolume = .8f, uiVolume = .8f;
        public bool mute, backgroundAudio = true;
        public void Sanitize()
        {
            volume = Level(volume, .8f); musicVolume = Level(musicVolume, .75f);
            sfxVolume = Level(sfxVolume, 1); ambienceVolume = Level(ambienceVolume, .8f); uiVolume = Level(uiVolume, .8f);
        }
        private static float Level(float value, float fallback) => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp01(value);
        public AudioPreferences Copy() => (AudioPreferences)MemberwiseClone();
        public static AudioPreferences Read()
        {
            AudioPreferences settings = null;
            try { if (PlayerPrefs.HasKey(Key)) settings = JsonUtility.FromJson<AudioPreferences>(PlayerPrefs.GetString(Key)); }
            catch (ArgumentException) { }
            settings = settings ?? new AudioPreferences(); settings.Sanitize(); return settings;
        }
        public static float ToDecibels(float amplitude) => amplitude <= .0001f ? -80 : 20 * Mathf.Log10(Mathf.Clamp01(amplitude));
        public static float ToAmplitude(float decibels) => decibels <= -80 ? 0 : Mathf.Pow(10, Mathf.Clamp(decibels, -80, 0) / 20);
    }
}
