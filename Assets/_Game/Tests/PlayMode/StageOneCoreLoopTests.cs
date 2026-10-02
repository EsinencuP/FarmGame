using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.Farming;
using MyLittleFarm.Gameplay.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MyLittleFarm.Tests.PlayMode
{
    /// <summary>Проверяет покупку, шесть культур, продажу, расширение и сохранение Этапа 1.</summary>
    public sealed class StageOneCoreLoopTests : FarmPlayModeFixture
    {
        [UnityTest]
        public IEnumerator SixCropsShopSectorAndUpgradesSurviveSaveRestore()
        {
            // Шесть соседних клеток доступны с начальной позиции игрока на базовом уровне инструмента.
            yield return null;
            var catalog = UnityEngine.Object.FindFirstObjectByType<FarmCatalog>();
            var slots = UnityEngine.Object.FindFirstObjectByType<QuickSlotSystem>();
            var shop = UnityEngine.Object.FindFirstObjectByType<SeedShopSystem>();
            var sector = UnityEngine.Object.FindFirstObjectByType<SectorSystem>();
            var upgrades = UnityEngine.Object.FindFirstObjectByType<ToolUpgradeSystem>();
            var inventory = UnityEngine.Object.FindFirstObjectByType<InventorySystem>();
            var wallet = UnityEngine.Object.FindFirstObjectByType<WalletSystem>();
            var interaction = UnityEngine.Object.FindFirstObjectByType<InteractionSystem>();
            var selling = UnityEngine.Object.FindFirstObjectByType<SellingSystem>();
            var grid = UnityEngine.Object.FindFirstObjectByType<GridSystem>();
            var save = UnityEngine.Object.FindFirstObjectByType<SaveSystem>();
            var buildings = UnityEngine.Object.FindFirstObjectByType<BuildSystem>();
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            var cells = new[] { new Vector2Int(2, 0), new Vector2Int(3, 0),
                new Vector2Int(4, 0), new Vector2Int(2, 1), new Vector2Int(3, 1),
                new Vector2Int(4, 1) };
            Assert.That(catalog.Crops.Count, Is.EqualTo(6));
            var growthTimes = new HashSet<float>();
            var yields = new HashSet<int>();
            var prices = new HashSet<int>();
            foreach (var crop in catalog.Crops)
            {
                growthTimes.Add(crop.GrowDurationSeconds);
                yields.Add(crop.YieldAmount);
                Assert.That(catalog.TryGetItem(crop.HarvestItemId, out var item), Is.True);
                prices.Add(item.SellPrice);
            }
            Assert.That(growthTimes.Count, Is.EqualTo(6));
            Assert.That(yields.Count, Is.EqualTo(4));
            Assert.That(prices.Count, Is.EqualTo(6));
            wallet.SetCoins(250);
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var balanceAwayFromMarket = wallet.Coins;
            Assert.That(shop.BuySelected(), Is.False, "The seed shop requires the market");
            Assert.That(wallet.Coins, Is.EqualTo(balanceAwayFromMarket));
            player.Teleport(new Vector3(7.5f, 1.1f, 1.5f));

            // Покупки проходят у рынка; после этого игрок возвращается к грядкам.
            for (var index = 0; index < catalog.Crops.Count; index++)
            {
                Assert.That(slots.Select(index), Is.True);
                Assert.That(shop.BuySelected(), Is.True);
            }
            player.Teleport(grid.CellToWorld(new Vector2Int(3, 0)) + Vector3.up * 1.1f);

            for (var index = 0; index < catalog.Crops.Count; index++)
            {
                // Для каждого вида цикл проходит вспашку, посадку и сбор.
                var crop = catalog.Crops[index];
                Assert.That(slots.Select(index), Is.True);
                Assert.That(interaction.InteractWithCell(cells[index], now - 30_000), Is.True);
                Assert.That(interaction.InteractWithCell(cells[index], now - 30_000), Is.True);
                Assert.That(interaction.InteractWithCell(cells[index], now), Is.True);
                Assert.That(inventory.GetAmount(crop.HarvestItemId), Is.EqualTo(crop.YieldAmount));
            }

            Assert.That(selling.SellAllCrops(), Is.GreaterThan(0));
            foreach (var crop in catalog.Crops)
                Assert.That(inventory.GetAmount(crop.HarvestItemId), Is.Zero);
            Assert.That(grid.GetCellType(new Vector2Int(10, 0)), Is.EqualTo(CellType.Locked));
            Assert.That(sector.TryPurchase(), Is.True);
            Assert.That(grid.GetCellType(new Vector2Int(10, 0)), Is.EqualTo(CellType.Grass));
            Assert.That(buildings.TryPlace("flowerbed", new Vector2Int(10, 0), 0, out var reason),
                Is.True, reason);
            var buildingId = buildings.GetAt(new Vector2Int(10, 0)).id;
            Assert.That(upgrades.TryUpgrade(), Is.True);
            Assert.That(upgrades.TryUpgrade(), Is.True);
            var snapshot = save.Capture();
            Assert.That(snapshot.saveVersion, Is.EqualTo(SaveSystem.CurrentSaveVersion));

            // Восстановление проверяет сохранение трёх независимых настроек прогресса.
            wallet.SetCoins(0);
            slots.Select(0);
            upgrades.Restore(0);
            save.Restore(snapshot);
            Assert.That(wallet.Coins, Is.EqualTo(snapshot.coins));
            Assert.That(slots.SelectedIndex, Is.EqualTo(5));
            Assert.That(sector.IsUnlocked, Is.True);
            Assert.That(upgrades.Level, Is.EqualTo(2));
            Assert.That(grid.GetCellType(new Vector2Int(10, 0)), Is.EqualTo(CellType.Building));
            Assert.That(buildings.GetAt(new Vector2Int(10, 0)).id, Is.EqualTo(buildingId));
        }

        /// <summary>Проверяет перенос нового прогресса через реальный JSON-файл сохранения.</summary>
        [UnityTest]
        public IEnumerator StageOneProgressSurvivesJsonSaveFile()
        {
            yield return null;
            var wallet = UnityEngine.Object.FindFirstObjectByType<WalletSystem>();
            var sector = UnityEngine.Object.FindFirstObjectByType<SectorSystem>();
            var upgrades = UnityEngine.Object.FindFirstObjectByType<ToolUpgradeSystem>();
            var slots = UnityEngine.Object.FindFirstObjectByType<QuickSlotSystem>();
            var save = UnityEngine.Object.FindFirstObjectByType<SaveSystem>();
            var grid = UnityEngine.Object.FindFirstObjectByType<GridSystem>();
            var path = Path.Combine(Application.temporaryCachePath, $"stage-one-{Guid.NewGuid():N}.json");

            try
            {
                wallet.SetCoins(100);
                Assert.That(sector.TryPurchase(), Is.True);
                Assert.That(upgrades.TryUpgrade(), Is.True);
                Assert.That(upgrades.TryUpgrade(), Is.True);
                Assert.That(slots.Select(2), Is.True);
                Assert.That(save.SaveToPath(path), Is.True);

                // После намеренной смены всех трёх состояний загрузка должна вернуть их из файла.
                wallet.SetCoins(0);
                sector.Restore(false);
                upgrades.Restore(0);
                slots.Select(0);
                Assert.That(save.LoadFromPath(path), Is.True);
                Assert.That(wallet.Coins, Is.EqualTo(20));
                Assert.That(sector.IsUnlocked, Is.True);
                Assert.That(upgrades.Level, Is.EqualTo(2));
                Assert.That(slots.SelectedIndex, Is.EqualTo(2));
                Assert.That(grid.GetCellType(new Vector2Int(10, 0)), Is.EqualTo(CellType.Grass));
            }
            finally
            {
                // Удаляются только временные файлы этого теста и резервная копия записи.
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        }
    }
}
