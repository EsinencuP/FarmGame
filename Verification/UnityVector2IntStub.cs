using System;

namespace UnityEngine
{
    /// <summary>Минимальная форма Vector2Int для автономной проверки данных без Unity Editor.</summary>
    public struct Vector2Int : IEquatable<Vector2Int>
    {
        // Координаты нужны настоящему ChunkData только для адреса чанка.
        public int x;
        public int y;

        /// <summary>Создаёт целочисленную координату.</summary>
        public Vector2Int(int x, int y) { this.x = x; this.y = y; }
        /// <summary>Сравнивает обе составляющие адреса.</summary>
        public bool Equals(Vector2Int other) => x == other.x && y == other.y;
        /// <summary>Сравнивает координату с произвольным объектом.</summary>
        public override bool Equals(object other) => other is Vector2Int coordinate && Equals(coordinate);
        /// <summary>Создаёт стабильный хеш для словарей.</summary>
        public override int GetHashCode() => unchecked((x * 397) ^ y);
    }
}
