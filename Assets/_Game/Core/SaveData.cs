using System;
using System.Collections.Generic;
using MyLittleFarm.Gameplay.Farming;
using MyLittleFarm.Gameplay.World;
using MyLittleFarm.Gameplay.Building;

namespace MyLittleFarm.Core
{
    [Serializable]
    public sealed class SaveData
    {
        public int saveVersion = 1;
        public long savedAtUnixMs;
        public PlayerPositionData player = new PlayerPositionData();
        public int coins;
        public List<InventoryEntryData> inventory = new List<InventoryEntryData>();
        public List<GridCellSaveData> gridCells = new List<GridCellSaveData>();
        public List<CropRuntimeState> crops = new List<CropRuntimeState>();
        public List<BuildingRuntimeState> buildings = new List<BuildingRuntimeState>();
    }

    [Serializable]
    public sealed class PlayerPositionData
    {
        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    public sealed class InventoryEntryData
    {
        public string itemId;
        public int amount;
    }
}
