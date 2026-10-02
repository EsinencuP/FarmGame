using UnityEngine;

namespace MyLittleFarm.Gameplay.Economy
{
    /// <summary>Описание предмета для инвентаря, магазина и временного интерфейса.</summary>
    [CreateAssetMenu(menuName = "My Little Farm/Item Definition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [SerializeField] private string itemId;
        [SerializeField] private string displayName;
        [SerializeField, Min(0)] private int buyPrice;
        [SerializeField, Min(0)] private int sellPrice;
        [SerializeField] private bool canBuy;
        [SerializeField] private Sprite icon;

        // Цены и иконку можно заменить через Inspector без правки логики магазина.
        public string ItemId => itemId;
        public string DisplayName => displayName;
        public int BuyPrice => buyPrice;
        public int SellPrice => sellPrice;
        public bool CanBuy => canBuy;
        public Sprite Icon => icon;

        /// <summary>Создаёт характеристики временного предмета до появления арт-ассетов.</summary>
        public void ConfigurePlaceholder(string id, string title, int purchase, int sale, bool availableInShop)
        {
            itemId = id;
            displayName = title;
            buyPrice = Mathf.Max(0, purchase);
            sellPrice = Mathf.Max(0, sale);
            canBuy = availableInShop;
        }
    }
}
