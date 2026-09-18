using MyLittleFarm.Core;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Economy
{
    /// <summary>Преобразует весь собранный урожай моркови в монеты по фиксированной цене.</summary>
    public sealed class SellingSystem : MonoBehaviour
    {
        // Цена одной моркови является общей игровой константой.
        public const int CarrotSellPrice = 5;

        // Ссылки позволяют провести удаление товара и начисление денег как одну операцию.
        private InventorySystem _inventory;
        private WalletSystem _wallet;

        public void Configure(InventorySystem inventory, WalletSystem wallet)
        {
            // Зависимости передаются bootstrap-скриптом после создания экономики.
            _inventory = inventory;
            _wallet = wallet;
        }

        public int SellAllCarrots()
        {
            // До удаления урожая проверяется переполнение будущего баланса.
            var amount = _inventory.GetAmount(InventorySystem.CarrotId);
            var earnedLong = (long)amount * CarrotSellPrice;
            if (amount <= 0 || earnedLong > int.MaxValue - _wallet.Coins
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
