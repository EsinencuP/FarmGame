using System;
using System.Collections.Generic;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.Farming;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Animals
{
    /// <summary>Содержит кур и коров, сохраняя их определения, производство и привязку к загону.</summary>
    [DefaultExecutionOrder(-55)]
    public sealed partial class AnimalSystem : MonoBehaviour
    {
        [Header("Chicken Placeholder")]
        [SerializeField] private AnimalDefinition chickenAsset;
        [SerializeField, Min(1)] private int maxAnimals = 8;
        [SerializeField] private Vector2Int penMinCell = new Vector2Int(0, 3);
        [SerializeField] private Vector2Int penMaxCell = new Vector2Int(5, 7);
        [SerializeField, Min(0.1f)] private float productIntervalSeconds = 40f;
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
        private MyLittleFarm.UI.AnimalStatusOverlay _status;
        internal ICollection<AnimalView> Views => _views.Values;
        private int _nextAnimalNumber;

        /// <summary>Подключает grid, инвентарь и каталог предметов.</summary>
        public void Configure(IWorldGridReader grid, InventorySystem inventory, FarmCatalog catalog,
            WalletSystem wallet = null)
        {
            _grid = grid;
            _inventory = inventory;
            _wallet = wallet;
            if(Application.isPlaying)
            {
                var farm=GetComponentInParent<GameBootstrap>();
                _status=GetComponent<MyLittleFarm.UI.AnimalStatusOverlay>() ?? gameObject.AddComponent<MyLittleFarm.UI.AnimalStatusOverlay>();
                _status.Configure(farm.GetComponent<InputReader>(),farm.GetComponentInChildren<Camera>());
            }
            _chicken = chickenAsset != null ? chickenAsset : CreateChickenPlaceholder();
            _cow = cowAsset != null ? cowAsset : Resources.Load<AnimalDefinition>("FarmContent/Cow");
            if (catalog == null || !catalog.TryGetItem(_chicken.FeedItemId, out _)
                || !catalog.TryGetItem(_chicken.ProductItemId, out var product)
                || product.SellPrice < 1)
                throw new InvalidOperationException("Chicken feed or product is missing from FarmCatalog.");
            if (_cow != null && (_cow.AnimalId != "cow" || _cow.FeedItemId != "wheat"
                || _cow.VisualPrefab == null || !catalog.TryGetItem(_cow.ProductItemId, out var milk) || milk.SellPrice < 1))
                throw new InvalidOperationException("Cow visual, feed or milk is missing from FarmCatalog.");
        }

        /// <summary>Количество размещённых животных для HUD и проверок сохранения.</summary>
        public int Count => _animals.Count;
        /// <summary>Верхняя граница животных, заданная в Inspector.</summary>
        public int MaxAnimals => maxAnimals;
        /// <summary>Стабильный ID вида, назначенного в Inspector.</summary>
        public string AnimalDefinitionId => _chicken == null ? string.Empty : _chicken.AnimalId;
        /// <summary>Цена курицы, доступная для настройки в Inspector.</summary>
        public int ChickenPrice => chickenPrice;
        public AnimalDefinition Definition => _chicken != null ? _chicken : chickenAsset;
        private CharacterController _playerBody;
        public void BindPlayer(CharacterController playerBody) => _playerBody = playerBody;
        public float ProductIntervalSeconds => Definition != null ? Definition.ProductIntervalSeconds : productIntervalSeconds;
        /// <summary>Нижняя граница загона в клетках для редакторского макета.</summary>
        public Vector2Int PenMinCell => new Vector2Int(Mathf.Min(penMinCell.x, penMaxCell.x),
            Mathf.Min(penMinCell.y, penMaxCell.y));
        /// <summary>Верхняя граница загона в клетках для редакторского макета.</summary>
        public Vector2Int PenMaxCell => new Vector2Int(Mathf.Max(penMinCell.x, penMaxCell.x),
            Mathf.Max(penMinCell.y, penMaxCell.y));

        /// <summary>Покупает курицу на выбранной клетке загона после проверки денег и места.</summary>
        public bool TryBuyChicken(Vector2Int position, long nowUnixMs)
        {
            if (_mutating || nowUnixMs < 0 || _wallet == null || _wallet.Coins < chickenPrice || !CanAddChicken(position)) return false;
            _mutating = true;
            try
            {
                if (!_wallet.TrySpend(chickenPrice)) return false;
                if (AddChickenCore(position, nowUnixMs)){GameEvents.Record("chicken");return true;}
                _wallet.AddCoins(chickenPrice);
                GameEvents.Record("refund",value:chickenPrice);
                return false;
            }
            finally { _mutating = false; }
        }

        private List<Vector2Int> DeliveryCells(int amount)
        {
            var cells = new List<Vector2Int>();
            if (amount < 1 || amount > maxAnimals - ChickenCount) return cells;
            for (var z = PenMinCell.y; z <= PenMaxCell.y && cells.Count < amount; z++)
                for (var x = PenMinCell.x; x <= PenMaxCell.x && cells.Count < amount; x++)
                { var cell = new Vector2Int(x, z); if (CanAddChicken(cell)) cells.Add(cell); }
            return cells;
        }
        public bool CanDeliver(int amount) => amount > 0 && DeliveryCells(amount).Count == amount;
        internal void Deliver(int amount, long nowUnixMs)
        {
            foreach (var cell in DeliveryCells(amount))
                if (AddChicken(cell, nowUnixMs)) GameEvents.Record("chicken");
        }

        /// <summary>Проверяет, находится ли клетка внутри прямоугольного загона.</summary>
        public bool IsInPen(Vector2Int position) => position.x >= Mathf.Min(penMinCell.x, penMaxCell.x)
            && position.x <= Mathf.Max(penMinCell.x, penMaxCell.x)
            && position.y >= Mathf.Min(penMinCell.y, penMaxCell.y)
            && position.y <= Mathf.Max(penMinCell.y, penMaxCell.y);

        public bool OccupiesHomeCell(Vector2Int position)
        {
            foreach (var animal in _animals.Values)
                if (animal.Position == position) return true;
            return false;
        }

        /// <summary>Добавляет курицу в свободную клетку загона без изменения terrain occupancy.</summary>
        public bool AddChicken(Vector2Int position, long nowUnixMs)
        {
            if (_mutating || nowUnixMs < 0 || !CanAddChicken(position)) return false;
            _mutating = true;
            try { return AddChickenCore(position, nowUnixMs); }
            finally { _mutating = false; }
        }

        private bool AddChickenCore(Vector2Int position, long nowUnixMs)
        {
            if (!CanAddChicken(position)) return false;
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
                CreateView(stateToAdd));
            GameEvents.RaiseInventoryChanged();
            return true;
        }

        /// <summary>Проверяет лимит, слой мира и отсутствие другой курицы перед размещением.</summary>
        private bool CanAddChicken(Vector2Int position)
        {
            if (_grid == null || _chicken == null || ChickenCount >= maxAnimals || !IsInPen(position)
                || !_grid.CanPlantTree(position)) return false;
            if (_playerBody != null)
            {
                var spawn = _grid.GridToWorld(position);
                var playerBounds = _playerBody.bounds;
                var size = (_grid as GridSystem)?.CellSize ?? 1f;
                var home = new Bounds(spawn + Vector3.up * 0.35f,
                    new Vector3(size, 0.7f, size));
                if (playerBounds.Intersects(home)) return false;
            }
            return !OccupiesHomeCell(position);
        }

        /// <summary>Кормит конкретную курицу одной единицей пшеницы.</summary>
        public bool Feed(string animalId, long nowUnixMs, bool quiet=false)=>FeedCore(animalId,nowUnixMs,quiet,false);
        private bool FeedCore(string animalId,long nowUnixMs,bool quiet,bool onlyFeeder)
        {
            if (_mutating || animalId == null || !_animals.TryGetValue(animalId, out var state)
                || !TryGetDefinition(state.definitionId, out var definition) || nowUnixMs < 0
                || nowUnixMs > MaxTimestamp - (long)(definition.ProductIntervalSeconds * 1000)
                || state.readyProductCount > 0 || state.nextProductAtUnixMs > 0
                || _inventory == null) return false;
            var fromFeeder=_buildings?.Feeders?.CanSupply(_grid.GridToWorld(state.Position))==true;
            if(!fromFeeder && (onlyFeeder || _inventory.GetAmount(definition.FeedItemId)<1))return false;
            _mutating = true;
            try
            {
                if(fromFeeder && !_buildings.Feeders.Consume(_grid.GridToWorld(state.Position)))return false;
                state.lastFedAtUnixMs = nowUnixMs;
                state.nextProductAtUnixMs = nowUnixMs + (long)(definition.ProductIntervalSeconds * 1000f);
                if(fromFeeder)GameEvents.RaiseInventoryChanged();else _inventory.TryRemove(definition.FeedItemId, 1);
                if (_views.TryGetValue(animalId, out var view)) view.PlayEating();
                if(!quiet)GameEvents.RaiseStatusChanged(definition.DisplayName + " накормлена");
                GameEvents.Record("fed", definition.AnimalId);
                return true;
            }
            finally { _mutating = false; }
        }

        /// <summary>Собирает накопившиеся яйца из конкретной курицы.</summary>
        public bool TryCollect(string animalId, out int yield, bool quiet=false)
            => TryCollectCore(animalId, out yield, quiet, false);

        private bool TryCollectCore(string animalId, out int yield, bool quiet, bool byShepherd)
        {
            yield = 0;
            if (_mutating || animalId == null || !_animals.TryGetValue(animalId, out var state)
                || !TryGetDefinition(state.definitionId, out var definition)
                || state.readyProductCount <= 0
                || _inventory == null) return false;
            var productCount = (long)state.readyProductCount * definition.ProductYield;
            if (productCount > int.MaxValue - _inventory.GetAmount(definition.ProductItemId)) return false;
            _mutating = true;
            try
            {
                yield = (int)productCount;
                var timely = !byShepherd && state.definitionId == AnimalDefinitionId && !state.shepherdObserved &&
                    ShepherdObserving?.Invoke() == true;
                state.readyProductCount = 0;
                state.nextProductAtUnixMs = 0;
                state.shepherdWaitSeconds = 0;
                state.shepherdObserved = false;
                if (timely) RecordShepherdObservation?.Invoke(true);
                _inventory.Add(definition.ProductItemId, yield);
                if(!quiet)GameEvents.RaiseStatusChanged($"Собрано: {yield} × " + (definition.ProductItemId == "milk" ? "молоко" : "яйцо"));
                GameEvents.Record(definition.ProductItemId == "milk" ? "milk" : "eggs",amount:yield);
                GameEvents.RaiseActionFeedback("collect", _grid.GridToWorld(state.Position), definition.ProductItemId, yield);
                return true;
            }
            finally { _mutating = false; }
        }

        internal const float ManualCollectionWindow = 12f;
        internal Func<bool> ShepherdObserving { get; set; }
        internal Action<bool> RecordShepherdObservation { get; set; }

        private void ObserveReadyEggs(float elapsedSeconds)
        {
            if (ShepherdObserving?.Invoke() != true) return;
            foreach (var state in _animals.Values)
            {
                if (state.definitionId != AnimalDefinitionId || state.readyProductCount == 0 || state.shepherdObserved) continue;
                state.shepherdWaitSeconds = Mathf.Min(ManualCollectionWindow, state.shepherdWaitSeconds + elapsedSeconds);
                if (state.shepherdWaitSeconds < ManualCollectionWindow) continue;
                state.shepherdObserved = true;
                RecordShepherdObservation?.Invoke(false);
            }
        }

        public void AutomaticCare(long nowUnixMs, ShepherdPriority priority = ShepherdPriority.FeedFirst)
        {
            var collect = priority == ShepherdPriority.CollectFirst;
            if (!TryCare(nowUnixMs, collect)) TryCare(nowUnixMs, !collect);
        }

        private bool TryCare(long nowUnixMs, bool collect)
        {
            // The oldest eligible task wins within each priority, with a stable tie break.
            var candidates = new List<AnimalRuntimeState>();
            foreach (var state in _animals.Values)
            {
                if (state.definitionId != AnimalDefinitionId) continue;
                if (collect ? state.readyProductCount > 0 && state.shepherdObserved :
                    state.readyProductCount == 0 && state.nextProductAtUnixMs == 0)
                    candidates.Add(state);
            }
            candidates.Sort((left, right) =>
            {
                var order = (collect ? left.nextProductAtUnixMs : left.lastFedAtUnixMs)
                    .CompareTo(collect ? right.nextProductAtUnixMs : right.lastFedAtUnixMs);
                return order == 0 ? string.CompareOrdinal(left.animalId, right.animalId) : order;
            });
            foreach (var state in candidates)
                if (collect ? TryCollectCore(state.animalId, out _, true, true) : Feed(state.animalId, nowUnixMs, true))
                    return true;
            return false;
        }

        /// <summary>Обрабатывает ближайшую курицу: сначала сбор яйца, затем кормление.</summary>
        public bool TryInteractNearest(Vector3 playerPosition, long nowUnixMs)
        {
            var nearest = FindNearest(playerPosition);
            if (nearest == null) return false;
            if (TryCollect(nearest.animalId, out _)) return true;
            if (Feed(nearest.animalId, nowUnixMs)) return true;
            GameEvents.RaiseStatusChanged(TargetPrompt(nearest.animalId, playerPosition));
            return false;
        }

        /// <summary>Возвращает true, если рядом есть животное и для него показывается подсказка.</summary>
        public bool HasNearby(Vector3 playerPosition) => FindNearest(playerPosition) != null;

        public bool IsWithinReach(string id, Vector3 playerPosition)
        {
            if (id == null || !_views.TryGetValue(id, out var view) || view == null) return false;
            return view.HorizontalDistanceSquared(playerPosition) <= interactionDistance * interactionDistance;
        }

        public bool TryInteract(string id, Vector3 playerPosition, long nowUnixMs)
        {
            if (!IsWithinReach(id, playerPosition)) return false;
            if (TryCollect(id, out _) || Feed(id, nowUnixMs)) return true;
            GameEvents.RaiseStatusChanged(TargetPrompt(id, playerPosition));
            return false;
        }

        public string TargetPrompt(string id, Vector3 playerPosition)
        {
            if (!IsWithinReach(id, playerPosition)) return "Животное слишком далеко — подойдите ближе";
            if (!_animals.TryGetValue(id, out var animal)) return string.Empty;
            var cow = animal.definitionId == "cow";
            if (animal.readyProductCount > 0) return $"{InputReader.ActionHint} — собрать " + (cow ? "молоко" : "яйцо");
            if (animal.nextProductAtUnixMs > 0) return cow ? "Корова накормлена — молоко созревает" : "Курица накормлена — яйцо созревает";
            if(_buildings?.Feeders?.CanSupply(_grid.GridToWorld(animal.Position))==true)return InputReader.ActionHint+" — покормить из кормушки";
            return _inventory != null && _inventory.GetAmount("wheat") > 0
                ? $"{InputReader.ActionHint} — покормить " + (cow ? "корову пшеницей" : "курицу пшеницей")
                : cow ? "Корове нужна пшеница" : "Курице нужна пшеница";
        }

        /// <summary>Подсказывает сбор, ожидание или кормление ближайшей курицы.</summary>
        public string NearbyPrompt(Vector3 playerPosition)
        {
            var nearest = FindNearest(playerPosition);
            if (nearest == null) return string.Empty;
            return TargetPrompt(nearest.animalId, playerPosition);
        }

        /// <summary>Снимает независимую копию животных для SaveSystem.</summary>
        public List<AnimalRuntimeState> Capture()
        {
            var result = new List<AnimalRuntimeState>(_animals.Count);
            foreach (var state in _animals.Values)
                result.Add(Copy(state));
            return result;
        }

        /// <summary>Восстанавливает только проверенные состояния животных и пересоздаёт их views.</summary>
        public void Restore(List<AnimalRuntimeState> states)
        {
            ClearAll();
            if (states == null || _grid == null) return;
            foreach (var saved in states)
            {
                if (saved == null || !TryGetDefinition(saved.definitionId, out _) || _animals.ContainsKey(saved.animalId)) continue;
                if (saved.definitionId != "cow" && (!IsInPen(saved.Position) || !_grid.CanPlantTree(saved.Position))) continue;
                var copy = Copy(saved);
                _animals.Add(copy.animalId, copy);
                _views.Add(copy.animalId, CreateView(copy));
            }
        }

        /// <summary>Удаляет runtime-индекс и визуальные placeholder-объекты.</summary>
        public void ClearAll()
        {
            foreach (var view in _views.Values)
                if (view != null)
                {
                    view.gameObject.SetActive(false);
                    if (Application.isPlaying) Destroy(view.gameObject);
                    else DestroyImmediate(view.gameObject);
                }
            _status?.Clear();
            _views.Clear();
            _animals.Clear();
        }

        /// <summary>Обновляет готовность яйца и перемещает визуальные модели с заданным интервалом.</summary>
        private float _nextFeederCare;
        private void Update()
        {
            if (Time.timeScale <= 0) return;
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var becameReady = RefreshReadyProducts(now);
            if (Application.isFocused) ObserveReadyEggs(Time.unscaledDeltaTime);
            if (becameReady > 0) GameEvents.RaiseInventoryChanged();
            if(Time.unscaledTime>=_nextFeederCare && _buildings?.Feeders!=null)
            {
                _nextFeederCare=Time.unscaledTime+1;
                foreach(var id in new List<string>(_animals.Keys))FeedCore(id,now,true,true);
            }
            SyncProductionViews(now);
        }

        private int RefreshReadyProducts(long now)
        {
            var count = 0;
            foreach (var state in _animals.Values)
                if (state.readyProductCount == 0 && state.nextProductAtUnixMs > 0
                    && now >= state.nextProductAtUnixMs) { state.readyProductCount = 1; count++; }
            return count;
        }

        private void SyncProductionViews(long now)
        {
            foreach(var pair in _views) pair.Value.SyncProduction(_animals[pair.Key],now);
        }

        internal int FinishProductionForDemo(long now)
        {
            if (_mutating) return 0;
            foreach (var state in _animals.Values)
            {
                if (state.readyProductCount != 0 || state.nextProductAtUnixMs <= now) continue;
                state.nextProductAtUnixMs = now;
                state.lastFedAtUnixMs = Math.Min(state.lastFedAtUnixMs, now);
            }
            var count = RefreshReadyProducts(now);
            SyncProductionViews(now);
            if (count > 0) GameEvents.RaiseInventoryChanged();
            return count;
        }

        /// <summary>Ищет ближайшую курицу по горизонтальной дистанции без выделения коллекций.</summary>
        private AnimalRuntimeState FindNearest(Vector3 playerPosition)
        {
            if (_grid == null) return null;
            AnimalRuntimeState result = null;
            var bestDistance = interactionDistance * interactionDistance;
            foreach (var state in _animals.Values)
            {
                if(!_views.TryGetValue(state.animalId,out var view) || view==null) continue;
                var distance=view.HorizontalDistanceSquared(playerPosition);
                if (distance <= bestDistance) { bestDistance = distance; result = state; }
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
