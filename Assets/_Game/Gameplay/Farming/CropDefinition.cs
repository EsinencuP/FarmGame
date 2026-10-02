using UnityEngine;

namespace MyLittleFarm.Gameplay.Farming
{
    /// <summary>Редактируемые в Inspector данные культуры, общие для посадки, роста и продажи.</summary>
    [CreateAssetMenu(menuName = "My Little Farm/Crop Definition")]
    public sealed class CropDefinition : ScriptableObject
    {
        [SerializeField] private string cropId;
        [SerializeField] private string displayName;
        [SerializeField] private string seedItemId;
        [SerializeField] private string harvestItemId;
        [SerializeField, Min(0.1f)] private float growDurationSeconds = 8f;
        [SerializeField, Min(1)] private int yieldAmount = 2;
        [SerializeField, Min(2)] private int stageCount = 3;
        [SerializeField] private Color matureColor = new Color(0.95f, 0.42f, 0.08f);
        [SerializeField] private GameObject[] stagePrefabs;

        // Стабильные ID используются сохранением; числовые параметры можно менять в Inspector.
        public string CropId => cropId;
        public string DisplayName => displayName;
        public string SeedItemId => seedItemId;
        public string HarvestItemId => harvestItemId;
        public float GrowDurationSeconds => growDurationSeconds;
        public int YieldAmount => yieldAmount;
        public int StageCount => stageCount;
        public Color MatureColor => matureColor;
        public GameObject[] StagePrefabs => stagePrefabs;

        /// <summary>Заполняет временный ScriptableObject, когда отдельные .asset ещё не созданы.</summary>
        public void ConfigurePlaceholder(string id, string title, float seconds, int amount, Color color)
        {
            cropId = id;
            displayName = title;
            seedItemId = id + "_seed";
            harvestItemId = id;
            growDurationSeconds = Mathf.Max(0.1f, seconds);
            yieldAmount = Mathf.Max(1, amount);
            stageCount = 3;
            matureColor = color;
        }
    }
}
