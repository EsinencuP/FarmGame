using System;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Farming
{
    /// <summary>Сериализуемые данные одного посева, не зависящие от его визуального объекта.</summary>
    [Serializable]
    public sealed class CropRuntimeState
    {
        // x/z определяют клетку, cropId — вид культуры, plantedAtUnixMs — реальное UTC-время посадки.
        public int x;
        public int z;
        public string cropId;
        public long plantedAtUnixMs;
        // Длительность и число стадий позволяют рассчитать прогресс и внешний вид после загрузки.
        public float growDurationSeconds;
        public float weatherGrowthBonusSeconds;
        public int stageCount;
        public bool isRegrowing;
        public bool manuallyWatered;
        public float rainySeconds;
        public bool sizeReady;
        public HarvestSize harvestSize;

        /// <summary>Возвращает клеточную позицию в формате, принятом GridSystem.</summary>
        public Vector2Int Position => new Vector2Int(x, z);

        /// <summary>Проверяет достижение полного прогресса роста.</summary>
        public bool IsMature(long nowUnixMs)
        {
            return GetGrowthRatio(nowUnixMs) >= 1f;
        }

        public int GetStage(long nowUnixMs) => StageFor(GetGrowthRatio(nowUnixMs), stageCount, isRegrowing);

        internal static int StageFor(float growth, int stageCount, bool isRegrowing)
        {
            // Последняя визуальная стадия зарезервирована для полностью созревшего растения.
            var finalStage = Mathf.Max(0, stageCount - 1);
            if (growth >= 1f || finalStage == 0) return finalStage;
            if (isRegrowing) return Mathf.Min(1, finalStage - 1);
            return Mathf.Min(Mathf.FloorToInt(growth * finalStage), finalStage - 1);
        }

        public float GetGrowthRatio(long nowUnixMs) =>
            GrowthRatioAt(nowUnixMs, plantedAtUnixMs, growDurationSeconds, weatherGrowthBonusSeconds);

        internal static float GrowthRatioAt(long nowUnixMs, long plantedAtUnixMs, float duration, float weatherBonus)
        {
            // UTC-время позволяет росту продолжаться между игровыми сессиями.
            if (duration <= 0f) return 1f;
            var elapsedSeconds = ((double)nowUnixMs - plantedAtUnixMs) / 1000.0;
            return Mathf.Clamp01((float)((elapsedSeconds + weatherBonus) / duration));
        }
    }
}
