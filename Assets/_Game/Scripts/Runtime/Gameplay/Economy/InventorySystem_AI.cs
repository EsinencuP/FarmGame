using System;
using System.Collections.Generic;
using System.IO;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Farming;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Economy
{
    /// <summary>Хранит количества предметов по стабильным строковым идентификаторам.</summary>
    public sealed class InventorySystem_AI : MonoBehaviour
    {
        // Эти id используются логикой фермы, UI и форматом сохранения.
        public const string CarrotSeedId = "carrot_seed";
        public const string CarrotId = "carrot";

        [Header("Starting Inventory")]
        [Tooltip("Количество семян моркови в новой игре до загрузки сохранения.")]
        [SerializeField, Min(0)] private int startingCarrotSeeds = 8;

        // Словарь обеспечивает быстрый доступ к количеству любого предмета.
        private readonly Dictionary<string, int> _items = new Dictionary<string, int>();

        /// <summary>Заполняет новый прототип стартовыми семенами.</summary>
        public void ConfigurePrototypeInventory()
        {
            _items.Clear();
            _items[CarrotSeedId] = startingCarrotSeeds;
            Revision++;
            GameEvents.RaiseInventoryChanged();
        }

        public long Revision { get; private set; }
        internal void SetAmountSilently(string id, int amount)
        {
            var current = GetAmount(id);
            if (!HarvestQuality.IsSized(id) || amount >= current)
            {
                SetStackSilently(id, GetStackAmount(id) + (amount - current));
                return;
            }

            var remaining = current - amount;
            foreach (var size in HarvestQuality.Sizes)
            {
                var stackId = HarvestQuality.StackId(id, size);
                var count = GetStackAmount(stackId);
                var removed = Math.Min(count, remaining);
                if (removed > 0) SetStackSilently(stackId, count - removed);
                remaining -= removed;
                if (remaining == 0) break;
            }
        }

        internal void SetStackSilently(string stackId, int amount)
        {
            if (amount == 0) _items.Remove(stackId);
            else _items[stackId] = amount;
            Revision++;
        }

        // Считаю один размер, например только XL. Это нужно складу и продаже.
        public int GetStackAmount(string stackId) =>
            stackId != null && _items.TryGetValue(stackId, out var amount) ? amount : 0;

        // Суммирую все размеры одного товара для рецептов, заказов и кормления.
        public int GetAmount(string itemId)
        {
            if (!HarvestQuality.IsSized(itemId)) return GetStackAmount(itemId);
            var total = 0;
            foreach (var size in HarvestQuality.Sizes) total += GetStackAmount(HarvestQuality.StackId(itemId, size));
            return total;
        }

        public void Add(string itemId, int amount) => Add(itemId, amount, HarvestSize.M);

        public void Add(string itemId, int amount, HarvestSize size)
        {
            // checked не позволяет переполнению превратить большое количество в отрицательное.
            if (string.IsNullOrWhiteSpace(itemId) || amount <= 0 || !HarvestQuality.IsValidSize(size) ||
                (size != HarvestSize.M && !HarvestQuality.IsSized(itemId)))
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            if ((long)GetAmount(itemId) + amount > int.MaxValue) throw new OverflowException();
            var stackId = HarvestQuality.StackId(itemId, size);
            SetStackSilently(stackId, GetStackAmount(stackId) + amount);
            GameEvents.RaiseInventoryChanged();
        }

        public bool TryRemove(string itemId, int amount)
        {
            // Метод ничего не меняет, если количество некорректно или предметов недостаточно.
            if (amount <= 0 || GetAmount(itemId) < amount)
            {
                return false;
            }

            SetAmountSilently(itemId, GetAmount(itemId) - amount);
            GameEvents.RaiseInventoryChanged();
            return true;
        }

        public List<InventoryEntryData> Capture()
        {
            // DTO отделяют сериализуемый снимок от внутреннего словаря.
            var result = new List<InventoryEntryData>();
            foreach (var pair in _items)
            {
                var (itemId, size) = HarvestQuality.ParseStack(pair.Key);
                result.Add(new InventoryEntryData { itemId = itemId, size = size, amount = pair.Value });
            }

            return result;
        }

        public void Restore(List<InventoryEntryData> entries)
        {
            // Текущее содержимое полностью заменяется данными проверенного снимка.
            _items.Clear();
            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    if (entry != null && !string.IsNullOrWhiteSpace(entry.itemId) && entry.amount >= 0)
                    {
                        _items[entry.StackId] = entry.amount;
                    }
                }
            }

            Revision++;
            GameEvents.RaiseInventoryChanged();
        }

        internal static void ValidateEntries(List<InventoryEntryData> entries)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var totals = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.itemId) || entry.amount < 0 ||
                    !HarvestQuality.IsValidSize(entry.size) || (entry.size != HarvestSize.M && !HarvestQuality.IsSized(entry.itemId)) ||
                    HarvestQuality.ParseStack(entry.itemId).size != HarvestSize.M || !ids.Add(entry.StackId))
                    throw new InvalidDataException("Invalid or duplicate item stack.");
                totals.TryGetValue(entry.itemId, out var total);
                total += entry.amount;
                if (total > int.MaxValue) throw new InvalidDataException("Item stock exceeds its quantity limit.");
                totals[entry.itemId] = total;
            }
        }
    }
}
