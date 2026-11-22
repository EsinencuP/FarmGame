using System;
using System.Collections.Generic;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.Gameplay.Farming;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Economy
{
    /// <summary>Продаёт собранный урожай по ценам предметов из каталога.</summary>
    public sealed class SellingSystem : MonoBehaviour
    {
        public const int DefaultCarrotSellPrice = 5;

        [Header("Selling")]
        [Tooltip("Количество монет за одну проданную морковь.")]
        [SerializeField, Min(0)] private int carrotSellPrice = DefaultCarrotSellPrice;

        // Ссылки позволяют провести удаление товара и начисление денег как одну операцию.
        private InventorySystem_AI _inventory;
        private WalletSystem _wallet;
        private FarmCatalog _catalog;

        /// <summary>Цена одной моркови, установленная в Inspector.</summary>
        public int CarrotSellPrice => _catalog!=null && _catalog.TryGetItem(InventorySystem_AI.CarrotId,out var item)?item.SellPrice:carrotSellPrice;

        /// <summary>Quantity sale for the inventory UI; caller supplies the existing market reach check.</summary>
        public bool TrySellItem(string itemId, int amount, SeedShopSystem market)
        {
            if (market == null || !market.CanTrade || amount <= 0 || _inventory == null || _wallet == null
                || _catalog == null || !_catalog.TryGetItem(itemId, out var item) || item.SellPrice <= 0
                || _catalog.IsRareSeed(itemId) || itemId.EndsWith("_seed", StringComparison.Ordinal)
                || _inventory.GetAmount(itemId) < amount
                || !TransactionMath.TryAddSale(item.SellPrice, amount, _wallet.Coins, 0, out var earned)) return false;
            _inventory.SetAmountSilently(itemId, _inventory.GetAmount(itemId) - amount);
            CompleteSale((int)earned);
            GameEvents.Record("sale", itemId, amount, earned);
            GameEvents.RaiseStatusChanged("Продано: " + amount + " × " + item.DisplayName + " (+" + earned + ")");
            GameEvents.RaiseProgressAction("sale");
            return true;
        }

        public void Configure(InventorySystem_AI inventory, WalletSystem wallet, FarmCatalog catalog = null)
        {
            // Зависимости передаются bootstrap-скриптом после создания экономики.
            _inventory = inventory;
            _wallet = wallet;
            _catalog = catalog;
        }

        /// <summary>Продаёт все виды урожая одной операцией после проверки переполнения кошелька.</summary>
        public int SellAllCrops()
        {
            if (_catalog == null) return SellAllCarrots();
            long total = 0;
            long sold = 0;
            var items = new HashSet<string>(StringComparer.Ordinal);
            foreach (var crop in _catalog.Crops)
                if (!QuoteItem(crop.HarvestItemId)) return 0;
            foreach (var id in FarmCatalog.FarmProductIds)
                if (!QuoteItem(id)) return 0;
            if (sold == 0) return 0;
            foreach (var id in items) _inventory.SetAmountSilently(id, 0);
            CompleteSale((int)total);
            GameEvents.Record("sale", items.Contains(ApplePresses.JuiceId) ? ApplePresses.JuiceId : string.Empty, amount: sold, value: total);
            GameEvents.RaiseStatusChanged($"Продано урожая: {sold}  +{total} монет");
            return (int)total;

            bool QuoteItem(string id)
            {
                var amount = _inventory.GetAmount(id);
                if (amount == 0 || !_catalog.TryGetItem(id, out var item) || item.SellPrice <= 0 || !items.Add(id))
                    return true;
                if (!TransactionMath.TryAddSale(item.SellPrice, amount, _wallet.Coins, total, out var nextTotal))
                    return false;
                total = nextTotal;
                sold += amount;
                return true;
            }
        }

        public int SellAllCarrots()
        {
            // До удаления урожая проверяется переполнение будущего баланса.
            var amount = _inventory.GetAmount(InventorySystem_AI.CarrotId);
            var price = CarrotSellPrice;
            if (amount <= 0 || price <= 0
                || !TransactionMath.TryAddSale(price, amount, _wallet.Coins, 0, out var earnedLong))
            {
                return 0;
            }

            var earned = (int)earnedLong;
            _inventory.SetAmountSilently(InventorySystem_AI.CarrotId, 0);
            CompleteSale(earned);
            GameEvents.Record("sale",amount:amount,value:earned);
            GameEvents.RaiseStatusChanged($"Продано: {amount} моркови  +{earned} монет");
            return earned;
        }

        private void CompleteSale(int earned)
        {
            _wallet.SetCoinsSilently(_wallet.Coins + earned);
            GameEvents.RaiseInventoryChanged();
            GameEvents.RaiseMoneyChanged(_wallet.Coins);
        }
    }
}
