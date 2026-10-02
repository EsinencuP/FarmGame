using MyLittleFarm.Core;
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
        private InventorySystem _inventory;
        private WalletSystem _wallet;
        private FarmCatalog _catalog;

        /// <summary>Цена одной моркови, установленная в Inspector.</summary>
        public int CarrotSellPrice => carrotSellPrice;

        public void Configure(InventorySystem inventory, WalletSystem wallet, FarmCatalog catalog = null)
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
            foreach (var crop in _catalog.Crops)
            {
                if (!_catalog.TryGetItem(crop.HarvestItemId, out var item)) continue;
                var amount = _inventory.GetAmount(item.ItemId);
                // Проверка до умножения не допускает переполнения long при больших запасах.
                if (!TransactionMath.TryAddSale(item.SellPrice, amount, _wallet.Coins, total,
                    out var nextTotal)) return 0;
                total = nextTotal;
                sold += amount;
            }
            if (_catalog.TryGetItem("apple", out var apple))
            {
                var amount = _inventory.GetAmount(apple.ItemId);
                if (!TransactionMath.TryAddSale(apple.SellPrice, amount, _wallet.Coins, total,
                    out var nextTotal)) return 0;
                total = nextTotal;
                sold += amount;
            }
            if (_catalog.TryGetItem("egg", out var egg))
            {
                var amount = _inventory.GetAmount(egg.ItemId);
                if (!TransactionMath.TryAddSale(egg.SellPrice, amount, _wallet.Coins, total,
                    out var nextTotal)) return 0;
                total = nextTotal;
                sold += amount;
            }
            if (sold == 0) return 0;
            foreach (var crop in _catalog.Crops)
                if (_catalog.TryGetItem(crop.HarvestItemId, out var item))
                {
                    var amount = _inventory.GetAmount(item.ItemId);
                    if (amount > 0) _inventory.TryRemove(item.ItemId, amount);
                }
            if (_catalog.TryGetItem("apple", out var appleToRemove))
            {
                var amount = _inventory.GetAmount(appleToRemove.ItemId);
                if (amount > 0) _inventory.TryRemove(appleToRemove.ItemId, amount);
            }
            if (_catalog.TryGetItem("egg", out var eggToRemove))
            {
                var amount = _inventory.GetAmount(eggToRemove.ItemId);
                if (amount > 0) _inventory.TryRemove(eggToRemove.ItemId, amount);
            }
            _wallet.AddCoins((int)total);
            GameEvents.RaiseStatusChanged($"Продано урожая: {sold}  +{total} монет");
            return (int)total;
        }

        public int SellAllCarrots()
        {
            // До удаления урожая проверяется переполнение будущего баланса.
            var amount = _inventory.GetAmount(InventorySystem.CarrotId);
            long earnedLong = 0;
            if (amount <= 0 || !TransactionMath.TryAddSale(carrotSellPrice, amount, _wallet.Coins,
                    0, out earnedLong)
                || !_inventory.TryRemove(InventorySystem.CarrotId, amount))
            {
                return 0;
            }

            var earned = (int)earnedLong;
            _wallet.AddCoins(earned);
            GameEvents.RaiseStatusChanged($"Продано: {amount} моркови  +{earned} монет");
            return earned;
        }
    }
}
