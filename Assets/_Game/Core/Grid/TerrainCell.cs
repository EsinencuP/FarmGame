using System;

namespace MyLittleFarm.Core.Grid
{
    /// <summary>Описывает постоянную поверхность клетки независимо от грядок и построек.</summary>
    [Serializable]
    public struct TerrainCell
    {
        // Тип определяет базовую проходимость и разрешённые действия.
        public CellType type;
        // biome и moisture оставлены для будущих правил генерации и полива.
        public byte biome;
        public byte moisture;

        /// <summary>Создаёт поверхность с заданным исходным типом.</summary>
        public TerrainCell(CellType initialType)
        {
            type = initialType;
            biome = 0;
            moisture = 0;
        }
    }
}
