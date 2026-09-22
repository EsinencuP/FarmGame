using System;
using System.Collections.Generic;
using MyLittleFarm.Gameplay.Farming;
using MyLittleFarm.Gameplay.World;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.Core.Grid;

namespace MyLittleFarm.Core
{
    /// <summary>Корневой сериализуемый снимок всего изменяемого состояния игры.</summary>
    [Serializable]
    public sealed class SaveData
    {
        // Версия определяет, какую схему данных умеет прочитать SaveSystem.
        public int saveVersion = 1;
        // UTC-время записи используется для диагностики и проверки корректности файла.
        public long savedAtUnixMs;
        // Отдельные блоки сохраняют позицию, экономику, клетки, посевы и постройки.
        public PlayerPositionData player = new PlayerPositionData();
        public int coins;
        public List<InventoryEntryData> inventory = new List<InventoryEntryData>();
        public GridSaveData grid = new GridSaveData();
        // gridCells читается только при миграции файлов версий 1–2 со старой фиксированной сетки.
        public List<GridCellSaveData> gridCells = new List<GridCellSaveData>();
        public List<CropRuntimeState> crops = new List<CropRuntimeState>();
        public List<BuildingRuntimeState> buildings = new List<BuildingRuntimeState>();
    }

    /// <summary>Положение игрока в мировых координатах Unity.</summary>
    [Serializable]
    public sealed class PlayerPositionData
    {
        // Компоненты Vector3 записаны полями, понятными JsonUtility.
        public float x;
        public float y;
        public float z;
    }

    /// <summary>Одна строка инвентаря: стабильный идентификатор предмета и его количество.</summary>
    [Serializable]
    public sealed class InventoryEntryData
    {
        public string itemId;
        public int amount;
    }
}
