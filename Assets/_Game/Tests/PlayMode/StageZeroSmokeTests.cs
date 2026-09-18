using System;
using System.Collections;
using System.IO;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.Farming;
using MyLittleFarm.Gameplay.World;
using MyLittleFarm.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MyLittleFarm.Tests.PlayMode
{
    /// <summary>Дымовые тесты подтверждают, что Stage 0 собирается в связный игровой цикл.</summary>
    public sealed class StageZeroSmokeTests : FarmPlayModeFixture
    {
        /// <summary>Проходит путь обработки земли, посадки, сбора, продажи и восстановления снимка.</summary>
        [UnityTest]
        public IEnumerator Prototype_CompletesFarmSellAndSaveRestoreLoop()
        {
            // Farm уже создан изолированной фикстурой; повторный вызов проверяет идемпотентность bootstrap.
            yield return null;

            var bootstrap = Farm;

            bootstrap.BuildPrototype();
            Assert.That(
                UnityEngine.Object.FindObjectsByType<GridSystem>(FindObjectsSortMode.None),
                Has.Length.EqualTo(1),
                "Bootstrap must be idempotent");

            // Ищем все основные системы, чтобы ранняя ошибка сборки давала точное сообщение теста.
            var grid = UnityEngine.Object.FindFirstObjectByType<GridSystem>();
            var soil = UnityEngine.Object.FindFirstObjectByType<SoilSystem>();
            var crops = UnityEngine.Object.FindFirstObjectByType<CropSystem>();
            var inventory = UnityEngine.Object.FindFirstObjectByType<InventorySystem>();
            var wallet = UnityEngine.Object.FindFirstObjectByType<WalletSystem>();
            var selling = UnityEngine.Object.FindFirstObjectByType<SellingSystem>();
            var save = UnityEngine.Object.FindFirstObjectByType<SaveSystem>();
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            var cameraController = UnityEngine.Object.FindFirstObjectByType<IsometricCameraController>();
            var selector = UnityEngine.Object.FindFirstObjectByType<CellSelector>();
            var interaction = UnityEngine.Object.FindFirstObjectByType<InteractionSystem>();
            var hud = UnityEngine.Object.FindFirstObjectByType<HUDController>();

            Assert.That(grid, Is.Not.Null);
            Assert.That(grid.Width, Is.EqualTo(8));
            Assert.That(grid.Height, Is.EqualTo(8));
            Assert.That(soil, Is.Not.Null);
            Assert.That(crops, Is.Not.Null);
            Assert.That(inventory, Is.Not.Null);
            Assert.That(wallet, Is.Not.Null);
            Assert.That(selling, Is.Not.Null);
            Assert.That(save, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(cameraController, Is.Not.Null);
            Assert.That(selector, Is.Not.Null);
            Assert.That(interaction, Is.Not.Null);
            Assert.That(hud, Is.Not.Null);

            player.Teleport(grid.CellToWorld(new Vector2Int(0, 0)) + Vector3.up * 1.1f);
            player.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            selector.RefreshSelection();
            Assert.That(selector.HasSelection, Is.True);

            // Посадка датируется прошлым, чтобы в том же тесте получить зрелый урожай.
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var position = selector.SelectedPosition;
            Assert.That(interaction.InteractWithCell(position, now - 9_000), Is.True, "Till soil");
            Assert.That(interaction.InteractWithCell(position, now - 9_000), Is.True, "Plant seed");
            Assert.That(interaction.InteractWithCell(position, now), Is.True, "Harvest mature crop");
            Assert.That(inventory.GetAmount(InventorySystem.CarrotId), Is.EqualTo(CropSystem.PrototypeYield));

            var coinsBeforeSale = wallet.Coins;
            player.Teleport(new Vector3(2.5f, 1.1f, -7f));
            Assert.That(interaction.Interact(now), Is.True, "Sell harvested crop");
            Assert.That(wallet.Coins, Is.GreaterThan(coinsBeforeSale));

            var snapshot = save.Capture();
            var expectedCoins = wallet.Coins;
            wallet.SetCoins(0);
            save.Restore(snapshot);

            Assert.That(wallet.Coins, Is.EqualTo(expectedCoins));
            Assert.That(snapshot.saveVersion, Is.EqualTo(SaveSystem.CurrentSaveVersion));
        }

        /// <summary>Проверяет запись файла и восстановление всех данных фермы и игрока.</summary>
        [UnityTest]
        public IEnumerator SaveFile_RestoresGridCropInventoryMoneyAndPlayer()
        {
            yield return null;

            var grid = UnityEngine.Object.FindFirstObjectByType<GridSystem>();
            var soil = UnityEngine.Object.FindFirstObjectByType<SoilSystem>();
            var crops = UnityEngine.Object.FindFirstObjectByType<CropSystem>();
            var inventory = UnityEngine.Object.FindFirstObjectByType<InventorySystem>();
            var wallet = UnityEngine.Object.FindFirstObjectByType<WalletSystem>();
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            var save = UnityEngine.Object.FindFirstObjectByType<SaveSystem>();
            // Уникальный временный путь исключает влияние параллельных и прошлых запусков теста.
            var path = Path.Combine(Application.temporaryCachePath, $"stage-zero-{Guid.NewGuid():N}.json");

            try
            {
                var plantedPosition = new Vector2Int(1, 1);
                Assert.That(soil.Till(plantedPosition), Is.True);
                Assert.That(inventory.TryRemove(InventorySystem.CarrotSeedId, 1), Is.True);
                Assert.That(crops.Plant(plantedPosition, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), Is.True);
                inventory.Add(InventorySystem.CarrotId, 3);
                wallet.SetCoins(73);
                player.Teleport(new Vector3(1.5f, 1.1f, -2.5f));

                Assert.That(save.SaveToPath(path), Is.True);
                Assert.That(File.Exists(path), Is.True);

                crops.ClearAll();
                grid.Restore(null);
                inventory.Restore(null);
                wallet.SetCoins(0);
                player.Teleport(Vector3.zero);

                Assert.That(save.LoadFromPath(path), Is.True);
                Assert.That(wallet.Coins, Is.EqualTo(73));
                Assert.That(inventory.GetAmount(InventorySystem.CarrotId), Is.EqualTo(3));
                Assert.That(crops.Contains(plantedPosition), Is.True);
                Assert.That(grid.TryGetCell(plantedPosition, out var cell), Is.True);
                Assert.That(cell.State, Is.EqualTo(GridCellState.Tilled));
                Assert.That(player.transform.position, Is.EqualTo(new Vector3(1.5f, 1.1f, -2.5f)));
            }
            finally
            {
                DeleteIfPresent(path);
                DeleteIfPresent(path + ".tmp");
                DeleteIfPresent(path + ".bak");
            }
        }

        private static void DeleteIfPresent(string path)
        {
            // Удаляет только явно переданный тестовый файл, если он был создан.
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
