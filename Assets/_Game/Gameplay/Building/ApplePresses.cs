using System;
using System.Collections.Generic;
using System.IO;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.UI;

namespace MyLittleFarm.Gameplay.Building
{
    /// <summary>One uncollected batch per building instance, independent of its position.</summary>
    internal sealed class ApplePresses
    {
        public const string BuildingId = "apple_press";
        public const string FruitId = "apple";
        public const string JuiceId = "apple_juice";
        public const int BuildingPrice = 80;
        public const int AppleCost = 3;
        public const int BatchSeconds = 60;
        public const int JuicePrice = 65;
        private const long MaxUnixMs = 253402300799999L;

        private readonly InventorySystem _inventory;
        private readonly Dictionary<string, long> _batches = new Dictionary<string, long>(StringComparer.Ordinal);

        public ApplePresses(InventorySystem inventory) => _inventory = inventory;

        public bool HasBatches => _batches.Count > 0;
        public bool IsOccupied(string id) => _batches.ContainsKey(id);

        public string Prompt(string id, long now, string actionHint)
        {
            if (_batches.TryGetValue(id, out var readyAt))
            {
                if (now < readyAt) return Processing(readyAt, now);
                return _inventory.GetAmount(JuiceId) == int.MaxValue
                    ? FarmText.Get("press.inventoryFull")
                    : FarmText.Get("press.collect", actionHint);
            }
            return _inventory.GetAmount(FruitId) < AppleCost
                ? FarmText.Get("press.needApples", AppleCost)
                : FarmText.Get("press.start", AppleCost, FarmText.Duration(BatchSeconds), actionHint);
        }

        public bool TryUse(string id, long now, out string message, out bool collected)
        {
            collected = false;
            if (_batches.TryGetValue(id, out var readyAt))
            {
                if (now < readyAt) { message = Processing(readyAt, now); return false; }
                if (_inventory.GetAmount(JuiceId) == int.MaxValue)
                { message = FarmText.Get("press.inventoryFull"); return false; }

                // Publish the inventory event only after both sides of the transfer are coherent.
                _batches.Remove(id);
                _inventory.Add(JuiceId, 1);
                collected = true;
                message = FarmText.Get("press.collected");
                return true;
            }

            if (_inventory.GetAmount(FruitId) < AppleCost)
            { message = FarmText.Get("press.needApples", AppleCost); return false; }
            _batches.Add(id, now + BatchSeconds * 1000L);
            _inventory.TryRemove(FruitId, AppleCost);
            message = FarmText.Get("press.started", FarmText.Duration(BatchSeconds));
            return true;
        }

        private static string Processing(long readyAt, long now) =>
            FarmText.Get("press.processing", FarmText.Duration((float)Math.Ceiling((readyAt - now) / 1000d)));

        public List<ApplePressBatchData> Capture()
        {
            var result = new List<ApplePressBatchData>(_batches.Count);
            foreach (var batch in _batches)
                result.Add(new ApplePressBatchData { buildingId = batch.Key, readyAtUnixMs = batch.Value });
            return result;
        }

        public void Restore(List<ApplePressBatchData> batches)
        {
            _batches.Clear();
            foreach (var batch in batches) _batches.Add(batch.buildingId, batch.readyAtUnixMs);
        }

        public static void Validate(List<ApplePressBatchData> batches, List<BuildingRuntimeState> buildings)
        {
            if (batches.Count == 0) return;
            var presses = new HashSet<string>(StringComparer.Ordinal);
            foreach (var building in buildings)
                if (building.definitionId == BuildingId) presses.Add(building.id);
            foreach (var batch in batches)
                if (batch == null || batch.readyAtUnixMs <= 0 || batch.readyAtUnixMs > MaxUnixMs
                    || !presses.Remove(batch.buildingId))
                    throw new InvalidDataException("Invalid apple press batch or missing building.");
        }
    }
}
