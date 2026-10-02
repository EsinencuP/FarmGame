using UnityEngine;

namespace MyLittleFarm.Core.Grid
{
    /// <summary>Даёт уполномоченным системам явные операции записи отдельных слоёв.</summary>
    public interface IWorldGridWriter : IWorldGridReader
    {
        void SetTerrainCell(Vector2Int coordinate, TerrainCell terrain);
        void SetFarmingCell(Vector2Int coordinate, FarmingCell farming);
        void SetBuildingCell(Vector2Int coordinate, BuildingCell building);
        void ClearFarmingCell(Vector2Int coordinate);
        void ClearBuildingCell(Vector2Int coordinate);
    }
}
