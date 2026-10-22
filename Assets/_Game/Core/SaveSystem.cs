using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.Farming;
using MyLittleFarm.Gameplay.Animals;
using MyLittleFarm.Gameplay.World;
using UnityEngine;

namespace MyLittleFarm.Core
{
    /// <summary>
    /// Создаёт, проверяет и восстанавливает полный снимок игры. GridSystem v2 сохраняется
    /// разреженно по чанкам, а запись на диск выполняется через временный файл.
    /// </summary>
    public sealed partial class SaveSystem : MonoBehaviour
    {
        // Версия 5 сохраняет только отличия от базовой карты и поверхность под игровыми слоями.
        public const int CurrentSaveVersion = 11;
        private const int MaxCollectionEntries = 100_000;

        [Header("Persistence")]
        [Tooltip("Интервал автоматического сохранения в секундах реального времени.")]
        [SerializeField, Min(10f)] private float autosaveIntervalSeconds = 300f;

        // Все зависимости передаются bootstrap, поэтому Update не выполняет поиски по сцене.
        private InputReader _input;
        private PlayerController _player;
        private GridSystem _grid;
        private CropSystem _crops;
        private InventorySystem _inventory;
        private WalletSystem _wallet;
        private BuildSystem _buildings;
        private FarmCatalog _catalog;
        private QuickSlotSystem _slots;
        private SectorSystem _sector;
        private ToolUpgradeSystem _upgrades;
        private OrchardSystem _orchard;
        private AnimalSystem _animals;
        private OnboardingSystem _onboarding;
        private DayNightCycle _dayNight;
        private WeatherController _weather;
        private FarmProgression _progression;
        private float _nextAutosaveAt;
        // false запрещает автозапись после ошибки загрузки, чтобы не затереть повреждённый файл.
        private bool _persistenceEnabled;
        private string _recoveredPath;

        /// <summary>True when the latest load used the last valid backup instead of the primary file.</summary>
        public bool LastLoadUsedBackup { get; private set; }

        /// <summary>Платформенно безопасный путь основного файла сохранения.</summary>
        public string SavePath => SlotPath(ActiveSlot, false);

        /// <summary>Передаёт все источники состояния и запускает таймер автосохранения.</summary>
        public void Configure(InputReader input, PlayerController player, GridSystem grid, CropSystem crops,
            InventorySystem inventory, WalletSystem wallet, BuildSystem buildings = null, FarmCatalog catalog = null,
            QuickSlotSystem slots = null, SectorSystem sector = null, ToolUpgradeSystem upgrades = null,
            OrchardSystem orchard = null, AnimalSystem animals = null, OnboardingSystem onboarding = null)
        {
            _input = input;
            _player = player;
            _grid = grid;
            _crops = crops;
            _inventory = inventory;
            _wallet = wallet;
            _buildings = buildings;
            _catalog = catalog;
            _slots = slots;
            _sector = sector;
            _upgrades = upgrades;
            _orchard = orchard;
            _animals = animals;
            _onboarding = onboarding;
            var root=grid==null?null:grid.GetComponentInParent<GameBootstrap>();
            _dayNight=root==null?null:root.GetComponentInChildren<DayNightCycle>(true);
            _weather=root==null?null:root.GetComponentInChildren<WeatherController>(true);
            _progression=root==null?null:root.GetComponentInChildren<FarmProgression>(true);
            _persistenceEnabled = false;
            _playTimeSeconds = 0;
            _lastPreview = null;
            _newFarm = null; _showStartupMenu = false; _legacyUnavailable = false;
            _recoveredPath = null; LastLoadUsedBackup = false; ActiveSlot = 0;
            if (slotMenu != null) slotMenu.Configure(this, input);
            _nextAutosaveAt = Time.unscaledTime + autosaveIntervalSeconds;
        }

        /// <summary>Готовит новую ферму, переносит старый файл и планирует открытие трёх слотов.</summary>
        public void StartPersistence()
        {
            StartSlotPersistence();
        }

        private void Update()
        {
            // Ручная загрузка имеет приоритет над записью и автосохранением в том же кадре.
            if (_input == null) return;
            if (_persistenceEnabled && !_input.ModalInputActive && Application.isFocused)
                _playTimeSeconds += Time.unscaledDeltaTime;
            if (_input.LoadPressed)
            {
                if (slotMenu != null) slotMenu.Open(); else LoadFromDisk();
                return;
            }
            if (_input.SavePressed) { SaveToDisk(); return; }
            if (_persistenceEnabled && !_input.ModalInputActive && Time.unscaledTime >= _nextAutosaveAt)
            {
                SaveActivePoint(true);
                _nextAutosaveAt = Time.unscaledTime + autosaveIntervalSeconds;
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
                playTimeSeconds = _playTimeSeconds,
                previewPngBase64 = _lastPreview,
                progression = _progression==null?null:_progression.Capture(),
                player = new PlayerPositionData { x = position.x, y = position.y, z = position.z },
                coins = _wallet.Coins,
                inventory = _inventory.Capture(),
                grid = _grid.Serialize(),
                worldBaseHash = _grid.ComputeBaseMapHash(),
                gridCells = new List<GridCellSaveData>(),
                crops = _crops.Capture(),
                trees = _orchard == null ? new List<TreeRuntimeState>() : _orchard.Capture(),
                animals = _animals == null ? new List<AnimalRuntimeState>() : _animals.Capture(),
                buildings = _buildings == null ? new List<BuildingRuntimeState>() : _buildings.Capture(),
                selectedQuickSlot = _slots == null ? 0 : _slots.SelectedIndex,
                sectorUnlocked = _sector != null && _sector.IsUnlocked,
                toolUpgradeLevel = _upgrades == null ? 0 : _upgrades.Level,
                onboardingStep = _onboarding == null ? 0 : _onboarding.Capture(),
                onboardingSchema = 1,
                onboardingSuppliesGranted = _onboarding != null && _onboarding.SuppliesGranted,
                onboardingGardenPlanted = _onboarding != null && _onboarding.GardenPlanted,
                onboardingSaleCompleted = _onboarding != null && _onboarding.SaleCompleted,
                onboardingGardenX = _onboarding == null ? 0 : _onboarding.GardenCell.x,
                onboardingGardenZ = _onboarding == null ? 0 : _onboarding.GardenCell.y,
                hasWorldTime = _dayNight != null,
                worldHour = _dayNight == null ? 0f : _dayNight.Hour,
                weather = _weather == null ? null : _weather.Capture()
            };
        }

        /// <summary>Проверяет снимок целиком и только после успеха изменяет игровой мир.</summary>
        public void Restore(SaveData data)
        {
            // Migrations operate on a detached candidate; failures preserve both the live farm and caller's snapshot.
            if (data != null) data = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
            var migratedGrid = ValidateAndMigrate(data);
            var rollback = Capture();
            _progression?.BeginRestore();
            try { ApplySnapshot(data, migratedGrid); }
            catch
            {
                try { ApplySnapshot(rollback, ValidateAndMigrate(rollback)); }
                catch (Exception recoveryError)
                {
                    _persistenceEnabled = false;
                    Debug.LogException(recoveryError);
                }
                throw;
            }
            finally{_progression?.EndRestore();}
        }

        private void ApplySnapshot(SaveData data, GridSaveData migratedGrid)
        {
            _crops.ClearAll();
            _orchard?.ClearAll();
            _animals?.ClearAll();
            _grid.Deserialize(migratedGrid);
            _inventory.Restore(data.inventory);
            _wallet.SetCoins(data.coins);
            _crops.Restore(data.crops);
            _buildings?.Restore(data.buildings);
            _orchard?.Restore(data.trees);
            _animals?.Restore(data.animals);
            _onboarding?.Restore(data.onboardingStep, data.onboardingSuppliesGranted,
                data.onboardingGardenPlanted, new Vector2Int(data.onboardingGardenX, data.onboardingGardenZ),
                data.onboardingSaleCompleted);
            _sector?.Restore(data.sectorUnlocked);
            _upgrades?.Restore(data.toolUpgradeLevel);
            _slots?.Select(data.selectedQuickSlot);
            _player.Teleport(new Vector3(data.player.x, data.player.y, data.player.z));
            if (_weather != null && (data.weather ?? _newFarm?.weather) != null)
                _weather.Restore(data.weather ?? _newFarm.weather);
            if (_dayNight != null) _dayNight.SetHour(data.hasWorldTime ? data.worldHour : _dayNight.StartingHour);
            _playTimeSeconds = data.playTimeSeconds;
            _lastPreview = data.previewPngBase64;
            _progression?.Restore(data.progression);
            if (previewCamera != null)
            {
                var follow = previewCamera.GetComponent<IsometricCameraController>();
                if (follow != null) follow.RefreshPosition();
            }
            GameEvents.RaiseInventoryChanged();
        }

        /// <summary>Сохраняет рабочий снимок и сообщает результат в HUD.</summary>
        public void SaveToDisk()
        {
            SaveActivePoint(false);
        }

        /// <summary>Загружает последнюю доступную точку активного слота.</summary>
        public bool LoadFromDisk()
        {
            return LoadSlot(ActiveSlot, false);
        }

        /// <summary>Записывает снимок по произвольному пути; используется интеграционными тестами.</summary>
        public bool SaveToPath(string path, bool includePreview = false)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Save path is required.", nameof(path));
                var snapshot = Capture();
                if (includePreview) snapshot.previewPngBase64 = CapturePreview();
                ValidateAndMigrate(snapshot);
                var json = JsonUtility.ToJson(snapshot, true);
                ValidateAndMigrate(JsonUtility.FromJson<SaveData>(json));
                var fullPath = Path.GetFullPath(path);
                var preserveBackup = string.Equals(fullPath, _recoveredPath, StringComparison.Ordinal);
                WriteAtomically(fullPath, json, preserveBackup);
                if (preserveBackup) _recoveredPath = null;
                if (includePreview) _lastPreview = snapshot.previewPngBase64;
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
            LastLoadUsedBackup = false;
            if (string.IsNullOrWhiteSpace(path)) return false;
            try
            {
                var fullPath = Path.GetFullPath(path);
                if (TryReadValidated(fullPath, out var data))
                {
                    Restore(data);
                    if (string.Equals(fullPath, _recoveredPath, StringComparison.Ordinal)) _recoveredPath = null;
                    return true;
                }
                if (!TryReadValidated(fullPath + ".bak", out data)) return false;
                Restore(data);
                _recoveredPath = fullPath;
                LastLoadUsedBackup = true;
                Debug.LogWarning("My Little Farm: restored the last valid backup; primary save was missing or invalid.");
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
        }

        /// <summary>Validates a candidate completely before any live subsystem is changed.</summary>
        private bool TryReadValidated(string path, out SaveData data, bool quiet = false)
        {
            data = null;
            if (!File.Exists(path)) return false;
            try
            {
                if (new FileInfo(path).Length > 16 * 1024 * 1024)
                    throw new InvalidDataException("Save file is too large.");
                var candidate = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                ValidateAndMigrate(candidate);
                data = candidate;
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidDataException
                || exception is IOException || exception is UnauthorizedAccessException)
            {
                if (!quiet) Debug.LogWarning($"My Little Farm: cannot read save '{Path.GetFileName(path)}': {exception.Message}");
                return false;
            }
        }

        private static void WriteAtomically(string path, string contents, bool preserveBackup)
        {
            // Сначала пишется временный файл; существующий снимок заменяется только после успешной записи.
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var temporaryPath = path + ".tmp";
            var backupPath = path + ".bak";
            using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, true))
                    writer.Write(contents);
                stream.Flush(true);
            }
            // After recovery the good backup must not be replaced by the corrupt primary.
            if (File.Exists(path)) File.Replace(temporaryPath, path, preserveBackup ? null : backupPath);
            else File.Move(temporaryPath, path);
        }

        /// <summary>Проверяет общие данные и возвращает grid в формате разреженных чанков.</summary>
        private GridSaveData ValidateAndMigrate(SaveData data)
        {
            if (data == null) throw new InvalidDataException("Save data is empty.");
            if (data.saveVersion < 1 || data.saveVersion > CurrentSaveVersion)
                throw new InvalidDataException($"Unsupported save version {data.saveVersion}.");
            if(!FarmProgression.IsValid(data.progression)||(data.progression!=null&&_progression==null))
                throw new InvalidDataException("Farm progression state is invalid or its system is missing.");
            if (double.IsNaN(data.playTimeSeconds) || double.IsInfinity(data.playTimeSeconds)
                || data.playTimeSeconds < 0 || data.playTimeSeconds > 1e12)
                throw new InvalidDataException("Save metadata is invalid.");
            if (data.previewPngBase64 != null && data.previewPngBase64.Length > 512 * 1024)
                data.previewPngBase64 = null;
            if(data.hasWorldTime && (!IsFinite(data.worldHour)||data.worldHour<0f||data.worldHour>=24f))
                throw new InvalidDataException("World clock is invalid.");
            if(data.weather!=null&&!WeatherController.IsValid(data.weather))
                throw new InvalidDataException("Weather state is invalid.");
            if (data.saveVersion >= 5 && data.worldBaseHash != _grid.ComputeBaseMapHash())
            {
                // The MVP expands the original two-chunk scene without changing their authored cells.
                var originalChunks = new[] { Vector2Int.zero, new Vector2Int(1, 0) };
                var phaseZeroChunks = new List<Vector2Int>();
                for(var x=-1;x<=1;x++) for(var z=-1;z<=1;z++) phaseZeroChunks.Add(new Vector2Int(x,z));
                var matchesPhaseZero = phaseZeroChunks.TrueForAll(_grid.IsChunkLoaded)
                    && data.worldBaseHash == _grid.ComputeBaseMapHash(phaseZeroChunks);
                if (!matchesPhaseZero && (data.saveVersion != 5
                    || data.worldBaseHash != _grid.ComputeBaseMapHash(originalChunks)))
                    throw new InvalidDataException("Save was created for a different base world map.");
            }
            if (data.player == null || data.coins < 0 || data.savedAtUnixMs <= 0
                || !IsFinite(data.player.x) || !IsFinite(data.player.y) || !IsFinite(data.player.z)
                || data.player.y < -10f || data.player.y > 1000f)
                throw new InvalidDataException("Player or economy state is invalid.");
            if (data.inventory == null || data.crops == null)
                throw new InvalidDataException("Save collections are missing.");
            if (data.trees == null) data.trees = new List<TreeRuntimeState>();
            if (data.animals == null) data.animals = new List<AnimalRuntimeState>();
            if (data.saveVersion < 4)
            {
                // Старые файлы не содержали эти поля и используют значения новой игры.
                data.selectedQuickSlot = 0;
                data.sectorUnlocked = false;
                data.toolUpgradeLevel = 0;
            }
            if (data.onboardingSchema < 0 || data.onboardingSchema > 1
                || data.onboardingStep < 0 || data.onboardingStep > (data.onboardingSchema == 0 ? 6 : 5))
                throw new InvalidDataException("Onboarding state is invalid.");
            if (data.onboardingSchema == 0)
            {
                var legacyStep = data.onboardingStep;
                data.onboardingStep = legacyStep >= 6 ? 5 : legacyStep >= 4 ? 3 : legacyStep >= 2 ? 2 : legacyStep;
                data.onboardingGardenPlanted = legacyStep == 3;
                data.onboardingSaleCompleted = legacyStep == 5;
                data.onboardingSchema = 1;
            }
            if (data.selectedQuickSlot < 0 || data.selectedQuickSlot > 5
                || data.toolUpgradeLevel < 0 || data.toolUpgradeLevel > 2)
                throw new InvalidDataException("Progression state is invalid.");
            if (data.saveVersion == 1) data.buildings = new List<BuildingRuntimeState>();
            if (data.buildings == null || (_buildings == null && data.buildings.Count > 0))
                throw new InvalidDataException("Building data cannot be restored.");
            if (data.inventory.Count > 128 || data.crops.Count > MaxCollectionEntries
                || data.trees.Count > MaxCollectionEntries || data.animals.Count > MaxCollectionEntries
                || data.buildings.Count > MaxCollectionEntries)
                throw new InvalidDataException("Save contains too many entries.");

            ValidateInventory(data.inventory);
            var grid = data.saveVersion >= 3 ? data.grid : MigrateLegacyGrid(data);
            var savedCells = ValidateGrid(grid, data.saveVersion);
            ValidateSector(data, savedCells);
            ValidateCrops(data.crops, savedCells);
            ValidateTrees(data.trees, savedCells);
            ValidateAnimals(data.animals, savedCells);
            if (data.saveVersion < 7 && data.buildings.Count > 0)
            {
                // Some tests/older exports already carry current footprints. Validate before deciding to migrate.
                var current = true;
                try { ValidateBuildings(data.buildings, savedCells, data.player); }
                catch (InvalidDataException) { current = false; }
                if (!current)
                {
                    ValidateBuildings(data.buildings, savedCells, data.player, BuildingDefinition.LegacyCatalog);
                    ValidateOccupantLinks(data.crops, data.trees, data.buildings, savedCells, BuildingDefinition.LegacyCatalog);
                    data.grid = grid;
                    BuildingScaleMigration.Apply(data, _grid, p => _sector != null && !data.sectorUnlocked && _sector.ContainsCell(p));
                    grid = data.grid;
                    savedCells = ValidateGrid(grid, data.saveVersion);
                }
            }
            ValidateBuildings(data.buildings, savedCells, data.player);
            ValidateOccupantLinks(data.crops, data.trees, data.buildings, savedCells);

            var playerCell = _grid.WorldToGrid(new Vector3(data.player.x, 0f, data.player.z));
            if (!_grid.IsChunkLoaded(_grid.GridToChunk(playerCell)))
                throw new InvalidDataException("Player is outside loaded scene chunks.");
            if (_sector != null && !data.sectorUnlocked && _sector.ContainsCell(playerCell))
                throw new InvalidDataException("Player is inside a locked sector.");
            return grid;
        }

        /// <summary>Не допускает открытые клетки в закрытом секторе и обратную ошибку.</summary>
        private void ValidateSector(SaveData data, Dictionary<Vector2Int, CellData> cells)
        {
            if (_sector == null)
            {
                if (data.sectorUnlocked) throw new InvalidDataException("Sector system is missing.");
                return;
            }
            foreach (var pair in cells)
            {
                if (!_sector.ContainsCell(pair.Key)) continue;
                if (data.sectorUnlocked && pair.Value.type == CellType.Locked)
                    throw new InvalidDataException("Unlocked sector contains locked cells.");
                if (!data.sectorUnlocked && pair.Value.type != CellType.Locked)
                    throw new InvalidDataException("Locked sector contains open cells.");
            }
        }

        private static void ValidateInventory(List<InventoryEntryData> inventory)
        {
            // HashSet одновременно проверяет уникальность стабильных идентификаторов предметов.
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in inventory)
                if (item == null || string.IsNullOrWhiteSpace(item.itemId) || item.amount < 0 || !ids.Add(item.itemId))
                    throw new InvalidDataException("Inventory contains an invalid or duplicate item.");
        }

        private Dictionary<Vector2Int, CellData> ValidateGrid(GridSaveData grid, int saveVersion)
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
                        || !IsValidCellType(entry.cellType)
                        || (saveVersion >= 5 && !entry.hasTerrainData)
                        || (entry.hasTerrainData && !IsValidTerrainType(entry.terrainType))
                        || (entry.permissions & ~31) != 0 || entry.pathSurface > 2
                        || (entry.permissions != 0 && (entry.permissions & 1) == 0)
                        || (entry.hasTerrainData && IsNaturalCellType((CellType)entry.cellType)
                            && entry.cellType != entry.terrainType))
                        throw new InvalidDataException("Grid contains an invalid cell.");
                    var local = new Vector2Int(entry.localX, entry.localZ);
                    if (!locals.Add(local)) throw new InvalidDataException("Grid contains a duplicate cell.");
                    var position = _grid.ChunkLocalToGrid(chunkCoord, local);
                    if (!_grid.IsChunkLoaded(chunkCoord))
                        throw new InvalidDataException("Grid references a chunk absent from the base map.");
                    cells.Add(position, new CellData((CellType)entry.cellType) { occupantId = EmptyToNull(entry.occupantId) });
                }
            }
            return cells;
        }

        private void ValidateCrops(List<CropRuntimeState> crops, Dictionary<Vector2Int, CellData> cells)
        {
            var positions = new HashSet<Vector2Int>();
            foreach (var crop in crops)
            {
                if (crop == null || (_catalog == null ? crop.cropId != CropSystem.PrototypeCropId
                        : !_catalog.TryGetCrop(crop.cropId, out _))
                    || !IsFinite(crop.growDurationSeconds) || crop.growDurationSeconds <= 0f
                    || !IsFinite(crop.weatherGrowthBonusSeconds) || crop.weatherGrowthBonusSeconds<0f
                    || crop.weatherGrowthBonusSeconds>crop.growDurationSeconds
                    || crop.stageCount < 2 || crop.plantedAtUnixMs < 0 || !positions.Add(crop.Position)
                    || !cells.TryGetValue(crop.Position, out var cell) || cell.type != CellType.Planted
                    || cell.occupantId != CropSystem.CropOccupantId(crop.Position))
                    throw new InvalidDataException("Crop state does not match its grid cell.");
            }
        }

        /// <summary>Проверяет, что каждая сохранённая яблоня занимает клетку Tree с тем же ID.</summary>
        private void ValidateTrees(List<TreeRuntimeState> trees, Dictionary<Vector2Int, CellData> cells)
        {
            if (_orchard == null && trees.Count > 0)
                throw new InvalidDataException("Orchard system is missing.");
            var positions = new HashSet<Vector2Int>();
            foreach (var tree in trees)
            {
                if (tree == null || tree.treeId != _orchard.TreeDefinitionId
                    || tree.plantedAtUnixMs < 0 || tree.lastHarvestAtUnixMs < 0
                    || (tree.lastHarvestAtUnixMs > 0 && tree.lastHarvestAtUnixMs < tree.plantedAtUnixMs)
                    || !positions.Add(tree.Position)
                    || !cells.TryGetValue(tree.Position, out var cell) || cell.type != CellType.Tree
                    || cell.occupantId != OrchardSystem.TreeOccupantId(tree.Position))
                    throw new InvalidDataException("Tree state does not match its grid cell.");
            }
        }

        /// <summary>Проверяет ID, координаты и тайминги кур до изменения runtime-состояния.</summary>
        private void ValidateAnimals(List<AnimalRuntimeState> animals, Dictionary<Vector2Int, CellData> savedCells)
        {
            if (_animals == null && animals.Count > 0)
                throw new InvalidDataException("Animal system is missing.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var positions = new HashSet<Vector2Int>();
            foreach (var animal in animals)
            {
                if (animal == null || string.IsNullOrWhiteSpace(animal.animalId)
                    || animal.definitionId != _animals.AnimalDefinitionId || !ids.Add(animal.animalId)
                    || !positions.Add(animal.Position) || animal.lastFedAtUnixMs < 0
                    || animal.nextProductAtUnixMs < 0 || animal.readyProductCount < 0
                    || animal.readyProductCount > 100)
                    throw new InvalidDataException("Animal state is invalid.");
                // Валидация должна читать загружаемый снимок: текущая сцена может быть уже изменена.
                var savedType = savedCells.TryGetValue(animal.Position, out var savedCell)
                    ? savedCell.type : _grid.GetBaseTerrainCell(animal.Position).type;
                if (_animals != null && (!_animals.IsInPen(animal.Position)
                    || !_grid.IsChunkLoaded(_grid.GridToChunk(animal.Position))
                    || (savedType != CellType.Grass && savedType != CellType.Dirt
                        && savedType != CellType.Sand)))
                    throw new InvalidDataException("Animal is outside the configured pen.");
            }
            if (_animals != null && animals.Count > _animals.MaxAnimals)
                throw new InvalidDataException("Save contains too many animals.");
        }

        private void ValidateBuildings(List<BuildingRuntimeState> buildings, Dictionary<Vector2Int, CellData> cells,
            PlayerPositionData player, IReadOnlyList<BuildingDefinition> definitions = null)
        {
            definitions = definitions ?? BuildingDefinition.Catalog;
            var controller = _player == null ? null : _player.GetComponent<CharacterController>();
            var playerScale = _player == null ? Vector3.one : _player.transform.lossyScale;
            var radius = controller == null ? .24f : Mathf.Max(.01f,
                controller.radius * Mathf.Max(Mathf.Abs(playerScale.x),Mathf.Abs(playerScale.z)) - controller.skinWidth);
            var halfHeight = controller == null ? .72f : controller.height * Mathf.Abs(playerScale.y) * .5f;
            var centerY = player.y + (controller == null ? 0 : controller.center.y * playerScale.y);
            // Layout проверяет ID, цены, повороты и взаимные пересечения независимо от текущего мира.
            var layout = new BuildingLayout(definitions);
            Func<int,int,bool> blocked = (x, z) =>
            {
                var coordinate = new Vector2Int(x, z);
                var type = cells.TryGetValue(coordinate, out var cell)
                    ? cell.type : _grid.GetBaseTerrainCell(coordinate).type;
                return type != CellType.Grass && type != CellType.Dirt && type != CellType.Road
                    && type != CellType.Building && type != CellType.BuildingEdge;
            };
            foreach (var state in buildings)
                if (!layout.TryPlace(state, blocked, out var reason)) throw new InvalidDataException(reason);
            foreach (var state in buildings)
            {
                var definition = Definition(state.definitionId, definitions);
                if (definition == null) throw new InvalidDataException("Building definition is missing.");
                for (var x = 0; x < definition.RotatedWidth(state.quarterTurns); x++)
                for (var z = 0; z < definition.RotatedDepth(state.quarterTurns); z++)
                {
                    var position = new Vector2Int(state.x + x, state.z + z);
                    if (!cells.TryGetValue(position, out var cell) || cell.occupantId != state.id
                        || cell.type != (x == 0 && z == 0 ? CellType.Building : CellType.BuildingEdge))
                        throw new InvalidDataException("Building footprint does not match grid occupancy.");
                }

                // Use current physical art and capsule, not the old mannequin radius/reserved rectangle.
                var center = BuildingView.Center(state.x, state.z, state.quarterTurns, definition, _grid);
                var bounds = _buildings != null && ReferenceEquals(definitions,BuildingDefinition.Catalog)
                    ? _buildings.SavedCollisionBounds(state,definition)
                    : new Bounds(center+Vector3.up*definition.Height*.5f,
                        new Vector3(definition.RotatedWidth(state.quarterTurns)*_grid.CellSize-.12f,
                            definition.Height,definition.RotatedDepth(state.quarterTurns)*_grid.CellSize-.12f));
                var dx = Mathf.Max(0f,Mathf.Abs(player.x-bounds.center.x)-bounds.extents.x);
                var dz = Mathf.Max(0f,Mathf.Abs(player.z-bounds.center.z)-bounds.extents.z);
                if (dx * dx + dz * dz < radius * radius
                    && centerY+halfHeight > bounds.min.y+.01f && centerY-halfHeight < bounds.max.y-.01f)
                    throw new InvalidDataException("Player overlaps a saved building.");
            }
        }

        private static void ValidateOccupantLinks(List<CropRuntimeState> crops,
            List<TreeRuntimeState> trees, List<BuildingRuntimeState> buildings,
            Dictionary<Vector2Int, CellData> cells, IReadOnlyList<BuildingDefinition> definitions = null)
        {
            // Обратная проверка не даёт файлу спрятать «осиротевшую» занятость без данных подсистемы.
            var cropIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var crop in crops) cropIds.Add(CropSystem.CropOccupantId(crop.Position));
            var treeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tree in trees) treeIds.Add(OrchardSystem.TreeOccupantId(tree.Position));
            var buildingIds = new HashSet<string>(StringComparer.Ordinal);
            var expectedBuildingCells = new Dictionary<Vector2Int, string>();
            foreach (var building in buildings)
            {
                buildingIds.Add(building.id);
                var definition = Definition(building.definitionId, definitions);
                for (var x = 0; x < definition.RotatedWidth(building.quarterTurns); x++)
                for (var z = 0; z < definition.RotatedDepth(building.quarterTurns); z++)
                    expectedBuildingCells[new Vector2Int(building.x + x, building.z + z)] = building.id;
            }
            foreach (var pair in cells)
            {
                var cell = pair.Value;
                if (cell.type == CellType.Planted && !cropIds.Contains(cell.occupantId))
                    throw new InvalidDataException("Grid contains an orphan crop occupant.");
                if (cell.type == CellType.Tree && !treeIds.Contains(cell.occupantId))
                    throw new InvalidDataException("Grid contains an orphan tree occupant.");
                if ((cell.type == CellType.Building || cell.type == CellType.BuildingEdge)
                    && (!buildingIds.Contains(cell.occupantId)
                        || !expectedBuildingCells.TryGetValue(pair.Key, out var expectedOwner)
                        || expectedOwner != cell.occupantId))
                    throw new InvalidDataException("Grid contains an orphan building occupant.");
                if (cell.occupantId != null && cell.type != CellType.Planted
                    && cell.type != CellType.Tree && cell.type != CellType.Building
                    && cell.type != CellType.BuildingEdge)
                    throw new InvalidDataException("Grid occupant is attached to an incompatible cell type.");
            }
        }

        private static BuildingDefinition Definition(string id, IReadOnlyList<BuildingDefinition> definitions = null)
        {
            foreach (var definition in definitions ?? BuildingDefinition.Catalog)
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
                var definition = Definition(building.definitionId, BuildingDefinition.LegacyCatalog);
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

        /// <summary>Проверяет, что под игровым слоем записан настоящий тип поверхности.</summary>
        private static bool IsValidTerrainType(byte value)
        {
            var type = (CellType)value;
            return IsValidCellType(value) && type != CellType.Tilled && type != CellType.Planted
                && type != CellType.Watered && type != CellType.Tree
                && type != CellType.Building && type != CellType.BuildingEdge;
        }

        /// <summary>Отличает постоянную поверхность от грядки и занятия постройкой.</summary>
        private static bool IsNaturalCellType(CellType type) =>
            type != CellType.Tilled && type != CellType.Planted && type != CellType.Watered
            && type != CellType.Tree && type != CellType.Building && type != CellType.BuildingEdge;

        private static string EmptyToNull(string value) => string.IsNullOrEmpty(value) ? null : value;
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private void OnApplicationQuit()
        {
            // Выход сохраняет игру только после успешной инициализации подсистемы хранения.
            if (_persistenceEnabled && _player != null && _grid != null && _crops != null
                && _crops.IsConfigured && _inventory != null && _wallet != null) SaveActivePoint(true);
        }
    }
}
