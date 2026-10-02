using System;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Animals
{
    /// <summary>Сериализуемое состояние одного животного без ссылки на его временный view.</summary>
    [Serializable]
    public sealed class AnimalRuntimeState
    {
        // Стабильный ID и координата позволяют восстановить животное в том же загоне.
        public string animalId;
        public string definitionId;
        public int x;
        public int z;
        // Время последнего кормления и следующего яйца работают независимо от FPS.
        public long lastFedAtUnixMs;
        public long nextProductAtUnixMs;
        public int readyProductCount;

        /// <summary>Возвращает клетку загона из сериализованных координат.</summary>
        public Vector2Int Position => new Vector2Int(x, z);
    }
}
