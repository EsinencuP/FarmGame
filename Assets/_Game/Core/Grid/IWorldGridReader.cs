using System.Collections.Generic;
using UnityEngine;

namespace MyLittleFarm.Core.Grid
{
    /// <summary>Открывает игровым системам чтение слоёв без доступа к их внутренним массивам.</summary>
    public interface IWorldGridReader
    {
        TerrainCell GetTerrainCell(Vector2Int coordinate);
        FarmingCell GetFarmingCell(Vector2Int coordinate);
        BuildingCell GetBuildingCell(Vector2Int coordinate);
        CellType GetCellType(Vector2Int coordinate);
        bool CanTill(Vector2Int coordinate);
        bool CanPlant(Vector2Int coordinate);
        bool CanPlantTree(Vector2Int coordinate);
        bool CanBuild(Vector2Int coordinate);
        bool IsWalkable(Vector2Int coordinate);
        Vector2Int WorldToGrid(Vector3 worldPosition);
        Vector3 GridToWorld(Vector2Int coordinate);
        IEnumerable<Vector2Int> GetPlantedCells();
    }
}
