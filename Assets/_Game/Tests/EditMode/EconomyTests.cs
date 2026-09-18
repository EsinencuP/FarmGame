using MyLittleFarm.Gameplay.Economy;
using NUnit.Framework;
using UnityEngine;

namespace MyLittleFarm.Tests.EditMode
{
    /// <summary>Проверяет атомарность и сохранность данных инвентаря, кошелька и продажи.</summary>
    public sealed class EconomyTests
    {
        /// <summary>Переполнение баланса не должно удалять урожай или менять деньги.</summary>
        [Test]
        public void OverflowDoesNotLoseCropsOrChangeWallet()
        {
            var root = new GameObject("Overflow Test");
            try
            {
                var inventory = root.AddComponent<InventorySystem>();
                inventory.Add(InventorySystem.CarrotId, 3);
                var wallet = root.AddComponent<WalletSystem>();
                wallet.SetCoins(int.MaxValue - 1);
                var selling = root.AddComponent<SellingSystem>();
                selling.Configure(inventory, wallet);
                Assert.That(selling.SellAllCarrots(), Is.Zero);
                Assert.That(inventory.GetAmount(InventorySystem.CarrotId), Is.EqualTo(3));
                Assert.That(wallet.Coins, Is.EqualTo(int.MaxValue - 1));
                Assert.Throws<System.OverflowException>(() => wallet.AddCoins(2));
                Assert.That(wallet.Coins, Is.EqualTo(int.MaxValue - 1));
            }
            finally { Object.DestroyImmediate(root); }
        }

        /// <summary>Обычная продажа удаляет весь товар и начисляет точную стоимость.</summary>
        [Test]
        public void SellingCarrots_RemovesItemsAndAddsCoins()
        {
            var root = new GameObject("Economy Test");
            try
            {
                var inventory = root.AddComponent<InventorySystem>();
                inventory.ConfigurePrototypeInventory();
                inventory.Add(InventorySystem.CarrotId, 3);
                var wallet = root.AddComponent<WalletSystem>();
                wallet.Configure(25);
                var selling = root.AddComponent<SellingSystem>();
                selling.Configure(inventory, wallet);

                var earned = selling.SellAllCarrots();

                Assert.That(earned, Is.EqualTo(3 * SellingSystem.CarrotSellPrice));
                Assert.That(inventory.GetAmount(InventorySystem.CarrotId), Is.Zero);
                Assert.That(wallet.Coins, Is.EqualTo(40));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>Снимок инвентаря восстанавливает оба количества после промежуточных изменений.</summary>
        [Test]
        public void InventoryCaptureAndRestore_PreservesCounts()
        {
            var root = new GameObject("Inventory Test");
            try
            {
                var inventory = root.AddComponent<InventorySystem>();
                inventory.ConfigurePrototypeInventory();
                inventory.Add(InventorySystem.CarrotId, 4);
                var snapshot = inventory.Capture();
                inventory.TryRemove(InventorySystem.CarrotSeedId, 3);
                inventory.Restore(snapshot);

                Assert.That(inventory.GetAmount(InventorySystem.CarrotSeedId), Is.EqualTo(8));
                Assert.That(inventory.GetAmount(InventorySystem.CarrotId), Is.EqualTo(4));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
