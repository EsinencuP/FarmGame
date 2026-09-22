using System;
using System.Collections.Generic;
using MyLittleFarm.Core;
using UnityEngine;

namespace MyLittleFarm.Core.Grid
{
    /// <summary>
    /// Делит весь игровой мир на клетки без фиксированных границ. В памяти присутствуют только
    /// чанки, зарегистрированные объектами TilemapChunk или явно созданные загрузчиком мира.
    /// </summary>
    public sealed class GridSystem : MonoBehaviour
    {
        // Instance нужен TilemapChunk в сцене; игровые системы по-прежнему получают ссылку через bootstrap.
        public static GridSystem Instance { get; private set; }

        [Header("Cell Configuration")]
        [SerializeField, Min(0.01f)] private float cellSize = 1f;
        [Header("Chunk Configuration")]
        [SerializeField, Min(1)] private int chunkSizeX = 10;
        [SerializeField, Min(1)] private int chunkSizeZ = 10;
        [Header("Selection")]
        [SerializeField] private GameObject selectionView;
        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private Color gizmoGrassColor = new Color(0.5f, 0.8f, 0.5f, 0.3f);
        [SerializeField] private Color gizmoTilledColor = new Color(0.6f, 0.4f, 0.2f, 0.5f);
        [SerializeField] private Color gizmoBuildingColor = new Color(0.3f, 0.3f, 0.8f, 0.5f);
        [SerializeField] private Color gizmoLockedColor = new Color(0.8f, 0.2f, 0.2f, 0.3f);
        [SerializeField] private Color gizmoWaterColor = new Color(0.2f, 0.5f, 0.9f, 0.4f);

        // Ключ словаря — координата чанка, а значение содержит его плотный локальный массив.
        private readonly Dictionary<Vector2Int, ChunkData> _chunks = new Dictionary<Vector2Int, ChunkData>();
        // Зарегистрированные компоненты позволяют перед загрузкой сбросить мир к настройкам сцены.
        private readonly List<TilemapChunk> _sceneChunks = new List<TilemapChunk>();
        // Nullable-координата отличает отсутствие наведения от клетки (0, 0).
        private Vector2Int? _selected;

        public float CellSize => cellSize;
        public int ChunkSizeX => chunkSizeX;
        public int ChunkSizeZ => chunkSizeZ;
        public int LoadedChunkCount => _chunks.Count;
        public Vector2Int? SelectedCell => _selected;

        /// <summary>Сообщает подписчикам только о фактическом изменении типа клетки.</summary>
        public event Action<Vector2Int, CellType> OnCellChanged;

        private void Awake()
        {
            // В сцене допустим только один источник координат и клеточных данных.
            if (Instance != null && Instance != this)
            {
                Debug.LogError("Only one GridSystem can be active in a scene.", this);
                enabled = false;
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            // Сбрасываем singleton только если уничтожается его текущий владелец.
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Назначает геометрию сетки до регистрации чанков. Создание selectionView разрешено только
        /// редакторскому baker и тестовой фикстуре через allowSceneCreation.
        /// </summary>
        public void Configure(float newCellSize, int newChunkSizeX, int newChunkSizeZ, bool allowSceneCreation = false)
        {
            if (_chunks.Count > 0) throw new InvalidOperationException("Grid geometry cannot change after chunks are loaded.");
            if (!IsFinitePositive(newCellSize)) throw new ArgumentOutOfRangeException(nameof(newCellSize));
            if (newChunkSizeX < 1) throw new ArgumentOutOfRangeException(nameof(newChunkSizeX));
            if (newChunkSizeZ < 1) throw new ArgumentOutOfRangeException(nameof(newChunkSizeZ));

            cellSize = newCellSize;
            chunkSizeX = newChunkSizeX;
            chunkSizeZ = newChunkSizeZ;
            if (selectionView == null)
                selectionView = transform.Find("Cell Selection")?.gameObject;
            if (selectionView == null && allowSceneCreation)
                selectionView = CreateSelectionView();
            Select(null);
        }

        /// <summary>Переводит мировую точку XZ в неограниченную клеточную координату.</summary>
        public Vector2Int WorldToGrid(Vector3 worldPosition)
        {
            return new Vector2Int(
                Mathf.FloorToInt(worldPosition.x / cellSize),
                Mathf.FloorToInt(worldPosition.z / cellSize));
        }

        /// <summary>Возвращает мировой центр клетки на плоскости Y=0.</summary>
        public Vector3 GridToWorld(Vector2Int gridPosition)
        {
            return new Vector3(
                (gridPosition.x + 0.5f) * cellSize,
                0f,
                (gridPosition.y + 0.5f) * cellSize);
        }

        /// <summary>Перегрузка позволяет не создавать Vector2Int в вызывающем коде.</summary>
        public Vector3 GridToWorld(int x, int z) => GridToWorld(new Vector2Int(x, z));

        /// <summary>Совместимое имя для систем представления, созданных до GridSystem v2.</summary>
        public Vector3 CellToWorld(Vector2Int gridPosition) => GridToWorld(gridPosition);

        /// <summary>Переводит клетку в координату чанка с корректным округлением отрицательных чисел вниз.</summary>
        public Vector2Int GridToChunk(Vector2Int gridPosition)
        {
            return new Vector2Int(
                Mathf.FloorToInt((float)gridPosition.x / chunkSizeX),
                Mathf.FloorToInt((float)gridPosition.y / chunkSizeZ));
        }

        /// <summary>Возвращает положительную локальную координату клетки внутри её чанка.</summary>
        public Vector2Int GridToLocalCell(Vector2Int gridPosition)
        {
            return new Vector2Int(PositiveModulo(gridPosition.x, chunkSizeX), PositiveModulo(gridPosition.y, chunkSizeZ));
        }

        /// <summary>Собирает мировую клеточную координату из адреса чанка и локального адреса.</summary>
        public Vector2Int ChunkLocalToGrid(Vector2Int chunkCoord, Vector2Int localCell)
        {
            return new Vector2Int(
                checked(chunkCoord.x * chunkSizeX + localCell.x),
                checked(chunkCoord.y * chunkSizeZ + localCell.y));
        }

        /// <summary>Возвращает существующий чанк или создаёт новый блок с Grass-клетками.</summary>
        public ChunkData GetOrCreateChunk(Vector2Int chunkCoord)
        {
            if (!_chunks.TryGetValue(chunkCoord, out var chunk))
            {
                chunk = new ChunkData(chunkCoord, chunkSizeX, chunkSizeZ);
                _chunks.Add(chunkCoord, chunk);
            }
            return chunk;
        }

        /// <summary>Возвращает загруженный чанк либо null без скрытого создания данных.</summary>
        public ChunkData GetChunk(Vector2Int chunkCoord)
        {
            _chunks.TryGetValue(chunkCoord, out var chunk);
            return chunk;
        }

        /// <summary>Инициализирует весь чанк одним типом и очищает прежних владельцев.</summary>
        public void InitializeChunk(Vector2Int chunkCoord, CellType defaultType = CellType.Grass)
        {
            GetOrCreateChunk(chunkCoord).Fill(defaultType);
        }

        /// <summary>Инициализирует прямоугольную область мира указанным типом клеток.</summary>
        public void InitializeArea(Vector3 worldOrigin, float worldWidth, float worldDepth, CellType defaultType = CellType.Grass)
        {
            if (!IsFinitePositive(worldWidth) || !IsFinitePositive(worldDepth))
                throw new ArgumentOutOfRangeException(nameof(worldWidth));
            var start = WorldToGrid(worldOrigin);
            var cellsX = Mathf.RoundToInt(worldWidth / cellSize);
            var cellsZ = Mathf.RoundToInt(worldDepth / cellSize);
            for (var x = 0; x < cellsX; x++)
            for (var z = 0; z < cellsZ; z++)
                SetCellType(new Vector2Int(start.x + x, start.y + z), defaultType);
        }

        /// <summary>Проверяет, зарегистрирован ли указанный блок мира.</summary>
        public bool IsChunkLoaded(Vector2Int chunkCoord) => _chunks.ContainsKey(chunkCoord);

        /// <summary>Возвращает перечисление адресов загруженных чанков без копирования их содержимого.</summary>
        public IEnumerable<Vector2Int> GetLoadedChunkCoords() => _chunks.Keys;

        /// <summary>Регистрирует настройки одного сценового TilemapChunk.</summary>
        public void RegisterSceneChunk(TilemapChunk sceneChunk)
        {
            if (sceneChunk == null) throw new ArgumentNullException(nameof(sceneChunk));
            if (!_sceneChunks.Contains(sceneChunk)) _sceneChunks.Add(sceneChunk);
            sceneChunk.ApplyTo(this);
        }

        /// <summary>Регистрирует все TilemapChunk под корнем фермы до загрузки сохранения.</summary>
        public void RegisterSceneChunks(IEnumerable<TilemapChunk> sceneChunks)
        {
            if (sceneChunks == null) return;
            foreach (var sceneChunk in sceneChunks)
                if (sceneChunk != null) RegisterSceneChunk(sceneChunk);
        }

        /// <summary>Возвращает копию данных клетки; незагруженная область имеет тип OutOfBounds.</summary>
        public CellData GetCell(Vector2Int gridPosition)
        {
            var chunk = GetChunk(GridToChunk(gridPosition));
            if (chunk == null) return new CellData(CellType.OutOfBounds);
            var local = GridToLocalCell(gridPosition);
            return chunk.GetCell(local.x, local.y);
        }

        /// <summary>Возвращает только тип клетки для правил, которым не нужен occupantId.</summary>
        public CellType GetCellType(Vector2Int gridPosition) => GetCell(gridPosition).type;

        /// <summary>Try-вариант отличает незагруженную область от реальной клетки.</summary>
        public bool TryGetCell(Vector2Int gridPosition, out CellData cell)
        {
            cell = GetCell(gridPosition);
            return cell.type != CellType.OutOfBounds;
        }

        /// <summary>Переводит мировую точку в клетку и проверяет, что соответствующий чанк загружен.</summary>
        public bool TryWorldToCell(Vector3 worldPosition, out Vector2Int gridPosition)
        {
            gridPosition = WorldToGrid(worldPosition);
            return GetCellType(gridPosition) != CellType.OutOfBounds;
        }

        /// <summary>Меняет тип клетки и создаёт событие только при реальном изменении.</summary>
        public void SetCellType(Vector2Int gridPosition, CellType newType)
        {
            var chunk = GetOrCreateChunk(GridToChunk(gridPosition));
            var local = GridToLocalCell(gridPosition);
            ref var cell = ref chunk.GetCell(local.x, local.y);
            if (cell.type == newType) return;
            cell.type = newType;
            OnCellChanged?.Invoke(gridPosition, newType);
        }

        /// <summary>Записывает владельца клетки без изменения её типа.</summary>
        public void SetCellOccupant(Vector2Int gridPosition, string occupantId)
        {
            var chunk = GetOrCreateChunk(GridToChunk(gridPosition));
            var local = GridToLocalCell(gridPosition);
            chunk.GetCell(local.x, local.y).occupantId = string.IsNullOrEmpty(occupantId) ? null : occupantId;
        }

        /// <summary>Показывает единый маркер над выбранной загруженной клеткой.</summary>
        public void Select(Vector2Int? gridPosition)
        {
            _selected = gridPosition.HasValue && GetCellType(gridPosition.Value) != CellType.OutOfBounds
                ? gridPosition
                : null;
            if (selectionView == null) return;
            selectionView.SetActive(_selected.HasValue);
            if (_selected.HasValue)
                selectionView.transform.position = GridToWorld(_selected.Value) + Vector3.up * 0.03f;
        }

        /// <summary>Проверяет, можно ли вспахать клетку.</summary>
        public bool CanTill(Vector2Int gridPosition)
        {
            var cell = GetCell(gridPosition);
            return cell.occupantId == null && (cell.type == CellType.Grass || cell.type == CellType.Dirt);
        }

        /// <summary>Проверяет, можно ли посадить культуру на подготовленную свободную клетку.</summary>
        public bool CanPlant(Vector2Int gridPosition)
        {
            var cell = GetCell(gridPosition);
            return cell.occupantId == null && (cell.type == CellType.Tilled || cell.type == CellType.Watered);
        }

        /// <summary>Проверяет проходимость клетки с учётом препятствий и занятости постройкой.</summary>
        public bool IsWalkable(Vector2Int gridPosition)
        {
            var type = GetCellType(gridPosition);
            return type != CellType.Water && type != CellType.Rock && type != CellType.Locked
                && type != CellType.OutOfBounds && type != CellType.Building && type != CellType.BuildingEdge;
        }

        /// <summary>Проверяет, допускает ли тип свободной клетки строительство.</summary>
        public bool CanBuild(Vector2Int gridPosition)
        {
            var cell = GetCell(gridPosition);
            return cell.occupantId == null
                && (cell.type == CellType.Grass || cell.type == CellType.Dirt || cell.type == CellType.Road);
        }

        /// <summary>Проверяет прямоугольный footprint постройки.</summary>
        public bool IsFreeForBuilding(Vector2Int origin, Vector2Int size)
        {
            if (size.x < 1 || size.y < 1) return false;
            for (var x = 0; x < size.x; x++)
            for (var z = 0; z < size.y; z++)
                if (!CanBuild(new Vector2Int(origin.x + x, origin.y + z))) return false;
            return true;
        }

        /// <summary>Помечает первую клетку как центр постройки, остальные как края и записывает общий ID.</summary>
        public void OccupyWithBuilding(Vector2Int origin, Vector2Int size, string buildingId)
        {
            if (string.IsNullOrWhiteSpace(buildingId)) throw new ArgumentException("Building ID is required.", nameof(buildingId));
            for (var x = 0; x < size.x; x++)
            for (var z = 0; z < size.y; z++)
            {
                var position = new Vector2Int(origin.x + x, origin.y + z);
                SetCellType(position, x == 0 && z == 0 ? CellType.Building : CellType.BuildingEdge);
                SetCellOccupant(position, buildingId);
            }
        }

        /// <summary>Освобождает footprint только там, где он принадлежит указанной постройке.</summary>
        public void FreeBuilding(Vector2Int origin, Vector2Int size, string buildingId = null)
        {
            for (var x = 0; x < size.x; x++)
            for (var z = 0; z < size.y; z++)
            {
                var position = new Vector2Int(origin.x + x, origin.y + z);
                var cell = GetCell(position);
                if (buildingId != null && cell.occupantId != buildingId) continue;
                if (cell.type != CellType.Building && cell.type != CellType.BuildingEdge) continue;
                SetCellOccupant(position, null);
                SetCellType(position, CellType.Grass);
            }
        }

        /// <summary>Открывает Locked-клетки внутри прямоугольной мировой области.</summary>
        public void UnlockArea(Vector3 worldOrigin, float worldWidth, float worldDepth)
        {
            var start = WorldToGrid(worldOrigin);
            var cellsX = Mathf.RoundToInt(worldWidth / cellSize);
            var cellsZ = Mathf.RoundToInt(worldDepth / cellSize);
            for (var x = 0; x < cellsX; x++)
            for (var z = 0; z < cellsZ; z++)
            {
                var position = new Vector2Int(start.x + x, start.y + z);
                if (GetCellType(position) == CellType.Locked) SetCellType(position, CellType.Grass);
            }
        }

        /// <summary>Находит клетки указанного типа во всех загруженных чанках; операция имеет стоимость O(n).</summary>
        public List<Vector2Int> FindCellsOfType(CellType type)
        {
            var result = new List<Vector2Int>();
            foreach (var pair in _chunks)
            for (var x = 0; x < chunkSizeX; x++)
            for (var z = 0; z < chunkSizeZ; z++)
                if (pair.Value.GetCell(x, z).type == type)
                    result.Add(ChunkLocalToGrid(pair.Key, new Vector2Int(x, z)));
            return result;
        }

        /// <summary>Создаёт разреженный снимок всех клеток, отличающихся от Grass.</summary>
        public GridSaveData Serialize()
        {
            var result = new GridSaveData();
            foreach (var pair in _chunks)
            {
                var chunkEntry = new ChunkSaveEntry { chunkX = pair.Key.x, chunkZ = pair.Key.y };
                for (var x = 0; x < chunkSizeX; x++)
                for (var z = 0; z < chunkSizeZ; z++)
                {
                    ref var cell = ref pair.Value.GetCell(x, z);
                    if (cell.type == CellType.Grass && cell.occupantId == null) continue;
                    chunkEntry.modifiedCells.Add(new CellSaveEntry
                    {
                        localX = x,
                        localZ = z,
                        cellType = (byte)cell.type,
                        occupantId = cell.occupantId
                    });
                }
                if (chunkEntry.modifiedCells.Count > 0) result.chunks.Add(chunkEntry);
            }
            return result;
        }

        /// <summary>Сбрасывает сценовые чанки к их исходным типам и накладывает разреженный снимок.</summary>
        public void Deserialize(GridSaveData saveData)
        {
            _chunks.Clear();
            foreach (var sceneChunk in _sceneChunks)
                if (sceneChunk != null) sceneChunk.ApplyTo(this);
            if (saveData?.chunks == null) return;
            foreach (var chunkEntry in saveData.chunks)
            {
                var chunk = GetOrCreateChunk(new Vector2Int(chunkEntry.chunkX, chunkEntry.chunkZ));
                if (chunkEntry.modifiedCells == null) continue;
                foreach (var cellEntry in chunkEntry.modifiedCells)
                {
                    ref var cell = ref chunk.GetCell(cellEntry.localX, cellEntry.localZ);
                    cell.type = (CellType)cellEntry.cellType;
                    cell.occupantId = string.IsNullOrEmpty(cellEntry.occupantId) ? null : cellEntry.occupantId;
                }
            }
            Select(null);
        }

        private GameObject CreateSelectionView()
        {
            // Один тонкий куб заменяет отдельный Renderer на каждой клетке и не создаёт коллайдеров.
            var view = GameObject.CreatePrimitive(PrimitiveType.Cube);
            view.name = "Cell Selection";
            view.transform.SetParent(transform, false);
            view.transform.localScale = new Vector3(cellSize * 0.94f, 0.03f, cellSize * 0.94f);
            RuntimeMaterials.RemoveCollider(view);
            RuntimeMaterials.Paint(view.GetComponent<Renderer>(), new Color(1f, 0.78f, 0.20f, 0.75f));
            view.SetActive(false);
            return view;
        }

        private static int PositiveModulo(int value, int divisor) => ((value % divisor) + divisor) % divisor;
        private static bool IsFinitePositive(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private void OnDrawGizmos()
        {
            // Отладочная заливка не нагружает Scene View вне Play Mode.
            if (!drawGizmos || !Application.isPlaying) return;
            foreach (var pair in _chunks)
            {
                for (var x = 0; x < chunkSizeX; x++)
                for (var z = 0; z < chunkSizeZ; z++)
                {
                    var type = pair.Value.GetCell(x, z).type;
                    Gizmos.color = GizmoColor(type);
                    Gizmos.DrawCube(
                        GridToWorld(ChunkLocalToGrid(pair.Key, new Vector2Int(x, z))) + Vector3.up * 0.01f,
                        new Vector3(cellSize * 0.95f, 0.01f, cellSize * 0.95f));
                }
                var origin = new Vector3(pair.Key.x * chunkSizeX * cellSize, 0.02f, pair.Key.y * chunkSizeZ * cellSize);
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(origin + new Vector3(chunkSizeX * cellSize * 0.5f, 0f, chunkSizeZ * cellSize * 0.5f),
                    new Vector3(chunkSizeX * cellSize, 0.02f, chunkSizeZ * cellSize));
            }
        }

        private Color GizmoColor(CellType type)
        {
            switch (type)
            {
                case CellType.Grass: return gizmoGrassColor;
                case CellType.Tilled:
                case CellType.Watered: return gizmoTilledColor;
                case CellType.Building:
                case CellType.BuildingEdge: return gizmoBuildingColor;
                case CellType.Locked: return gizmoLockedColor;
                case CellType.Water: return gizmoWaterColor;
                case CellType.Road: return Color.gray;
                default: return Color.white;
            }
        }
    }

    /// <summary>Корневой сериализуемый блок разреженных данных GridSystem.</summary>
    [Serializable]
    public sealed class GridSaveData
    {
        // chunks содержит только чанки, где есть хотя бы одна изменённая клетка.
        public List<ChunkSaveEntry> chunks = new List<ChunkSaveEntry>();
    }

    /// <summary>Разреженные изменения одного чанка.</summary>
    [Serializable]
    public sealed class ChunkSaveEntry
    {
        public int chunkX;
        public int chunkZ;
        public List<CellSaveEntry> modifiedCells = new List<CellSaveEntry>();
    }

    /// <summary>Одна сохранённая локальная клетка чанка.</summary>
    [Serializable]
    public sealed class CellSaveEntry
    {
        public int localX;
        public int localZ;
        public byte cellType;
        public string occupantId;
    }
}
