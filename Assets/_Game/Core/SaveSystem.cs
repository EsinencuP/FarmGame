using System;
using System.Collections.Generic;
using System.IO;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.Farming;
using MyLittleFarm.Gameplay.World;
using UnityEngine;

namespace MyLittleFarm.Core
{
    /// <summary>
    /// Создаёт, проверяет и восстанавливает полный снимок игры. GridSystem v2 сохраняется
    /// разреженно по чанкам, а запись на диск выполняется через временный файл.
    /// </summary>
    public sealed class SaveSystem : MonoBehaviour
    {
        // Версия 3 вводит world-scale чанки; версии 1–2 мигрируются при чтении.
        public const int CurrentSaveVersion = 3;
        private const float AutosaveIntervalSeconds = 180f;
        private const int MaxCollectionEntries = 100_000;

        // Все зависимости передаются bootstrap, поэтому Update не выполняет поиски по сцене.
        private InputReader _input;
        private PlayerController _player;
        private GridSystem _grid;
        private CropSystem _crops;
        private InventorySystem _inventory;
        private WalletSystem _wallet;
        private BuildSystem _buildings;
        private float _nextAutosaveAt;
        // false запрещает автозапись после ошибки загрузки, чтобы не затереть повреждённый файл.
        private bool _persistenceEnabled;

        /// <summary>Платформенно безопасный путь основного файла сохранения.</summary>
        public string SavePath => Path.Combine(Application.persistentDataPath, "my-little-farm-save.json");

        /// <summary>Передаёт все источники состояния и запускает таймер автосохранения.</summary>
        public void Configure(InputReader input, PlayerController player, GridSystem grid, CropSystem crops,
            InventorySystem inventory, WalletSystem wallet, BuildSystem buildings = null)
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

        /// <summary>Загружает существующий файл или разрешает сохранение новой игры.</summary>
        public void StartPersistence()
        {
            _persistenceEnabled = !File.Exists(SavePath) || LoadFromDisk();
        }

        private void Update()
        {
            // Ручная загрузка имеет приоритет над записью и автосохранением в том же кадре.
            if (_input == null) return;
            if (_input.LoadPressed) { LoadFromDisk(); return; }
            if (_input.SavePressed) { SaveToDisk(); return; }
            if (_persistenceEnabled && Time.unscaledTime >= _nextAutosaveAt)
            {
                SaveToDisk();
                _nextAutosaveAt = Time.unscaledTime + AutosaveIntervalSeconds;
            }
        }

        /// <summary>Снимает отделённую копию изменяемого состояния всех подсистем.</summary>
        public SaveData Capture()
        {
            var position = _player.transform.position;
            return new SaveData
            {
                saveVersion = CurrentSaveVersion,
                savedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                player = new PlayerPositionData { x = position.x, y = position.y, z = position.z },
                coins = _wallet.Coins,
                inventory = _inventory.Capture(),
                grid = _grid.Serialize(),
                gridCells = new List<GridCellSaveData>(),
                crops = _crops.Capture(),
                buildings = _buildings == null ? new List<BuildingRuntimeState>() : _buildings.Capture()
            };
        }

        /// <summary>Проверяет снимок целиком и только после успеха изменяет игровой мир.</summary>
        public void Restore(SaveData data)
        {
            var migratedGrid = ValidateAndMigrate(data);
            _crops.ClearAll();
            _grid.Deserialize(migratedGrid);
            _inventory.Restore(data.inventory);
            _wallet.SetCoins(data.coins);
            _crops.Restore(data.crops);
            _buildings?.Restore(data.buildings);
            _player.Teleport(new Vector3(data.player.x, data.player.y, data.player.z));
        }

        /// <summary>Сохраняет рабочий снимок и сообщает результат в HUD.</summary>
        public void SaveToDisk()
        {
            if (SaveToPath(SavePath))
            {
                _persistenceEnabled = true;
                _nextAutosaveAt = Time.unscaledTime + AutosaveIntervalSeconds;
                GameEvents.RaiseStatusChanged("Игра сохранена  F9 — загрузить");
            }
            else GameEvents.RaiseStatusChanged("Не удалось сохранить игру");
        }

        /// <summary>Загружает рабочий снимок и блокирует автозапись при ошибке.</summary>
        public bool LoadFromDisk()
        {
            if (LoadFromPath(SavePath))
            {
                _persistenceEnabled = true;
                _nextAutosaveAt = Time.unscaledTime + AutosaveIntervalSeconds;
                GameEvents.RaiseStatusChanged("Сохранение загружено");
                return true;
            }
            _persistenceEnabled = false;
            GameEvents.RaiseStatusChanged(File.Exists(SavePath)
                ? "Сохранение повреждено или несовместимо"
                : "Сохранение ещё не создано  F5 — сохранить");
            return false;
        }

        /// <summary>Записывает снимок по произвольному пути; используется интеграционными тестами.</summary>
        public bool SaveToPath(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Save path is required.", nameof(path));
                var snapshot = Capture();
                ValidateAndMigrate(snapshot);
                var json = JsonUtility.ToJson(snapshot, true);
                ValidateAndMigrate(JsonUtility.FromJson<SaveData>(json));
                WriteAtomically(path, json);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
        }

        /// <summary>Читает и валидирует файл до первой мутации мира.</summary>
        public bool LoadFromPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
            try
            {
                Restore(JsonUtility.FromJson<SaveData>(File.ReadAllText(path)));
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
            // Сначала пишется временный файл; существующий снимок заменяется только после успешной записи.
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var temporaryPath = path + ".tmp";
            var backupPath = path + ".bak";
            File.WriteAllText(temporaryPath, contents);
            if (File.Exists(path)) File.Replace(temporaryPath, path, backupPath);
            else File.Move(temporaryPath, path);
        }

        /// <summary>Проверяет общие данные и возвращает grid в формате версии 3.</summary>
        private GridSaveData ValidateAndMigrate(SaveData data)
        {
            if (data == null) throw new InvalidDataException("Save data is empty.");
            if (data.saveVersion < 1 || data.saveVersion > CurrentSaveVersion)
                throw new InvalidDataException($"Unsupported save version {data.saveVersion}.");
            if (data.player == null || data.coins < 0 || data.savedAtUnixMs <= 0
                || !IsFinite(data.player.x) || !IsFinite(data.player.y) || !IsFinite(data.player.z)
                || data.player.y < -10f || data.player.y > 1000f)
                throw new InvalidDataException("Player or economy state is invalid.");
            if (data.inventory == null || data.crops == null)
                throw new InvalidDataException("Save collections are missing.");
            if (data.saveVersion == 1) data.buildings = new List<BuildingRuntimeState>();
            if (data.buildings == null || (_buildings == null && data.buildings.Count > 0))
                throw new InvalidDataException("Building data cannot be restored.");
            if (data.inventory.Count > 128 || data.crops.Count > MaxCollectionEntries || data.buildings.Count > MaxCollectionEntries)
                throw new InvalidDataException("Save contains too many entries.");

            ValidateInventory(data.inventory);
            var grid = data.saveVersion >= 3 ? data.grid : MigrateLegacyGrid(data);
            var savedCells = ValidateGrid(grid);
            ValidateCrops(data.crops, savedCells);
            ValidateBuildings(data.buildings, savedCells, data.player);
            ValidateOccupantLinks(data.crops, data.buildings, savedCells);

            var playerCell = _grid.WorldToGrid(new Vector3(data.player.x, 0f, data.player.z));
            if (!_grid.IsChunkLoaded(_grid.GridToChunk(playerCell)))
                throw new InvalidDataException("Player is outside loaded scene chunks.");
            return grid;
        }

        private static void ValidateInventory(List<InventoryEntryData> inventory)
        {
            // HashSet одновременно проверяет уникальность стабильных идентификаторов предметов.
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in inventory)
                if (item == null || string.IsNullOrWhiteSpace(item.itemId) || item.amount < 0 || !ids.Add(item.itemId))
                    throw new InvalidDataException("Inventory contains an invalid or duplicate item.");
        }

        private Dictionary<Vector2Int, CellData> ValidateGrid(GridSaveData grid)
        {
            if (grid?.chunks == null || grid.chunks.Count > MaxCollectionEntries)
                throw new InvalidDataException("Grid save data is missing or too large.");
            var chunkIds = new HashSet<Vector2Int>();
            var cells = new Dictionary<Vector2Int, CellData>();
            foreach (var chunk in grid.chunks)
            {
                if (chunk == null || chunk.modifiedCells == null
                    || chunk.modifiedCells.Count > _grid.ChunkSizeX * _grid.ChunkSizeZ)
                    throw new InvalidDataException("Grid contains an invalid chunk.");
                var chunkCoord = new Vector2Int(chunk.chunkX, chunk.chunkZ);
                if (!chunkIds.Add(chunkCoord)) throw new InvalidDataException("Grid contains a duplicate chunk.");
                var locals = new HashSet<Vector2Int>();
                foreach (var entry in chunk.modifiedCells)
                {
                    if (entry == null || entry.localX < 0 || entry.localX >= _grid.ChunkSizeX
                        || entry.localZ < 0 || entry.localZ >= _grid.ChunkSizeZ
                        || !IsValidCellType(entry.cellType))
                        throw new InvalidDataException("Grid contains an invalid cell.");
                    var local = new Vector2Int(entry.localX, entry.localZ);
                    if (!locals.Add(local)) throw new InvalidDataException("Grid contains a duplicate cell.");
                    var position = _grid.ChunkLocalToGrid(chunkCoord, local);
                    cells.Add(position, new CellData((CellType)entry.cellType) { occupantId = EmptyToNull(entry.occupantId) });
                }
            }
            return cells;
        }

        private static void ValidateCrops(List<CropRuntimeState> crops, Dictionary<Vector2Int, CellData> cells)
        {
            var positions = new HashSet<Vector2Int>();
            foreach (var crop in crops)
            {
                if (crop == null || crop.cropId != CropSystem.PrototypeCropId
                    || !IsFinite(crop.growDurationSeconds) || crop.growDurationSeconds <= 0f
                    || crop.stageCount != 3 || crop.plantedAtUnixMs < 0 || !positions.Add(crop.Position)
                    || !cells.TryGetValue(crop.Position, out var cell) || cell.type != CellType.Planted
                    || cell.occupantId != CropSystem.CropOccupantId(crop.Position))
                    throw new InvalidDataException("Crop state does not match its grid cell.");
            }
        }

        private void ValidateBuildings(List<BuildingRuntimeState> buildings, Dictionary<Vector2Int, CellData> cells,
            PlayerPositionData player)
        {
            // Layout проверяет ID, цены, повороты и взаимные пересечения независимо от текущего мира.
            _buildings?.ValidateSnapshot(buildings, (x, z) =>
            {
                var type = cells.TryGetValue(new Vector2Int(x, z), out var cell) ? cell.type : CellType.Grass;
                return type != CellType.Grass && type != CellType.Dirt && type != CellType.Road
                    && type != CellType.Building && type != CellType.BuildingEdge;
            });
            foreach (var state in buildings)
            {
                var definition = Definition(state.definitionId);
                if (definition == null) throw new InvalidDataException("Building definition is missing.");
                for (var x = 0; x < definition.RotatedWidth(state.quarterTurns); x++)
                for (var z = 0; z < definition.RotatedDepth(state.quarterTurns); z++)
                {
                    var position = new Vector2Int(state.x + x, state.z + z);
                    if (!cells.TryGetValue(position, out var cell) || cell.occupantId != state.id
                        || cell.type != (x == 0 && z == 0 ? CellType.Building : CellType.BuildingEdge))
                        throw new InvalidDataException("Building footprint does not match grid occupancy.");
                }

                // Игрок не может быть восстановлен внутри физического объёма постройки.
                var center = BuildingView.Center(state.x, state.z, state.quarterTurns, definition, _grid);
                var dx = Mathf.Max(0f, Mathf.Abs(player.x - center.x)
                    - (definition.RotatedWidth(state.quarterTurns) * _grid.CellSize * 0.5f - 0.06f));
                var dz = Mathf.Max(0f, Mathf.Abs(player.z - center.z)
                    - (definition.RotatedDepth(state.quarterTurns) * _grid.CellSize * 0.5f - 0.06f));
                if (dx * dx + dz * dz < 0.36f * 0.36f && player.y < definition.Height + 1f)
                    throw new InvalidDataException("Player overlaps a saved building.");
            }
        }

        private static void ValidateOccupantLinks(List<CropRuntimeState> crops,
            List<BuildingRuntimeState> buildings, Dictionary<Vector2Int, CellData> cells)
        {
            // Обратная проверка не даёт файлу спрятать «осиротевшую» занятость без данных подсистемы.
            var cropIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var crop in crops) cropIds.Add(CropSystem.CropOccupantId(crop.Position));
            var buildingIds = new HashSet<string>(StringComparer.Ordinal);
            var expectedBuildingCells = new Dictionary<Vector2Int, string>();
            foreach (var building in buildings)
            {
                buildingIds.Add(building.id);
                var definition = Definition(building.definitionId);
                for (var x = 0; x < definition.RotatedWidth(building.quarterTurns); x++)
                for (var z = 0; z < definition.RotatedDepth(building.quarterTurns); z++)
                    expectedBuildingCells[new Vector2Int(building.x + x, building.z + z)] = building.id;
            }
            foreach (var pair in cells)
            {
                var cell = pair.Value;
                if (cell.type == CellType.Planted && !cropIds.Contains(cell.occupantId))
                    throw new InvalidDataException("Grid contains an orphan crop occupant.");
                if ((cell.type == CellType.Building || cell.type == CellType.BuildingEdge)
                    && (!buildingIds.Contains(cell.occupantId)
                        || !expectedBuildingCells.TryGetValue(pair.Key, out var expectedOwner)
                        || expectedOwner != cell.occupantId))
                    throw new InvalidDataException("Grid contains an orphan building occupant.");
                if (cell.occupantId != null && cell.type != CellType.Planted
                    && cell.type != CellType.Building && cell.type != CellType.BuildingEdge)
                    throw new InvalidDataException("Grid occupant is attached to an incompatible cell type.");
            }
        }

        private static BuildingDefinition Definition(string id)
        {
            foreach (var definition in BuildingDefinition.Catalog)
                if (definition.Id == id) return definition;
            return null;
        }

        private GridSaveData MigrateLegacyGrid(SaveData data)
        {
            // Старые координаты сохраняются как мировые grid-координаты внутри стартового чанка.
            var cells = new Dictionary<Vector2Int, CellData>();
            if (data.gridCells != null)
                foreach (var old in data.gridCells)
                {
                    if (old == null || (old.state != GridCellState.Tilled && old.state != GridCellState.Blocked))
                        throw new InvalidDataException("Legacy grid contains an invalid cell.");
                    var position = new Vector2Int(old.x, old.z);
                    if (!cells.TryAdd(position, new CellData(old.state == GridCellState.Tilled ? CellType.Tilled : CellType.Locked)))
                        throw new InvalidDataException("Legacy grid contains a duplicate cell.");
                }
            foreach (var crop in data.crops)
                cells[crop.Position] = new CellData(CellType.Planted) { occupantId = CropSystem.CropOccupantId(crop.Position) };
            foreach (var building in data.buildings)
            {
                var definition = Definition(building.definitionId);
                if (definition == null) throw new InvalidDataException("Legacy building definition is missing.");
                for (var x = 0; x < definition.RotatedWidth(building.quarterTurns); x++)
                for (var z = 0; z < definition.RotatedDepth(building.quarterTurns); z++)
                    cells[new Vector2Int(building.x + x, building.z + z)] = new CellData(
                        x == 0 && z == 0 ? CellType.Building : CellType.BuildingEdge) { occupantId = building.id };
            }
            return BuildSparseGrid(cells);
        }

        private GridSaveData BuildSparseGrid(Dictionary<Vector2Int, CellData> cells)
        {
            var result = new GridSaveData();
            var chunks = new Dictionary<Vector2Int, ChunkSaveEntry>();
            foreach (var pair in cells)
            {
                var chunkCoord = _grid.GridToChunk(pair.Key);
                if (!chunks.TryGetValue(chunkCoord, out var chunk))
                {
                    chunk = new ChunkSaveEntry { chunkX = chunkCoord.x, chunkZ = chunkCoord.y };
                    chunks.Add(chunkCoord, chunk);
                    result.chunks.Add(chunk);
                }
                var local = _grid.GridToLocalCell(pair.Key);
                chunk.modifiedCells.Add(new CellSaveEntry { localX = local.x, localZ = local.y,
                    cellType = (byte)pair.Value.type, occupantId = pair.Value.occupantId });
            }
            return result;
        }

        private static bool IsValidCellType(byte value)
        {
            // Enum.IsDefined запрещает неизвестные значения и служебный OutOfBounds в файле.
            return Enum.IsDefined(typeof(CellType), (CellType)value) && (CellType)value != CellType.OutOfBounds;
        }

        private static string EmptyToNull(string value) => string.IsNullOrEmpty(value) ? null : value;
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private void OnApplicationQuit()
        {
            // Выход сохраняет игру только после успешной инициализации подсистемы хранения.
            if (_persistenceEnabled && _player != null) SaveToDisk();
        }
    }
}
