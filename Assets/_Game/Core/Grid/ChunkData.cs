using System;
using System.Collections.Generic;
using UnityEngine;

namespace MyLittleFarm.Core.Grid
{
    /// <summary>Хранит поверхность, грядки и постройки одного чанка в независимых плоских массивах.</summary>
    public sealed class ChunkData
    {
        // Координата и размеры определяют стабильное преобразование локальной клетки в индекс.
        public readonly Vector2Int chunkCoord;
        public readonly int chunkSizeX;
        public readonly int chunkSizeZ;
        // Поверхность существует всегда; редко используемые слои создаются по первому изменению.
        private readonly TerrainCell[] _terrain;
        private readonly TerrainCell[] _baseTerrain;
        private FarmingCell[] _farming;
        private BuildingCell[] _buildings;
        // Набор содержит только индексы, отличающиеся от базовой карты.
        private readonly HashSet<int> _modifiedIndices = new HashSet<int>();
        // Version сообщает представлению об изменении, SaveDirty — о необходимости нового снимка.
        public int Version { get; private set; }
        public bool SaveDirty { get; private set; }
        public bool HasFarmingLayer => _farming != null;
        public bool HasBuildingLayer => _buildings != null;
        public int ModifiedCellCount => _modifiedIndices.Count;
        public IEnumerable<int> ModifiedIndices => _modifiedIndices;

        /// <summary>Выделяет память только для обязательного слоя поверхности.</summary>
        public ChunkData(Vector2Int coord, int sizeX, int sizeZ, CellType initialType = CellType.Grass)
        {
            if (sizeX < 1 || sizeZ < 1) throw new ArgumentOutOfRangeException(nameof(sizeX));
            chunkCoord = coord;
            chunkSizeX = sizeX;
            chunkSizeZ = sizeZ;
            _terrain = new TerrainCell[checked(sizeX * sizeZ)];
            _baseTerrain = new TerrainCell[_terrain.Length];
            Fill(initialType);
            CommitBase();
        }

        /// <summary>Проверяет локальную координату без обращения к массиву.</summary>
        public bool IsInBounds(int localX, int localZ) =>
            localX >= 0 && localX < chunkSizeX && localZ >= 0 && localZ < chunkSizeZ;

        /// <summary>Возвращает отдельную постоянную поверхность клетки.</summary>
        public TerrainCell GetTerrain(int localX, int localZ) => _terrain[Index(localX, localZ)];

        /// <summary>Возвращает исходную поверхность из сцены для проверки сохранённого delta.</summary>
        public TerrainCell GetBaseTerrain(int localX, int localZ) => _baseTerrain[Index(localX, localZ)];

        /// <summary>Возвращает грядку или пустое значение без создания всего слоя.</summary>
        public FarmingCell GetFarming(int localX, int localZ)
        {
            var index = Index(localX, localZ);
            return _farming == null ? default : _farming[index];
        }

        /// <summary>Возвращает часть постройки или пустое значение без создания всего слоя.</summary>
        public BuildingCell GetBuilding(int localX, int localZ)
        {
            var index = Index(localX, localZ);
            return _buildings == null ? default : _buildings[index];
        }

        /// <summary>Собирает совместимый вид клетки для существующих игровых правил и сохранений.</summary>
        public CellData GetCell(int localX, int localZ)
        {
            var index = Index(localX, localZ);
            if (_buildings != null && _buildings[index].type != 0)
                return new CellData(_buildings[index].type) { occupantId = _buildings[index].buildingId };
            if (_farming != null && _farming[index].soilType != 0)
                return new CellData(_farming[index].soilType) { occupantId = _farming[index].occupantId };
            return new CellData(_terrain[index].type);
        }

        /// <summary>Меняет только поверхность; активные грядки и постройки остаются самостоятельными слоями.</summary>
        public void SetTerrain(int localX, int localZ, TerrainCell terrain)
        {
            ValidateTerrain(terrain.type);
            var index = Index(localX, localZ);
            if (_terrain[index].type == terrain.type && _terrain[index].biome == terrain.biome
                && _terrain[index].moisture == terrain.moisture) return;
            _terrain[index] = terrain;
            MarkChanged(index);
        }

        /// <summary>Меняет состояние грядки, выделяя массив только при реальном действии.</summary>
        public void SetFarming(int localX, int localZ, FarmingCell farming)
        {
            if (farming.soilType != 0 && farming.soilType != CellType.Tilled
                && farming.soilType != CellType.Watered && farming.soilType != CellType.Planted)
                throw new ArgumentOutOfRangeException(nameof(farming));
            if (farming.soilType == 0) farming = default;
            var index = Index(localX, localZ);
            if (_farming == null)
            {
                if (farming.soilType == 0) return;
                _farming = new FarmingCell[_terrain.Length];
            }
            _farming[index] = farming;
            MarkChanged(index);
        }

        /// <summary>Меняет занятость постройкой, не меняя исходную поверхность.</summary>
        public void SetBuilding(int localX, int localZ, BuildingCell building)
        {
            if (building.type != 0 && building.type != CellType.Building && building.type != CellType.BuildingEdge
                && building.type != CellType.Tree)
                throw new ArgumentOutOfRangeException(nameof(building));
            if (building.type == 0) building = default;
            var index = Index(localX, localZ);
            if (_buildings == null)
            {
                if (building.type == 0) return;
                _buildings = new BuildingCell[_terrain.Length];
            }
            _buildings[index] = building;
            MarkChanged(index);
        }

        /// <summary>Поддерживает старый API, направляя тип в соответствующий слой.</summary>
        public void SetLegacyType(int localX, int localZ, CellType type)
        {
            if (type == CellType.Tilled || type == CellType.Watered || type == CellType.Planted)
            {
                var farming = GetFarming(localX, localZ);
                farming.soilType = type;
                if (type != CellType.Planted) { farming.occupantId = null; farming.cropId = null; }
                SetFarming(localX, localZ, farming);
            }
            else if (type == CellType.Building || type == CellType.BuildingEdge || type == CellType.Tree)
                SetBuilding(localX, localZ, new BuildingCell { type = type });
            else
            {
                ValidateTerrain(type);
                SetFarming(localX, localZ, default);
                SetBuilding(localX, localZ, default);
                if (GetTerrain(localX, localZ).type != type)
                    SetTerrain(localX, localZ, new TerrainCell(type));
            }
        }

        /// <summary>Записывает ID владельца в активный слой совместимого представления.</summary>
        public void SetOccupant(int localX, int localZ, string occupantId)
        {
            var cell = GetCell(localX, localZ);
            if (cell.type == CellType.Building || cell.type == CellType.BuildingEdge || cell.type == CellType.Tree)
            {
                var building = GetBuilding(localX, localZ);
                building.buildingId = occupantId;
                SetBuilding(localX, localZ, building);
            }
            else if (cell.type == CellType.Planted)
            {
                var farming = GetFarming(localX, localZ);
                farming.occupantId = occupantId;
                SetFarming(localX, localZ, farming);
            }
            else if (!string.IsNullOrEmpty(occupantId))
                throw new InvalidOperationException("Only planted or building cells can have an occupant.");
        }

        /// <summary>Сбрасывает слои перед регистрацией карты сцены.</summary>
        public void Fill(CellType type)
        {
            ValidateTerrain(type);
            for (var index = 0; index < _terrain.Length; index++) _terrain[index] = new TerrainCell(type);
            _farming = null;
            _buildings = null;
            Version++;
            SaveDirty = true;
            RebuildModifiedIndex();
        }

        /// <summary>Фиксирует нарисованную в сцене поверхность как базу для разреженного сохранения.</summary>
        public void CommitBase()
        {
            Array.Copy(_terrain, _baseTerrain, _terrain.Length);
            SaveDirty = false;
            RebuildModifiedIndex();
        }

        /// <summary>Проверяет, отличается ли клетка от базовой карты или содержит игровой слой.</summary>
        public bool IsModified(int localX, int localZ)
        {
            var index = Index(localX, localZ);
            var current = _terrain[index];
            var baseline = _baseTerrain[index];
            return current.type != baseline.type || current.biome != baseline.biome
                || current.moisture != baseline.moisture
                || (_farming != null && _farming[index].soilType != 0)
                || (_buildings != null && _buildings[index].type != 0);
        }

        /// <summary>Возвращает плоский индекс и запрещает выход за границы чанка.</summary>
        private int Index(int localX, int localZ)
        {
            if (!IsInBounds(localX, localZ)) throw new ArgumentOutOfRangeException(nameof(localX));
            return localZ * chunkSizeX + localX;
        }

        /// <summary>Обновляет индекс delta только для одной затронутой клетки.</summary>
        private void MarkChanged(int index)
        {
            // Смена версии позволяет перестроить только затронутый меш и повторно сохранить его delta.
            Version++;
            SaveDirty = true;
            if (IsModified(index % chunkSizeX, index / chunkSizeX)) _modifiedIndices.Add(index);
            else _modifiedIndices.Remove(index);
        }

        /// <summary>Пересчитывает delta после замены всей базовой поверхности чанка.</summary>
        private void RebuildModifiedIndex()
        {
            _modifiedIndices.Clear();
            for (var index = 0; index < _terrain.Length; index++)
                if (IsModified(index % chunkSizeX, index / chunkSizeX)) _modifiedIndices.Add(index);
        }

        /// <summary>Не допускает игровых overlay-типов в постоянный слой поверхности.</summary>
        private static void ValidateTerrain(CellType type)
        {
            if (type == CellType.Tilled || type == CellType.Planted || type == CellType.Watered
                || type == CellType.Building || type == CellType.BuildingEdge || type == CellType.Tree
                || type == CellType.OutOfBounds)
                throw new ArgumentOutOfRangeException(nameof(type));
        }
    }
}
