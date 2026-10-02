using System;
using System.Collections;
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
    /// <summary>Проверяет совместную работу строительства с физикой, экономикой, фермой и сохранением.</summary>
    public sealed class BuildingIntegrationTests : FarmPlayModeFixture
    {
        /// <summary>Ищет систему внутри изолированного корня текущего теста.</summary>
        private T System<T>() where T : Component => Farm.GetComponentInChildren<T>();

        /// <summary>Проверяет полный цикл постройки, переноса, отмены, удаления и восстановления.</summary>
        [UnityTest]
        public IEnumerator PlacementMoveCancelDeleteAndSavePreserveEconomyAndTerrain()
        {
            // Все зависимости извлекаются из одного созданного bootstrap-объекта.
            var build = System<BuildSystem>();
            var wallet = System<WalletSystem>();
            var grid = System<GridSystem>();
            var soil = System<SoilSystem>();
            var crops = System<CropSystem>();
            var save = System<SaveSystem>();
            // Origin — исходный угол дома размером 2x2 клетки.
            var origin = new Vector2Int(0, 3);
            Assert.That(build.TryPlace("house", origin, 0, out var reason), Is.True, reason);
            Assert.That(wallet.Coins, Is.EqualTo(5));
            Assert.That(build.Count, Is.EqualTo(1));
            Assert.That(grid.IsOccupied(new Vector2Int(1, 4)), Is.True);
            Assert.That(soil.Till(new Vector2Int(1, 4)), Is.False);
            Assert.That(crops.Plant(origin, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), Is.False);
            Assert.That(build.TryPlace("flowerbed", origin, 0, out _), Is.False);
            Assert.That(build.TryPlace("house", new Vector2Int(5, 5), 0, out _), Is.False, "Not enough money");
            Assert.That(wallet.Coins, Is.EqualTo(5));
            var id = build.GetAt(origin).id;
            Assert.That(build.TryMove(id, new Vector2Int(1, 3), 1, out reason), Is.True, reason);
            Assert.That(build.TryMove(id, new Vector2Int(10, 10), 0, out _), Is.False, "Unloaded chunk blocks placement");
            Assert.That(grid.IsOccupied(origin), Is.False);
            Assert.That(grid.TryGetCell(origin, out var underlying), Is.True);
            Assert.That(underlying.type, Is.EqualTo(CellType.Grass), "Moving frees the old footprint");
            Assert.That(build.GetAt(new Vector2Int(1, 3)).quarterTurns, Is.EqualTo(1));
            build.SetActive(true);
            build.SetActive(false);
            Assert.That(wallet.Coins, Is.EqualTo(5), "Move and cancellation are free");

            // Отдельная клетка с культурой проверяет конфликт строительства и земледелия.
            var cropCell = new Vector2Int(6, 6);
            soil.Till(cropCell);
            crops.Plant(cropCell, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            Assert.That(build.TryPlace("flowerbed", cropCell, 0, out _), Is.False, "Crops block placement");
            // Snapshot также используется для искусственного создания повреждённого дубликата.
            var snapshot = save.Capture();
            var path = Path.Combine(Application.temporaryCachePath, "building-test-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                Assert.That(save.SaveToPath(path), Is.True);
                Assert.That(build.TryRemove(id, out _), Is.True);
                Assert.That(wallet.Coins, Is.EqualTo(15), "Half original cost refunded");
                Assert.That(build.TryRemove(id, out _), Is.False);
                Assert.That(save.LoadFromPath(path), Is.True);
                Assert.That(wallet.Coins, Is.EqualTo(5));
                Assert.That(build.Count, Is.EqualTo(1));
                Assert.That(build.GetAt(new Vector2Int(1, 3)).id, Is.EqualTo(id));
                Assert.That(crops.Contains(cropCell), Is.True);

                snapshot.buildings.Add(snapshot.buildings[0].Copy());
                Assert.Throws<InvalidDataException>(() => save.Restore(snapshot));
                Assert.That(wallet.Coins, Is.EqualTo(5), "Invalid save does not mutate economy");
                Assert.That(build.Count, Is.EqualTo(1), "Invalid save does not mutate layout");
                Assert.That(crops.Contains(cropCell), Is.True);
            }
            finally
            {
                foreach (var suffix in new[] { "", ".tmp", ".bak" })
                    if (File.Exists(path + suffix)) File.Delete(path + suffix);
            }
            yield return null;
        }

        /// <summary>Игрок и произвольный коллайдер должны запретить покупку без списания денег.</summary>
        [UnityTest]
        public IEnumerator PlayerAndExternalObstaclesBlockPlacementWithoutCharging()
        {
            var build = System<BuildSystem>();
            var grid = System<GridSystem>();
            var player = System<PlayerController>();
            var wallet = System<WalletSystem>();
            player.Teleport(grid.CellToWorld(new Vector2Int(5, 5)) + Vector3.up * 1.1f);
            Assert.That(build.TryPlace("flowerbed", new Vector2Int(5, 5), 0, out _), Is.False);
            var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.transform.position = grid.CellToWorld(new Vector2Int(6, 5)) + Vector3.up * 0.5f;
            Assert.That(build.TryPlace("flowerbed", new Vector2Int(6, 5), 0, out _), Is.False);
            Assert.That(wallet.Coins, Is.EqualTo(25));
            Assert.That(build.Count, Is.Zero);
            UnityEngine.Object.Destroy(obstacle);
            yield return null;
        }

        /// <summary>Подтверждает перемещение единого маркера подсветки между клетками.</summary>
        [UnityTest]
        public IEnumerator SelectionClearsPreviousTileAndMaterialsAreShared()
        {
            var grid = System<GridSystem>();
            var selector = System<CellSelector>();
            var marker = grid.transform.Find("Cell Selection").gameObject;
            grid.Select(new Vector2Int(0, 0));
            Assert.That(marker.activeSelf, Is.True);
            grid.Select(new Vector2Int(1, 0));
            Assert.That(marker.transform.position.x, Is.EqualTo(1.5f).Within(0.001f));
            grid.Select(null);
            Assert.That(marker.activeSelf, Is.False);

            // Выбор мировой точки проверяет тот же финальный шаг, который использует луч от курсора.
            var hoveredPosition = new Vector2Int(4, 5);
            Assert.That(selector.SelectWorldPoint(grid.CellToWorld(hoveredPosition)), Is.True);
            Assert.That(selector.SelectedPosition, Is.EqualTo(hoveredPosition));
            Assert.That(selector.SelectWorldPoint(new Vector3(1000f, 0f, 1000f)), Is.False);
            yield return null;
        }

        /// <summary>Выгруженная сцена содержит постоянный TilemapChunk с физической поверхностью.</summary>
        [UnityTest]
        public IEnumerator GeneratedFixtureContainsPersistentGridMarkers()
        {
            var grid = System<GridSystem>();
            var chunks = Farm.GetComponentsInChildren<TilemapChunk>(true);
            Assert.That(chunks, Has.Length.EqualTo(2));
            Assert.That(chunks[0].GetComponentInChildren<Collider>(), Is.Not.Null);
            Assert.That(grid.LoadedChunkCount, Is.EqualTo(2));
            yield return null;
        }

        /// <summary>Проверяет миграцию версии 1 и принцип «проверить до изменения мира».</summary>
        [UnityTest]
        public IEnumerator LegacySaveLoadsAndInvalidSaveLeavesWorldIntact()
        {
            var save = System<SaveSystem>();
            var wallet = System<WalletSystem>();
            var legacy = save.Capture();
            legacy.saveVersion = 1;
            legacy.buildings = null;
            save.Restore(legacy);
            Assert.That(System<BuildSystem>().Count, Is.Zero);
            var invalid = save.Capture();
            invalid.player.x = float.NaN;
            invalid.coins = 999;
            Assert.Throws<InvalidDataException>(() => save.Restore(invalid));
            Assert.That(wallet.Coins, Is.EqualTo(25));
            invalid = save.Capture();
            invalid.saveVersion = 2;
            invalid.gridCells.Add(new GridCellSaveData { x = 0, z = 0, state = (GridCellState)999 });
            Assert.Throws<InvalidDataException>(() => save.Restore(invalid));
            yield return null;
        }
    }
}
