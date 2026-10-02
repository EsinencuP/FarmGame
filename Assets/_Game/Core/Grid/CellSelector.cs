using System;
using System.Collections.Generic;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Building;
using UnityEngine;

namespace MyLittleFarm.Core.Grid
{
    /// <summary>
    /// Определяет клетку под курсором лучом по коллайдерам TilemapChunk. Координаты не ограничены,
    /// но выбор считается действительным только внутри загруженного чанка.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class CellSelector : MonoBehaviour
    {
        // Буфер исключает выделения памяти в Update и позволяет отфильтровать близкие посторонние коллайдеры.
        private static readonly RaycastHit[] Hits = new RaycastHit[16];
        // Только коллайдеры поверхностей сценовых чанков допускаются для выбора клетки.
        private readonly HashSet<Collider> _terrainColliders = new HashSet<Collider>();

        [SerializeField] private LayerMask terrainLayer = ~0;
        [SerializeField] private Camera mainCamera;
        [SerializeField, Min(1f)] private float maxRayDistance = 500f;

        // Зависимости назначаются GameBootstrap; player сохранён для будущих ограничений радиуса действия.
        private GridSystem _grid;
        private Transform _player;
        private InputReader _input;
        private BuildSystem _buildings;
        private Vector2Int _lastCell;
        private bool _hadLastCell;

        public Vector2Int HoveredCell { get; private set; }
        public Vector3 HoveredWorldPos { get; private set; }
        public bool IsHoveringValidCell { get; private set; }
        public CellType HoveredCellType { get; private set; } = CellType.OutOfBounds;
        // Старые имена сохранены как читаемые свойства для InteractionSystem.
        public bool HasSelection => IsHoveringValidCell;
        public Vector2Int SelectedPosition => HoveredCell;

        /// <summary>Срабатывает только когда курсор вошёл в другую клетку или покинул сетку.</summary>
        public event Action<Vector2Int, CellType> OnCellChanged;

        /// <summary>Назначает сетку, ввод, камеру и игрока без глобального поиска в Update.</summary>
        public void Configure(GridSystem grid, Transform player, InputReader input = null, Camera camera = null)
        {
            // Повторная конфигурация сначала снимает старую подписку, чтобы не получать событие дважды.
            if (_grid != null) _grid.OnCellChanged -= HandleGridCellChanged;
            _grid = grid;
            _player = player;
            _input = input;
            mainCamera = camera != null ? camera : Camera.main;
            _buildings = grid != null ? grid.GetComponentInParent<GameBootstrap>()?.GetComponentInChildren<BuildSystem>() : null;
            _terrainColliders.Clear();
            if (grid != null)
            {
                var root = grid.GetComponentInParent<GameBootstrap>();
                if (root != null)
                    foreach (var chunk in root.GetComponentsInChildren<TilemapChunk>(true))
                    {
                        var collider = chunk.TerrainCollider;
                        if (collider != null && collider.enabled) _terrainColliders.Add(collider);
                    }
            }
            if (_grid != null) _grid.OnCellChanged += HandleGridCellChanged;
        }

        private void OnDestroy()
        {
            // Снимает подписку, чтобы GridSystem не удерживал уничтоженный селектор.
            if (_grid != null) _grid.OnCellChanged -= HandleGridCellChanged;
        }

        private void Update()
        {
            // Поле считывается, чтобы Unity не удалял зависимость player при будущей оптимизации компонента.
            if (_player == null) ClearSelection();
            else RefreshSelection();
        }

        /// <summary>Пересчитывает наведение по текущей позиции указателя.</summary>
        public void RefreshSelection()
        {
            if (_grid == null || _input == null || !_input.HasPointer || mainCamera == null
                || (_buildings != null && _buildings.IsActive))
            {
                ClearSelection();
                return;
            }

            var ray = mainCamera.ScreenPointToRay(_input.PointerPosition);
            var hitCount = Physics.RaycastNonAlloc(ray, Hits, maxRayDistance, terrainLayer, QueryTriggerInteraction.Ignore);
            var nearestDistance = float.MaxValue;
            var found = false;
            var point = default(Vector3);
            for (var index = 0; index < hitCount; index++)
            {
                var hit = Hits[index];
                if (hit.collider == null || hit.distance >= nearestDistance
                    || !_terrainColliders.Contains(hit.collider)) continue;
                nearestDistance = hit.distance;
                point = hit.point;
                found = true;
            }

            if (!found || !SelectWorldPoint(point)) ClearSelection();
        }

        /// <summary>Выбирает клетку по готовой мировой точке; метод также используется детерминированными тестами.</summary>
        public bool SelectWorldPoint(Vector3 worldPoint)
        {
            if (_grid == null || !_grid.TryWorldToCell(worldPoint, out var cell))
            {
                ClearSelection();
                return false;
            }

            HoveredWorldPos = worldPoint;
            IsHoveringValidCell = true;
            HoveredCell = cell;
            HoveredCellType = _grid.GetCellType(cell);
            _grid.Select(cell);
            if (!_hadLastCell || _lastCell != cell)
            {
                _lastCell = cell;
                _hadLastCell = true;
                OnCellChanged?.Invoke(cell, HoveredCellType);
            }
            return true;
        }

        /// <summary>Ищет ближайшую клетку требуемого типа внутри заданного мирового радиуса.</summary>
        public bool TryGetNearestCell(Vector3 fromWorldPosition, CellType requiredType, float radius, out Vector2Int result)
        {
            var center = _grid.WorldToGrid(fromWorldPosition);
            var cellRadius = Mathf.CeilToInt(radius / _grid.CellSize);
            var bestSquaredDistance = radius * radius;
            var found = false;
            result = default;
            for (var x = -cellRadius; x <= cellRadius; x++)
            for (var z = -cellRadius; z <= cellRadius; z++)
            {
                var candidate = center + new Vector2Int(x, z);
                if (_grid.GetCellType(candidate) != requiredType) continue;
                var offset = _grid.GridToWorld(candidate) - fromWorldPosition;
                offset.y = 0f;
                if (offset.sqrMagnitude > bestSquaredDistance) continue;
                bestSquaredDistance = offset.sqrMagnitude;
                result = candidate;
                found = true;
            }
            return found;
        }

        private void ClearSelection()
        {
            // Событие OutOfBounds посылается один раз при выходе курсора из грида.
            if (IsHoveringValidCell) OnCellChanged?.Invoke(HoveredCell, CellType.OutOfBounds);
            IsHoveringValidCell = false;
            HoveredCellType = CellType.OutOfBounds;
            _hadLastCell = false;
            _grid?.Select(null);
        }

        private void HandleGridCellChanged(Vector2Int position, CellType type)
        {
            // Тип под неподвижным курсором должен обновиться сразу после вспашки, посадки или строительства.
            if (!IsHoveringValidCell || HoveredCell != position) return;
            HoveredCellType = type;
            OnCellChanged?.Invoke(position, type);
        }
    }
}
