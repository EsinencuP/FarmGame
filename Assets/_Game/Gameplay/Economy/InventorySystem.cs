using System;
using System.Collections.Generic;
using MyLittleFarm.Core;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Economy
{
    /// <summary>Хранит количества предметов по стабильным строковым идентификаторам.</summary>
    public sealed class InventorySystem : MonoBehaviour
    {
        // Эти id используются логикой фермы, UI и форматом сохранения.
        public const string CarrotSeedId = "carrot_seed";
        public const string CarrotId = "carrot";

        // Словарь обеспечивает быстрый доступ к количеству любого предмета.
        private readonly Dictionary<string, int> _items = new Dictionary<string, int>();

        /// <summary>Заполняет новый прототип стартовыми семенами.</summary>
        public void ConfigurePrototypeInventory()
        {
            _items.Clear();
            _items[CarrotSeedId] = 8;
            GameEvents.RaiseInventoryChanged();
        }

        public int GetAmount(string itemId)
        {
            // Неизвестный или null идентификатор считается отсутствующим предметом.
            return itemId != null && _items.TryGetValue(itemId, out var amount) ? amount : 0;
        }

        public void Add(string itemId, int amount)
        {
            // checked не позволяет переполнению превратить большое количество в отрицательное.
            if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            _items[itemId] = checked(GetAmount(itemId) + amount);
            GameEvents.RaiseInventoryChanged();
        }

        public bool TryRemove(string itemId, int amount)
        {
            // Метод ничего не меняет, если количество некорректно или предметов недостаточно.
            if (amount <= 0 || GetAmount(itemId) < amount)
            {
                return false;
            }

            _items[itemId] -= amount;
            GameEvents.RaiseInventoryChanged();
            return true;
        }

        public List<InventoryEntryData> Capture()
        {
            // DTO отделяют сериализуемый снимок от внутреннего словаря.
            var result = new List<InventoryEntryData>();
            foreach (var pair in _items)
            {
                result.Add(new InventoryEntryData { itemId = pair.Key, amount = pair.Value });
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
                        _items[entry.itemId] = entry.amount;
                    }
                }
            }

            GameEvents.RaiseInventoryChanged();
        }
    }
}
