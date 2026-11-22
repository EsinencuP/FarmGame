using System.Collections.Generic;
using UnityEngine;

namespace MyLittleFarm.Core.Grid
{
    /// <summary>Изолирует хранение чанков для будущей загрузки областей мира по требованию.</summary>
    public interface IChunkProvider
    {
        ChunkData GetChunk(Vector2Int chunkCoordinate);
        ChunkData GetOrCreateChunk(Vector2Int chunkCoordinate);
        IEnumerable<Vector2Int> GetLoadedChunkCoords();
    }
}
