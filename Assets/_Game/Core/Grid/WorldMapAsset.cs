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
        // Индекс строится лениво, чтобы Unity продолжал сериализовать обычный список.
        private Dictionary<Vector2Int, int> _indices;
        public int CellCount => cells.Count;

        /// <summary>Сохраняет изменённую в Scene View клетку в базовой карте.</summary>
        public void SetTerrain(Vector2Int coordinate, TerrainCell terrain)
        {
            if (terrain.type == CellType.Tilled || terrain.type == CellType.Planted
                || terrain.type == CellType.Watered || terrain.type == CellType.Building
                || terrain.type == CellType.BuildingEdge || terrain.type == CellType.OutOfBounds)
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
        private void OnEnable() => _indices = null;
        /// <summary>Ручные правки списка в Inspector также сбрасывают индекс.</summary>
        private void OnValidate() => _indices = null;

        private void EnsureIndex()
        {
            // Пересоздаёт индекс после загрузки ассета или редактирования списка в Inspector.
            if (_indices != null) return;
            _indices = new Dictionary<Vector2Int, int>(cells.Count);
            for (var index = 0; index < cells.Count; index++)
                _indices[new Vector2Int(cells[index].x, cells[index].z)] = index;
        }
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
