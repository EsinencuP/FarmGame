using System;
using System.Collections.Generic;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.Economy;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Farming
{
    /// <summary>Сажает яблони, рассчитывает повторное плодоношение и выдаёт урожай.</summary>
    [DefaultExecutionOrder(-60)]
    public sealed class OrchardSystem : MonoBehaviour
    {
        [Header("Apple Tree Placeholder")]
        [SerializeField] private TreeDefinition appleTreeAsset;
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
        private InventorySystem _inventory;
        private TreeDefinition _appleTree;
        private float _nextRefresh;

        /// <summary>Стабильный ID выбранного в Inspector дерева для проверки сохранения.</summary>
        public string TreeDefinitionId => _appleTree == null ? string.Empty : _appleTree.TreeId;

        /// <summary>Подключает слой мира, инвентарь и каталог предметов.</summary>
        public void Configure(IWorldGridWriter grid, InventorySystem inventory, FarmCatalog catalog)
        {
            _grid = grid;
            _inventory = inventory;
            _appleTree = appleTreeAsset != null ? appleTreeAsset : CreateApplePlaceholder();
            if (catalog == null || !catalog.TryGetItem(_appleTree.FruitItemId, out var fruit)
                || fruit.SellPrice < 1)
                throw new InvalidOperationException("Tree fruit is missing from FarmCatalog.");
        }

        /// <summary>Проверяет, есть ли дерево в указанной клетке.</summary>
        public bool Contains(Vector2Int position) => _trees.ContainsKey(position);

        /// <summary>Сажает яблоню на свободную клетку и фиксирует время посадки.</summary>
        public bool PlantApple(Vector2Int position, long plantedAtUnixMs)
        {
            if (_grid == null || _appleTree == null || plantedAtUnixMs < 0 || Contains(position)
                || !_grid.CanPlantTree(position)) return false;
            var state = new TreeRuntimeState
            {
                x = position.x,
                z = position.y,
                treeId = _appleTree.TreeId,
                plantedAtUnixMs = plantedAtUnixMs,
                lastHarvestAtUnixMs = 0
            };
            _trees.Add(position, state);
            _grid.SetBuildingCell(position, new BuildingCell
            {
                type = CellType.Tree,
                buildingId = TreeOccupantId(position)
            });
            AddView(state);
            return true;
        }

        /// <summary>Собирает яблоки, если прошёл первый или очередной интервал плодоношения.</summary>
        public bool TryHarvest(Vector2Int position, long nowUnixMs, out int yield)
        {
            yield = 0;
            if (!_trees.TryGetValue(position, out var state) || _appleTree == null
                || !IsReady(state, nowUnixMs)) return false;
            var itemId = _appleTree.FruitItemId;
            if (_inventory == null || _inventory.GetAmount(itemId) > int.MaxValue - _appleTree.YieldAmount)
                return false;
            state.lastHarvestAtUnixMs = nowUnixMs;
            _inventory.Add(itemId, _appleTree.YieldAmount);
            yield = _appleTree.YieldAmount;
            UpdateView(position, nowUnixMs);
            GameEvents.RaiseStatusChanged($"Собрано: {yield} × {_appleTree.DisplayName}");
            GameEvents.RaiseActionFeedback("harvest", _grid.GridToWorld(position));
            GameEvents.RaiseProgressAction("harvest");
            return true;
        }

        /// <summary>Возвращает состояние деревьев для сохранения без ссылок на сценовые объекты.</summary>
        public List<TreeRuntimeState> Capture()
        {
            var result = new List<TreeRuntimeState>(_trees.Count);
            foreach (var state in _trees.Values)
                result.Add(new TreeRuntimeState
                {
                    x = state.x, z = state.z, treeId = state.treeId,
                    plantedAtUnixMs = state.plantedAtUnixMs,
                    lastHarvestAtUnixMs = state.lastHarvestAtUnixMs
                });
            return result;
        }

        /// <summary>Восстанавливает проверенные деревья поверх уже загруженного grid-состояния.</summary>
        public void Restore(List<TreeRuntimeState> trees)
        {
            ClearAll();
            if (trees == null || _grid == null) return;
            foreach (var tree in trees)
            {
                if (tree == null || tree.treeId != _appleTree.TreeId
                    || _grid.GetBuildingCell(tree.Position).buildingId != TreeOccupantId(tree.Position))
                    continue;
                _trees[tree.Position] = new TreeRuntimeState
                {
                    x = tree.x, z = tree.z, treeId = tree.treeId,
                    plantedAtUnixMs = tree.plantedAtUnixMs,
                    lastHarvestAtUnixMs = tree.lastHarvestAtUnixMs
                };
                AddView(tree);
            }
        }

        /// <summary>Удаляет только визуальные объекты и очищает runtime-индекс деревьев.</summary>
        public void ClearAll()
        {
            foreach (var view in _views.Values)
                if (view != null)
                {
                    if (Application.isPlaying) Destroy(view.gameObject);
                    else DestroyImmediate(view.gameObject);
                }
            _views.Clear();
            _trees.Clear();
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
            var view = TreeView.Create(_grid.GridToWorld(state.Position), _appleTree, transform);
            _views[state.Position] = view;
            UpdateView(state.Position, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        /// <summary>Синхронизирует состояние готовности кроны с UTC-временем.</summary>
        private void UpdateView(Vector2Int position, long nowUnixMs)
        {
            if (!_views.TryGetValue(position, out var view) || !_trees.TryGetValue(position, out var state)) return;
            view.SetFruitReady(IsReady(state, nowUnixMs));
        }

        /// <summary>Проверяет первый сбор относительно посадки, а последующие — относительно прошлого сбора.</summary>
        private bool IsReady(TreeRuntimeState state, long nowUnixMs)
        {
            var start = state.lastHarvestAtUnixMs > 0 ? state.lastHarvestAtUnixMs : state.plantedAtUnixMs;
            var duration = state.lastHarvestAtUnixMs > 0 ? _appleTree.RegrowSeconds : _appleTree.FirstHarvestSeconds;
            return (nowUnixMs - start) / 1000.0 >= duration;
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
