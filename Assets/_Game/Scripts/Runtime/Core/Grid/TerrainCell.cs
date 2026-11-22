using System;

namespace MyLittleFarm.Core.Grid
{
    public enum PathSurface : byte { Dirt = 0, Stone = 1, Wood = 2 }
    [Flags]
    public enum CellPermissions : byte
    {
        Inherit = 0, Custom = 1, Walk = 2, Farm = 4, Build = 8, Orchard = 16
    }
    /// <summary>Описывает постоянную поверхность клетки независимо от грядок и построек.</summary>
    [Serializable]
    public struct TerrainCell
    {
        // Тип определяет базовую проходимость и разрешённые действия.
        public CellType type;
        // biome и moisture оставлены для будущих правил генерации и полива.
        public byte biome;
        public byte moisture;
        public CellPermissions permissions;
        // Road remains the stable gameplay type; this selects its authored surface.
        public PathSurface pathSurface;
        public bool Allows(CellPermissions permission, bool inherited) =>
            (permissions & CellPermissions.Custom) == 0 ? inherited : (permissions & permission) != 0;

        /// <summary>Создаёт поверхность с заданным исходным типом.</summary>
        public TerrainCell(CellType initialType)
        {
            type = initialType;
            biome = 0;
            moisture = 0;
            permissions = CellPermissions.Inherit;
            pathSurface = PathSurface.Dirt;
        }
    }
}
