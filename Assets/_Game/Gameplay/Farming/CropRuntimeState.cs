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
        public int stageCount;

        /// <summary>Возвращает клеточную позицию в формате, принятом GridSystem.</summary>
        public Vector2Int Position => new Vector2Int(x, z);

        /// <summary>Проверяет достижение полного прогресса роста.</summary>
        public bool IsMature(long nowUnixMs)
        {
            return GetGrowthRatio(nowUnixMs) >= 1f;
        }

        public int GetStage(long nowUnixMs)
        {
            // Последняя визуальная стадия зарезервирована для полностью созревшего растения.
            var safeStageCount = Mathf.Max(1, stageCount);
            if (IsMature(nowUnixMs))
            {
                return safeStageCount - 1;
            }

            return Mathf.Clamp(
                Mathf.FloorToInt(GetGrowthRatio(nowUnixMs) * Mathf.Max(1, safeStageCount - 1)),
                0,
                safeStageCount - 1);
        }

        public float GetGrowthRatio(long nowUnixMs)
        {
            // UTC-время позволяет росту продолжаться между игровыми сессиями.
            if (growDurationSeconds <= 0f)
            {
                return 1f;
            }

            var elapsedSeconds = (float)(((double)nowUnixMs - plantedAtUnixMs) / 1000.0);
            return Mathf.Clamp01(elapsedSeconds / growDurationSeconds);
        }
    }
}
