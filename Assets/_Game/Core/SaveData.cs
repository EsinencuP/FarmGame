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
        // Real play time excludes the slot menu and time spent outside the application.
        public double playTimeSeconds;
        // Small PNG travels atomically with the exact state it previews; absent in older saves.
        public string previewPngBase64;
        public FarmProgressData progression;
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
        // Schema 1 has five goals; schema 0 is the legacy six-action tutorial.
        public int onboardingSchema;
        public bool onboardingSuppliesGranted;
        public bool onboardingGardenPlanted;
        public bool onboardingSaleCompleted;
        public int onboardingGardenX;
        public int onboardingGardenZ;
        // Optional lighting clock; older saves keep the authored starting hour.
        public bool hasWorldTime;
        public float worldHour;
        // Optional in older saves. The weather clock pauses while the game is closed.
        public WeatherSaveData weather;
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
