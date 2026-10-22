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
        [SerializeField] private Vector3[] stageSizes;
        [Header("Time-of-day harvest bonus")]
        [SerializeField] private bool rareSeeds;
        [SerializeField, Range(0,23.99f)] private float bonusStartHour;
        [SerializeField, Range(0,23.99f)] private float bonusEndHour;
        [SerializeField, Range(0,10)] private int bonusHarvestYield;
        public bool RareSeeds => rareSeeds;
        public int BonusHarvestYield => bonusHarvestYield;
        public string BonusDescription => bonusHarvestYield <= 0 ? string.Empty
            : $"{displayName}: {HourText(bonusStartHour)}–{HourText(bonusEndHour)}, +{bonusHarvestYield} к сбору";
        private static string HourText(float hour){var m=Mathf.FloorToInt(Mathf.Repeat(hour,24)*60);return (m/60).ToString("00")+":"+(m%60).ToString("00");}
        public int HarvestYield(float hour)
        {
            var active=hour>=0 && bonusStartHour!=bonusEndHour && (bonusStartHour<bonusEndHour
                ? hour>=bonusStartHour&&hour<bonusEndHour : hour>=bonusStartHour||hour<bonusEndHour);
            return (int)System.Math.Min(int.MaxValue,(long)yieldAmount+(active?Mathf.Clamp(bonusHarvestYield,0,10):0));
        }

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
        public Vector3 StageSize(int stage)
        {
            stage = Mathf.Clamp(stage, 0, Mathf.Max(0, stageCount - 1));
            if (stageSizes != null && stage < stageSizes.Length && stageSizes[stage].x > 0f
                && stageSizes[stage].y > 0f && stageSizes[stage].z > 0f) return stageSizes[stage];
            var progress = stage / (float)Mathf.Max(1, stageCount - 1);
            var width = Mathf.Lerp(0.22f, 0.65f, progress);
            return new Vector3(width, Mathf.Lerp(0.14f, 0.75f, progress), width);
        }

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
            rareSeeds=id=="corn";
            bonusHarvestYield=id=="tomato"?2:id=="carrot"||id=="cucumber"?1:0;
            bonusStartHour=id=="carrot"?5:id=="tomato"?11:id=="cucumber"?18:0;
            bonusEndHour=id=="carrot"?9:id=="tomato"?15:id=="cucumber"?22:0;
        }
    }
}
