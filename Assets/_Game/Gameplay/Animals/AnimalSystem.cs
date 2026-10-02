using System;
using System.Collections.Generic;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.Economy;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Animals
{
    /// <summary>Управляет курами: размещением, кормлением, движением и сбором яиц.</summary>
    [DefaultExecutionOrder(-55)]
    public sealed class AnimalSystem : MonoBehaviour
    {
        [Header("Chicken Placeholder")]
        [SerializeField] private AnimalDefinition chickenAsset;
        [SerializeField, Min(1)] private int maxAnimals = 8;
        [SerializeField] private Vector2Int penMinCell = new Vector2Int(0, 3);
        [SerializeField] private Vector2Int penMaxCell = new Vector2Int(5, 7);
        [SerializeField, Min(0.1f)] private float productIntervalSeconds = 40f;
        [SerializeField, Min(0.1f)] private float movementIntervalSeconds = 1.2f;
        [SerializeField, Min(0.1f)] private float interactionDistance = 2.4f;
        [SerializeField, Min(0)] private int chickenPrice = 12;
        [SerializeField] private Color chickenColor = new Color(0.92f, 0.9f, 0.76f);

        // Состояние животных хранится по стабильному ID, а view создаётся заново при загрузке.
        private readonly Dictionary<string, AnimalRuntimeState> _animals =
            new Dictionary<string, AnimalRuntimeState>(StringComparer.Ordinal);
        private readonly Dictionary<string, AnimalView> _views =
            new Dictionary<string, AnimalView>(StringComparer.Ordinal);
        private readonly List<ScriptableObject> _placeholders = new List<ScriptableObject>();
        private IWorldGridReader _grid;
        private InventorySystem _inventory;
        private WalletSystem _wallet;
        private AnimalDefinition _chicken;
        private float _nextMovementAt;
        private int _nextAnimalNumber;

        /// <summary>Подключает grid, инвентарь и каталог предметов.</summary>
        public void Configure(IWorldGridReader grid, InventorySystem inventory, FarmCatalog catalog,
            WalletSystem wallet = null)
        {
            _grid = grid;
            _inventory = inventory;
            _wallet = wallet;
            _chicken = chickenAsset != null ? chickenAsset : CreateChickenPlaceholder();
            if (catalog == null || !catalog.TryGetItem(_chicken.FeedItemId, out _)
                || !catalog.TryGetItem(_chicken.ProductItemId, out var product)
                || product.SellPrice < 1)
                throw new InvalidOperationException("Chicken feed or product is missing from FarmCatalog.");
        }

        /// <summary>Количество размещённых животных для HUD и проверок сохранения.</summary>
        public int Count => _animals.Count;
        /// <summary>Верхняя граница животных, заданная в Inspector.</summary>
        public int MaxAnimals => maxAnimals;
        /// <summary>Стабильный ID вида, назначенного в Inspector.</summary>
        public string AnimalDefinitionId => _chicken == null ? string.Empty : _chicken.AnimalId;
        /// <summary>Цена курицы, доступная для настройки в Inspector.</summary>
        public int ChickenPrice => chickenPrice;
        /// <summary>Нижняя граница загона в клетках для редакторского макета.</summary>
        public Vector2Int PenMinCell => new Vector2Int(Mathf.Min(penMinCell.x, penMaxCell.x),
            Mathf.Min(penMinCell.y, penMaxCell.y));
        /// <summary>Верхняя граница загона в клетках для редакторского макета.</summary>
        public Vector2Int PenMaxCell => new Vector2Int(Mathf.Max(penMinCell.x, penMaxCell.x),
            Mathf.Max(penMinCell.y, penMaxCell.y));

        /// <summary>Покупает курицу на выбранной клетке загона после проверки денег и места.</summary>
        public bool TryBuyChicken(Vector2Int position, long nowUnixMs)
        {
            if (_wallet == null || _wallet.Coins < chickenPrice || !CanAddChicken(position)) return false;
            if (!_wallet.TrySpend(chickenPrice)) return false;
            if (AddChicken(position, nowUnixMs)) return true;
            _wallet.AddCoins(chickenPrice);
            return false;
        }

        /// <summary>Проверяет, находится ли клетка внутри прямоугольного загона.</summary>
        public bool IsInPen(Vector2Int position) => position.x >= Mathf.Min(penMinCell.x, penMaxCell.x)
            && position.x <= Mathf.Max(penMinCell.x, penMaxCell.x)
            && position.y >= Mathf.Min(penMinCell.y, penMaxCell.y)
            && position.y <= Mathf.Max(penMinCell.y, penMaxCell.y);

        /// <summary>Добавляет курицу в свободную клетку загона без изменения terrain occupancy.</summary>
        public bool AddChicken(Vector2Int position, long nowUnixMs)
        {
            if (nowUnixMs < 0 || !CanAddChicken(position)) return false;
            var generatedId = "chicken_" + (++_nextAnimalNumber);
            while (_animals.ContainsKey(generatedId))
                generatedId = "chicken_" + (++_nextAnimalNumber);
            var stateToAdd = new AnimalRuntimeState
            {
                animalId = generatedId,
                definitionId = _chicken.AnimalId,
                x = position.x,
                z = position.y,
                lastFedAtUnixMs = 0,
                nextProductAtUnixMs = 0,
                readyProductCount = 0
            };
            _animals.Add(stateToAdd.animalId, stateToAdd);
            _views.Add(stateToAdd.animalId,
                AnimalView.Create(_grid.GridToWorld(position), _chicken, transform));
            GameEvents.RaiseInventoryChanged();
            return true;
        }

        /// <summary>Проверяет лимит, слой мира и отсутствие другой курицы перед размещением.</summary>
        private bool CanAddChicken(Vector2Int position)
        {
            if (_grid == null || _chicken == null || _animals.Count >= maxAnimals || !IsInPen(position)
                || !_grid.CanPlantTree(position)) return false;
            foreach (var state in _animals.Values)
                if (state.Position == position) return false;
            return true;
        }

        /// <summary>Кормит конкретную курицу одной единицей пшеницы.</summary>
        public bool Feed(string animalId, long nowUnixMs)
        {
            if (animalId == null || _chicken == null || !_animals.TryGetValue(animalId, out var state)
                || state.readyProductCount > 0 || state.nextProductAtUnixMs > 0
                || _inventory == null || !_inventory.TryRemove(_chicken.FeedItemId, 1)) return false;
            state.lastFedAtUnixMs = nowUnixMs;
            state.nextProductAtUnixMs = nowUnixMs + (long)(_chicken.ProductIntervalSeconds * 1000f);
            GameEvents.RaiseStatusChanged("Курица накормлена");
            return true;
        }

        /// <summary>Собирает накопившиеся яйца из конкретной курицы.</summary>
        public bool TryCollect(string animalId, out int yield)
        {
            yield = 0;
            if (animalId == null || _chicken == null || !_animals.TryGetValue(animalId, out var state)
                || state.readyProductCount <= 0
                || _inventory == null) return false;
            var productCount = (long)state.readyProductCount * _chicken.ProductYield;
            if (productCount > int.MaxValue - _inventory.GetAmount(_chicken.ProductItemId)) return false;
            yield = (int)productCount;
            _inventory.Add(_chicken.ProductItemId, yield);
            state.readyProductCount = 0;
            state.nextProductAtUnixMs = 0;
            GameEvents.RaiseStatusChanged($"Собрано: {yield} × яйцо");
            GameEvents.RaiseActionFeedback("collect", _grid.GridToWorld(state.Position));
            return true;
        }

        /// <summary>Обрабатывает ближайшую курицу: сначала сбор яйца, затем кормление.</summary>
        public bool TryInteractNearest(Vector3 playerPosition, long nowUnixMs)
        {
            var nearest = FindNearest(playerPosition);
            if (nearest == null) return false;
            if (TryCollect(nearest.animalId, out _)) return true;
            if (Feed(nearest.animalId, nowUnixMs)) return true;
            GameEvents.RaiseStatusChanged(nearest.nextProductAtUnixMs > 0
                ? "Курица уже накормлена — яйцо созревает" : "Для кормления нужна пшеница");
            return false;
        }

        /// <summary>Возвращает true, если рядом есть животное и для него показывается подсказка.</summary>
        public bool HasNearby(Vector3 playerPosition) => FindNearest(playerPosition) != null;

        /// <summary>Подсказывает сбор, ожидание или кормление ближайшей курицы.</summary>
        public string NearbyPrompt(Vector3 playerPosition)
        {
            var nearest = FindNearest(playerPosition);
            if (nearest == null) return string.Empty;
            if (nearest.readyProductCount > 0) return "E / ЛКМ — собрать яйцо";
            if (nearest.nextProductAtUnixMs > 0) return "Курица накормлена — яйцо созревает";
            return _inventory != null && _chicken != null && _inventory.GetAmount(_chicken.FeedItemId) > 0
                ? "E / ЛКМ — покормить курицу пшеницей"
                : "Курице нужна пшеница";
        }

        /// <summary>Снимает независимую копию животных для SaveSystem.</summary>
        public List<AnimalRuntimeState> Capture()
        {
            var result = new List<AnimalRuntimeState>(_animals.Count);
            foreach (var state in _animals.Values)
                result.Add(new AnimalRuntimeState
                {
                    animalId = state.animalId, definitionId = state.definitionId,
                    x = state.x, z = state.z, lastFedAtUnixMs = state.lastFedAtUnixMs,
                    nextProductAtUnixMs = state.nextProductAtUnixMs,
                    readyProductCount = state.readyProductCount
                });
            return result;
        }

        /// <summary>Восстанавливает только проверенные состояния животных и пересоздаёт их views.</summary>
        public void Restore(List<AnimalRuntimeState> states)
        {
            ClearAll();
            if (states == null || _grid == null) return;
            foreach (var saved in states)
            {
                if (saved == null || saved.definitionId != _chicken.AnimalId || !IsInPen(saved.Position)
                    || !_grid.CanPlantTree(saved.Position) || _animals.ContainsKey(saved.animalId)) continue;
                var copy = new AnimalRuntimeState
                {
                    animalId = saved.animalId, definitionId = saved.definitionId,
                    x = saved.x, z = saved.z, lastFedAtUnixMs = saved.lastFedAtUnixMs,
                    nextProductAtUnixMs = saved.nextProductAtUnixMs,
                    readyProductCount = saved.readyProductCount
                };
                _animals.Add(copy.animalId, copy);
                _views.Add(copy.animalId, AnimalView.Create(_grid.GridToWorld(copy.Position), _chicken, transform));
            }
        }

        /// <summary>Удаляет runtime-индекс и визуальные placeholder-объекты.</summary>
        public void ClearAll()
        {
            foreach (var view in _views.Values)
                if (view != null)
                {
                    if (Application.isPlaying) Destroy(view.gameObject);
                    else DestroyImmediate(view.gameObject);
                }
            _views.Clear();
            _animals.Clear();
        }

        /// <summary>Обновляет готовность яйца и перемещает визуальные модели с заданным интервалом.</summary>
        private void Update()
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            foreach (var state in _animals.Values)
                if (state.readyProductCount == 0 && state.nextProductAtUnixMs > 0
                    && now >= state.nextProductAtUnixMs) state.readyProductCount = 1;
            if (Time.unscaledTime < _nextMovementAt) return;
            _nextMovementAt = Time.unscaledTime + movementIntervalSeconds;
            foreach (var pair in _views)
            {
                var state = _animals[pair.Key];
                var phase = (state.animalId.GetHashCode() & 1023) * 0.01f + Time.unscaledTime;
                pair.Value.SetWanderOffset(Mathf.Sin(phase) * 0.25f, Mathf.Cos(phase * 0.8f) * 0.25f);
            }
        }

        /// <summary>Ищет ближайшую курицу по горизонтальной дистанции без выделения коллекций.</summary>
        private AnimalRuntimeState FindNearest(Vector3 playerPosition)
        {
            if (_grid == null) return null;
            AnimalRuntimeState result = null;
            var bestDistance = interactionDistance * interactionDistance;
            foreach (var state in _animals.Values)
            {
                var delta = _grid.GridToWorld(state.Position) - playerPosition;
                delta.y = 0f;
                if (delta.sqrMagnitude <= bestDistance) { bestDistance = delta.sqrMagnitude; result = state; }
            }
            return result;
        }

        /// <summary>Создаёт временные настраиваемые данные курицы, пока нет art-ассета.</summary>
        private AnimalDefinition CreateChickenPlaceholder()
        {
            var definition = ScriptableObject.CreateInstance<AnimalDefinition>();
            definition.ConfigurePlaceholder("chicken", "Курица", "wheat", "egg",
                productIntervalSeconds, 1, chickenColor);
            _placeholders.Add(definition);
            return definition;
        }

        /// <summary>Показывает границы загона в Scene View для настройки клеток через Inspector.</summary>
        private void OnDrawGizmosSelected()
        {
            var grid = GetComponentInParent<GameBootstrap>()?.GetComponentInChildren<GridSystem>(true);
            if (grid == null) return;
            var minimumX = Mathf.Min(penMinCell.x, penMaxCell.x);
            var maximumX = Mathf.Max(penMinCell.x, penMaxCell.x);
            var minimumZ = Mathf.Min(penMinCell.y, penMaxCell.y);
            var maximumZ = Mathf.Max(penMinCell.y, penMaxCell.y);
            var center = grid.GridToWorld(new Vector2Int(minimumX, minimumZ))
                + new Vector3((maximumX - minimumX) * grid.CellSize * 0.5f, 0.03f,
                    (maximumZ - minimumZ) * grid.CellSize * 0.5f);
            Gizmos.color = new Color(0.95f, 0.74f, 0.22f, 0.85f);
            Gizmos.DrawWireCube(center, new Vector3((maximumX - minimumX + 1) * grid.CellSize,
                0.06f, (maximumZ - minimumZ + 1) * grid.CellSize));
        }

        /// <summary>Освобождает только временные views и ScriptableObject этого компонента.</summary>
        private void OnDestroy()
        {
            ClearAll();
            foreach (var placeholder in _placeholders)
                if (placeholder != null)
                {
                    if (Application.isPlaying) Destroy(placeholder);
                    else DestroyImmediate(placeholder);
                }
            _placeholders.Clear();
        }
    }
}
