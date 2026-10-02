using System.Collections;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MyLittleFarm.Tests.PlayMode
{
    /// <summary>Проверяет связь базовой карты, раздельных слоёв и delta-сохранения в игровой сцене.</summary>
    public sealed class GridArchitectureTests : FarmPlayModeFixture
    {
        /// <summary>Нетронутая сцена не записывает клетки закрытого сектора в сохранение.</summary>
        [UnityTest]
        public IEnumerator BaseMapIsNotDuplicatedInSave()
        {
            yield return null;
            var grid = Object.FindFirstObjectByType<GridSystem>();
            Assert.That(grid.GetBaseTerrainCell(new Vector2Int(10, 0)).type, Is.EqualTo(CellType.Locked));
            Assert.That(grid.Serialize().chunks, Is.Empty);
        }

        /// <summary>Снятие игровых слоёв возвращает исходную поверхность и сохраняет только изменённую клетку.</summary>
        [UnityTest]
        public IEnumerator FarmingAndBuildingLayersPreserveTerrain()
        {
            yield return null;
            var grid = Object.FindFirstObjectByType<GridSystem>();
            var coordinate = new Vector2Int(1, 1);
            grid.SetTerrainCell(coordinate, new TerrainCell(CellType.Dirt));
            grid.SetFarmingCell(coordinate, new FarmingCell { soilType = CellType.Tilled });
            grid.SetBuildingCell(coordinate, new BuildingCell
            {
                type = CellType.Building,
                buildingId = "test-building"
            });
            Assert.That(grid.GetCellType(coordinate), Is.EqualTo(CellType.Building));
            grid.ClearBuildingCell(coordinate);
            Assert.That(grid.GetCellType(coordinate), Is.EqualTo(CellType.Tilled));
            grid.ClearFarmingCell(coordinate);
            Assert.That(grid.GetCellType(coordinate), Is.EqualTo(CellType.Dirt));
            var delta = grid.Serialize();
            Assert.That(delta.chunks, Has.Count.EqualTo(1));
            Assert.That(delta.chunks[0].modifiedCells, Has.Count.EqualTo(1));
            grid.Deserialize(delta);
            Assert.That(grid.GetCellType(coordinate), Is.EqualTo(CellType.Dirt));
            Assert.That(grid.GetBaseTerrainCell(coordinate).type, Is.EqualTo(CellType.Grass));
        }

        /// <summary>Формат версии 4 продолжает загружаться после перехода на базовую карту и delta.</summary>
        [UnityTest]
        public IEnumerator LegacyVersionFourSaveMigrates()
        {
            yield return null;
            var grid = Object.FindFirstObjectByType<GridSystem>();
            var save = Object.FindFirstObjectByType<SaveSystem>();
            var coordinate = new Vector2Int(2, 2);
            grid.SetCellType(coordinate, CellType.Tilled);
            var snapshot = save.Capture();
            snapshot.saveVersion = 4;
            snapshot.worldBaseHash = 0;
            foreach (var chunk in snapshot.grid.chunks)
                foreach (var cell in chunk.modifiedCells) cell.hasTerrainData = false;
            save.Restore(snapshot);
            Assert.That(grid.GetCellType(coordinate), Is.EqualTo(CellType.Tilled));
        }
    }
}
