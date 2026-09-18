using System;
using System.Collections.Generic;
using System.IO;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.Farming;
using MyLittleFarm.Gameplay.World;
using MyLittleFarm.Gameplay.Building;
using UnityEngine;

namespace MyLittleFarm.Core
{
    /// <summary>
    /// Создаёт, проверяет и восстанавливает полный снимок игры. Запись выполняется атомарно,
    /// чтобы прерванное сохранение не уничтожило предыдущий корректный файл.
    /// </summary>
    public sealed class SaveSystem : MonoBehaviour
    {
        // Текущая версия схемы; версия 1 поддерживается как наследуемый формат без построек.
        public const int CurrentSaveVersion = 2;

        // Пауза между автоматическими сохранениями в секундах реального времени.
        private const float AutosaveIntervalSeconds = 180f;

        // Ссылки на системы, состояние которых входит в единый снимок.
        private InputReader _input;
        private PlayerController _player;
        private GridSystem _grid;
        private CropSystem _crops;
        private InventorySystem _inventory;
        private WalletSystem _wallet;
        // Время следующего автосохранения по шкале unscaledTime.
        private float _nextAutosaveAt;
        private BuildSystem _buildings;
        // false блокирует автозапись после ошибки чтения, чтобы не затереть повреждённый файл.
        private bool _persistenceEnabled;

        /// <summary>Загружает существующий файл или разрешает сохранение новой игры.</summary>
        public void StartPersistence()
        {
            // A failed load must not be overwritten by autosave or application quit.
            _persistenceEnabled = !File.Exists(SavePath) || LoadFromDisk();
        }

        /// <summary>Платформенно безопасный путь постоянного файла сохранения.</summary>
        public string SavePath => Path.Combine(Application.persistentDataPath, "my-little-farm-save.json");

        /// <summary>Передаёт зависимости и запускает отсчёт до первого автосохранения.</summary>
        public void Configure(
            InputReader input,
            PlayerController player,
            GridSystem grid,
            CropSystem crops,
            InventorySystem inventory,
            WalletSystem wallet,
            BuildSystem buildings = null)
        {
            _input = input;
            _player = player;
            _grid = grid;
            _crops = crops;
            _inventory = inventory;
            _wallet = wallet;
            _buildings = buildings;
            _persistenceEnabled = false;
            _nextAutosaveAt = Time.unscaledTime + AutosaveIntervalSeconds;
        }

        private void Update()
        {
            // Загрузка имеет приоритет над ручным сохранением и автосохранением в одном кадре.
            if (_input == null)
            {
                return;
            }

            if (_input.LoadPressed)
            {
                LoadFromDisk();
                return;
            }

            if (_input.SavePressed)
            {
                SaveToDisk();
                return;
            }

            if (_persistenceEnabled && Time.unscaledTime >= _nextAutosaveAt)
            {
                SaveToDisk();
                _nextAutosaveAt = Time.unscaledTime + AutosaveIntervalSeconds;
            }
        }

        public SaveData Capture()
        {
            // Каждая система возвращает отделённую копию данных, пригодную для сериализации.
            var position = _player.transform.position;
            return new SaveData
            {
                saveVersion = CurrentSaveVersion,
                savedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                player = new PlayerPositionData { x = position.x, y = position.y, z = position.z },
                coins = _wallet.Coins,
                inventory = _inventory.Capture(),
                gridCells = _grid.CaptureChangedCells(),
                crops = _crops.Capture(),
                buildings = _buildings == null ? new List<BuildingRuntimeState>() : _buildings.Capture()
            };
        }

        public void Restore(SaveData data)
        {
            // Валидация идёт до первой мутации, поэтому некорректный файл не портит текущий мир.
            Validate(data);

            // Восстановление соблюдает зависимости: земля → посевы → постройки → игрок.
            _crops.ClearAll();
            _grid.Restore(data.gridCells);
            _inventory.Restore(data.inventory);
            _wallet.SetCoins(data.coins);
            _crops.Restore(data.crops);
            _buildings?.Restore(data.buildings);
            _player.Teleport(new Vector3(data.player.x, data.player.y, data.player.z));
        }

        public void SaveToDisk()
        {
            // Обёртка для рабочего пути обновляет HUD и состояние автосохранения.
            if (SaveToPath(SavePath))
            {
                _persistenceEnabled = true;
                _nextAutosaveAt = Time.unscaledTime + AutosaveIntervalSeconds;
                GameEvents.RaiseStatusChanged("Игра сохранена  F9 — загрузить");
            }
            else
            {
                GameEvents.RaiseStatusChanged("Не удалось сохранить игру");
            }
        }

        public bool LoadFromDisk()
        {
            // Ошибка чтения отключает автоматическую перезапись до успешного ручного сохранения/загрузки.
            if (LoadFromPath(SavePath))
            {
                _persistenceEnabled = true;
                _nextAutosaveAt = Time.unscaledTime + AutosaveIntervalSeconds;
                GameEvents.RaiseStatusChanged("Сохранение загружено");
                return true;
            }

            _persistenceEnabled = false;
            GameEvents.RaiseStatusChanged(
                File.Exists(SavePath)
                    ? "Сохранение повреждено или несовместимо"
                    : "Сохранение ещё не создано  F5 — сохранить");
            return false;
        }

        public bool SaveToPath(string path)
        {
            // Публичный вариант с произвольным путём используется интеграционными тестами.
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    throw new ArgumentException("Save path is required.", nameof(path));
                }

                var snapshot = Capture(); // Состояние на один согласованный момент времени.
                Validate(snapshot);
                var json = JsonUtility.ToJson(snapshot, true);
                Validate(JsonUtility.FromJson<SaveData>(json));
                WriteAtomically(path, json);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
        }

        public bool LoadFromPath(string path)
        {
            // Файл сначала полностью читается и проверяется, затем применяется к игровым системам.
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return false;
            }

            try
            {
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                Restore(data);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
        }

        private static void WriteAtomically(string path, string contents)
        {
            // Новые данные сначала попадают во временный файл; старый файл становится резервной копией.
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporaryPath = path + ".tmp";
            var backupPath = path + ".bak";
            File.WriteAllText(temporaryPath, contents);

            if (File.Exists(path))
            {
                File.Replace(temporaryPath, path, backupPath);
            }
            else
            {
                File.Move(temporaryPath, path);
            }
        }

        private void Validate(SaveData data)
        {
            // Защита охватывает версию, числа, размеры коллекций, дубликаты и связи между подсистемами.
            if (data == null)
            {
                throw new InvalidDataException("Save data is empty.");
            }

            if (data.saveVersion != 1 && data.saveVersion != CurrentSaveVersion)
            {
                throw new InvalidDataException($"Unsupported save version {data.saveVersion}.");
            }

            if (data.player == null || data.coins < 0 || data.savedAtUnixMs <= 0
                || !IsFinite(data.player.x) || !IsFinite(data.player.y) || !IsFinite(data.player.z)
                || data.player.y < 0f || data.player.y > 10f)
            {
                throw new InvalidDataException("Player or economy state is invalid.");
            }

            var playerPosition = new Vector3(data.player.x, data.player.y, data.player.z);
            if (_grid.ClampToGround(playerPosition) != playerPosition)
                throw new InvalidDataException("Player is outside the ground bounds.");
            if (data.inventory == null || data.gridCells == null || data.crops == null)
                throw new InvalidDataException("Save collections are missing.");
            if (data.saveVersion == 1) data.buildings = new List<BuildingRuntimeState>();
            if (data.buildings == null || (_buildings == null && data.buildings.Count > 0))
                throw new InvalidDataException("Building data cannot be restored.");
            var capacity = _grid.Width * _grid.Height;
            if (data.gridCells.Count > capacity || data.crops.Count > capacity || data.buildings.Count > capacity
                || data.inventory.Count > 128)
                throw new InvalidDataException("Save contains too many entries.");

            // Множества обеспечивают проверку уникальности за O(1) на элемент.
            var itemIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in data.inventory)
            {
                if (item == null
                    || string.IsNullOrWhiteSpace(item.itemId)
                    || item.amount < 0
                    || !itemIds.Add(item.itemId))
                {
                    throw new InvalidDataException("Inventory contains an invalid or duplicate item.");
                }
            }

            // Словарь одновременно обнаруживает повтор клетки и даёт состояние для проверки посева.
            var savedCells = new Dictionary<Vector2Int, GridCellState>();
            foreach (var cell in data.gridCells)
            {
                if (cell == null)
                {
                    throw new InvalidDataException("Grid state contains an empty cell.");
                }

                var position = new Vector2Int(cell.x, cell.z);
                if (!_grid.TryGetCell(position, out _)
                    || (cell.state != GridCellState.Tilled && cell.state != GridCellState.Blocked)
                    || !savedCells.TryAdd(position, cell.state))
                {
                    throw new InvalidDataException("Grid state contains an invalid or duplicate cell.");
                }
            }

            // Позиции посевов нужны также для запрета сохранённых построек поверх растений.
            var cropPositions = new HashSet<Vector2Int>();
            foreach (var crop in data.crops)
            {
                if (crop == null
                    || crop.cropId != CropSystem.PrototypeCropId
                    || !IsFinite(crop.growDurationSeconds) || crop.growDurationSeconds <= 0f
                    || crop.stageCount != 3 || crop.plantedAtUnixMs < 0
                    || !_grid.TryGetCell(crop.Position, out _)
                    || !savedCells.TryGetValue(crop.Position, out var cellState)
                    || cellState != GridCellState.Tilled
                    || !cropPositions.Add(crop.Position))
                {
                    throw new InvalidDataException("Crop state is invalid or does not match a tilled cell.");
                }
            }

            _buildings?.ValidateSnapshot(data.buildings, (x, z) =>
            {
                // Для проверки снимка используется только снимок, а не текущее состояние сцены.
                var cell = new Vector2Int(x, z);
                return cropPositions.Contains(cell) || (savedCells.TryGetValue(cell, out var state) && state == GridCellState.Blocked);
            });
            foreach (var building in data.buildings)
            {
                // Дополнительно запрещаем загружать игрока внутри коллайдера сохранённой постройки.
                BuildingDefinition definition = null;
                foreach (var candidate in BuildingDefinition.Catalog)
                    if (candidate.Id == building.definitionId) { definition = candidate; break; }
                var center = BuildingView.Center(building.x, building.z, building.quarterTurns, definition, _grid);
                var dx = Mathf.Max(0, Mathf.Abs(data.player.x - center.x) - (definition.RotatedWidth(building.quarterTurns) * _grid.CellSize * 0.5f - 0.06f));
                var dz = Mathf.Max(0, Mathf.Abs(data.player.z - center.z) - (definition.RotatedDepth(building.quarterTurns) * _grid.CellSize * 0.5f - 0.06f));
                // Circular capsule footprint, allowing CharacterController skin width at contact.
                if (dx * dx + dz * dz < 0.36f * 0.36f
                    && data.player.y < definition.Height + 1f)
                    throw new InvalidDataException("Player overlaps a saved building.");
            }
        }

        /// <summary>Отсекает NaN и бесконечность, которые разрушают мировые координаты.</summary>
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private void OnApplicationQuit()
        {
            // Автосохранение при выходе разрешено только после успешного запуска подсистемы хранения.
            if (_persistenceEnabled && _player != null)
            {
                SaveToDisk();
            }
        }
    }
}
