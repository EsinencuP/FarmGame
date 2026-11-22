using System;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Farming
{
    /// <summary>Сохраняемое состояние одного дерева без ссылки на визуальный GameObject.</summary>
    [Serializable]
    public sealed class TreeRuntimeState
    {
        // Координата и ID дерева восстанавливают его в том же месте после загрузки.
        public int x;
        public int z;
        public string treeId;
        // Время посадки и последнего сбора позволяют вычислить офлайн-плодоношение.
        public long plantedAtUnixMs;
        public long lastHarvestAtUnixMs;
        // После сбора эти данные сбрасываются для следующей партии плодов.
        public bool manuallyWatered;
        public float rainySeconds;
        public bool sizeReady;
        public HarvestSize harvestSize;
        [NonSerialized] internal long moistureSampleAt;
        [NonSerialized] internal double rainSample;

        /// <summary>Возвращает координату клетки дерева.</summary>
        public Vector2Int Position => new Vector2Int(x, z);
    }
}
