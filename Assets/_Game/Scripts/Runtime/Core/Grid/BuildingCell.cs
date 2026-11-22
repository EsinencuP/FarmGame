using System;

namespace MyLittleFarm.Core.Grid
{
    /// <summary>Отмечает занятый объектом footprint, не стирая тип поверхности под ним.</summary>
    [Serializable]
    public struct BuildingCell
    {
        // type равен Building/BuildingEdge для здания или Tree для дерева.
        public CellType type;
        // buildingId содержит стабильный ID здания или дерева.
        public string buildingId;
    }
}
