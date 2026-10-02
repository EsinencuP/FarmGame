using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.Farming;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Economy
{
    /// <summary>Покупает семена выбранной культуры за монеты через временную команду P.</summary>
    [DefaultExecutionOrder(-90)]
    public sealed class SeedShopSystem : MonoBehaviour
    {
        [SerializeField, Min(1)] private int seedsPerPurchase = 4;
        [Tooltip("Максимальная горизонтальная дистанция покупки до ящика или построенного рынка.")]
        [SerializeField, Min(0.1f)] private float shoppingDistance = 2.4f;
        /// <summary>Количество семян в одной покупке, заданное в Inspector.</summary>
        public int PurchaseAmount => seedsPerPurchase;

        // Ссылки передаются bootstrap и не ищутся каждый кадр.
        private InputReader _input;
        private FarmCatalog _catalog;
        private QuickSlotSystem _slots;
        private InventorySystem _inventory;
        private WalletSystem _wallet;
        private Transform _player;
        private Transform _stall;
        private GridSystem _grid;

        /// <summary>Подключает каталог, инвентарь, деньги и выбранный быстрый слот.</summary>
        public void Configure(InputReader input, FarmCatalog catalog, QuickSlotSystem slots,
            InventorySystem inventory, WalletSystem wallet, Transform player = null,
            Transform stall = null, GridSystem grid = null)
        {
            _input = input;
            _catalog = catalog;
            _slots = slots;
            _inventory = inventory;
            _wallet = wallet;
            _player = player;
            _stall = stall;
            _grid = grid;
        }

        private void Update()
        {
            // В режиме строительства команда покупки не должна менять игровые ресурсы.
            if (_input != null && _input.ShopPressed && !_input.BuildModeActive
                && !_input.SuppressGameplayThisFrame) BuySelected();
        }

        /// <summary>Проверяет цену и вместимость до списания денег, затем добавляет семена.</summary>
        public bool BuySelected()
        {
            var crop = _slots?.SelectedCrop;
            return crop != null && TryBuyItem(crop.SeedItemId, seedsPerPurchase);
        }

        /// <summary>Покупает любой разрешённый каталогом предмет, сохраняя деньги при неудаче.</summary>
        public bool TryBuyItem(string itemId, int amount)
        {
            if (!CanShop())
            {
                GameEvents.RaiseStatusChanged("Подойдите к рынку для покупки");
                return false;
            }
            if (amount <= 0 || _catalog == null || !_catalog.TryGetItem(itemId, out var item)
                || !item.CanBuy) return false;
            if (!TransactionMath.TryPurchase(item.BuyPrice, amount,
                _inventory.GetAmount(item.ItemId), _wallet.Coins, out var cost))
            {
                GameEvents.RaiseStatusChanged((long)item.BuyPrice * amount > _wallet.Coins
                    ? "Недостаточно монет для покупки" : "Инвентарь заполнен");
                return false;
            }
            if (!_wallet.TrySpend(cost))
            {
                GameEvents.RaiseStatusChanged("Недостаточно монет для семян");
                return false;
            }
            _inventory.Add(item.ItemId, amount);
            GameEvents.RaiseStatusChanged($"Куплено: {amount} × {item.DisplayName} (-{cost})");
            GameEvents.RaiseProgressAction("purchase");
            return true;
        }

        /// <summary>Проверяет, находится ли игрок возле исходного ящика или размещённого рынка.</summary>
        private bool CanShop()
        {
            // Отсутствующий игрок означает незавершённое подключение магазина.
            if (_player == null) return false;
            if (_grid != null && _grid.Buildings != null
                && _grid.Buildings.IsNearMarket(_player.position, shoppingDistance)) return true;
            if (_stall == null) return false;
            var delta = _player.position - _stall.position;
            delta.y = 0f;
            return delta.sqrMagnitude <= shoppingDistance * shoppingDistance;
        }
    }
}
