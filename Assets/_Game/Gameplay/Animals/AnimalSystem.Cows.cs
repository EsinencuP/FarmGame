using System;
using System.Collections.Generic;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Building;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Animals
{
    public sealed partial class AnimalSystem
    {
        [Header("Cows")]
        [SerializeField] private AnimalDefinition cowAsset;
        [SerializeField, Min(1)] private int maxCows = 8;
        private AnimalDefinition _cow;
        private BuildSystem _buildings;
        private bool _mutating;
        private const long MaxTimestamp = 253402300799999L;
        private readonly Collider[] _deliveryObstacles = new Collider[16];

        public bool IsChanging => _mutating;
        public int ChickenCount => SpeciesCount(AnimalDefinitionId);
        public int CowCount => SpeciesCount("cow");
        public int MaxCows => maxCows;
        public AnimalDefinition CowDefinition => _cow != null ? _cow : cowAsset != null ? cowAsset
            : Resources.Load<AnimalDefinition>("FarmContent/Cow");
        public int CowPrice => CowDefinition == null ? 40 : CowDefinition.PurchasePrice;

        public void BindBuildings(BuildSystem buildings)
        { _buildings = buildings; buildings.BindAnimals(this); }

        private int SpeciesCount(string species)
        {
            var count = 0;
            foreach (var animal in _animals.Values) if (animal.definitionId == species) count++;
            return count;
        }

        public bool TryGetDefinition(string id, out AnimalDefinition definition)
        {
            definition = id == AnimalDefinitionId ? _chicken : id == "cow" ? _cow : null;
            return definition != null;
        }

        private AnimalView CreateView(AnimalRuntimeState state)
        {
            if (!TryGetDefinition(state.definitionId, out var definition))
                throw new InvalidOperationException("Unknown animal definition: " + state.definitionId);
            var view = AnimalView.Create(_grid.GridToWorld(state.Position), definition, transform, state.animalId, _grid);
            if (state.definitionId == "cow")
            {
                var barn = _buildings?.GetBuilding(state.homeBuildingId);
                if (barn == null) throw new InvalidOperationException("Cow barn is missing.");
                SetCowHabitat(view,state,barn);
            }
            view.SyncProduction(state,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            _status?.Register(view);
            return view;
        }

        private void SetCowHabitat(AnimalView view,AnimalRuntimeState state,BuildingRuntimeState barn)
        {
            // Separate half-paddocks retain a gap, including the complete turning/animation envelope.
            var offset=Quaternion.Euler(0,barn.quarterTurns*90f,0)*new Vector3(-.525f,0,state.homeSlot==0?.05f:-.05f);
            view.SetHabitat(_grid.GridToWorld(state.Position)+offset,barn.quarterTurns*90f,new Vector2(1.78f,1.40f));
        }
        public bool HasResidents(string buildingId)
        {
            foreach (var state in _animals.Values) if (state.homeBuildingId == buildingId) return true;
            return false;
        }

        public bool BlocksFootprint(Vector2Int cell, int width, int depth, string movingBarn)
        {
            foreach (var animal in _animals.Values)
            {
                if (movingBarn != null && animal.homeBuildingId == movingBarn) continue;
                if (animal.x >= cell.x && animal.z >= cell.y
                    && (long)animal.x < (long)cell.x + width && (long)animal.z < (long)cell.y + depth) return true;
            }
            return false;
        }

        public bool CanMoveBarn(BuildingRuntimeState candidate, out string reason)
        {
            reason = "Завершите действие с животными перед переносом коровника";
            if (_mutating) return false;
            foreach (var state in _animals.Values)
                if (state.homeBuildingId == candidate.id && !BarnHabitat.TryCell(candidate, state.homeSlot, out _))
                { reason = "Нет безопасного места для коров в новом загоне"; return false; }
            reason = string.Empty;
            return true;
        }

        public void ApplyBarnMove(BuildingRuntimeState barn)
        {
            foreach (var state in _animals.Values)
                if (state.homeBuildingId == barn.id && BarnHabitat.TryCell(barn, state.homeSlot, out var cell))
                {
                    state.x = cell.x; state.z = cell.y;
                    SetCowHabitat(_views[state.animalId],state,barn);
                }
        }

        private bool CowSlotFree(BuildingRuntimeState barn, int slot, List<AnimalRuntimeState> plan, out Vector2Int cell)
        {
            cell = default;
            if (!BarnHabitat.TryCell(barn, slot, out cell) || _grid.GetBuildingCell(cell).buildingId != barn.id) return false;
            foreach (var state in _animals.Values)
                if (state.Position == cell || (state.homeBuildingId == barn.id && state.homeSlot == slot)) return false;
            foreach (var state in plan)
                if (state.homeBuildingId == barn.id && state.homeSlot == slot) return false;
            // Ignore the ground and trigger-only animal targets, but never deliver into the player or props.
            Physics.SyncTransforms();
            var count = Physics.OverlapBoxNonAlloc(_grid.GridToWorld(cell) + Vector3.up * .8f,
                new Vector3(.65f, .7f, 1.45f), _deliveryObstacles, Quaternion.Euler(0, barn.quarterTurns * 90f, 0),
                ~0, QueryTriggerInteraction.Ignore);
            return count == 0;
        }

        private bool PlanDelivery(IReadOnlyDictionary<string, int> order, long now, out List<AnimalRuntimeState> plan,
            out int price, out string reason)
        {
            plan = new List<AnimalRuntimeState>(); price = 0;
            reason = "Выберите животных";
            if (_mutating || _grid == null || now < 0 || now > MaxTimestamp || order == null || order.Count == 0) return false;
            long total = 0;
            foreach (var line in order)
            {
                if (!TryGetDefinition(line.Key, out _) || line.Value < 1
                    || line.Value > (line.Key == "cow" ? maxCows - CowCount : maxAnimals - ChickenCount))
                { reason = "Достигнут лимит животных этого вида"; return false; }
                total += (long)(line.Key == "cow" ? CowPrice : chickenPrice) * line.Value;
                if (total > int.MaxValue) { reason = "Слишком большой заказ"; return false; }
                if (line.Key != "cow")
                {
                    var cells = DeliveryCells(line.Value);
                    if (cells.Count != line.Value) { reason = "В загоне кур нет свободного места"; return false; }
                    foreach (var cell in cells) plan.Add(new AnimalRuntimeState { definitionId = line.Key, x = cell.x, z = cell.y });
                    continue;
                }
                var remaining = line.Value;
                var barns = _buildings?.Capture();
                if (barns == null) { reason = "Сначала постройте коровник"; return false; }
                foreach (var barn in barns)
                {
                    for (var slot = 0; slot < BarnHabitat.Capacity && remaining > 0; slot++)
                        if (CowSlotFree(barn, slot, plan, out var cell))
                        {
                            plan.Add(new AnimalRuntimeState { definitionId = "cow", x = cell.x, z = cell.y,
                                homeBuildingId = barn.id, homeSlot = slot });
                            remaining--;
                        }
                    if (remaining == 0) break;
                }
                if (remaining > 0) { reason = "Нужен свободный коровник: до двух коров, загон должен быть свободен"; return false; }
            }
            price = (int)total; reason = string.Empty;
            return true;
        }

        public bool CanDeliver(string species, int amount, out string reason)
            => PlanDelivery(new Dictionary<string, int> { { species, amount } }, 0, out _, out _, out reason);

        /// <summary>Checks the whole animal order, prepares its views, then commits one payment.</summary>
        public bool TryPurchase(IReadOnlyDictionary<string, int> order, long now, out string reason)
        {
            if (!PlanDelivery(order, now, out var plan, out var total, out reason)) return false;
            if (_wallet == null || _wallet.Coins < total) { reason = "Недостаточно монет"; return false; }
            _mutating = true;
            var drafts = new List<AnimalView>();
            var committed = false;
            try
            {
                foreach (var state in plan)
                {
                    do { state.animalId = state.definitionId == "cow" ? "cow_" + Guid.NewGuid().ToString("N")
                        : "chicken_" + (++_nextAnimalNumber); } while (_animals.ContainsKey(state.animalId));
                    var view = CreateView(state); view.gameObject.SetActive(false); drafts.Add(view);
                }
                // Publish the wallet event only after all animals exist in the captured state.
                for (var i = 0; i < plan.Count; i++)
                { _animals.Add(plan[i].animalId, plan[i]); _views.Add(plan[i].animalId, drafts[i]); }
                if (!_wallet.TrySpend(total))
                {
                    foreach (var state in plan) { _animals.Remove(state.animalId); _views.Remove(state.animalId); }
                    reason = "Недостаточно монет"; return false;
                }
                committed = true;
                foreach (var view in drafts) view.gameObject.SetActive(true);
                foreach (var state in plan) GameEvents.Record(state.definitionId == "cow" ? "cow" : "chicken");
                GameEvents.RaiseInventoryChanged();
                reason = "Животные доставлены";
                return true;
            }
            finally
            {
                if (!committed)
                {
                    foreach (var state in plan)
                        if (state.animalId != null) { _animals.Remove(state.animalId); _views.Remove(state.animalId); }
                    foreach (var view in drafts)
                        if (view != null) { view.gameObject.SetActive(false); if (Application.isPlaying) Destroy(view.gameObject); else DestroyImmediate(view.gameObject); }
                }
                _mutating = false;
            }
        }

        private static AnimalRuntimeState Copy(AnimalRuntimeState state) => new AnimalRuntimeState
        {
            animalId = state.animalId, definitionId = state.definitionId, x = state.x, z = state.z,
            lastFedAtUnixMs = state.lastFedAtUnixMs, nextProductAtUnixMs = state.nextProductAtUnixMs,
            readyProductCount = state.readyProductCount, homeBuildingId = state.homeBuildingId, homeSlot = state.homeSlot
        };
    }
}
