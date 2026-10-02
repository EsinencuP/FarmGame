using System;

namespace MyLittleFarm.Core.Grid
{
    /// <summary>Хранит состояние грядки и посева отдельно от исходной поверхности.</summary>
    [Serializable]
    public struct FarmingCell
    {
        // soilType равен Tilled, Watered или Planted; Grass означает отсутствие грядки.
        public CellType soilType;
        // occupantId связывает посев с сохранением; cropId хранит стабильный вид культуры.
        public string occupantId;
        public string cropId;
        // Время и параметры роста сохраняют прогресс между игровыми сессиями.
        public long plantedAtUnixMs;
        public float growDurationSeconds;
        public int stageCount;
        // Уровни нужны следующим механикам и не меняют существующий цикл роста.
        public byte moisture;
        public byte fertilizer;
        // День последнего полива и flags зарезервированы для дискретной симуляции следующих этапов.
        public ushort lastWateredDay;
        public byte flags;
    }
}
