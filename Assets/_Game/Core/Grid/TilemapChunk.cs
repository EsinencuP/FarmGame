using System;
using UnityEngine;

namespace MyLittleFarm.Core.Grid
{
    /// <summary>
    /// Отмечает один визуальный tilemap-блок сцены как загруженный чанк GridSystem.
    /// Позиция Transform считается левым нижним углом блока на плоскости XZ.
    /// </summary>
    [ExecuteAlways]
    public sealed class TilemapChunk : MonoBehaviour
    {
        [Header("Chunk Settings")]
        [SerializeField] private CellType defaultCellType = CellType.Grass;
        [SerializeField] private bool isLockedSector;
        [Header("Override Zones")]
        [SerializeField] private ZoneOverride[] zoneOverrides = Array.Empty<ZoneOverride>();

        // Последняя координата выводится наружу для инспекции и тестов сцены.
        private Vector2Int _registeredChunkCoord;
        public Vector2Int RegisteredChunkCoord => _registeredChunkCoord;

        /// <summary>Описывает прямоугольную локальную зону с типом, отличным от типа всего чанка.</summary>
        [Serializable]
        public sealed class ZoneOverride
        {
            // localOffset задаёт нижнюю левую клетку зоны внутри чанка.
            public Vector2Int localOffset;
            // size задаёт ширину X и глубину Z в клетках.
            public Vector2Int size = Vector2Int.one;
            // overrideType заменяет исходный тип в пределах зоны.
            public CellType overrideType = CellType.Water;
        }

        private void Start()
        {
            // Bootstrap обычно регистрирует все чанки раньше загрузки save; этот вызов страхует отдельные сцены.
            RegisterInGrid();
        }

        /// <summary>Регистрирует компонент в текущем GridSystem, если сетка уже доступна.</summary>
        public void RegisterInGrid()
        {
            if (GridSystem.Instance == null)
            {
                if (Application.isPlaying)
                    Debug.LogWarning($"TilemapChunk '{name}' cannot register because GridSystem is missing.", this);
                return;
            }
            GridSystem.Instance.RegisterSceneChunk(this);
        }

        /// <summary>Применяет исходный тип и локальные переопределения к переданной сетке.</summary>
        public void ApplyTo(GridSystem grid)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            var gridOrigin = grid.WorldToGrid(transform.position);
            _registeredChunkCoord = grid.GridToChunk(gridOrigin);
            grid.InitializeChunk(_registeredChunkCoord, isLockedSector ? CellType.Locked : defaultCellType);

            if (zoneOverrides == null) return;
            foreach (var zone in zoneOverrides)
            {
                if (zone == null || zone.size.x <= 0 || zone.size.y <= 0) continue;
                for (var x = 0; x < zone.size.x; x++)
                for (var z = 0; z < zone.size.y; z++)
                {
                    var local = zone.localOffset + new Vector2Int(x, z);
                    if (local.x < 0 || local.x >= grid.ChunkSizeX || local.y < 0 || local.y >= grid.ChunkSizeZ)
                        continue;
                    grid.SetCellType(grid.ChunkLocalToGrid(_registeredChunkCoord, local), zone.overrideType);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Контур помогает совместить pivot ассета с границами чанка прямо в Scene View.
            var grid = GridSystem.Instance;
            if (grid == null) return;
            var width = grid.ChunkSizeX * grid.CellSize;
            var depth = grid.ChunkSizeZ * grid.CellSize;
            Gizmos.color = isLockedSector ? Color.red : Color.green;
            Gizmos.DrawWireCube(
                transform.position + new Vector3(width * 0.5f, 0.1f, depth * 0.5f),
                new Vector3(width, 0.1f, depth));
        }
    }
}
