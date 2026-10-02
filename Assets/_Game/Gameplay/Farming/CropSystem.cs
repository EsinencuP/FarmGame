using System;
using System.Collections.Generic;
using MyLittleFarm.Core.Grid;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Farming
{
    /// <summary>Управляет посевами, их ростом, сбором, визуальными объектами и снимками сохранения.</summary>
    public sealed class CropSystem : MonoBehaviour
    {
        // ID является частью сохранения и не меняется через Inspector.
        public const string PrototypeCropId = "carrot";
        public const float DefaultGrowDurationSeconds = 8f;
        public const int DefaultYield = 2;

        [Header("Legacy Crop Fallback")]
        [Tooltip("Время роста моркови только для тестовой сцены без FarmCatalog; в игре настройка находится в каталоге.")]
        [SerializeField, Min(0.1f)] private float growDurationSeconds = DefaultGrowDurationSeconds;
        [Tooltip("Урожай моркови только для сцены без FarmCatalog.")]
        [SerializeField, Min(1)] private int yieldAmount = DefaultYield;
        [Tooltip("Количество визуальных стадий роста культуры.")]
        [SerializeField, Min(2)] private int stageCount = 3;
        [Tooltip("Интервал обновления внешнего вида растущих культур.")]
        [SerializeField, Min(0.02f)] private float viewRefreshInterval = 0.25f;

        // Словарь содержит только представления; игровое состояние каждого посева живёт в FarmingCell чанка.
        private readonly Dictionary<Vector2Int, CropView> _views = new Dictionary<Vector2Int, CropView>();
        private IWorldGridWriter _grid;
        // Каталог определяет параметры каждого вида; null сохраняет совместимость старых тестовых сцен.
        private FarmCatalog _catalog;
        // Время следующего визуального обновления ограничивает работу четырьмя проверками в секунду.
        private float _nextRefresh;

        /// <summary>Текущая урожайность одной клетки, установленная в Inspector.</summary>
        public int YieldAmount => yieldAmount;
        /// <summary>Число визуальных стадий, используемое новыми культурами.</summary>
        public int StageCount => stageCount;

        /// <summary>Подключает сетку, по которой проверяются клетки и вычисляются мировые позиции.</summary>
        public void Configure(IWorldGridWriter grid, FarmCatalog catalog = null)
        {
            _grid = grid;
            _catalog = catalog;
        }

        private void Update()
        {
            // Рост основан на UTC, а unscaledTime только регулирует частоту обновления представлений.
            if (Time.unscaledTime < _nextRefresh)
            {
                return;
            }

            _nextRefresh = Time.unscaledTime + viewRefreshInterval;
            RefreshViews(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        public bool Contains(Vector2Int position)
        {
            // Быстрая проверка используется строительством для запрета размещения поверх растения.
            return _grid != null && _grid.GetFarmingCell(position).soilType == CellType.Planted;
        }

        public bool TryGet(Vector2Int position, out CropRuntimeState crop)
        {
            // Возвращает состояние для подсказки прогресса и попытки сбора.
            crop = null;
            if (_grid == null) return false;
            var farming = _grid.GetFarmingCell(position);
            if (farming.soilType != CellType.Planted || string.IsNullOrEmpty(farming.cropId)) return false;
            crop = ToRuntimeState(position, farming);
            return true;
        }

        public bool Plant(Vector2Int position, long plantedAtUnixMs)
            => Plant(position, PrototypeCropId, plantedAtUnixMs);

        /// <summary>Сажает выбранный вид культуры по его определению из каталога.</summary>
        public bool Plant(Vector2Int position, string cropId, long plantedAtUnixMs)
        {
            // Посадка допустима на свободной обработанной клетке без существующего посева.
            if (_grid == null || Contains(position) || !_grid.CanPlant(position))
            {
                return false;
            }

            CropDefinition definition = null;
            if (_catalog != null && !_catalog.TryGetCrop(cropId, out definition)) return false;
            if (_catalog == null && cropId != PrototypeCropId) return false;

            var crop = new CropRuntimeState
            {
                x = position.x,
                z = position.y,
                cropId = cropId,
                plantedAtUnixMs = plantedAtUnixMs,
                growDurationSeconds = definition == null ? growDurationSeconds : definition.GrowDurationSeconds,
                stageCount = definition == null ? stageCount : definition.StageCount
            };
            _grid.SetFarmingCell(position, ToFarmingCell(crop));
            AddView(crop);
            return true;
        }

        public bool TryHarvest(Vector2Int position, long nowUnixMs, out int yield)
        {
            return TryHarvest(position, nowUnixMs, out _, out yield);
        }

        /// <summary>Собирает зрелый урожай и возвращает ID предмета и количество.</summary>
        public bool TryHarvest(Vector2Int position, long nowUnixMs, out string itemId, out int yield)
        {
            // Незрелое растение остаётся неизменным; зрелое удаляется из данных и сцены.
            itemId = null;
            yield = 0;
            if (!TryGet(position, out var crop) || !crop.IsMature(nowUnixMs))
            {
                return false;
            }

            CropDefinition definition = null;
            if (_catalog != null && !_catalog.TryGetCrop(crop.cropId, out definition)) return false;
            if (_views.TryGetValue(position, out var view))
            {
                _views.Remove(position);
                Destroy(view.gameObject);
            }

            _grid.SetFarmingCell(position, new FarmingCell { soilType = CellType.Tilled });
            itemId = definition == null ? crop.cropId : definition.HarvestItemId;
            yield = definition == null ? yieldAmount : definition.YieldAmount;
            return true;
        }

        public List<CropRuntimeState> Capture()
        {
            // Каждое состояние копируется, чтобы снимок не ссылался на рабочий словарь.
            var result = new List<CropRuntimeState>();
            foreach (var position in _grid.GetPlantedCells())
            {
                if (!TryGet(position, out var crop)) continue;
                result.Add(new CropRuntimeState
                {
                    x = crop.x,
                    z = crop.z,
                    cropId = crop.cropId,
                    plantedAtUnixMs = crop.plantedAtUnixMs,
                    growDurationSeconds = crop.growDurationSeconds,
                    stageCount = crop.stageCount
                });
            }

            return result;
        }

        public void Restore(List<CropRuntimeState> crops)
        {
            // Старые данные и views очищаются перед воссозданием проверенного снимка.
            ClearAll();
            if (crops == null)
            {
                return;
            }

            foreach (var crop in crops)
            {
                if (_grid.GetCellType(crop.Position) == CellType.Planted
                    && _grid.GetFarmingCell(crop.Position).occupantId == CropOccupantId(crop.Position))
                {
                    var copy = new CropRuntimeState { x = crop.x, z = crop.z, cropId = crop.cropId,
                        plantedAtUnixMs = crop.plantedAtUnixMs, growDurationSeconds = crop.growDurationSeconds, stageCount = crop.stageCount };
                    var existing = _grid.GetFarmingCell(crop.Position);
                    var restored = ToFarmingCell(copy);
                    restored.moisture = existing.moisture;
                    restored.fertilizer = existing.fertilizer;
                    restored.lastWateredDay = existing.lastWateredDay;
                    restored.flags = existing.flags;
                    _grid.SetFarmingCell(crop.Position, restored);
                    AddView(copy);
                }
            }
        }

        public void ClearAll()
        {
            // Уничтожает только визуальные объекты; GridSystem сбрасывается отдельно при загрузке.
            foreach (var view in _views.Values)
            {
                if (view != null)
                {
                    Destroy(view.gameObject);
                }
            }

            _views.Clear();
        }

        /// <summary>Создаёт представление для состояния, уже записанного в чанке.</summary>
        private void AddView(CropRuntimeState crop)
        {
            // Только видимые объекты имеют GameObject; данные растения не дублируются в MonoBehaviour.
            CropDefinition definition = null;
            _catalog?.TryGetCrop(crop.cropId, out definition);
            var view = CropView.Create(_grid.GridToWorld(crop.Position), definition, transform);
            _views.Add(crop.Position, view);
            view.SetStage(crop.GetStage(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), crop.stageCount);
        }

        /// <summary>Строит стабильный ID владельца клетки для связи разреженного грида с CropSystem.</summary>
        public static string CropOccupantId(Vector2Int position) => $"crop:{position.x}:{position.y}";

        private void RefreshViews(long nowUnixMs)
        {
            // Синхронизирует визуальную стадию каждой культуры с рассчитанным прогрессом.
            foreach (var pair in _views)
            {
                var farming = _grid.GetFarmingCell(pair.Key);
                if (farming.soilType != CellType.Planted) continue;
                var elapsed = ((double)nowUnixMs - farming.plantedAtUnixMs) / 1000.0;
                var ratio = farming.growDurationSeconds <= 0f ? 1f
                    : Mathf.Clamp01((float)(elapsed / farming.growDurationSeconds));
                var stage = ratio >= 1f ? farming.stageCount - 1
                    : Mathf.FloorToInt(ratio * Mathf.Max(1, farming.stageCount - 1));
                pair.Value.SetStage(stage, farming.stageCount);
            }
        }

        /// <summary>Переводит сохраняемый посев в отдельный слой чанка.</summary>
        private static FarmingCell ToFarmingCell(CropRuntimeState crop) => new FarmingCell
        {
            soilType = CellType.Planted,
            occupantId = CropOccupantId(crop.Position),
            cropId = crop.cropId,
            plantedAtUnixMs = crop.plantedAtUnixMs,
            growDurationSeconds = crop.growDurationSeconds,
            stageCount = crop.stageCount
        };

        /// <summary>Возвращает совместимое DTO из источника истины для HUD и сохранения.</summary>
        private static CropRuntimeState ToRuntimeState(Vector2Int position, FarmingCell farming) =>
            new CropRuntimeState
            {
                x = position.x,
                z = position.y,
                cropId = farming.cropId,
                plantedAtUnixMs = farming.plantedAtUnixMs,
                growDurationSeconds = farming.growDurationSeconds,
                stageCount = farming.stageCount
            };
    }
}
