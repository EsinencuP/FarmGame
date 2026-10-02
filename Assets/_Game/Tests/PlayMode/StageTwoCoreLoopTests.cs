using System;
using System.Collections;
using System.IO;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.Animals;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.Farming;
using MyLittleFarm.Gameplay.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MyLittleFarm.Tests.PlayMode
{
    /// <summary>Проверяет яблоню с повторным урожаем и цикл курицы из задач Этапа 2.</summary>
    public sealed class StageTwoCoreLoopTests : FarmPlayModeFixture
    {
        /// <summary>Проверяет, что макет загона и курятник доступны без внешних моделей.</summary>
        [UnityTest]
        public IEnumerator PlaceholderPenAndCoopWorkWithoutArtAssets()
        {
            yield return null;
            var pen = Farm.transform.Find("Chicken Pen Mockup");
            var animals = UnityEngine.Object.FindFirstObjectByType<AnimalSystem>();
            var buildings = UnityEngine.Object.FindFirstObjectByType<BuildSystem>();
            var wallet = UnityEngine.Object.FindFirstObjectByType<WalletSystem>();

            Assert.That(pen, Is.Not.Null);
            Assert.That(pen.childCount, Is.GreaterThan(0));
            Assert.That(pen.GetComponentsInChildren<Collider>(), Is.Empty,
                "Visual pen rails must not block grid interaction");
            Assert.That(animals.IsInPen(new Vector2Int(1, 3)), Is.True);
            wallet.SetCoins(30);
            Assert.That(buildings.TryPlace("coop", new Vector2Int(6, 5), 0, out var reason),
                Is.True, reason);
            Assert.That(buildings.GetAt(new Vector2Int(6, 5)).definitionId, Is.EqualTo("coop"));
            Assert.That(wallet.Coins, Is.EqualTo(12));
        }

        /// <summary>Яблоня созревает, плодоносит повторно и восстанавливается из JSON.</summary>
        [UnityTest]
        public IEnumerator AppleTreeRepeatsHarvestAndSurvivesSaveFile()
        {
            yield return null;
            var orchard = UnityEngine.Object.FindFirstObjectByType<OrchardSystem>();
            var grid = UnityEngine.Object.FindFirstObjectByType<GridSystem>();
            var inventory = UnityEngine.Object.FindFirstObjectByType<InventorySystem>();
            var save = UnityEngine.Object.FindFirstObjectByType<SaveSystem>();
            var position = new Vector2Int(1, 2);
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            Assert.That(orchard.PlantApple(position, now - 31_000), Is.True);
            Assert.That(grid.GetCellType(position), Is.EqualTo(CellType.Tree));
            Assert.That(orchard.TryHarvest(position, now, out var firstYield), Is.True);
            Assert.That(firstYield, Is.EqualTo(3));
            Assert.That(orchard.TryHarvest(position, now + 1_000, out _), Is.False);
            Assert.That(orchard.TryHarvest(position, now + 46_001, out var repeatYield), Is.True);
            Assert.That(repeatYield, Is.EqualTo(3));

            var path = Path.Combine(Application.temporaryCachePath, $"stage-two-tree-{Guid.NewGuid():N}.json");
            try
            {
                Assert.That(save.SaveToPath(path), Is.True);
                orchard.ClearAll();
                inventory.Restore(null);
                Assert.That(save.LoadFromPath(path), Is.True);
                Assert.That(orchard.Contains(position), Is.True);
                Assert.That(grid.GetCellType(position), Is.EqualTo(CellType.Tree));
                Assert.That(inventory.GetAmount("apple"), Is.EqualTo(6));
            }
            finally
            {
                DeleteIfPresent(path);
                DeleteIfPresent(path + ".tmp");
                DeleteIfPresent(path + ".bak");
            }
        }

        /// <summary>Курица принимает корм, выдаёт яйцо и сохраняет прогресс загона.</summary>
        [UnityTest]
        public IEnumerator ChickenFeedEggAndRestoreLoop()
        {
            yield return null;
            var animals = UnityEngine.Object.FindFirstObjectByType<AnimalSystem>();
            var inventory = UnityEngine.Object.FindFirstObjectByType<InventorySystem>();
            var grid = UnityEngine.Object.FindFirstObjectByType<GridSystem>();
            var save = UnityEngine.Object.FindFirstObjectByType<SaveSystem>();
            var wallet = UnityEngine.Object.FindFirstObjectByType<WalletSystem>();
            var selling = UnityEngine.Object.FindFirstObjectByType<SellingSystem>();
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            inventory.Add("wheat", 1);
            var position = new Vector2Int(1, 3);

            wallet.SetCoins(30);
            Assert.That(animals.TryBuyChicken(position, now), Is.True);
            Assert.That(wallet.Coins, Is.EqualTo(30 - animals.ChickenPrice));
            var snapshotBeforeFeed = animals.Capture();
            Assert.That(snapshotBeforeFeed, Has.Count.EqualTo(1));
            var testedAnimal = snapshotBeforeFeed[snapshotBeforeFeed.Count - 1];
            Assert.That(animals.Feed(testedAnimal.animalId, now), Is.True);
            var readySnapshot = animals.Capture();
            var readyState = readySnapshot.Find(state => state.animalId == testedAnimal.animalId);
            readyState.nextProductAtUnixMs = now - 1;
            readyState.readyProductCount = 1;
            animals.Restore(readySnapshot);
            Assert.That(animals.TryCollect(testedAnimal.animalId, out var eggs), Is.True);
            Assert.That(eggs, Is.EqualTo(1));
            Assert.That(inventory.GetAmount("egg"), Is.EqualTo(1));

            var saveSnapshot = save.Capture();
            animals.ClearAll();
            inventory.Restore(null);
            // Изменение текущей сцены не должно мешать проверке загружаемого снимка.
            grid.SetTerrainCell(position, new TerrainCell(CellType.Water));
            Assert.That(saveSnapshot.animals, Has.Count.EqualTo(1));
            save.Restore(saveSnapshot);
            Assert.That(animals.Count, Is.EqualTo(1));
            Assert.That(grid.GetCellType(position), Is.EqualTo(CellType.Grass));
            Assert.That(inventory.GetAmount("egg"), Is.EqualTo(1));
            Assert.That(selling.SellAllCrops(), Is.GreaterThanOrEqualTo(14));
            Assert.That(inventory.GetAmount("egg"), Is.Zero);
        }

        /// <summary>Онбординг принимает действия в заданном порядке и переносит шаг в сохранение.</summary>
        [UnityTest]
        public IEnumerator OnboardingSequenceSurvivesSaveRestore()
        {
            yield return null;
            var onboarding = UnityEngine.Object.FindFirstObjectByType<OnboardingSystem>();
            var save = UnityEngine.Object.FindFirstObjectByType<SaveSystem>();
            foreach (var action in new[] { "house", "chest", "plant", "harvest", "sale", "purchase" })
                GameEvents.RaiseProgressAction(action);
            Assert.That(onboarding.Step, Is.EqualTo(6));
            var snapshot = save.Capture();
            onboarding.Restore(0);
            save.Restore(snapshot);
            Assert.That(onboarding.Step, Is.EqualTo(6));
        }

        private static void DeleteIfPresent(string path)
        {
            // Удаляется только временный файл конкретного теста.
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
