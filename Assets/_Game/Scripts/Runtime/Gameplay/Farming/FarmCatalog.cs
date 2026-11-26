using System;
using System.Collections.Generic;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.World;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Farming
{
    /// <summary>Объединяет ScriptableObject культуры и предметы по стабильным ID.</summary>
    public sealed class FarmCatalog : MonoBehaviour
    {
        /// <summary>Настройки временной культуры до появления отдельного ScriptableObject-ассета.</summary>
        [Serializable]
        private sealed class PlaceholderCropSettings
        {
            // Название видно в HUD, остальные поля управляют циклом покупки, роста и продажи.
            public string displayName;
            [Min(0.1f)] public float growthSeconds;
            [Min(1)] public int yieldAmount;
            [Min(0)] public int seedPrice;
            [Min(0)] public int sellPrice;
            public Color matureColor;

            public PlaceholderCropSettings(string title, float seconds, int yield, int purchase, int sale, Color color)
            {
                displayName = title;
                growthSeconds = seconds;
                yieldAmount = yield;
                seedPrice = purchase;
                sellPrice = sale;
                matureColor = color;
            }
        }

        [Header("Optional Content Assets")]
        [Tooltip("Если ассеты назначены, они заменяют встроенные временные определения с тем же ID.")]
        [SerializeField] private CropDefinition[] cropAssets = Array.Empty<CropDefinition>();
        [SerializeField] private ItemDefinition[] itemAssets = Array.Empty<ItemDefinition>();
        [Header("Script-Only Placeholder Data")]
        [SerializeField] private PlaceholderCropSettings carrot = new PlaceholderCropSettings(
            "Морковь", 8f, 2, 2, 5, new Color(0.95f, 0.42f, 0.08f));
        [SerializeField] private PlaceholderCropSettings potato = new PlaceholderCropSettings(
            "Картофель", 16f, 3, 3, 7, new Color(0.62f, 0.44f, 0.22f));
        [SerializeField] private PlaceholderCropSettings wheat = new PlaceholderCropSettings(
            "Пшеница", 12f, 4, 4, 4, new Color(0.95f, 0.78f, 0.26f));
        [SerializeField] private PlaceholderCropSettings tomato = new PlaceholderCropSettings(
            "Томат", 24f, 5, 5, 12, new Color(0.88f, 0.16f, 0.12f));
        [SerializeField] private PlaceholderCropSettings corn = new PlaceholderCropSettings(
            "Кукуруза", 18f, 4, 6, 10, new Color(0.96f, 0.80f, 0.20f));
        [SerializeField] private PlaceholderCropSettings cucumber = new PlaceholderCropSettings(
            "Огурец", 10f, 3, 7, 13, new Color(0.22f, 0.70f, 0.28f));

        // Временные объекты существуют только в памяти и не создают файлы в проекте.
        private readonly List<ScriptableObject> _placeholders = new List<ScriptableObject>();
        private readonly Dictionary<string, CropDefinition> _crops = new Dictionary<string, CropDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, ItemDefinition> _items = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
        private readonly List<CropDefinition> _orderedCrops = new List<CropDefinition>();

        public IReadOnlyList<CropDefinition> Crops => _orderedCrops;
        internal static readonly string[] FarmProductIds = { ApplePresses.FruitId, "egg", ApplePresses.JuiceId, "milk", "pear", "flour", "cheese", FishPond.FishId };
        internal static readonly string[] SaplingIds = { "apple_sapling", "pear_sapling" };

        /// <summary>Собирает восемь игровых культур и заменяет их назначенными ассетами.</summary>
        public void Configure()
        {
            ClearPlaceholders();
            _crops.Clear();
            _items.Clear();
            _orderedCrops.Clear();
            AddPlaceholder("carrot", carrot);
            AddPlaceholder("potato", potato);
            AddPlaceholder("wheat", wheat);
            AddPlaceholder("tomato", tomato);
            AddPlaceholder("corn", corn);
            AddPlaceholder("cucumber", cucumber);
            AddPlaceholder("strawberry",new PlaceholderCropSettings("Клубника",30,3,8,7,new Color(.8f,.1f,.15f)));
            AddPlaceholder("pumpkin",new PlaceholderCropSettings("Тыква",35,2,10,16,new Color(1f,.5f,.1f)));
            AddItemPlaceholder("apple_sapling", "Саженец яблони", 35, 0, true);
            if (cropAssets != null)
                foreach (var crop in cropAssets)
                    if (crop != null && !string.IsNullOrWhiteSpace(crop.CropId)) _crops[crop.CropId] = crop;
            if (itemAssets != null)
                foreach (var item in itemAssets)
                    if (item != null && !string.IsNullOrWhiteSpace(item.ItemId)) _items[item.ItemId] = item;
            foreach (var resource in new[] { "Milk", "PearFruit", "PearSapling", "Flour", "Cheese" })
            {
                var item = Resources.Load<ItemDefinition>("FarmContent/" + resource);
                if (item != null && (itemAssets == null || !Array.Exists(itemAssets, i => i != null && i.ItemId == item.ItemId)))
                    _items[item.ItemId] = item;
            }
            foreach(var id in new[]{"strawberry","pumpkin"})
            {
                var definition=Resources.Load<CropDefinition>("FarmContent/"+id);
                if(definition!=null && (cropAssets==null || !Array.Exists(cropAssets,c=>c!=null && c.CropId==id)))_crops[id]=definition;
            }
            ApplyBalancePrices();
            var usedItemIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in CropIds)
            {
                var crop = _crops[id];
                if (crop.GrowDurationSeconds <= 0f || float.IsNaN(crop.GrowDurationSeconds)
                    || float.IsInfinity(crop.GrowDurationSeconds)
                    || crop.StageCount < 2 || string.IsNullOrWhiteSpace(crop.DisplayName)
                    || string.IsNullOrWhiteSpace(crop.SeedItemId)
                    || string.IsNullOrWhiteSpace(crop.HarvestItemId)
                    || !usedItemIds.Add(crop.SeedItemId)
                    || !usedItemIds.Add(crop.HarvestItemId)
                    || !_items.TryGetValue(crop.SeedItemId, out var seed)
                    || !_items.TryGetValue(crop.HarvestItemId, out var harvest)
                    || (!seed.CanBuy && !crop.RareSeeds) || seed.BuyPrice < 0 || harvest.SellPrice < 1)
                    throw new InvalidOperationException($"Crop '{id}' has invalid growth, seed or sale data.");
                _orderedCrops.Add(crop);
            }
        }

        /// <summary>Стабильный порядок культур используется быстрыми слотами и UI.</summary>
        public static readonly string[] CropIds =
            { "carrot", "potato", "wheat", "tomato", "corn", "cucumber", "strawberry", "pumpkin" };

        /// <summary>Возвращает культуру, если ID присутствует в текущем каталоге.</summary>
        public bool TryGetCrop(string id, out CropDefinition crop) => _crops.TryGetValue(id ?? string.Empty, out crop);

        /// <summary>Возвращает предмет и его цены для торговли.</summary>
        public bool TryGetItem(string id, out ItemDefinition item) => _items.TryGetValue(id ?? string.Empty, out item);
        public bool IsRareSeed(string id){foreach(var crop in _orderedCrops)if(crop.SeedItemId==id)return crop.RareSeeds;return false;}

        private void AddPlaceholder(string id, PlaceholderCropSettings settings)
        {
            // Каждый вид содержит два предмета: покупаемое семя и продаваемый урожай.
            var crop = ScriptableObject.CreateInstance<CropDefinition>();
            crop.ConfigurePlaceholder(id, settings.displayName, settings.growthSeconds,
                settings.yieldAmount, settings.matureColor);
            _crops.Add(id, crop);
            _placeholders.Add(crop);
            var seed = ScriptableObject.CreateInstance<ItemDefinition>();
            seed.ConfigurePlaceholder(crop.SeedItemId, "Семена: " + settings.displayName,
                settings.seedPrice, 0, id!="corn");
            _items.Add(seed.ItemId, seed);
            _placeholders.Add(seed);
            var harvest = ScriptableObject.CreateInstance<ItemDefinition>();
            harvest.ConfigurePlaceholder(crop.HarvestItemId, settings.displayName, 0, settings.sellPrice, false);
            _items.Add(harvest.ItemId, harvest);
            _placeholders.Add(harvest);
        }

        /// <summary>Добавляет предмет вертикального среза, который не привязан к культуре.</summary>
        private void AddItemPlaceholder(string id, string title, int purchase, int sale, bool availableInShop)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.ConfigurePlaceholder(id, title, purchase, sale, availableInShop);
            _items.Add(item.ItemId, item);
            _placeholders.Add(item);
        }

        // Runtime copies keep scene-assigned assets intact while all economy consumers use one tuning.
        private void ApplyBalancePrices()
        {
            var seedPrices = new[] { 2, 4, 5, 6, 6, 8, 9, 11 };
            var salePrices = new[] { SellingSystem.DefaultCarrotSellPrice, 3, 2, 4, 5, 4, 6, 12 };
            for (var i = 0; i < CropIds.Length; i++)
            {
                var crop = _crops[CropIds[i]];
                SetItemPrices(crop.SeedItemId, seedPrices[i], 0);
                SetItemPrices(crop.HarvestItemId, 0, salePrices[i]);
            }

            SetProductPrices("pear_sapling", "Саженец груши", 45, 0, true);
            SetProductPrices(ApplePresses.FruitId, "Яблоко", 0, 12);
            SetProductPrices("pear", "Груша", 0, 16);
            SetProductPrices("egg", "Яйцо", 0, 10);
            SetProductPrices("milk", "Молоко", 0, 20);
            SetProductPrices(FishPond.FishId, "Озёрная рыба", 0, FishPond.SellPrice);
            foreach (var recipe in ProductionRecipe.All)
                if (recipe != ProductionRecipe.CornMill)
                    SetProductPrices(recipe.OutputId, recipe.OutputName, 0, recipe.SellPrice);
        }

        private void SetProductPrices(string id, string name, int purchase, int sale, bool canBuy = false)
        {
            if (_items.ContainsKey(id)) SetItemPrices(id, purchase, sale);
            else AddItemPlaceholder(id, name, purchase, sale, canBuy);
        }

        private void SetItemPrices(string id, int purchase, int sale)
        {
            var copy = Instantiate(_items[id]);
            copy.SetPrices(purchase, sale);
            _items[id] = copy;
            _placeholders.Add(copy);
        }
        private void OnDestroy() => ClearPlaceholders();

        private void ClearPlaceholders()
        {
            // Очищает только временные ScriptableObject, назначенные проектные ассеты не затрагивает.
            foreach (var placeholder in _placeholders)
                if (placeholder != null)
                {
                    if (Application.isPlaying) Destroy(placeholder);
                    else DestroyImmediate(placeholder);
                }
            _placeholders.Clear();
        }
    }
}
