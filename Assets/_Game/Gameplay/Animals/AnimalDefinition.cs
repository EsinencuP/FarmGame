using UnityEngine;

namespace MyLittleFarm.Gameplay.Animals
{
    /// <summary>Данные животного: его ID, корм и интервал получения продукта.</summary>
    [CreateAssetMenu(menuName = "My Little Farm/Animal Definition")]
    public sealed class AnimalDefinition : ScriptableObject
    {
        [SerializeField] private string animalId = "chicken";
        [SerializeField] private string displayName = "Курица";
        [SerializeField] private string feedItemId = "wheat";
        [SerializeField] private string productItemId = "egg";
        [SerializeField, Min(0.1f)] private float productIntervalSeconds = 40f;
        [SerializeField, Min(1)] private int productYield = 1;
        [SerializeField] private Color bodyColor = new Color(0.92f, 0.9f, 0.76f);

        // Поля доступны системам и могут быть заменены Inspector-ассетом без правки логики.
        public string AnimalId => animalId;
        public string DisplayName => displayName;
        public string FeedItemId => feedItemId;
        public string ProductItemId => productItemId;
        public float ProductIntervalSeconds => productIntervalSeconds;
        public int ProductYield => productYield;
        public Color BodyColor => bodyColor;

        /// <summary>Заполняет временное описание курицы для тестовой сцены без импорта модели.</summary>
        public void ConfigurePlaceholder(string id, string title, string feedId, string productId,
            float intervalSeconds, int yield, Color color)
        {
            animalId = id;
            displayName = title;
            feedItemId = feedId;
            productItemId = productId;
            productIntervalSeconds = Mathf.Max(0.1f, intervalSeconds);
            productYield = Mathf.Max(1, yield);
            bodyColor = color;
        }
    }
}
