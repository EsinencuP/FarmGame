using System;
using System.Collections.Generic;
using UnityEngine;

namespace MyLittleFarm.Core.Grid
{
    /// <summary>Хранит нарисованную базовую карту; сохранение игрока содержит только отличия от неё.</summary>
    [CreateAssetMenu(fileName = "FarmWorld", menuName = "My Little Farm/World Map")]
    public sealed class WorldMapAsset : ScriptableObject
    {
        [SerializeField] private List<WorldMapCell> cells = new List<WorldMapCell>();
        [SerializeField] private List<WorldMapVertex> vertices = new List<WorldMapVertex>();
        private Dictionary<Vector2Int, int> _vertexIndices;
        // Индекс строится лениво, чтобы Unity продолжал сериализовать обычный список.
        private Dictionary<Vector2Int, int> _indices;
        public int CellCount => cells.Count;
        public bool HasRelief => vertices.Count > 0;

        public float VertexHeight(Vector2Int coordinate)
        {
            EnsureVertexIndex();
            return _vertexIndices.TryGetValue(coordinate, out var index) ? vertices[index].height : 0f;
        }

        /// <summary>Stores an authored world-space height at a grid corner, shared across chunk seams.</summary>
        public void SetVertexHeight(Vector2Int coordinate, float height)
        {
            if (float.IsNaN(height) || float.IsInfinity(height)) throw new ArgumentOutOfRangeException(nameof(height));
            EnsureVertexIndex();
            if (_vertexIndices.TryGetValue(coordinate, out var index)) vertices[index].height = height;
            else { _vertexIndices.Add(coordinate, vertices.Count); vertices.Add(new WorldMapVertex { x=coordinate.x, z=coordinate.y, height=height }); }
        }

        private void EnsureVertexIndex()
        {
            if (_vertexIndices != null) return;
            _vertexIndices = new Dictionary<Vector2Int, int>(vertices.Count);
            for (var i=0; i<vertices.Count; i++) _vertexIndices[new Vector2Int(vertices[i].x, vertices[i].z)] = i;
        }
        public bool TryGetTerrain(Vector2Int coordinate, out TerrainCell terrain)
        {
            EnsureIndex();
            if (_indices.TryGetValue(coordinate, out var index)) { terrain = cells[index].terrain; return true; }
            terrain = default;
            return false;
        }

        /// <summary>Сохраняет изменённую в Scene View клетку в базовой карте.</summary>
        public void SetTerrain(Vector2Int coordinate, TerrainCell terrain)
        {
            if (terrain.type == CellType.Tilled || terrain.type == CellType.Planted
                || terrain.type == CellType.Watered || terrain.type == CellType.Building
                || terrain.type == CellType.BuildingEdge || terrain.type == CellType.OutOfBounds
                || terrain.type == CellType.Tree)
                throw new ArgumentOutOfRangeException(nameof(terrain));
            EnsureIndex();
            if (_indices.TryGetValue(coordinate, out var index))
                cells[index].terrain = terrain;
            else
            {
                _indices.Add(coordinate, cells.Count);
                cells.Add(new WorldMapCell { x = coordinate.x, z = coordinate.y, terrain = terrain });
            }
        }

        /// <summary>Накладывает только клетки указанного чанка на исходный тип сценового блока.</summary>
        public void ApplyTo(GridSystem grid, Vector2Int chunkCoordinate)
        {
            foreach (var cell in cells)
            {
                var coordinate = new Vector2Int(cell.x, cell.z);
                if (grid.GridToChunk(coordinate) == chunkCoordinate)
                    grid.SetTerrainCell(coordinate, cell.terrain);
            }
        }

        /// <summary>После загрузки Unity строит индекс заново при первом изменении.</summary>
        private void OnEnable() { _indices = null; _vertexIndices = null; }
        /// <summary>Ручные правки списка в Inspector также сбрасывают индекс.</summary>
        private void OnValidate() { _indices = null; _vertexIndices = null; }

        private void EnsureIndex()
        {
            // Пересоздаёт индекс после загрузки ассета или редактирования списка в Inspector.
            if (_indices != null) return;
            _indices = new Dictionary<Vector2Int, int>(cells.Count);
            for (var index = 0; index < cells.Count; index++)
                _indices[new Vector2Int(cells[index].x, cells[index].z)] = index;
        }
    }

    [Serializable]
    public sealed class WorldMapVertex
    {
        public int x;
        public int z;
        public float height;
    }

    /// <summary>Одна клетка базовой карты с целочисленными мировыми координатами.</summary>
    [Serializable]
    public sealed class WorldMapCell
    {
        public int x;
        public int z;
        public TerrainCell terrain;
    }
}
