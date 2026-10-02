using System;
using System.Collections.Generic;
using MyLittleFarm.Gameplay.Farming;
using MyLittleFarm.Gameplay.Animals;
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
        // Версия 5 связывает delta с конкретной базовой картой сцены.
        public long worldBaseHash;
        // gridCells читается только при миграции файлов версий 1–2 со старой фиксированной сетки.
        public List<GridCellSaveData> gridCells = new List<GridCellSaveData>();
        public List<CropRuntimeState> crops = new List<CropRuntimeState>();
        // Этап 2 хранит яблони отдельно от культур и построек.
        public List<TreeRuntimeState> trees = new List<TreeRuntimeState>();
        // Животные сохраняются отдельным списком, потому что они двигаются и не занимают terrain-клетку.
        public List<AnimalRuntimeState> animals = new List<AnimalRuntimeState>();
        public List<BuildingRuntimeState> buildings = new List<BuildingRuntimeState>();
        // Версия 4 сохраняет прогресс; версия 5 сравнивает клетки с базовой картой сцены.
        public int selectedQuickSlot;
        public bool sectorUnlocked;
        public int toolUpgradeLevel;
        // Номер шага короткого онбординга сохраняется вместе с остальным прогрессом.
        public int onboardingStep;
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
