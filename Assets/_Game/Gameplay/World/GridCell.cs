using System;

namespace MyLittleFarm.Gameplay.World
{
    /// <summary>Старые состояния фиксированной сетки, сохранённые только для миграции файлов версий 1–2.</summary>
    public enum GridCellState
    {
        Soil = 0,   // Исходная необработанная земля.
        Tilled = 1, // Земля готова к посадке.
        Blocked = 2 // Клетка запрещена для земледелия и строительства.
    }

    /// <summary>Одна изменённая клетка старого JSON-формата версий 1–2.</summary>
    [Serializable]
    public sealed class GridCellSaveData
    {
        // Поля сохраняют прежние имена, чтобы JsonUtility мог прочитать существующий файл.
        public int x;
        public int z;
        public GridCellState state;
    }
}
