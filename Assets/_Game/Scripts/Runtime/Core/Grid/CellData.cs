using System;

namespace MyLittleFarm.Core.Grid
{
    /// <summary>
    /// Совместимый снимок клетки для существующих правил и старых сохранений. Источник данных —
    /// отдельные массивы TerrainCell, FarmingCell и BuildingCell внутри ChunkData.
    /// </summary>
    [Serializable]
    public struct CellData
    {
        // type определяет правила движения и доступные действия на клетке.
        public CellType type;
        // occupantId содержит стабильный ID культуры или постройки; null означает отсутствие владельца.
        public string occupantId;

        /// <summary>Создаёт свободную клетку указанного типа.</summary>
        public CellData(CellType initialType)
        {
            type = initialType;
            occupantId = null;
        }
    }
}
