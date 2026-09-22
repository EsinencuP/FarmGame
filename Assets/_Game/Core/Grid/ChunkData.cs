using UnityEngine;

namespace MyLittleFarm.Core.Grid
{
    /// <summary>
    /// Хранит прямоугольный блок клеток, соответствующий одному tilemap-блоку сцены.
    /// Локальные координаты всегда лежат от нуля до размера чанка минус один.
    /// </summary>
    public sealed class ChunkData
    {
        // chunkCoord — адрес блока в пространстве чанков, включая отрицательные координаты.
        public readonly Vector2Int chunkCoord;
        // Размеры нужны обходам сериализации и отладочной визуализации.
        public readonly int chunkSizeX;
        public readonly int chunkSizeZ;
        // Двумерный массив компактен внутри уже загруженного чанка.
        private readonly CellData[,] _cells;

        /// <summary>Создаёт чанк и заполняет все его клетки исходным типом.</summary>
        public ChunkData(Vector2Int coord, int sizeX, int sizeZ, CellType initialType = CellType.Grass)
        {
            chunkCoord = coord;
            chunkSizeX = sizeX;
            chunkSizeZ = sizeZ;
            _cells = new CellData[sizeX, sizeZ];
            Fill(initialType);
        }

        /// <summary>Возвращает ссылку на клетку, чтобы вызывающий код менял struct без копирования.</summary>
        public ref CellData GetCell(int localX, int localZ) => ref _cells[localX, localZ];

        /// <summary>Проверяет, принадлежит ли локальная координата этому чанку.</summary>
        public bool IsInBounds(int localX, int localZ)
        {
            return localX >= 0 && localX < chunkSizeX && localZ >= 0 && localZ < chunkSizeZ;
        }

        /// <summary>Сбрасывает все клетки в один тип и очищает владельцев.</summary>
        public void Fill(CellType type)
        {
            for (var x = 0; x < chunkSizeX; x++)
            for (var z = 0; z < chunkSizeZ; z++)
                _cells[x, z] = new CellData(type);
        }
    }
}
