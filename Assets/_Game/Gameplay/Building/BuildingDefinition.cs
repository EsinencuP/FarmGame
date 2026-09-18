using System;
using System.Collections.Generic;

namespace MyLittleFarm.Gameplay.Building
{
    public sealed class BuildingDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public int Width { get; }
        public int Depth { get; }
        public int Price { get; }
        public float Height { get; }

        public BuildingDefinition(string id, string name, int width, int depth, int price, float height)
        {
            if (string.IsNullOrWhiteSpace(id) || width < 1 || depth < 1 || price < 0
                || height <= 0 || float.IsNaN(height) || float.IsInfinity(height))
                throw new ArgumentException("Invalid building definition.");
            Id = id; Name = name; Width = width; Depth = depth; Price = price; Height = height;
        }

        public int RotatedWidth(int turns) => (turns & 1) == 0 ? Width : Depth;
        public int RotatedDepth(int turns) => (turns & 1) == 0 ? Depth : Width;

        // Code-only prototype catalog; identifiers are part of the save format.
        public static IReadOnlyList<BuildingDefinition> Catalog { get; } = Array.AsReadOnly(new[]
        {
            new BuildingDefinition("house", "Дом", 2, 2, 20, 2.8f),
            new BuildingDefinition("storage", "Склад", 2, 1, 12, 1.8f),
            new BuildingDefinition("market", "Торговая стойка", 1, 2, 10, 1.6f),
            new BuildingDefinition("flowerbed", "Клумба", 1, 1, 3, 0.45f)
        });
    }
}
