using System;
using System.IO;
using MyLittleFarm.UI;
using UnityEngine;

namespace MyLittleFarm.Core
{
    /// <summary>Read-only menu projection of one validated save point.</summary>
    public sealed class SavePointInfo
    {
        public bool Exists, CanLoad, IsAutosave, FromBackup;
        public long SavedAtUnixMs;
        public double PlayTimeSeconds;
        public int Coins, Crops, Trees, Animals, Buildings;
        public string PreviewPng;
    }

    public sealed class SaveSlotInfo
    {
        public int Index;
        public SavePointInfo Manual, Autosave;
        public bool IsEmpty => !Manual.Exists && !Autosave.Exists;
        public SavePointInfo Latest => !Manual.CanLoad ? (Autosave.CanLoad ? Autosave : null)
            : !Autosave.CanLoad || Manual.SavedAtUnixMs >= Autosave.SavedAtUnixMs ? Manual : Autosave;
    }

    public sealed partial class SaveSystem
    {
        public const int SlotCount = 3;
        private const string ActiveSlotKey = "MyLittleFarm.Save.ActiveSlot";
        [SerializeField] private SaveSlotsUI slotMenu;
        [SerializeField] private Camera previewCamera;
        private SaveData _newFarm;
        private double _playTimeSeconds;
        private string _lastPreview;
        private bool _showStartupMenu;
        public int ActiveSlot { get; private set; }
        public bool HasActiveFarm => _persistenceEnabled;
        public double PlayTimeSeconds => _playTimeSeconds;
        public event Action SlotsChanged;

        private static bool SlotIsValid(int slot) => slot >= 0 && slot < SlotCount;
        private static string SlotPath(int slot, bool autosave)
        {
            if (!SlotIsValid(slot)) throw new ArgumentOutOfRangeException(nameof(slot));
            return Path.Combine(Application.persistentDataPath, "Saves", "slot-" + (slot + 1),
                autosave ? "autosave.json" : "manual.json");
        }
        private static bool PointExists(string path) => File.Exists(path) || File.Exists(path + ".bak");

        private void StartSlotPersistence()
        {
            // Capture the configured authored farm before any saved state is applied.
            _newFarm = Capture();
            ActiveSlot = Mathf.Clamp(PlayerPrefs.GetInt(ActiveSlotKey, 0), 0, SlotCount - 1);
            if (previewCamera == null && _player != null)
                previewCamera = _player.GetComponentInParent<GameBootstrap>()?.GetComponentInChildren<Camera>(true);
            var legacy = Path.Combine(Application.persistentDataPath, "my-little-farm-save.json");
            // Import only into an entirely empty slot 1. Original legacy primary/backup stay untouched.
            if (!PointExists(SlotPath(0, false)) && !PointExists(SlotPath(0, true)) && PointExists(legacy))
            {
                if (LoadFromPath(legacy))
                {
                    ActiveSlot = 0;
                    if (!SaveToPath(SlotPath(0, false)))
                    {
                        _legacyUnavailable = true;
                        GameEvents.RaiseStatusChanged("Не удалось перенести старое сохранение. Исходный файл сохранён.");
                    }
                }
                else _legacyUnavailable = true;
                // Loading the legacy file was only for migration, not a silently selected active farm.
                Restore(_newFarm);
            }
            if (slotMenu != null) _showStartupMenu = true;
            else
            {
                // Small script-only scenes can operate without the authored menu.
                if (ReadSlot(ActiveSlot).IsEmpty) NewFarm(ActiveSlot);
                else LoadSlot(ActiveSlot, false);
            }
        }

        private void Start()
        {
            // All existing modal components have finished OnEnable before opening the startup menu.
            if (_showStartupMenu && slotMenu != null) slotMenu.Open();
        }

        private bool _legacyUnavailable;
        public bool LegacyUnavailable => _legacyUnavailable;

        public SaveSlotInfo[] ReadSlots()
        {
            var result = new SaveSlotInfo[SlotCount];
            for (var i = 0; i < result.Length; i++) result[i] = ReadSlot(i);
            return result;
        }
        private SaveSlotInfo ReadSlot(int slot) => new SaveSlotInfo
        { Index = slot, Manual = ReadPoint(slot, false), Autosave = ReadPoint(slot, true) };

        private SavePointInfo ReadPoint(int slot, bool autosave)
        {
            var path = SlotPath(slot, autosave);
            var result = new SavePointInfo { Exists = PointExists(path), IsAutosave = autosave };
            if (!result.Exists) return result;
            if (!TryReadValidated(path, out var snapshot, true))
            {
                if (!TryReadValidated(path + ".bak", out snapshot, true)) return result;
                result.FromBackup = true;
            }
            result.CanLoad = true; result.SavedAtUnixMs = snapshot.savedAtUnixMs;
            result.PlayTimeSeconds = snapshot.playTimeSeconds; result.Coins = snapshot.coins;
            result.Crops = snapshot.crops.Count; result.Trees = snapshot.trees.Count;
            result.Animals = snapshot.animals.Count; result.Buildings = snapshot.buildings.Count;
            result.PreviewPng = snapshot.previewPngBase64;
            return result;
        }

        public bool SaveActivePoint(bool autosave)
        {
            if (!_persistenceEnabled) return false;
            var success = SaveToPath(SlotPath(ActiveSlot, autosave), true);
            _nextAutosaveAt = Time.unscaledTime + autosaveIntervalSeconds;
            if (success)
            {
                RememberActiveSlot();
                SlotsChanged?.Invoke();
                GameEvents.RaiseStatusChanged((autosave ? "Автосохранение" : "Игра сохранена")
                    + " · слот " + (ActiveSlot + 1));
            }
            else GameEvents.RaiseStatusChanged("Не удалось сохранить слот " + (ActiveSlot + 1));
            return success;
        }

        public bool LoadSlot(int slot, bool manualOnly)
        {
            if (!SlotIsValid(slot)) return false;
            var info = ReadSlot(slot);
            var point = manualOnly ? (info.Manual.CanLoad ? info.Manual : null) : info.Latest;
            if (point == null)
            {
                GameEvents.RaiseStatusChanged("В слоте нет доступной точки загрузки; файлы сохранены.");
                return false;
            }
            // Switching farms must not silently lose the active one's unsaved progress.
            if (_persistenceEnabled && slot != ActiveSlot && !SaveActivePoint(true)) return false;
            if (!LoadFromPath(SlotPath(slot, point.IsAutosave)))
            {
                GameEvents.RaiseStatusChanged("Не удалось загрузить слот. Текущая ферма сохранена.");
                return false;
            }
            ActiveSlot = slot; _persistenceEnabled = true; RememberActiveSlot();
            _nextAutosaveAt = Time.unscaledTime + autosaveIntervalSeconds;
            SlotsChanged?.Invoke();
            GameEvents.RaiseStatusChanged(LastLoadUsedBackup ? "Ферма восстановлена из резервной копии"
                : "Загружен слот " + (slot + 1));
            return true;
        }

        /// <summary>Only empty slots accept new farms; occupied/corrupt slots cannot be overwritten here.</summary>
        public bool NewFarm(int slot)
        {
            if (!SlotIsValid(slot) || _newFarm == null || !ReadSlot(slot).IsEmpty
                || (slot == 0 && _legacyUnavailable)) return false;
            if (_persistenceEnabled && !SaveActivePoint(true)) return false;
            var previous = Capture(); var previousSlot = ActiveSlot; var wasActive = _persistenceEnabled;
            try
            {
                var fresh = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(_newFarm));
                fresh.savedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                fresh.playTimeSeconds = 0; fresh.previewPngBase64 = null;
                Restore(fresh);
                // Do not activate/switch ownership until the new point is safely written.
                if (!SaveToPath(SlotPath(slot, true), true))
                    throw new IOException("Could not write the initial farm save.");
            }
            catch (Exception error)
            {
                Debug.LogException(error); ActiveSlot = previousSlot; _persistenceEnabled = wasActive;
                try { Restore(previous); }
                catch (Exception recoveryError) { _persistenceEnabled = false; Debug.LogException(recoveryError); }
                return false;
            }
            ActiveSlot = slot; _persistenceEnabled = true; RememberActiveSlot();
            _nextAutosaveAt = Time.unscaledTime + autosaveIntervalSeconds;
            SlotsChanged?.Invoke();
            GameEvents.RaiseStatusChanged("Новая ферма · слот " + (slot + 1));
            return true;
        }

        private void RememberActiveSlot()
        {
            try { PlayerPrefs.SetInt(ActiveSlotKey, ActiveSlot); PlayerPrefs.Save(); }
            catch (Exception error) { Debug.LogWarning("Cannot remember the selected slot: " + error.Message); }
        }

        private string CapturePreview()
        {
            if (!Application.isPlaying || previewCamera == null
                || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return _lastPreview;
            GameObject capture = null; RenderTexture target = null; Texture2D image = null;
            var previous = RenderTexture.active;
            try
            {
                capture = new GameObject("Save thumbnail") { hideFlags = HideFlags.HideAndDontSave };
                var camera = capture.AddComponent<Camera>(); camera.CopyFrom(previewCamera);
                camera.transform.SetPositionAndRotation(previewCamera.transform.position, previewCamera.transform.rotation);
                camera.cullingMask &= ~((1 << 5) | (1 << 2)); // Farm geometry, no UI/goal markers.
                target = new RenderTexture(320, 180, 24); camera.targetTexture = target; camera.aspect = 16f / 9f;
                camera.Render(); RenderTexture.active = target;
                image = new Texture2D(320, 180, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 320, 180), 0, 0); image.Apply();
                return Convert.ToBase64String(image.EncodeToPNG());
            }
            catch (Exception error)
            {
                Debug.LogWarning("Save preview unavailable: " + error.Message);
                return _lastPreview;
            }
            finally
            {
                RenderTexture.active = previous;
                if (capture != null) { capture.GetComponent<Camera>().targetTexture = null; DestroyImmediate(capture); }
                if (target != null) { target.Release(); DestroyImmediate(target); }
                if (image != null) DestroyImmediate(image);
            }
        }
    }
}
