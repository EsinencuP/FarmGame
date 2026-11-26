using System;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.Farming;
using MyLittleFarm.Gameplay.Animals;
using MyLittleFarm.Gameplay.Building;
using UnityEngine;
using MyLittleFarm.UI;

namespace MyLittleFarm.Gameplay.World
{
    /// <summary>
    /// Координатор обычного действия игрока: продажа, сбор урожая, обработка земли и посадка.
    /// Сам не хранит состояние, а вызывает специализированные системы в нужном порядке.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed partial class InteractionSystem_AI : MonoBehaviour
    {
        [Header("Interaction")]
        [Tooltip("Максимальная горизонтальная дистанция до точки продажи.")]
        [SerializeField, Min(0.1f)] private float sellingDistance = 2.4f;
        [Tooltip("Интервал пересборки контекстной подсказки в секундах.")]
        [SerializeField, Min(0.02f)] private float promptRefreshInterval = 0.1f;

        // Зависимости предоставляют ввод, положение игрока, выбранную клетку и игровые данные.
        private InputReader_AI _input;
        private Transform _player;
        private CharacterController _playerBody;
        private Transform _saleCrate;
        private CellSelector _selector;
        private GridSystem _grid;
        private SoilSystem _soil;
        private CropSystem_AI _crops;
        private InventorySystem_AI _inventory;
        private SellingSystem _selling;
        private FarmCatalog _catalog;
        private QuickSlotSystem _slots;
        private ToolUpgradeSystem _upgrades;
        private OrchardSystem_AI _orchard;
        private AnimalSystem_AI _animals;
        private FishPond _fishing;
        public void BindFishing(FishPond fishing) => _fishing = fishing;
        // Ограничивает перестроение текста подсказки десятью разами в секунду.
        private float _nextPromptAt;
        private float _nextRepeatAt;
        private InteractionTarget _lastActionTarget;
        private Vector2Int _lastActionCell;

        /// <summary>Актуальная подсказка, которую отображает InteractionPromptUI.</summary>
        public string CurrentPrompt { get; private set; } = string.Empty;

        /// <summary>Подключает все системы, необходимые для обработки контекстного действия.</summary>
        public void Configure(
            InputReader_AI input,
            Transform player,
            Transform saleCrate,
            CellSelector selector,
            GridSystem grid,
            SoilSystem soil,
            CropSystem_AI crops,
            InventorySystem_AI inventory,
            SellingSystem selling,
            FarmCatalog catalog = null,
            QuickSlotSystem slots = null,
            ToolUpgradeSystem upgrades = null,
            OrchardSystem_AI orchard = null,
            AnimalSystem_AI animals = null)
        {
            _input = input;
            _player = player;
            _playerBody = player == null ? null : player.GetComponent<CharacterController>();
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
            ResetFieldInteraction();
            if(Application.isPlaying)
            {
                var preview=GetComponent<FieldAreaPreview>()??gameObject.AddComponent<FieldAreaPreview>();
                preview.Configure(this,selector,grid,input);
                if(player!=null)
                {
                    _heldHoe=player.GetComponent<HeldHoeView>()??player.gameObject.AddComponent<HeldHoeView>();
                    _heldHoe.Configure(player.GetComponent<PlayerController>(),slots,upgrades,input);
                }
            }
        }

        private void Update()
        {
            // Строительный режим полностью перехватывает ту же кнопку взаимодействия.
            if (_input == null || _input.BuildModeActive || _input.ModalInputActive || _input.SuppressGameplayThisFrame)
            {
                CurrentPrompt = string.Empty;
                return;
            }
            if (_fishing?.IsFishing == true)
            {
                CurrentPrompt = string.Empty;
                if (_input.InteractPressed) Interact(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                return;
            }
            if(_input.RotateFieldPressed)RotateField();
            if (_input.WaterPressed)
                WaterSelected(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            else if (_input.InteractPressed)
                HandlePress();
            else if (_input.PlantTreePressed && _orchard != null && _selector.HasSelection)
            {
                PlantTreeAt(_selector.SelectedPosition, _orchard.SelectedDefinition, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            }
            // Only the wording waits for hover to settle. The ray highlight and the command are independent.
            if (_selector.IsSelectionStable && (Time.unscaledTime >= _nextPromptAt || _input.InteractPressed || _input.WaterPressed))
            {
                CurrentPrompt = BuildPrompt();
                _nextPromptAt = Time.unscaledTime + promptRefreshInterval;
            }
        }

        // Беру растение под курсором и передаю полив грядке или дереву после проверки дистанции.
        private void WaterSelected(long nowUnixMs)
        {
            if (!_selector.HasSelection) return;
            var target = _selector.HoveredTarget;
            if (target != null && target.Kind != InteractionTargetKind.Tree) return;
            var position = target != null ? _grid.WorldToGrid(target.transform.position) : _selector.SelectedPosition;
            if (!IsWithinReach(position))
            {
                GameEvents.RaiseStatusChanged("Подойдите к растению для полива");
                return;
            }
            var watered = _orchard?.Contains(position) == true
                ? _orchard.TryWater(position, nowUnixMs) : _crops?.TryWater(position, nowUnixMs) == true;
            GameEvents.RaiseStatusChanged(watered ? "Растение полито" : "Поливать можно растущее растение один раз за цикл");
        }

        private void HandlePress()
        {
            var target = _selector.HoveredTarget;
            var cell = _selector.SelectedPosition;
            var selected = _selector.HasSelection;
            if (selected && Time.unscaledTime < _nextRepeatAt && target == _lastActionTarget
                && (target != null || cell == _lastActionCell)) return;
            if (!Interact(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) || !selected) return;
            _lastActionTarget = target;
            _lastActionCell = cell;
            // Prevent a double tap from immediately replanting the cell that just yielded a crop.
            _nextRepeatAt = Time.unscaledTime + .16f;
        }

        public bool Interact(long nowUnixMs)
        {
            // Продажа рядом с торговой точкой имеет приоритет над действием с клеткой.
            if (_input != null && (_input.BuildModeActive || _input.ModalInputActive || _input.SuppressGameplayThisFrame)) return false;
            if (_fishing?.IsFishing == true) return _fishing.Interact(nowUnixMs);
            if (_input != null && _input.PointerInteractPressed && (!_selector.HasSelection
                || (!_selector.IsSelectionStable && !_selector.ConfirmPointerSelection()))) return false;
            var target = _selector.HoveredTarget;
            if (target != null && target.Kind == InteractionTargetKind.Fishing)
                return _fishing?.Interact(nowUnixMs) == true;
            if(target!=null&&target.Kind==InteractionTargetKind.SeedCache)
                return target.GetComponent<RareSeedCache>()?.Collect()??false;
            if(target!=null&&target.Kind==InteractionTargetKind.Helper)
            {
                if (!IsNearTarget(target)) return false;
                GameEvents.RaiseStatusChanged($"Пастух кормит кур пшеницей и собирает яйца · {InputReader_AI.KeyLabel(InputReader_AI.Command.Guide)} — справочник");
                return true;
            }
            if (target != null && target.Kind == InteractionTargetKind.Animal)
                return _animals != null && _animals.TryInteract(target.TargetId, _player.position, nowUnixMs);
            if (target != null && target.Kind == InteractionTargetKind.Tree)
            {
                var position = _grid.WorldToGrid(target.transform.position);
                return _orchard != null && _orchard.Contains(position) && InteractWithCell(position, nowUnixMs);
            }
            if (target != null && target.Kind == InteractionTargetKind.Building
                && _grid.Buildings?.DoorFor(target.TargetId) is BarnDoor barnDoor)
                return barnDoor.TryToggle(_player.position);
            if(target!=null && target.Kind==InteractionTargetKind.Building && _grid.Buildings?.IsFeeder(target.TargetId)==true)
            {
                if(!_grid.Buildings.FeederInReach(target.TargetId,_player.position)){GameEvents.RaiseStatusChanged("Подойдите к кормушке");return false;}
                var menu=GetComponentInParent<GameBootstrap>()?.GetComponentInChildren<FarmMenuUI>(true);return menu!=null && menu.OpenFeeder(target.TargetId);
            }
            if(target!=null && target.Kind==InteractionTargetKind.Building && _grid.Buildings?.IsWarehouse(target.TargetId)==true)
            {
                if(!_grid.Buildings.WarehouseInReach(target.TargetId,_player.position)){GameEvents.RaiseStatusChanged("Подойдите к складу");return false;}
                var menu=GetComponentInParent<GameBootstrap>()?.GetComponentInChildren<FarmMenuUI>(true);
                return menu!=null && menu.OpenWarehouse(target.TargetId);
            }
            if (target != null && target.Kind == InteractionTargetKind.Building && _grid.Buildings?.RecipeFor(target.TargetId) != null)
            {
                if (!_grid.Buildings.ProductionInReach(target.TargetId,_player.position,sellingDistance)) { GameEvents.RaiseStatusChanged("Подойдите ближе к постройке"); return false; }
                var menus=GetComponentInParent<GameBootstrap>()?.GetComponentInChildren<FarmMenuUI>(true);
                return menus != null && menus.OpenProduction(target.TargetId);
            }
            if (target != null && (target.Kind == InteractionTargetKind.Obstacle || target.Kind == InteractionTargetKind.Building))
                return false;
            var marketTarget = target != null && target.Kind == InteractionTargetKind.Market;
            if (marketTarget && !IsNearMarketTarget(target))
            {
                GameEvents.RaiseStatusChanged(FarmText.Get("world.approachMarket"));
                return false;
            }
            if (marketTarget || (target == null && !_selector.HasSelection && IsNearSaleCrate()))
            {
                var menus = GetComponentInParent<GameBootstrap>()?.GetComponentInChildren<MyLittleFarm.UI.FarmMenuUI>(true);
                if (menus == null) return false;
                menus.OpenMarket();
                return menus.IsOpen;
            }

            // Животное получает приоритет перед клеткой, когда игрок подошёл к курице.
            if (!_selector.HasSelection && _animals != null && _animals.HasNearby(_player.position))
                return _animals.TryInteractNearest(_player.position, nowUnixMs);

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


            if (_orchard != null && _orchard.Contains(position))
            {
                if(!IsWithinReach(position))return false;
                if (_orchard.TryHarvest(position, nowUnixMs, out _)) return true;
                GameEvents.RaiseStatusChanged(FarmText.Get("world.notRipe"));
                return false;
            }

            if(_slots?.Mode==QuickSlotSystem.SelectionMode.Item && _orchard!=null && _orchard.TryGetSapling(_slots.SelectedItemId,out var selectedTree))
                return PlantTreeAt(position, selectedTree, nowUnixMs);
            return ApplyField(position,nowUnixMs);
        }

        private string TreePlantBlock(Vector2Int position, TreeDefinition tree)
        {
            if (tree == null) return "Выберите саженец";
            if (!IsWithinReach(position)) return "Подойдите ближе к месту посадки";
            if (_grid.IsLocationLocked(position)) return _grid.LockedParcelHint(position);
            if (_animals?.OccupiesHomeCell(position) == true) return "В этой клетке находится животное";
            if (!_grid.CanPlantTree(position))
            {
                var type = _grid.GetCellType(position);
                return type == CellType.Water ? "В воде дерево сажать нельзя"
                    : type == CellType.Locked ? "Участок ещё закрыт" : "Для дерева нужна свободная земля";
            }
            if (_playerBody != null)
            {
                var delta = _playerBody.bounds.center - _grid.CellToWorld(position);
                delta.y = 0f;
                var spacing = _playerBody.bounds.extents.x + TreeView.TrunkRadius;
                if (delta.sqrMagnitude < spacing * spacing) return "Отойдите от места посадки";
            }
            return !_orchard.IsFree(tree) && _inventory.GetAmount(tree.SaplingItemId) < 1
                ? "Нет саженца в инвентаре" : null;
        }

        private bool PlantTreeAt(Vector2Int position, TreeDefinition tree, long nowUnixMs)
        {
            var reason = TreePlantBlock(position, tree);
            var planted = reason == null && _orchard.Plant(position, tree.TreeId, nowUnixMs);
            GameEvents.RaiseStatusChanged(planted ? "Посажено · " + tree.DisplayName
                : reason ?? "Выберите другое место для дерева");
            return planted;
        }

        private string BuildPrompt()
        {
            // Формирует текст по тому же приоритету, что и реальное действие, чтобы подсказка не вводила в заблуждение.
            var target = _selector.HoveredTarget;
            if (target != null && target.Kind == InteractionTargetKind.Fishing)
                return _fishing?.Prompt(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) ?? string.Empty;
            if(target!=null&&target.Kind==InteractionTargetKind.SeedCache)
                return target.GetComponent<RareSeedCache>()?.Prompt??string.Empty;
            if(target!=null&&target.Kind==InteractionTargetKind.Helper)
                return FarmText.Get("world.helper", InputReader_AI.KeyLabel(InputReader_AI.Command.Guide));
            if (target != null && target.Kind == InteractionTargetKind.Animal)
                return _animals == null ? string.Empty : _animals.TargetPrompt(target.TargetId, _player.position);
            if (target != null && target.Kind == InteractionTargetKind.Tree)
            {
                var position = _grid.WorldToGrid(target.transform.position);
                if (_orchard == null || !_orchard.Contains(position)) return "Дерево — нет доступного действия";
                return IsWithinReach(position) ? _orchard.HarvestPrompt(position, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) : "Подойдите к дереву";
            }
            if (target != null && target.Kind == InteractionTargetKind.Obstacle) return FarmText.Get("world.obstacle");
            if (target != null && target.Kind == InteractionTargetKind.Building)
            {
                var door = _grid.Buildings?.DoorFor(target.TargetId);
                if (door != null) return door.Prompt(_player.position);
                if(_grid.Buildings?.IsFeeder(target.TargetId)==true)return _grid.Buildings.FeederInReach(target.TargetId,_player.position)?InputReader_AI.ActionHint+" — кормушка · пшеница "+_grid.Buildings.Feeders.Amount(target.TargetId)+" / 12":"Подойдите к кормушке";
                if(_grid.Buildings?.IsWarehouse(target.TargetId)==true)
                    return _grid.Buildings.WarehouseInReach(target.TargetId,_player.position)?InputReader_AI.ActionHint+" — открыть склад":"Подойдите к складу";
                if (_grid.Buildings?.RecipeFor(target.TargetId) != null)
                    return _grid.Buildings.ProductionInReach(target.TargetId,_player.position,sellingDistance)
                        ? _grid.Buildings.Production.Prompt(target.TargetId, _grid.Buildings.RecipeFor(target.TargetId), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), InputReader_AI.ActionHint)
                        : "Подойдите ближе к постройке";
                return FarmText.Get("world.buildingHint", InputReader_AI.KeyLabel(InputReader_AI.Command.Build), InputReader_AI.KeyLabel(InputReader_AI.Command.Customize));
            }
            if (target != null && target.Kind == InteractionTargetKind.Market)
                return IsNearMarketTarget(target) ? FarmText.Get("market.openHint", InputReader_AI.ActionHint)
                    : FarmText.Get("world.approachMarket");
            if (target == null && !_selector.HasSelection && IsNearSaleCrate())
            {
                return FarmText.Get("market.openHint", InputReader_AI.MarketHint);
            }

            if (!_selector.HasSelection && _animals != null && _animals.HasNearby(_player.position))
                return _animals.NearbyPrompt(_player.position).Replace(InputReader_AI.ActionHint, InputReader_AI.KeyLabel(InputReader_AI.Command.Interact));

            if (!_selector.HasSelection || !_grid.TryGetCell(_selector.SelectedPosition, out var cell))
            {
                return _input.WorldPointerBlocked ? string.Empty : "Колесо мыши — приблизить или отдалить";
            }
            if (_grid.IsLocationLocked(_selector.SelectedPosition))
                return _grid.LockedParcelHint(_selector.SelectedPosition);
            if (cell.type == CellType.Locked) return "Участок ещё закрыт";
            if(cell.type==CellType.Tree)return _orchard?.HarvestPrompt(_selector.SelectedPosition,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())??string.Empty;
            if(_slots?.Mode==QuickSlotSystem.SelectionMode.Item && _orchard!=null && _orchard.TryGetSapling(_slots.SelectedItemId,out var tree))
                return TreePlantBlock(_selector.SelectedPosition, tree) ?? InputReader_AI.ActionHint + " — посадить «" + tree.DisplayName + "»";
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var field = FieldPrompt(PlanField(_selector.SelectedPosition, now));
            var quality = IsWithinReach(_selector.SelectedPosition) ? _crops?.QualityHint(_selector.SelectedPosition, now) : null;
            return string.IsNullOrEmpty(quality) ? field : field + "\n" + quality;
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

        private bool IsNearTarget(InteractionTarget target)
        {
            var offset = target.transform.position - _player.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= sellingDistance * sellingDistance;
        }

        private bool IsNearMarketTarget(InteractionTarget target) =>
            string.IsNullOrEmpty(target.TargetId) ? IsNearTarget(target)
                : _grid.Buildings?.MarketInReach(target.TargetId, _player.position, sellingDistance) == true;

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
