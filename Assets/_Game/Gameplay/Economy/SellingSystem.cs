using MyLittleFarm.Core;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Economy
{
    public sealed class SellingSystem : MonoBehaviour
    {
        public const int CarrotSellPrice = 5;

        private InventorySystem _inventory;
        private WalletSystem _wallet;

        public void Configure(InventorySystem inventory, WalletSystem wallet)
        {
            _inventory = inventory;
            _wallet = wallet;
        }

        public int SellAllCarrots()
        {
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
