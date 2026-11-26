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
        public bool IsLegacy;
        public SavePointInfo Manual, Autosave;
        public bool IsEmpty => !Manual.Exists && !Autosave.Exists;
        public SavePointInfo Latest => !Manual.CanLoad ? (Autosave.CanLoad ? Autosave : null)
            : !Autosave.CanLoad || Manual.SavedAtUnixMs >= Autosave.SavedAtUnixMs ? Manual : Autosave;
    }

    public sealed partial class SaveSystem_AI
    {
        public const int SlotCount = 3;
        private const string ActiveSlotKey = "MyLittleFarm.Save.ActiveSlot";
        [SerializeField] private SaveSlotsUI slotMenu;
        [SerializeField] private FarmMenuUI mainMenu;
        [SerializeField] private Camera previewCamera;
        private SaveData _newFarm;
        private double _playTimeSeconds;
        private string _lastPreview;
        private bool _showStartupMenu;
        private FarmSceneFlow.FarmLaunch? _startupLaunch;
        public int ActiveSlot { get; private set; }
        public bool HasActiveFarm => _persistenceEnabled;
        public double PlayTimeSeconds => _playTimeSeconds;
        public event Action SlotsChanged;

        private static bool SlotIsValid(int slot) => slot >= 0 && slot < SlotCount;
#if UNITY_EDITOR
        private static string EditorSaveRoot => UnityEditor.SessionState.GetString("MyLittleFarm.QA.SaveRoot", "");
#endif
        private static string SaveRoot
        {
            get
            {
#if UNITY_EDITOR
                if (!string.IsNullOrEmpty(EditorSaveRoot)) return EditorSaveRoot;
#endif
                return Application.persistentDataPath;
            }
        }
        private static string SlotPath(int slot, bool autosave)
        {
            if (!SlotIsValid(slot)) throw new ArgumentOutOfRangeException(nameof(slot));
            return Path.Combine(SaveRoot, "Saves", "slot-" + (slot + 1),
                autosave ? "autosave.json" : "manual.json");
        }
        private static bool PointExists(string path) => File.Exists(path) || File.Exists(path + ".bak");
        private static string LegacyPath => Path.Combine(SaveRoot, "my-little-farm-save.json");
        private static bool HasUnimportedLegacy => !PointExists(SlotPath(0, false))
            && !PointExists(SlotPath(0, true)) && PointExists(LegacyPath);

        private void StartSlotPersistence()
        {
            // Capture the configured authored farm before any saved state is applied.
            _newFarm = Capture();
            _startupLaunch = gameObject.scene.path == FarmSceneFlow.GameScene ? FarmSceneFlow.TakeFarmLaunch() : null;
            ActiveSlot = Mathf.Clamp(PlayerPrefs.GetInt(ActiveSlotKey, 0), 0, SlotCount - 1);
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(EditorSaveRoot)) ActiveSlot = 0;
#endif
            if (previewCamera == null && _player != null)
                previewCamera = _player.GetComponentInParent<GameBootstrap>()?.GetComponentInChildren<Camera>(true);
            // Import only into an entirely empty slot 1. Original legacy primary/backup stay untouched.
            if (HasUnimportedLegacy && (!_startupLaunch.HasValue || _startupLaunch.Value.Slot == 0))
            {
                if (LoadFromPath(LegacyPath))
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
            if (_startupLaunch.HasValue) _input.ModalInputActive = true;
            else if (slotMenu != null) _showStartupMenu = true;
            else
            {
                // Small script-only scenes can operate without the authored menu.
                if (ReadSlot(ActiveSlot).IsEmpty) NewFarm(ActiveSlot);
                else LoadSlot(ActiveSlot, false);
            }
        }

        private void Start()
        {
            if (_startupLaunch.HasValue)
            {
                var launch = _startupLaunch.Value;
                _startupLaunch = null;
                var loaded = launch.NewFarm ? NewFarm(launch.Slot) : LoadSlot(launch.Slot, launch.Manual);
                if (loaded)
                {
                    mainMenu?.StartFarmSession();
                    _input.ModalInputActive = false;
                    _input.SuppressGameplayThisFrame = true;
                }
                else FarmSceneFlow.ReportLaunchFailure();
                return;
            }
            // All existing modal components have finished OnEnable before opening the startup menu.
            if (_showStartupMenu && mainMenu != null) mainMenu.OpenStartupBrowser();
            else if (_showStartupMenu && slotMenu != null) slotMenu.Open();
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
        {
            Index = slot,
            Manual = ReadPoint(SlotPath(slot, false), false, ReadValidatedPoint),
            Autosave = ReadPoint(SlotPath(slot, true), true, ReadValidatedPoint)
        };

        internal static SaveSlotInfo[] ReadSelectionSlots()
        {
            var result = new SaveSlotInfo[SlotCount];
            for (var slot = 0; slot < result.Length; slot++)
            {
                var legacy = slot == 0 && HasUnimportedLegacy;
                result[slot] = new SaveSlotInfo
                {
                    Index = slot,
                    IsLegacy = legacy,
                    Manual = ReadPoint(legacy ? LegacyPath : SlotPath(slot, false), false, ReadPreviewPoint),
                    Autosave = ReadPoint(SlotPath(slot, true), true, ReadPreviewPoint)
                };
            }
            return result;
        }

        private SaveData ReadValidatedPoint(string path) => TryReadValidated(path, out var snapshot, true)
            ? snapshot : null;

        private static SaveData ReadPreviewPoint(string path)
        {
            try
            {
                var snapshot = ReadSnapshotFile(path);
                if (snapshot == null || snapshot.saveVersion < 1 || snapshot.saveVersion > CurrentSaveVersion
                    || snapshot.savedAtUnixMs <= 0 || snapshot.coins < 0
                    || double.IsNaN(snapshot.playTimeSeconds) || double.IsInfinity(snapshot.playTimeSeconds)
                    || snapshot.playTimeSeconds < 0 || snapshot.playTimeSeconds > 1e12) return null;
                return snapshot;
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidDataException
                || error is IOException || error is UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static SavePointInfo ReadPoint(string path, bool autosave, Func<string, SaveData> read)
        {
            var result = new SavePointInfo { Exists = PointExists(path), IsAutosave = autosave };
            if (!result.Exists) return result;
            var snapshot = read(path);
            if (snapshot == null)
            {
                snapshot = read(path + ".bak");
                if (snapshot == null) return result;
                result.FromBackup = true;
            }
            result.CanLoad = true; result.SavedAtUnixMs = snapshot.savedAtUnixMs;
            result.PlayTimeSeconds = snapshot.playTimeSeconds; result.Coins = snapshot.coins;
            result.Crops = snapshot.crops?.Count ?? 0; result.Trees = snapshot.trees?.Count ?? 0;
            result.Animals = snapshot.animals?.Count ?? 0; result.Buildings = snapshot.buildings?.Count ?? 0;
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
                if (!autosave) MyLittleFarm.Audio.FarmAudio.Play(MyLittleFarm.Audio.SoundId.Save);
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
            var other = point.IsAutosave ? info.Manual : info.Autosave;
            var loadedPreferred = LoadFromPath(SlotPath(slot, point.IsAutosave));
            if (!loadedPreferred && (manualOnly || !other.CanLoad || !LoadFromPath(SlotPath(slot, other.IsAutosave))))
            {
                GameEvents.RaiseStatusChanged("Не удалось загрузить слот. Текущая ферма сохранена.");
                return false;
            }
            ActiveSlot = slot; _persistenceEnabled = true; RememberActiveSlot();
            _nextAutosaveAt = Time.unscaledTime + autosaveIntervalSeconds;
            mainMenu?.ResetMarketCart();
            SlotsChanged?.Invoke();
            GameEvents.RaiseStatusChanged(!loadedPreferred ? "Загружена предыдущая точка · слот " + (slot + 1)
                : LastLoadUsedBackup ? "Ферма восстановлена из резервной копии"
                : "Загружен слот " + (slot + 1));
            MyLittleFarm.Audio.FarmAudio.Play(MyLittleFarm.Audio.SoundId.Load);
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
            mainMenu?.ResetMarketCart();
            SlotsChanged?.Invoke();
            GameEvents.RaiseStatusChanged("Новая ферма · слот " + (slot + 1));
            return true;
        }

        private void RememberActiveSlot()
        {
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(EditorSaveRoot)) return;
#endif
            try { PlayerPrefs.SetInt(ActiveSlotKey, ActiveSlot); PlayerPrefs.Save(); }
            catch (Exception error) { Debug.LogWarning("Cannot remember the selected slot: " + error.Message); }
        }

        /// <summary>Remove a closed farm from the browser, retaining its files in a recovery folder.</summary>
        public bool ArchiveSlot(int slot)
        {
            if (!SlotIsValid(slot) || (_persistenceEnabled && slot == ActiveSlot) || (slot == 0 && _legacyUnavailable)) return false;
            if (!ArchiveClosedSlot(slot)) return false;
            SlotsChanged?.Invoke();
            return true;
        }

        internal static bool ArchiveClosedSlot(int slot)
        {
            if (!SlotIsValid(slot) || (slot == 0 && HasUnimportedLegacy)) return false;
            var source = Path.GetFullPath(Path.GetDirectoryName(SlotPath(slot, false)));
            var root = Path.GetFullPath(Path.Combine(SaveRoot, "Saves"));
            if (!source.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(source)) return false;
            try
            {
                var recovery = Path.Combine(root, "Removed"); Directory.CreateDirectory(recovery);
                Directory.Move(source, Path.Combine(recovery, "slot-" + (slot + 1) + "-" + Guid.NewGuid().ToString("N")));
                return true;
            }
            catch (Exception error) { Debug.LogWarning("Cannot remove farm slot: " + error.Message); return false; }
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
