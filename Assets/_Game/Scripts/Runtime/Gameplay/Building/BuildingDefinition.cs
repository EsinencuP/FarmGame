using System;
using System.Collections.Generic;
using MyLittleFarm.UI;

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
        public int ConstructionSeconds { get; }

        /// <summary>Создаёт проверенное описание типа постройки.</summary>
        public BuildingDefinition(string id, string name, int width, int depth, int price, float height, int constructionSeconds = 0)
        {
            if (string.IsNullOrWhiteSpace(id) || width < 1 || depth < 1 || price < 0
                || height <= 0 || float.IsNaN(height) || float.IsInfinity(height)
                || constructionSeconds < 0 || constructionSeconds > MaximumConstructionSeconds)
                throw new ArgumentException("Invalid building definition.");
            Id = id; Name = name; Width = width; Depth = depth; Price = price; Height = height;
            ConstructionSeconds = constructionSeconds;
        }

        /// <summary>Возвращает ширину следа после поворота на указанное число четвертей оборота.</summary>
        public int RotatedWidth(int turns) => (turns & 1) == 0 ? Width : Depth;
        /// <summary>Возвращает глубину следа после поворота на указанное число четвертей оборота.</summary>
        public int RotatedDepth(int turns) => (turns & 1) == 0 ? Depth : Width;

        // Каталог прототипа хранится в коде; идентификаторы являются частью формата сохранения.
        // Lazy construction prevents circular type initialization when a decoration is referenced first.
        private static readonly Lazy<IReadOnlyList<BuildingDefinition>> CatalogCache=new Lazy<IReadOnlyList<BuildingDefinition>>(CreateCatalog);
        public static IReadOnlyList<BuildingDefinition> Catalog=>CatalogCache.Value;
        internal const int MaximumConstructionSeconds = 3600;
        private static IReadOnlyList<BuildingDefinition> CreateCatalog()
        {
            var items=new List<BuildingDefinition>{
            new BuildingDefinition("house", "Дом", 7, 6, 20, 5f, 30),
            new BuildingDefinition("storage", "Склад", 5, 6, 12, 4f, 30),
            new BuildingDefinition("market", "Торговая стойка", 8, 5, 10, 3.1f, 30),
            new BuildingDefinition("flowerbed", "Клумба", 1, 1, 3, 0.45f),
            new BuildingDefinition("coop", "Курятник", 3, 5, 18, 4f, 30),
            new BuildingDefinition("mill", "Мельница", 5, 4, 80, 5.8f, 60),
            new BuildingDefinition("cheese_dairy", "Сыроварня", 5, 6, 100, 4f, 60),
            new BuildingDefinition(BarnHabitat.BuildingId, "Коровник с загоном", BarnHabitat.Width, BarnHabitat.Depth, BarnHabitat.Price, 4f, 60),
            new BuildingDefinition(ApplePresses.BuildingId, FarmText.Get("press.name"), 2, 2, ApplePresses.BuildingPrice, 1.8f, 60),
            new BuildingDefinition("feeder", "Кормушка", 1, 1, 18, .6f, 30)};
            items.AddRange(FarmDecorationCatalog.Items);return items.AsReadOnly();
        }

        // Version <= 6 footprints are used only to validate and migrate older saves.
        public static IReadOnlyList<BuildingDefinition> LegacyCatalog { get; } = Array.AsReadOnly(new[]
        {
            new BuildingDefinition("house", "Дом", 2, 2, 20, 2.8f),
            new BuildingDefinition("storage", "Склад", 2, 1, 12, 1.8f),
            new BuildingDefinition("market", "Торговая стойка", 1, 2, 10, 1.6f),
            new BuildingDefinition("flowerbed", "Клумба", 1, 1, 3, 0.45f),
            new BuildingDefinition("coop", "Курятник", 2, 2, 18, 1.7f)
        });
    }
}
