using UnityEngine;

namespace MyLittleFarm.Gameplay.Farming
{
    /// <summary>Data-driven описание плодового дерева и интервала повторного плодоношения.</summary>
    [CreateAssetMenu(menuName = "My Little Farm/Tree Definition")]
    public sealed class TreeDefinition : ScriptableObject
    {
        [SerializeField] private string treeId;
        [SerializeField] private string displayName;
        [SerializeField] private string fruitItemId = "apple";
        [SerializeField, Min(0.1f)] private float firstHarvestSeconds = 30f;
        [SerializeField, Min(0.1f)] private float regrowSeconds = 45f;
        [SerializeField, Min(1)] private int yieldAmount = 3;
        [SerializeField] private Color foliageColor = new Color(0.16f, 0.48f, 0.12f);
        [SerializeField] private Color fruitReadyColor = new Color(0.82f, 0.16f, 0.08f);

        // Стабильные идентификаторы и параметры можно изменять в Inspector без отдельного кода дерева.
        public string TreeId => treeId;
        public string DisplayName => displayName;
        public string FruitItemId => fruitItemId;
        public float FirstHarvestSeconds => firstHarvestSeconds;
        public float RegrowSeconds => regrowSeconds;
        public int YieldAmount => yieldAmount;
        public Color FoliageColor => foliageColor;
        public Color FruitReadyColor => fruitReadyColor;

        /// <summary>Заполняет временное определение яблони для прототипа без .asset-файла.</summary>
        public void ConfigurePlaceholder(string id, string title, float firstHarvest, float regrow,
            int yield, Color foliage, Color ready)
        {
            treeId = id;
            displayName = title;
            fruitItemId = "apple";
            firstHarvestSeconds = Mathf.Max(0.1f, firstHarvest);
            regrowSeconds = Mathf.Max(0.1f, regrow);
            yieldAmount = Mathf.Max(1, yield);
            foliageColor = foliage;
            fruitReadyColor = ready;
        }
    }
}
