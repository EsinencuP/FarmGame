using System;
using System.Collections.Generic;
using MyLittleFarm.Gameplay.World;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Farming
{
    /// <summary>Управляет посевами, их ростом, сбором, визуальными объектами и снимками сохранения.</summary>
    public sealed class CropSystem : MonoBehaviour
    {
        // Параметры единственной культуры прототипа используются при посадке и сборе.
        public const string PrototypeCropId = "carrot";
        public const float PrototypeGrowDurationSeconds = 8f;
        public const int PrototypeYield = 2;

        // Отдельные словари разделяют игровые данные и объекты представления по одной клетке-ключу.
        private readonly Dictionary<Vector2Int, CropRuntimeState> _crops = new Dictionary<Vector2Int, CropRuntimeState>();
        private readonly Dictionary<Vector2Int, CropView> _views = new Dictionary<Vector2Int, CropView>();
        private GridSystem _grid;
        // Время следующего визуального обновления ограничивает работу четырьмя проверками в секунду.
        private float _nextRefresh;

        /// <summary>Подключает сетку, по которой проверяются клетки и вычисляются мировые позиции.</summary>
        public void Configure(GridSystem grid)
        {
            _grid = grid;
        }

        private void Update()
        {
            // Рост основан на UTC, а unscaledTime только регулирует частоту обновления представлений.
            if (Time.unscaledTime < _nextRefresh)
            {
                return;
            }

            _nextRefresh = Time.unscaledTime + 0.25f;
            RefreshViews(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        public bool Contains(Vector2Int position)
        {
            // Быстрая проверка используется строительством для запрета размещения поверх растения.
            return _crops.ContainsKey(position);
        }

        public bool TryGet(Vector2Int position, out CropRuntimeState crop)
        {
            // Возвращает состояние для подсказки прогресса и попытки сбора.
            return _crops.TryGetValue(position, out crop);
        }

        public bool Plant(Vector2Int position, long plantedAtUnixMs)
        {
            // Посадка допустима на свободной обработанной клетке без существующего посева.
            if (_grid.IsOccupied(position) || _crops.ContainsKey(position)
                || !_grid.TryGetCell(position, out var cell)
                || cell.State != GridCellState.Tilled)
            {
                return false;
            }

            var crop = new CropRuntimeState
            {
                x = position.x,
                z = position.y,
                cropId = PrototypeCropId,
                plantedAtUnixMs = plantedAtUnixMs,
                growDurationSeconds = PrototypeGrowDurationSeconds,
                stageCount = 3
            };
            AddCrop(crop);
            return true;
        }

        public bool TryHarvest(Vector2Int position, long nowUnixMs, out int yield)
        {
            // Незрелое растение остаётся неизменным; зрелое удаляется из данных и сцены.
            yield = 0;
            if (!_crops.TryGetValue(position, out var crop) || !crop.IsMature(nowUnixMs))
            {
                return false;
            }

            _crops.Remove(position);
            if (_views.TryGetValue(position, out var view))
            {
                _views.Remove(position);
                Destroy(view.gameObject);
            }

            yield = PrototypeYield;
            return true;
        }

        public List<CropRuntimeState> Capture()
        {
            // Каждое состояние копируется, чтобы снимок не ссылался на рабочий словарь.
            var result = new List<CropRuntimeState>();
            foreach (var crop in _crops.Values)
            {
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
                if (_grid.TryGetCell(crop.Position, out var cell) && cell.State == GridCellState.Tilled)
                {
                    AddCrop(new CropRuntimeState { x = crop.x, z = crop.z, cropId = crop.cropId,
                        plantedAtUnixMs = crop.plantedAtUnixMs, growDurationSeconds = crop.growDurationSeconds, stageCount = crop.stageCount });
                }
            }
        }

        public void ClearAll()
        {
            // Уничтожает визуальные объекты и очищает обе части состояния культуры.
            foreach (var view in _views.Values)
            {
                if (view != null)
                {
                    Destroy(view.gameObject);
                }
            }

            _views.Clear();
            _crops.Clear();
        }

        private void AddCrop(CropRuntimeState crop)
        {
            // Создаёт только новый view и обновляет его, не перебирая уже существующие растения.
            _crops.Add(crop.Position, crop);
            var view = CropView.Create(_grid.CellToWorld(crop.Position), transform);
            _views.Add(crop.Position, view);
            view.SetStage(crop.GetStage(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), crop.stageCount);
        }

        private void RefreshViews(long nowUnixMs)
        {
            // Синхронизирует визуальную стадию каждой культуры с рассчитанным прогрессом.
            foreach (var pair in _crops)
            {
                if (_views.TryGetValue(pair.Key, out var view))
                {
                    view.SetStage(pair.Value.GetStage(nowUnixMs), pair.Value.stageCount);
                }
            }
        }
    }
}
