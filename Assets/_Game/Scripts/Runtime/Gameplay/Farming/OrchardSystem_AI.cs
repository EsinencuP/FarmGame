using System;
using System.Collections.Generic;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.World;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Farming
{
    /// <summary>Сажает яблони, рассчитывает повторное плодоношение и выдаёт урожай.</summary>
    [DefaultExecutionOrder(-60)]
    public sealed class OrchardSystem_AI : MonoBehaviour
    {
        [Header("Apple Tree Placeholder")]
        [SerializeField] private TreeDefinition appleTreeAsset;
        [SerializeField] private TreeDefinition[] additionalTreeAssets = Array.Empty<TreeDefinition>();
        [SerializeField, Min(0.1f)] private float firstHarvestSeconds = 30f;
        [SerializeField, Min(0.1f)] private float regrowSeconds = 45f;
        [SerializeField, Min(1)] private int yieldAmount = 3;
        [SerializeField] private Color foliageColor = new Color(0.16f, 0.48f, 0.12f);
        [SerializeField] private Color fruitReadyColor = new Color(0.82f, 0.16f, 0.08f);
        [Tooltip("Интервал обновления готовности деревьев в реальном времени.")]
        [SerializeField, Min(0.1f)] private float refreshInterval = 0.5f;

        // Состояние дерева является источником истины, а словарь views содержит только представления.
        private readonly Dictionary<Vector2Int, TreeRuntimeState> _trees =
            new Dictionary<Vector2Int, TreeRuntimeState>();
        private readonly Dictionary<Vector2Int, TreeView> _views =
            new Dictionary<Vector2Int, TreeView>();
        private readonly List<ScriptableObject> _placeholders = new List<ScriptableObject>();
        private IWorldGridWriter _grid;
        private InventorySystem_AI _inventory;
        private WeatherController_AI _weather;
        private TreeDefinition _appleTree;
        private readonly Dictionary<string, TreeDefinition> _definitions = new Dictionary<string, TreeDefinition>(StringComparer.Ordinal);
        private readonly List<TreeDefinition> _orderedDefinitions = new List<TreeDefinition>();
        private bool _changing;
        private const long MaxTimestamp = 253402300799999L;
        private float _nextRefresh;

        /// <summary>Стабильный ID выбранного в Inspector дерева для проверки сохранения.</summary>
        public string TreeDefinitionId => _appleTree == null ? string.Empty : _appleTree.TreeId;
        public TreeDefinition Definition => _appleTree != null ? _appleTree : appleTreeAsset;
        public float FirstHarvestSeconds => Definition != null ? Definition.FirstHarvestSeconds : firstHarvestSeconds;
        public float RegrowSeconds => Definition != null ? Definition.RegrowSeconds : regrowSeconds;
        public int YieldAmount => Definition != null ? Definition.YieldAmount : yieldAmount;
        public IReadOnlyList<TreeDefinition> Definitions => _orderedDefinitions;
        public string SelectedTreeId { get; private set; }
        public TreeDefinition SelectedDefinition => TryGetDefinition(SelectedTreeId, out var tree) ? tree : Definition;

        public bool TryGetDefinition(string id, out TreeDefinition definition)
        {
            definition = null;
            if (id == null) return false;
            if (_definitions.TryGetValue(id, out definition)) return true;
            // Edit Mode handbook can read definitions without configuring gameplay.
            if (_definitions.Count == 0)
            {
                definition = id == "apple" || id == "apple_tree" ? Definition
                    : id == "pear" ? Resources.Load<TreeDefinition>("FarmContent/Pear") : null;
            }
            return definition != null;
        }

        public bool TryGetSapling(string itemId, out TreeDefinition definition)
        {
            foreach (var tree in _orderedDefinitions)
                if (tree.SaplingItemId == itemId) { definition = tree; return true; }
            definition = null; return false;
        }

        public bool IsFree(TreeDefinition definition)
        {
            if (definition == null || definition != Definition) return false;
            foreach (var tree in _trees.Values)
                if (tree.treeId == definition.TreeId || tree.treeId is "apple" or "apple_tree") return false;
            return true;
        }

        internal int SaplingsAvailable(string itemId)
        {
            var stock = _inventory?.GetAmount(itemId) ?? 0;
            return stock < int.MaxValue && IsFree(Definition) && itemId == Definition.SaplingItemId ? stock + 1 : stock;
        }

        public bool SelectTree(string id)
        {
            if (_changing || !TryGetDefinition(id, out var definition)) return false;
            SelectedTreeId = definition.TreeId;
            GameEvents.RaiseInventoryChanged(); return true;
        }

        public string PlantHint(string key)
        {
            var tree = SelectedDefinition;
            if (tree == null) return string.Empty;
            var supply = IsFree(tree) ? "первая яблоня бесплатно" : "саженцев: " + SaplingsAvailable(tree.SaplingItemId);
            return key + " — посадить: " + tree.DisplayName + " (" + supply + ") · выбор в инвентаре";
        }

        /// <summary>Подключает слой мира, инвентарь и каталог предметов.</summary>
        public void Configure(IWorldGridWriter grid, InventorySystem_AI inventory, FarmCatalog catalog)
        {
            _grid = grid;
            _inventory = inventory;
            _weather = GetComponentInParent<GameBootstrap>()?.GetComponentInChildren<WeatherController_AI>(true);
            _appleTree = appleTreeAsset != null ? appleTreeAsset : CreateApplePlaceholder();
            _definitions.Clear(); _orderedDefinitions.Clear();
            Register(_appleTree, catalog);
            // Both authored apple and the original script-only apple_tree are persisted legacy IDs.
            _definitions["apple"] = _appleTree; _definitions["apple_tree"] = _appleTree;
            if (additionalTreeAssets != null) foreach (var tree in additionalTreeAssets) if (tree != null) Register(tree, catalog);
            if (!_definitions.ContainsKey("pear"))
            {
                var pear = Resources.Load<TreeDefinition>("FarmContent/Pear");
                if (pear != null) Register(pear, catalog);
            }
            SelectedTreeId = _appleTree.TreeId;
        }

        private void Register(TreeDefinition tree, FarmCatalog catalog)
        {
            if (string.IsNullOrWhiteSpace(tree.TreeId) || _definitions.ContainsKey(tree.TreeId)
                || _orderedDefinitions.Exists(d => d.SaplingItemId == tree.SaplingItemId)
                || catalog == null || !catalog.TryGetItem(tree.FruitItemId, out var fruit) || fruit.SellPrice < 1
                || !catalog.TryGetItem(tree.SaplingItemId, out _)
                || tree.YieldAmount < 1 || !ValidDuration(tree.FirstHarvestSeconds) || !ValidDuration(tree.RegrowSeconds))
                throw new InvalidOperationException("Invalid tree definition: " + tree.TreeId);
            _definitions.Add(tree.TreeId, tree); _orderedDefinitions.Add(tree);
        }
        private static bool ValidDuration(float seconds) => !float.IsNaN(seconds) && !float.IsInfinity(seconds) && seconds > 0 && seconds < 1e9f;

        /// <summary>Проверяет, есть ли дерево в указанной клетке.</summary>
        public bool Contains(Vector2Int position) => _trees.ContainsKey(position);

        internal bool TryRemove(Vector2Int position)
        {
            if (_changing || _grid.GetBuildingCell(position).buildingId != TreeOccupantId(position)
                || !_trees.Remove(position)) return false;
            if (_views.Remove(position, out var view))
            {
                view.gameObject.SetActive(false);
                Destroy(view.gameObject);
            }
            _grid.ClearBuildingCell(position);
            GameEvents.RaiseInventoryChanged();
            return true;
        }

        /// <summary>Сажает яблоню на свободную клетку и фиксирует время посадки.</summary>
        public bool PlantApple(Vector2Int position, long plantedAtUnixMs)
            => Plant(position, _appleTree == null ? null : _appleTree.TreeId, plantedAtUnixMs);

        public bool PlantSelected(Vector2Int position, long plantedAtUnixMs) => Plant(position, SelectedTreeId, plantedAtUnixMs);

        public bool Plant(Vector2Int position, string treeId, long plantedAtUnixMs)
        {
            if (_changing || _grid == null || !TryGetDefinition(treeId, out var definition)
                || plantedAtUnixMs < 0 || plantedAtUnixMs > MaxTimestamp || Contains(position)
                || !_grid.CanPlantTree(position)) return false;
            var free = IsFree(definition);
            if (!free && (_inventory == null || _inventory.GetAmount(definition.SaplingItemId) < 1)) return false;
            var state = new TreeRuntimeState
            {
                x = position.x,
                z = position.y,
                treeId = definition.TreeId,
                plantedAtUnixMs = plantedAtUnixMs,
                lastHarvestAtUnixMs = 0
            };
            _changing = true;
            try
            {
                ResetRainSample(state, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                _trees.Add(position, state);
                _grid.SetBuildingCell(position, new BuildingCell { type = CellType.Tree, buildingId = TreeOccupantId(position) });
                AddView(state);
                if (!free) _inventory.TryRemove(definition.SaplingItemId, 1);
                else GameEvents.RaiseInventoryChanged();
                GameEvents.Record("tree");
                return true;
            }
            finally { _changing = false; }
        }

        /// <summary>Собирает яблоки, если прошёл первый или очередной интервал плодоношения.</summary>
        // Собираю плоды одного размера и начинаю новый цикл на том же дереве.
        public bool TryHarvest(Vector2Int position, long nowUnixMs, out int yield)
        {
            yield = 0;
            if (_changing || nowUnixMs < 0 || nowUnixMs > MaxTimestamp || !_trees.TryGetValue(position, out var state)
                || !TryGetDefinition(state.treeId, out var definition)
                || !IsReady(state, nowUnixMs)) return false;
            var itemId = definition.FruitItemId;
            if (_inventory == null || _inventory.GetAmount(itemId) > int.MaxValue - definition.YieldAmount)
                return false;
            _changing = true;
            try
            {
                UpdateQuality(state, definition, nowUnixMs);
                var size = HarvestQuality.IsSized(itemId) ? state.harvestSize : HarvestSize.M;
                state.lastHarvestAtUnixMs = nowUnixMs;
                state.manuallyWatered = state.sizeReady = false;
                state.rainySeconds = 0;
                state.harvestSize = HarvestSize.M;
                ResetRainSample(state, nowUnixMs);
                yield = definition.YieldAmount;
                UpdateView(position, nowUnixMs);
                _inventory.Add(itemId, yield, size);
                GameEvents.Record(itemId == "apple" ? "apples" : "pears",amount:yield);
                GameEvents.RaiseStatusChanged($"Собрано: {yield} × {definition.DisplayName} · {size}");
                GameEvents.RaiseActionFeedback("harvest", _grid.GridToWorld(position), itemId, yield);
                GameEvents.RaiseProgressAction("harvest");
                return true;
            }
            finally { _changing = false; }
        }

        /// <summary>Возвращает состояние деревьев для сохранения без ссылок на сценовые объекты.</summary>
        // Делаю снимок сада с дождём, поливом и размером партии.
        public List<TreeRuntimeState> Capture()
        {
            var result = new List<TreeRuntimeState>(_trees.Count);
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            foreach (var state in _trees.Values)
            {
                var copy = Copy(state);
                if (TryGetDefinition(copy.treeId, out var definition)) UpdateQuality(copy, definition, now);
                result.Add(copy);
            }
            return result;
        }

        private static TreeRuntimeState Copy(TreeRuntimeState state) => new TreeRuntimeState
        {
            x = state.x, z = state.z, treeId = state.treeId,
            plantedAtUnixMs = state.plantedAtUnixMs, lastHarvestAtUnixMs = state.lastHarvestAtUnixMs,
            manuallyWatered = state.manuallyWatered, rainySeconds = state.rainySeconds,
            sizeReady = state.sizeReady, harvestSize = state.harvestSize,
            moistureSampleAt = state.moistureSampleAt, rainSample = state.rainSample
        };

        /// <summary>Восстанавливает проверенные деревья поверх уже загруженного grid-состояния.</summary>
        // Восстанавливаю деревья и начинаю отсчёт нового дождя от загрузки.
        public void Restore(List<TreeRuntimeState> trees)
        {
            ClearAll();
            if (trees == null || _grid == null) return;
            foreach (var tree in trees)
            {
                if (tree == null || !TryGetDefinition(tree.treeId, out _)
                    || _grid.GetBuildingCell(tree.Position).buildingId != TreeOccupantId(tree.Position))
                    continue;
                var copy = Copy(tree);
                ResetRainSample(copy, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                _trees[tree.Position] = copy;
                AddView(copy);
            }
        }

        /// <summary>Удаляет только визуальные объекты и очищает runtime-индекс деревьев.</summary>
        public void ClearAll()
        {
            foreach (var view in _views.Values)
                if (view != null)
                {
                    view.gameObject.SetActive(false);
                    if (Application.isPlaying) Destroy(view.gameObject);
                    else DestroyImmediate(view.gameObject);
                }
            _views.Clear();
            _trees.Clear();
            SelectedTreeId = Definition?.TreeId;
        }

        /// <summary>Проверяет созревание видимых деревьев через ограниченный интервал.</summary>
        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + refreshInterval;
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            foreach (var pair in _views) UpdateView(pair.Key, now);
        }

        /// <summary>Создаёт визуальное дерево только после записи его данных в grid.</summary>
        private void AddView(TreeRuntimeState state)
        {
            if (!TryGetDefinition(state.treeId, out var definition)) throw new InvalidOperationException("Unknown saved tree.");
            var view = TreeView.Create(_grid.GridToWorld(state.Position), definition, transform);
            _views[state.Position] = view;
            UpdateView(state.Position, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        /// <summary>Синхронизирует состояние готовности кроны с UTC-временем.</summary>
        private void UpdateView(Vector2Int position, long nowUnixMs)
        {
            if (!_views.TryGetValue(position, out var view) || !_trees.TryGetValue(position, out var state)) return;
            if (!TryGetDefinition(state.treeId, out var definition)) return;
            UpdateQuality(state, definition, nowUnixMs);
            view.SetFruitReady(state.sizeReady);
        }

        /// <summary>Проверяет первый сбор относительно посадки, а последующие — относительно прошлого сбора.</summary>
        private bool IsReady(TreeRuntimeState state, long nowUnixMs)
        {
            if (!TryGetDefinition(state.treeId, out var definition)) return false;
            var (start, duration) = Cycle(state, definition);
            return (nowUnixMs - start) / 1000.0 >= duration;
        }

        internal static (long start, float duration) Cycle(TreeRuntimeState state, TreeDefinition definition) =>
            state.lastHarvestAtUnixMs > 0 ? (state.lastHarvestAtUnixMs, definition.RegrowSeconds) :
                (state.plantedAtUnixMs, definition.FirstHarvestSeconds);

        internal int RipenForDemo(long now)
        {
            var count = 0;
            foreach (var state in _trees.Values)
            {
                if (!TryGetDefinition(state.treeId, out var definition)) continue;
                UpdateQuality(state, definition, now);
                if (!IsReady(state, now))
                {
                    var (start, duration) = Cycle(state, definition);
                    var readyStart = now - (long)Math.Ceiling(duration * 1000d);
                    state.harvestSize = HarvestQuality.Select(state.treeId, state.x, state.z, start, Moisture(state, duration));
                    state.sizeReady = true;
                    if (state.lastHarvestAtUnixMs > 0) state.lastHarvestAtUnixMs = readyStart;
                    state.plantedAtUnixMs = Math.Min(state.plantedAtUnixMs, readyStart);
                    count++;
                }
                UpdateView(state.Position, now);
            }
            return count;
        }

        private void ResetRainSample(TreeRuntimeState state, long nowUnixMs)
        {
            state.moistureSampleAt = nowUnixMs;
            state.rainSample = _weather?.GrowthBonusSeconds ?? 0;
        }

        // Беру дождь только за время роста. При созревании запоминаю размер партии.
        private void UpdateQuality(TreeRuntimeState state, TreeDefinition definition, long nowUnixMs)
        {
            if (state.sizeReady) return;
            var (start, duration) = Cycle(state, definition);
            var sampleSeconds = (nowUnixMs - state.moistureSampleAt) / 1000d;
            var available = Math.Max(0, duration - (state.moistureSampleAt - start) / 1000d);
            if (sampleSeconds > 0)
            {
                var rain = Math.Max(0, (_weather?.GrowthBonusSeconds ?? 0) - state.rainSample) * 5;
                state.rainySeconds = Math.Min(duration, state.rainySeconds + (float)Math.Min(rain, Math.Min(available, sampleSeconds)));
                ResetRainSample(state, nowUnixMs);
            }
            if ((nowUnixMs - start) / 1000d < duration) return;
            state.harvestSize = HarvestQuality.Select(state.treeId, state.x, state.z, start, Moisture(state, duration));
            state.sizeReady = true;
        }

        private static int Moisture(TreeRuntimeState state, float duration) => HarvestQuality.Moisture(
            state.manuallyWatered ? HarvestQuality.ManualMoisture : 0, state.rainySeconds, duration);

        // Поливаю растущее дерево один раз за текущий цикл.
        public bool TryWater(Vector2Int position, long nowUnixMs)
        {
            if (_changing || !_trees.TryGetValue(position, out var state) || state.manuallyWatered ||
                IsReady(state, nowUnixMs)) return false;
            state.manuallyWatered = true;
            return true;
        }

        // Показываю, когда будет сбор и как полив влияет на размер.
        public string HarvestPrompt(Vector2Int position, long nowUnixMs)
        {
            if (!_trees.TryGetValue(position, out var state) || !TryGetDefinition(state.treeId, out var definition)) return string.Empty;
            UpdateQuality(state, definition, nowUnixMs);
            if (state.sizeReady) return InputReader_AI.ActionHint + " — собрать: " + definition.DisplayName + " · " + state.harvestSize;
            var (start, duration) = Cycle(state, definition);
            var remaining = Mathf.Max(0, duration - (float)((nowUnixMs - start) / 1000.0));
            return definition.DisplayName + " · до урожая " + MyLittleFarm.UI.FarmText.Duration(remaining) +
                "\n" + HarvestQuality.GrowthHint(Moisture(state, duration), state.manuallyWatered);
        }

        /// <summary>Строит стабильную связь между яблоней и занятым объектным слоем.</summary>
        public static string TreeOccupantId(Vector2Int position) => $"tree:{position.x}:{position.y}";

        /// <summary>Создаёт временное описание яблони из значений Inspector.</summary>
        private TreeDefinition CreateApplePlaceholder()
        {
            var definition = ScriptableObject.CreateInstance<TreeDefinition>();
            definition.ConfigurePlaceholder("apple_tree", "Яблоня", firstHarvestSeconds,
                regrowSeconds, yieldAmount, foliageColor, fruitReadyColor);
            _placeholders.Add(definition);
            return definition;
        }

        /// <summary>Очищает созданные деревья и временные определения при выгрузке фермы.</summary>
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
