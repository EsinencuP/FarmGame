using System;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.Farming;
using UnityEngine;

namespace MyLittleFarm.Gameplay.World
{
    /// <summary>
    /// Координатор обычного действия игрока: продажа, сбор урожая, обработка земли и посадка.
    /// Сам не хранит состояние, а вызывает специализированные системы в нужном порядке.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class InteractionSystem : MonoBehaviour
    {
        // Максимальная горизонтальная дистанция до стационарной или построенной точки продажи.
        private const float SellingDistance = 2.4f;

        // Зависимости предоставляют ввод, положение игрока, выбранную клетку и игровые данные.
        private InputReader _input;
        private Transform _player;
        private Transform _saleCrate;
        private CellSelector _selector;
        private GridSystem _grid;
        private SoilSystem _soil;
        private CropSystem _crops;
        private InventorySystem _inventory;
        private SellingSystem _selling;
        // Ограничивает перестроение текста подсказки десятью разами в секунду.
        private float _nextPromptAt;

        /// <summary>Актуальная подсказка, которую отображает InteractionPromptUI.</summary>
        public string CurrentPrompt { get; private set; } = string.Empty;

        /// <summary>Подключает все системы, необходимые для обработки контекстного действия.</summary>
        public void Configure(
            InputReader input,
            Transform player,
            Transform saleCrate,
            CellSelector selector,
            GridSystem grid,
            SoilSystem soil,
            CropSystem crops,
            InventorySystem inventory,
            SellingSystem selling)
        {
            _input = input;
            _player = player;
            _saleCrate = saleCrate;
            _selector = selector;
            _grid = grid;
            _soil = soil;
            _crops = crops;
            _inventory = inventory;
            _selling = selling;
        }

        private void Update()
        {
            // Строительный режим полностью перехватывает ту же кнопку взаимодействия.
            if (_input == null || _input.BuildModeActive || _input.SuppressGameplayThisFrame) return;
            if (Time.unscaledTime >= _nextPromptAt || _input.InteractPressed)
            {
                CurrentPrompt = BuildPrompt();
                _nextPromptAt = Time.unscaledTime + 0.1f;
            }
            if (_input != null && _input.InteractPressed)
            {
                Interact(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            }
        }

        public bool Interact(long nowUnixMs)
        {
            // Продажа рядом с торговой точкой имеет приоритет над действием с клеткой.
            if (_input != null && (_input.BuildModeActive || _input.SuppressGameplayThisFrame)) return false;
            if (IsNearSaleCrate())
            {
                return _selling.SellAllCarrots() > 0;
            }

            if (!_selector.HasSelection)
            {
                return false;
            }

            return InteractWithCell(_selector.SelectedPosition, nowUnixMs);
        }

        public bool InteractWithCell(Vector2Int position, long nowUnixMs)
        {
            // Постройка или отсутствующая клетка запрещают земледельческое действие.
            if (_input != null && _input.BuildModeActive) return false;
            if (!_grid.TryGetCell(position, out var cell))
            {
                return false;
            }

            if (_crops.TryGet(position, out var crop))
            {
                // Растение имеет первый приоритет: зрелое собирается, незрелое остаётся на месте.
                if (_inventory.GetAmount(InventorySystem.CarrotId) > int.MaxValue - CropSystem.PrototypeYield) return false;
                if (!_crops.TryHarvest(position, nowUnixMs, out var yield))
                {
                    return false;
                }

                _inventory.Add(InventorySystem.CarrotId, yield);
                GameEvents.RaiseStatusChanged($"Собрано: {yield} моркови");
                return true;
            }

            if (_grid.CanTill(position))
            {
                // Первое действие переводит исходную землю в обработанную.
                var tilled = _soil.Till(position);
                if (tilled)
                {
                    GameEvents.RaiseStatusChanged("Земля обработана");
                }

                return tilled;
            }

            if (_grid.CanPlant(position))
            {
                // Семя снимается перед посадкой и возвращается, если посадка неожиданно не удалась.
                if (!_inventory.TryRemove(InventorySystem.CarrotSeedId, 1))
                {
                    GameEvents.RaiseStatusChanged("Семена закончились");
                    return false;
                }

                if (_crops.Plant(position, nowUnixMs))
                {
                    GameEvents.RaiseStatusChanged("Морковь посажена");
                    return true;
                }

                _inventory.Add(InventorySystem.CarrotSeedId, 1);
            }

            return false;
        }

        private string BuildPrompt()
        {
            // Формирует текст по тому же приоритету, что и реальное действие, чтобы подсказка не вводила в заблуждение.
            if (IsNearSaleCrate())
            {
                var amount = _inventory.GetAmount(InventorySystem.CarrotId);
                return amount > 0
                    ? $"E / ЛКМ — продать всю морковь ({amount})"
                    : "Ящик продажи — урожая пока нет";
            }

            if (!_selector.HasSelection || !_grid.TryGetCell(_selector.SelectedPosition, out var cell))
            {
                return "Подойдите к грядке";
            }

            if (_crops.TryGet(_selector.SelectedPosition, out var crop))
            {
                return crop.IsMature(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                    ? "E / ЛКМ — собрать морковь"
                    : $"Морковь растёт — {Mathf.RoundToInt(crop.GetGrowthRatio(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) * 100f)}%";
            }

            if (cell.occupantId != null || cell.type == CellType.Locked || cell.type == CellType.Water
                || cell.type == CellType.Rock || cell.type == CellType.Building || cell.type == CellType.BuildingEdge)
                return "Клетка занята  B — строительство";

            if (_grid.CanTill(_selector.SelectedPosition))
            {
                return "E / ЛКМ — обработать землю";
            }

            if (_grid.CanPlant(_selector.SelectedPosition))
            {
                var seeds = _inventory.GetAmount(InventorySystem.CarrotSeedId);
                return seeds > 0
                    ? $"E / ЛКМ — посадить морковь  Семена: {seeds}"
                    : "Семена закончились";
            }

            return "Для этой клетки пока нет действия";
        }

        private bool IsNearSaleCrate()
        {
            // Построенная торговая стойка и исходный ящик используют одно правило дистанции по плоскости XZ.
            if (_player != null && _grid.Buildings != null && _grid.Buildings.IsNearMarket(_player.position, SellingDistance)) return true;
            if (_player == null || _saleCrate == null)
            {
                return false;
            }

            var offset = _player.position - _saleCrate.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= SellingDistance * SellingDistance;
        }
    }
}
