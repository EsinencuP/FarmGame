using System;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Farming
{
    [Serializable]
    public sealed class CropRuntimeState
    {
        public int x;
        public int z;
        public string cropId;
        public long plantedAtUnixMs;
        public float growDurationSeconds;
        public int stageCount;

        public Vector2Int Position => new Vector2Int(x, z);

        public bool IsMature(long nowUnixMs)
        {
            return GetGrowthRatio(nowUnixMs) >= 1f;
        }

        public int GetStage(long nowUnixMs)
        {
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
            if (growDurationSeconds <= 0f)
            {
                return 1f;
            }

            var elapsedSeconds = (float)(((double)nowUnixMs - plantedAtUnixMs) / 1000.0);
            return Mathf.Clamp01(elapsedSeconds / growDurationSeconds);
        }
    }
}
