using System;
using System.Collections.Generic;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.Gameplay.World;
using UnityEngine;

namespace MyLittleFarm.Core.Grid
{
    /// <summary>
    /// Selects visible interaction targets before the terrain behind them. Coordinates remain chunk-based.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class CellSelector : MonoBehaviour
    {
        // Буфер исключает выделения памяти в Update и позволяет отфильтровать близкие посторонние коллайдеры.
        private static readonly RaycastHit[] Hits = new RaycastHit[128];
        // Только коллайдеры поверхностей сценовых чанков допускаются для выбора клетки.
        private readonly HashSet<Collider> _terrainColliders = new HashSet<Collider>();

        [SerializeField] private LayerMask terrainLayer = ~0;
        [SerializeField] private LayerMask interactionLayer = 1 << 8;
        [SerializeField] private Camera mainCamera;
        [SerializeField, Min(1f)] private float maxRayDistance = 500f;
        [SerializeField, Range(0f, 0.5f)] private float hoverSettleTime = 0.12f;

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
        public InteractionTarget HoveredTarget { get; private set; }
        private LineRenderer _targetOutline;
        private readonly Vector3[] _outlinePoints = new Vector3[16];
        private static readonly int[] OutlinePath = { 0,1,5,4,0,2,3,1,3,7,5,7,6,4,6,2 };
        private InteractionTarget _trackedTarget;
        private Vector2Int _trackedCell;
        private bool _trackedSelection;
        private float _selectionChangedAt = float.NegativeInfinity;
        public bool IsSelectionStable => Time.unscaledTime - _selectionChangedAt >= hoverSettleTime;

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
            var pickLayer = LayerMask.NameToLayer(InteractionTarget.PickLayerName);
            if (pickLayer >= 0) interactionLayer = 1 << pickLayer;
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
            ClearSelection();
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
            if (_grid == null || _input == null || !_input.HasPointer || _input.WorldPointerBlocked || mainCamera == null
                || (_buildings != null && _buildings.IsActive))
            {
                ClearSelection();
                return;
            }

            SelectRay(mainCamera.ScreenPointToRay(_input.PointerPosition));
        }

        public void SelectRay(Ray ray)
        {
            // Only authored terrain colliders can select ground. Object triggers have their own independent pass.
            var hitCount = Physics.RaycastNonAlloc(ray, Hits, maxRayDistance, terrainLayer, QueryTriggerInteraction.Ignore);
            var terrainDistance = maxRayDistance;
            var foundTerrain = false;
            var point = default(Vector3);
            for (var index = 0; index < hitCount; index++)
            {
                var hit = Hits[index];
                if (hit.collider == null || !_terrainColliders.Contains(hit.collider) || hit.distance > terrainDistance) continue;
                terrainDistance = hit.distance;
                point = hit.point;
                foundTerrain = true;
            }

            InteractionTarget target = null;
            var nearestDistance = float.MaxValue;
            hitCount = Physics.RaycastNonAlloc(ray, Hits, foundTerrain ? terrainDistance + 0.02f : maxRayDistance,
                interactionLayer, QueryTriggerInteraction.Collide);
            for (var index = 0; index < hitCount; index++)
            {
                var hit = Hits[index];
                if (hit.collider == null || hit.distance > nearestDistance) continue;
                var candidate = hit.collider.GetComponentInParent<InteractionTarget>();
                if (candidate == null || !candidate.isActiveAndEnabled) continue;
                // Stable tie-breaking when touching pick volumes are at exactly the same distance.
                if (target != null && Mathf.Abs(hit.distance - nearestDistance) < 0.0001f
                    && candidate.GetHashCode() > target.GetHashCode()) continue;
                nearestDistance = hit.distance; target = candidate;
            }
            if (target != null) SelectTarget(target);
            else if (foundTerrain) SelectWorldPoint(point);
            else ClearSelection();
        }

        public bool SelectTarget(InteractionTarget target)
        {
            if (target == null || !target.isActiveAndEnabled || _grid == null
                || !_grid.TryWorldToCell(target.transform.position, out var cell))
            { ClearSelection(); return false; }
            HoveredWorldPos = target.transform.position;
            IsHoveringValidCell = true;
            HoveredCell = cell;
            HoveredCellType = _grid.GetCellType(cell);
            HoveredTarget = target;
            _grid.Select(null);
            ShowTargetOutline(target);
            NotifyCellChanged(cell);
            TrackHover();
            return true;
        }

        private void ShowTargetOutline(InteractionTarget target)
        {
            if (!target.TryGetBounds(out var bounds))
            { if (_targetOutline != null) _targetOutline.enabled = false; return; }
            if (_targetOutline == null)
            {
                var outline = new GameObject("Pointer target outline");
                outline.transform.SetParent(transform, false);
                _targetOutline = outline.AddComponent<LineRenderer>();
                _targetOutline.useWorldSpace = true;
                outline.layer = 2;
                _targetOutline.widthMultiplier = 0.025f;
                _targetOutline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _targetOutline.receiveShadows = false;
                RuntimeMaterials.Paint(_targetOutline, new Color(1f, 0.78f, 0.2f));
            }
            bounds.Expand(0.05f);
            var a = bounds.min;
            var b = bounds.max;
            _targetOutline.positionCount = 16;
            for (var i = 0; i < OutlinePath.Length; i++)
            {
                var corner = OutlinePath[i];
                _outlinePoints[i] = new Vector3((corner & 1) == 0 ? a.x : b.x,
                    (corner & 2) == 0 ? a.y : b.y, (corner & 4) == 0 ? a.z : b.z);
            }
            _targetOutline.SetPositions(_outlinePoints);
            _targetOutline.enabled = true;
        }

        /// <summary>Выбирает клетку по готовой мировой точке; метод также используется детерминированными тестами.</summary>
        public bool SelectWorldPoint(Vector3 worldPoint)
        {
            HoveredTarget = null;
            if (_targetOutline != null) _targetOutline.enabled = false;
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
            NotifyCellChanged(cell);
            TrackHover();
            return true;
        }

        private void NotifyCellChanged(Vector2Int cell)
        {
            if (!_hadLastCell || _lastCell != cell)
            {
                _lastCell = cell;
                _hadLastCell = true;
                OnCellChanged?.Invoke(cell, HoveredCellType);
            }
        }

        private void TrackHover()
        {
            // Moving within the same animal/tree must not restart the dwell timer when its centre crosses a grid edge.
            var changed = !ReferenceEquals(_trackedTarget, HoveredTarget) || _trackedSelection != HasSelection
                || (HoveredTarget == null && HasSelection && _trackedCell != HoveredCell);
            _trackedTarget = HoveredTarget; _trackedSelection = HasSelection; _trackedCell = HoveredCell;
            if (changed) _selectionChangedAt = Time.unscaledTime;
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
            HoveredTarget = null;
            if (_targetOutline != null) _targetOutline.enabled = false;
            // Событие OutOfBounds посылается один раз при выходе курсора из грида.
            if (IsHoveringValidCell) OnCellChanged?.Invoke(HoveredCell, CellType.OutOfBounds);
            IsHoveringValidCell = false;
            HoveredCellType = CellType.OutOfBounds;
            _hadLastCell = false;
            _grid?.Select(null);
            TrackHover();
        }

        private void OnDisable() => ClearSelection();

        private void HandleGridCellChanged(Vector2Int position, CellType type)
        {
            // Тип под неподвижным курсором должен обновиться сразу после вспашки, посадки или строительства.
            if (!IsHoveringValidCell || HoveredCell != position) return;
            HoveredCellType = type;
            OnCellChanged?.Invoke(position, type);
        }
    }
}
