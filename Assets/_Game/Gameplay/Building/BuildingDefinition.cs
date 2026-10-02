using System;
using System.Collections.Generic;

namespace MyLittleFarm.Gameplay.Building
{
    /// <summary>Неизменяемые характеристики одного типа постройки из игрового каталога.</summary>
    public sealed class BuildingDefinition
    {
        // Id сохраняется на диск; Name показывается игроку; размеры заданы в клетках.
        public string Id { get; }
        public string Name { get; }
        public int Width { get; }
        public int Depth { get; }
        public int Price { get; }
        public float Height { get; }

        /// <summary>Создаёт проверенное описание типа постройки.</summary>
        public BuildingDefinition(string id, string name, int width, int depth, int price, float height)
        {
            if (string.IsNullOrWhiteSpace(id) || width < 1 || depth < 1 || price < 0
                || height <= 0 || float.IsNaN(height) || float.IsInfinity(height))
                throw new ArgumentException("Invalid building definition.");
            Id = id; Name = name; Width = width; Depth = depth; Price = price; Height = height;
        }

        /// <summary>Возвращает ширину следа после поворота на указанное число четвертей оборота.</summary>
        public int RotatedWidth(int turns) => (turns & 1) == 0 ? Width : Depth;
        /// <summary>Возвращает глубину следа после поворота на указанное число четвертей оборота.</summary>
        public int RotatedDepth(int turns) => (turns & 1) == 0 ? Depth : Width;

        // Каталог прототипа хранится в коде; идентификаторы являются частью формата сохранения.
        public static IReadOnlyList<BuildingDefinition> Catalog { get; } = Array.AsReadOnly(new[]
        {
            new BuildingDefinition("house", "Дом", 2, 2, 20, 2.8f),
            new BuildingDefinition("storage", "Склад", 2, 1, 12, 1.8f),
            new BuildingDefinition("market", "Торговая стойка", 1, 2, 10, 1.6f),
            new BuildingDefinition("flowerbed", "Клумба", 1, 1, 3, 0.45f),
            new BuildingDefinition("coop", "Курятник", 2, 2, 18, 1.7f)
        });
    }
}
