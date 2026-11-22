using System;
using System.Collections.Generic;
using System.IO;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.Gameplay.Farming;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Economy
{
    [Serializable]
    public sealed class MarketBalance
    {
        [SerializeField, Range(1, 500)] private int saleThreshold = 12;
        [SerializeField, Range(1, 500)] private int seedThreshold = 10;
        [SerializeField, Range(1, 500)] private int lowSupplyThreshold = 4;
        [SerializeField, Range(0, 100)] private int previousDayWeight = 50;
        [SerializeField, Range(1, 100)] private int surplusPercent = 75;
        [SerializeField, Range(1, 100)] private int anticipatedPercent = 90;
        [SerializeField, Range(100, 200)] private int demandPercent = 120;

        internal int SaleThreshold => Math.Clamp(saleThreshold, 1, 500);
        internal int SeedThreshold => Math.Clamp(seedThreshold, 1, 500);
        internal int LowSupplyThreshold => Math.Clamp(lowSupplyThreshold, 1, SaleThreshold);
        internal int PreviousDayWeight => Math.Clamp(previousDayWeight, 0, 100);
        internal int SurplusPercent => Math.Clamp(surplusPercent, 1, 100);
        internal int AnticipatedPercent => Math.Clamp(anticipatedPercent, SurplusPercent, 100);
        internal int DemandPercent => Math.Clamp(demandPercent, 100, 200);
    }

    public sealed partial class DailyMarketOrders
    {
        private const int MaxCount = 1_000_000;
        private const long MaxProductionSequence = 1_000_000_000_000L;
        private readonly MarketBalance _marketBalance;

        public (int percent, string reason) SalePriceRule(string itemId)
        {
            if (!IsHarvest(_catalog, itemId)) return (100, "");
            var row = FindActivity(itemId);
            if (SaleSignal(row) >= _marketBalance.SaleThreshold * 100)
                return (_marketBalance.SurplusPercent, "Избыток · много поставок этой культуры");
            if (Signal(row?.seedsToday ?? 0, row?.seedsYesterday ?? 0) >= _marketBalance.SeedThreshold * 100)
                return (_marketBalance.AnticipatedPercent, "Ожидаемый избыток · закуплено много семян или саженцев");
            if (itemId != "corn" && SaleSignal(row) < _marketBalance.LowSupplyThreshold * 100 &&
                _data.activity.Exists(other => other.itemId != itemId && SaleSignal(other) >= _marketBalance.SaleThreshold * 100))
                return (_marketBalance.DemandPercent, "Повышенный спрос · рынок ищет замену культуре в избытке");
            return (100, "Обычный рынок");
        }

        internal void RecordSale(string itemId, int amount)
        {
            if (!IsHarvest(_catalog, itemId)) return;
            var row = GetActivity(itemId);
            row.soldToday = AddCount(row.soldToday, amount);
        }

        internal void RecordSeedPurchase(string itemId, int amount)
        {
            var harvestId = itemId == "pear_sapling" ? "pear" : null;
            if (harvestId == null && _catalog != null)
                foreach (var crop in _catalog.Crops)
                    if (crop.SeedItemId == itemId) { harvestId = crop.HarvestItemId; break; }
            if (harvestId == null) return;
            var row = GetActivity(harvestId);
            row.seedsToday = AddCount(row.seedsToday, amount);
        }

        private void RecordProduction(string itemId)
        {
            if (!IsWorkshopProduct(itemId)) return;
            var row = GetActivity(itemId);
            row.batchesToday = AddCount(row.batchesToday, 1);
            _data.productionSequence = Math.Min(MaxProductionSequence, _data.productionSequence + 1);
            row.lastBatch = _data.productionSequence;
        }

        private static string ChoosePersonalOrder(MarketOrdersData data, List<string> eligible, int elapsedDays)
        {
            if (elapsedDays > 2) return null;
            MarketActivityData best = null;
            var bestCount = 1;
            foreach (var row in data.activity)
            {
                var count = row.batchesToday + (elapsedDays == 1 ? row.batchesYesterday : 0);
                if (count < 2 || !eligible.Contains(row.itemId)) continue;
                if (count < bestCount || (count == bestCount && best != null && row.lastBatch <= best.lastBatch)) continue;
                best = row;
                bestCount = count;
            }
            return best?.itemId;
        }

        private void RotateActivity(int days)
        {
            foreach (var row in _data.activity)
            {
                row.soldYesterday = days == 1 ? row.soldToday : 0;
                row.seedsYesterday = days == 1 ? row.seedsToday : 0;
                row.batchesYesterday = days == 1 ? row.batchesToday : 0;
                row.soldToday = row.seedsToday = row.batchesToday = 0;
            }
            _data.activity.RemoveAll(row => row.soldYesterday == 0 && row.seedsYesterday == 0 && row.batchesYesterday == 0);
        }

        private MarketActivityData FindActivity(string itemId) => _data.activity.Find(row => row.itemId == itemId);
        private MarketActivityData GetActivity(string itemId)
        {
            var row = FindActivity(itemId);
            if (row != null) return row;
            row = new MarketActivityData { itemId = itemId };
            _data.activity.Add(row);
            return row;
        }

        private static int AddCount(int count, int amount) => (int)Math.Min(MaxCount, (long)count + amount);
        private int Signal(int today, int yesterday) => today * 100 + yesterday * _marketBalance.PreviousDayWeight;
        private int SaleSignal(MarketActivityData row) => Signal(row?.soldToday ?? 0, row?.soldYesterday ?? 0);

        private static bool IsHarvest(FarmCatalog catalog, string itemId)
        {
            if (itemId == "apple" || itemId == "pear") return true;
            if (catalog == null) return Array.IndexOf(FarmCatalog.CropIds, itemId) >= 0;
            foreach (var crop in catalog.Crops)
                if (crop.HarvestItemId == itemId) return true;
            return false;
        }

        private static bool IsWorkshopProduct(string itemId) =>
            itemId == ProductionRecipe.Press.OutputId || itemId == ProductionRecipe.Mill.OutputId || itemId == ProductionRecipe.Dairy.OutputId;

        private static void ValidateActivity(MarketOrdersData data, FarmCatalog catalog)
        {
            if (data.activity == null || data.activity.Count > FarmCatalog.CropIds.Length + 5 ||
                data.productionSequence < 0 || data.productionSequence > MaxProductionSequence)
                throw new InvalidDataException("Invalid market activity.");
            var ids = new HashSet<string>();
            foreach (var row in data.activity)
            {
                if (row == null || !ids.Add(row.itemId) || (!IsHarvest(catalog, row.itemId) && !IsWorkshopProduct(row.itemId)) ||
                    !CountValid(row.soldToday) || !CountValid(row.soldYesterday) || !CountValid(row.seedsToday) ||
                    !CountValid(row.seedsYesterday) || !CountValid(row.batchesToday) || !CountValid(row.batchesYesterday) ||
                    row.lastBatch < 0 || row.lastBatch > data.productionSequence ||
                    (row.batchesToday + row.batchesYesterday > 0 && (!IsWorkshopProduct(row.itemId) || row.lastBatch == 0)) ||
                    (!IsHarvest(catalog, row.itemId) && row.soldToday + row.soldYesterday + row.seedsToday + row.seedsYesterday != 0))
                    throw new InvalidDataException("Invalid market activity row.");
            }
        }

        private static bool CountValid(int count) => count >= 0 && count <= MaxCount;
    }
}
