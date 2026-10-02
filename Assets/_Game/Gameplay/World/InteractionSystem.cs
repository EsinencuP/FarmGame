using System;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.Farming;
using MyLittleFarm.Gameplay.Animals;
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
        [Header("Interaction")]
        [Tooltip("Максимальная горизонтальная дистанция до точки продажи.")]
        [SerializeField, Min(0.1f)] private float sellingDistance = 2.4f;
        [Tooltip("Интервал пересборки контекстной подсказки в секундах.")]
        [SerializeField, Min(0.02f)] private float promptRefreshInterval = 0.1f;

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
        private FarmCatalog _catalog;
        private QuickSlotSystem _slots;
        private ToolUpgradeSystem _upgrades;
        private OrchardSystem _orchard;
        private AnimalSystem _animals;
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
            SellingSystem selling,
            FarmCatalog catalog = null,
            QuickSlotSystem slots = null,
            ToolUpgradeSystem upgrades = null,
            OrchardSystem orchard = null,
            AnimalSystem animals = null)
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
            _catalog = catalog;
            _slots = slots;
            _upgrades = upgrades;
            _orchard = orchard;
            _animals = animals;
        }

        private void Update()
        {
            // Строительный режим полностью перехватывает ту же кнопку взаимодействия.
            if (_input == null || _input.BuildModeActive || _input.SuppressGameplayThisFrame) return;
            if (Time.unscaledTime >= _nextPromptAt || _input.InteractPressed)
            {
                CurrentPrompt = BuildPrompt();
                _nextPromptAt = Time.unscaledTime + promptRefreshInterval;
            }
            if (_input != null && _input.InteractPressed)
            {
                Interact(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            }
            if (_input != null && _input.PlantTreePressed && _orchard != null && _selector.HasSelection)
            {
                var planted = IsWithinReach(_selector.SelectedPosition)
                    && _orchard.PlantApple(_selector.SelectedPosition, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                GameEvents.RaiseStatusChanged(planted ? "Яблоня посажена" : "Нельзя посадить яблоню здесь");
            }
            if (_input != null && _input.BuyChickenPressed && _animals != null && _selector.HasSelection)
            {
                var bought = IsWithinReach(_selector.SelectedPosition)
                    && _animals.TryBuyChicken(_selector.SelectedPosition,
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                GameEvents.RaiseStatusChanged(bought ? "Курица поселилась в загоне"
                    : "Курица: нужен свободный загон и монеты");
            }
        }

        public bool Interact(long nowUnixMs)
        {
            // Продажа рядом с торговой точкой имеет приоритет над действием с клеткой.
            if (_input != null && (_input.BuildModeActive || _input.SuppressGameplayThisFrame)) return false;
            if (IsNearSaleCrate())
            {
                var earned = _selling.SellAllCrops();
                if (earned > 0)
                {
                    GameEvents.RaiseActionFeedback("sale", _player.position);
                    GameEvents.RaiseProgressAction("sale");
                }
                return earned > 0;
            }

            // Животное получает приоритет перед клеткой, когда игрок подошёл к курице.
            if (_animals != null && _animals.TryInteractNearest(_player.position, nowUnixMs)) return true;

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
            if (!IsWithinReach(position)) return false;

            if (_orchard != null && _orchard.Contains(position))
            {
                if (_orchard.TryHarvest(position, nowUnixMs, out _)) return true;
                GameEvents.RaiseStatusChanged("Плоды ещё не созрели");
                return false;
            }

            if (_crops.TryGet(position, out var crop))
            {
                // Растение имеет первый приоритет: зрелое собирается, незрелое остаётся на месте.
                var harvestId = crop.cropId;
                var expectedYield = _crops.YieldAmount;
                if (_catalog != null && _catalog.TryGetCrop(crop.cropId, out var definition))
                {
                    harvestId = definition.HarvestItemId;
                    expectedYield = definition.YieldAmount;
                }
                if (_inventory.GetAmount(harvestId) > int.MaxValue - expectedYield) return false;
                if (!_crops.TryHarvest(position, nowUnixMs, out var itemId, out var yield))
                {
                    return false;
                }

                _inventory.Add(itemId, yield);
                GameEvents.RaiseActionFeedback("harvest", _grid.CellToWorld(position));
                GameEvents.RaiseProgressAction("harvest");
                GameEvents.RaiseStatusChanged($"Собрано: {yield} × {CropName(crop.cropId)}");
                return true;
            }

            if (cell.type == CellType.Tree) return false;

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
                var selectedCrop = _slots?.SelectedCrop;
                var cropId = selectedCrop == null ? CropSystem.PrototypeCropId : selectedCrop.CropId;
                var seedId = selectedCrop == null ? InventorySystem.CarrotSeedId : selectedCrop.SeedItemId;
                if (!_inventory.TryRemove(seedId, 1))
                {
                    GameEvents.RaiseStatusChanged("Семена закончились");
                    return false;
                }

                if (_crops.Plant(position, cropId, nowUnixMs))
                {
                    GameEvents.RaiseActionFeedback("plant", _grid.CellToWorld(position));
                    GameEvents.RaiseProgressAction("plant");
                    GameEvents.RaiseStatusChanged($"Посажено: {CropName(cropId)}");
                    return true;
                }

                _inventory.Add(seedId, 1);
            }

            return false;
        }

        private string BuildPrompt()
        {
            // Формирует текст по тому же приоритету, что и реальное действие, чтобы подсказка не вводила в заблуждение.
            if (IsNearSaleCrate())
            {
                long amount = 0;
                if (_catalog == null) amount = _inventory.GetAmount(InventorySystem.CarrotId);
                else foreach (var definition in _catalog.Crops)
                    amount += _inventory.GetAmount(definition.HarvestItemId);
                if (_catalog != null)
                    amount += _inventory.GetAmount("apple") + _inventory.GetAmount("egg");
                var selected = _slots?.SelectedCrop;
                var shopHint = selected == null ? "P — купить семена"
                    : $"P — купить семена: {selected.DisplayName}";
                return amount > 0
                    ? $"E / ЛКМ — продать урожай ({amount})   {shopHint}"
                    : $"Рынок — {shopHint}";
            }

            if (_animals != null && _animals.HasNearby(_player.position))
                return _animals.NearbyPrompt(_player.position);

            if (!_selector.HasSelection || !_grid.TryGetCell(_selector.SelectedPosition, out var cell))
            {
                return "Подойдите к грядке";
            }
            if (!IsWithinReach(_selector.SelectedPosition)) return "Клетка слишком далеко — улучшите инструмент (U)";

            if (_crops.TryGet(_selector.SelectedPosition, out var crop))
            {
                return crop.IsMature(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                    ? $"E / ЛКМ — собрать {CropName(crop.cropId)}"
                    : $"{CropName(crop.cropId)} растёт — {Mathf.RoundToInt(crop.GetGrowthRatio(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) * 100f)}%";
            }

            if (cell.type == CellType.Tree)
                return "Яблоня — собрать плоды";

            if (_orchard != null && _grid.CanPlantTree(_selector.SelectedPosition))
                return _animals != null && _animals.IsInPen(_selector.SelectedPosition)
                    ? $"C — купить курицу ({_animals.ChickenPrice})   T — яблоня"
                    : "T — посадить яблоню   E — обработать землю";

            if (cell.occupantId != null || cell.type == CellType.Locked || cell.type == CellType.Water
                || cell.type == CellType.Rock || cell.type == CellType.Building || cell.type == CellType.BuildingEdge)
                return "Клетка занята  B — строительство";

            if (_grid.CanTill(_selector.SelectedPosition))
            {
                return "E / ЛКМ — обработать землю";
            }

            if (_grid.CanPlant(_selector.SelectedPosition))
            {
                var selectedCrop = _slots?.SelectedCrop;
                var seedId = selectedCrop == null ? InventorySystem.CarrotSeedId : selectedCrop.SeedItemId;
                var cropName = selectedCrop == null ? "морковь" : selectedCrop.DisplayName;
                var seeds = _inventory.GetAmount(seedId);
                return seeds > 0
                    ? $"E / ЛКМ — посадить {cropName}  Семена: {seeds}"
                    : "Семена закончились";
            }

            return "Для этой клетки пока нет действия";
        }

        private bool IsNearSaleCrate()
        {
            // Построенная торговая стойка и исходный ящик используют одно правило дистанции по плоскости XZ.
            if (_player != null && _grid.Buildings != null
                && _grid.Buildings.IsNearMarket(_player.position, sellingDistance)) return true;
            if (_player == null || _saleCrate == null)
            {
                return false;
            }

            var offset = _player.position - _saleCrate.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= sellingDistance * sellingDistance;
        }

        /// <summary>Возвращает отображаемое имя культуры с запасным вариантом для старого сохранения.</summary>
        private string CropName(string id)
        {
            return _catalog != null && _catalog.TryGetCrop(id, out var definition)
                ? definition.DisplayName : id;
        }

        /// <summary>Сравнивает горизонтальную дистанцию до центра клетки с текущим уровнем инструмента.</summary>
        private bool IsWithinReach(Vector2Int position)
        {
            if (_upgrades == null || _player == null) return true;
            var delta = _grid.CellToWorld(position) - _player.position;
            delta.y = 0f;
            return delta.sqrMagnitude <= _upgrades.Reach * _upgrades.Reach;
        }
    }
}
