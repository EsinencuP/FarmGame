using System;
using MyLittleFarm.Core.Grid;
using UnityEngine;

namespace MyLittleFarm.Tests.EditMode
{
    /// <summary>Проверяет арифметику координат, разделение слоёв и разреженные изменения чанка.</summary>
    public static class GridArchitectureContractChecks
    {
        /// <summary>Выполняет предельные и детерминированные сценарии на реальных классах данных.</summary>
        public static int Run()
        {
            // Счётчик позволяет отличить полный проход от преждевременного выхода.
            var assertions = 0;
            Action<bool, string> check = (condition, message) =>
            {
                assertions++;
                if (!condition) throw new InvalidOperationException(message);
            };
            check(GridMath.FloorDivide(-1, 32) == -1 && GridMath.LocalIndex(-1, 32) == 31,
                "Negative one belongs to previous chunk");
            check(GridMath.FloorDivide(-32, 32) == -1 && GridMath.LocalIndex(-32, 32) == 0,
                "Negative chunk boundary");
            check(GridMath.FloorDivide(-33, 32) == -2 && GridMath.LocalIndex(-33, 32) == 31,
                "Cell before negative boundary");
            check(GridMath.FloorDivide(int.MinValue, 32) == -67108864,
                "Minimum integer coordinate");

            var chunk = new ChunkData(new Vector2Int(-1, 0), 32, 32);
            check(!chunk.HasFarmingLayer && !chunk.HasBuildingLayer, "Optional layers start absent");
            check(!chunk.IsModified(0, 0) && chunk.ModifiedCellCount == 0,
                "Fresh base map is not a save delta");
            chunk.SetTerrain(0, 0, new TerrainCell(CellType.Dirt) { biome = 2, moisture = 10 });
            chunk.CommitBase();
            check(!chunk.IsModified(0, 0) && chunk.ModifiedCellCount == 0
                && chunk.GetBaseTerrain(0, 0).type == CellType.Dirt,
                "Painted terrain becomes immutable save base");
            chunk.SetFarming(0, 0, new FarmingCell { soilType = CellType.Tilled });
            check(chunk.HasFarmingLayer && !chunk.HasBuildingLayer && chunk.IsModified(0, 0)
                && chunk.ModifiedCellCount == 1,
                "Tilling allocates only farming layer");
            chunk.SetBuilding(0, 0, new BuildingCell { type = CellType.Building, buildingId = "shed" });
            check(chunk.GetCell(0, 0).type == CellType.Building && chunk.GetCell(0, 0).occupantId == "shed",
                "Building takes priority in compatibility view");
            chunk.SetBuilding(0, 0, default);
            check(chunk.GetCell(0, 0).type == CellType.Tilled, "Removing building reveals farm layer");
            chunk.SetFarming(0, 0, default);
            check(chunk.GetCell(0, 0).type == CellType.Dirt && !chunk.IsModified(0, 0)
                && chunk.ModifiedCellCount == 0,
                "Removing farm layer preserves base dirt");
            chunk.SetLegacyType(1, 0, CellType.Planted);
            chunk.SetOccupant(1, 0, "crop:1:0");
            check(chunk.GetCell(1, 0).occupantId == "crop:1:0" && chunk.IsModified(1, 0),
                "Legacy save projection writes farming layer");
            chunk.SetLegacyType(1, 0, CellType.Tilled);
            check(chunk.GetCell(1, 0).occupantId == null, "Harvest clears crop occupant");

            // Широкий набор координат проверяет обратимость на переходах границ чанка.
            for (var coordinate = -4096; coordinate <= 4096; coordinate++)
            {
                var owner = GridMath.FloorDivide(coordinate, 32);
                var local = GridMath.LocalIndex(coordinate, 32);
                check(local >= 0 && local < 32 && owner * 32 + local == coordinate,
                    "Coordinate round trip");
            }
            return assertions;
        }
    }
}
