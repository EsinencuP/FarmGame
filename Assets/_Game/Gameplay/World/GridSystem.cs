using System.Collections.Generic;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Building;
using UnityEngine;

namespace MyLittleFarm.Gameplay.World
{
    /// <summary>
    /// Источник истины для клеток фермы: создаёт поле, переводит мировые координаты
    /// в клеточные, меняет состояние земли и управляет подсветкой выбора.
    /// </summary>
    public sealed class GridSystem : MonoBehaviour
    {
        // Разделяемые цвета показывают исходную, обработанную и выбранную клетку.
        private static readonly Color SoilColor = new Color(0.34f, 0.55f, 0.25f);
        private static readonly Color TilledColor = new Color(0.40f, 0.22f, 0.11f);
        private static readonly Color SelectionColor = new Color(1f, 0.78f, 0.20f);

        // Словарь позволяет получить клетку по координате без перебора всей сетки.
        private readonly Dictionary<Vector2Int, GridCell> _cells = new Dictionary<Vector2Int, GridCell>();
        // Nullable-координата хранит текущую подсвеченную клетку либо отсутствие выбора.
        private Vector2Int? _selected;
        // Origin — мировой угол сетки; остальные поля задают её геометрию.
        private Vector3 _origin;
        private int _width;
        private int _height;
        private float _cellSize;

        public int Width => _width;
        public int Height => _height;
        public float CellSize => _cellSize;
        // Ссылка на строительство добавляет отдельный слой занятости поверх состояния земли.
        public BuildSystem Buildings { get; internal set; }
        /// <summary>Проверяет, занят ли клеточный адрес постройкой.</summary>
        public bool IsOccupied(Vector2Int position) => Buildings != null && Buildings.IsOccupied(position);
        /// <summary>Удерживает мировую позицию в пределах созданной травяной площадки.</summary>
        public Vector3 ClampToGround(Vector3 position)
        {
            position.x = Mathf.Clamp(position.x, -(_width + 8) * _cellSize * 0.5f + 0.6f, (_width + 8) * _cellSize * 0.5f - 0.6f);
            position.z = Mathf.Clamp(position.z, -(_height + 8) * _cellSize * 0.5f + 0.6f, (_height + 8) * _cellSize * 0.5f - 0.6f);
            return position;
        }

        public void Configure(int width, int height, float cellSize, bool allowSceneCreation = false)
        {
            // Повторная конфигурация запрещена, иначе словарь и визуальные клетки разойдутся.
            if (_cells.Count != 0) throw new System.InvalidOperationException("Grid is already configured.");
            if (width < 1 || height < 1 || cellSize <= 0.1f || float.IsNaN(cellSize) || float.IsInfinity(cellSize))
                throw new System.ArgumentOutOfRangeException(nameof(cellSize));
            _width = width;
            _height = height;
            _cellSize = cellSize;
            _origin = new Vector3(-width * cellSize * 0.5f, 0f, -height * cellSize * 0.5f);

            // В выгруженной сцене клетки уже существуют и несут координаты в GridCellView.
            var existingViews = GetComponentsInChildren<GridCellView>(true);
            if (existingViews.Length > 0)
            {
                foreach (var view in existingViews)
                {
                    if (!_cells.TryAdd(view.Position, new GridCell(view.Position, view.State, view.gameObject)))
                    {
                        throw new System.InvalidOperationException($"Duplicate grid cell {view.Position}.");
                    }

                    RuntimeMaterials.Paint(view.GetComponent<Renderer>(),
                        view.State == GridCellState.Tilled ? TilledColor : SoilColor);
                }

                if (_cells.Count != width * height)
                {
                    throw new System.InvalidOperationException(
                        $"Baked grid contains {_cells.Count} cells instead of {width * height}.");
                }

                // Рамка не входит в словарь клеток, поэтому её материал восстанавливается отдельно.
                var border = transform.Find("Farm Grid/Grass Border");
                if (border != null && border.TryGetComponent<Renderer>(out var borderRenderer))
                {
                    RuntimeMaterials.Paint(borderRenderer, new Color(0.24f, 0.45f, 0.18f));
                }

                return;
            }

            if (!allowSceneCreation)
            {
                throw new System.InvalidOperationException(
                    "Baked grid objects are missing. Rebuild Prototype scene in Edit Mode.");
            }

            // Создание разрешено только editor baker и тестовой фикстуре.
            var root = new GameObject("Farm Grid").transform;
            root.SetParent(transform, false);

            // Двойной цикл создаёт полный прямоугольник клеток и их исходное состояние Soil.
            for (var z = 0; z < height; z++)
            {
                for (var x = 0; x < width; x++)
                {
                    var position = new Vector2Int(x, z);
                    var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tile.name = $"Cell {x},{z}";
                    tile.transform.SetParent(root, false);
                    tile.transform.position = CellToWorld(position) + new Vector3(0f, -0.08f, 0f);
                    tile.transform.localScale = new Vector3(cellSize - 0.06f, 0.14f, cellSize - 0.06f);
                    RuntimeMaterials.Paint(tile.GetComponent<Renderer>(), SoilColor);
                    RuntimeMaterials.RemoveCollider(tile);
                    tile.AddComponent<GridCellView>().Configure(position, GridCellState.Soil);
                    _cells.Add(position, new GridCell(position, GridCellState.Soil, tile));
                }
            }

            CreateSurroundings(root);
        }

        public bool TryGetCell(Vector2Int position, out GridCell cell)
        {
            // Try-паттерн возвращает false вместо исключения для координаты за пределами участка.
            return _cells.TryGetValue(position, out cell);
        }

        public bool TryWorldToCell(Vector3 worldPosition, out Vector2Int position)
        {
            // Смещение от origin делится на размер клетки и округляется вниз до индекса.
            var local = worldPosition - _origin;
            position = new Vector2Int(
                Mathf.FloorToInt(local.x / _cellSize),
                Mathf.FloorToInt(local.z / _cellSize));
            return _cells.ContainsKey(position);
        }

        public Vector3 CellToWorld(Vector2Int position)
        {
            // Смещение 0.5 помещает результат в центр клетки, а не в её угол.
            return _origin + new Vector3(
                (position.x + 0.5f) * _cellSize,
                0f,
                (position.y + 0.5f) * _cellSize);
        }

        public void SetCellState(Vector2Int position, GridCellState state)
        {
            // После изменения данных цвет немедленно синхронизируется с новым состоянием.
            if (!_cells.TryGetValue(position, out var cell))
            {
                return;
            }

            cell.SetState(state);
            RefreshCellColor(cell);
        }

        public void Select(Vector2Int? position)
        {
            // Сначала запоминается новый выбор, затем старая и новая клетки перекрашиваются корректно.
            if (_selected == position) return;
            var oldPosition = _selected;
            _selected = position;
            if (oldPosition.HasValue && _cells.TryGetValue(oldPosition.Value, out var previous))
            {
                RefreshCellColor(previous);
            }

            if (_selected.HasValue && _cells.TryGetValue(_selected.Value, out var current))
            {
                RefreshCellColor(current);
            }
        }

        public List<GridCellSaveData> CaptureChangedCells()
        {
            // Исходные Soil-клетки не записываются, уменьшая размер файла сохранения.
            var result = new List<GridCellSaveData>();
            foreach (var pair in _cells)
            {
                if (pair.Value.State == GridCellState.Soil)
                {
                    continue;
                }

                result.Add(new GridCellSaveData
                {
                    x = pair.Key.x,
                    z = pair.Key.y,
                    state = pair.Value.State
                });
            }

            return result;
        }

        public void Restore(List<GridCellSaveData> cells)
        {
            // Восстановление начинается с чистого состояния и применяет только сохранённые отличия.
            foreach (var cell in _cells.Values)
            {
                cell.SetState(GridCellState.Soil);
                RefreshCellColor(cell);
            }

            if (cells == null)
            {
                return;
            }

            foreach (var savedCell in cells)
            {
                SetCellState(new Vector2Int(savedCell.x, savedCell.z), savedCell.state);
            }
        }

        private void RefreshCellColor(GridCell cell)
        {
            // Подсветка имеет приоритет над цветом логического состояния.
            if (_selected.HasValue && _selected.Value == cell.Position)
            {
                RuntimeMaterials.Paint(cell.Renderer, SelectionColor);
                return;
            }

            RuntimeMaterials.Paint(cell.Renderer, cell.State == GridCellState.Tilled ? TilledColor : SoilColor);
        }

        private void CreateSurroundings(Transform root)
        {
            // Общий куб служит физической землёй и визуальной рамкой вокруг клеток.
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Grass Border";
            ground.transform.SetParent(root, false);
            ground.transform.position = new Vector3(0f, -0.35f, 0f);
            ground.transform.localScale = new Vector3((_width + 8) * _cellSize, 0.5f, (_height + 8) * _cellSize);
            RuntimeMaterials.Paint(ground.GetComponent<Renderer>(), new Color(0.24f, 0.45f, 0.18f));
        }
    }
}
