using System;
using System.Collections.Generic;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Animals;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.Gameplay.Farming;
using MyLittleFarm.Gameplay.World;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Economy
{
    /// <summary>Quotes and checks an entire order before changing the wallet or owned items.</summary>
    public sealed class FarmMarket : MonoBehaviour
    {
        public enum Category { Seeds, Sell, Upgrades, Animals }
        public sealed class Offer
        {
            public string Id, ItemId, Name, Icon, Description, Unavailable;
            public int Price, Maximum = 999;
            public bool Available => string.IsNullOrEmpty(Unavailable);
        }
        [SerializeField] private FarmCatalog catalog;
        [SerializeField] private InventorySystem inventory;
        [SerializeField] private WalletSystem wallet;
        [SerializeField] private SeedShopSystem proximity;
        [SerializeField] private ToolUpgradeSystem tool;
        [SerializeField] private SectorSystem sector;
        [SerializeField] private AnimalSystem animals;
        [SerializeField] private BuildSystem buildings;
        [SerializeField] private FarmProgression progress;
        private bool _committing;
        public bool CanTrade => proximity != null && proximity.CanTrade;
        public int Balance => wallet == null ? 0 : wallet.Coins;
        public event Action Changed;
        public int Owned(string id) => inventory == null ? 0 : inventory.GetAmount(id);

        public void Configure(FarmCatalog definitions, InventorySystem items, WalletSystem money, SeedShopSystem shop,
            ToolUpgradeSystem tools, SectorSystem land, AnimalSystem livestock, BuildSystem construction, FarmProgression progression)
        { catalog = definitions; inventory = items; wallet = money; proximity = shop; tool = tools; sector = land; animals = livestock; buildings = construction; progress = progression; }

        public List<Offer> Offers(Category category)
        {
            var result = new List<Offer>();
            if (catalog == null) return result;
            if (!Application.isPlaying && catalog.Crops.Count == 0) catalog.Configure();
            if (category == Category.Seeds || category == Category.Sell)
            {
                foreach (var crop in catalog.Crops)
                    AddItem(result, category == Category.Seeds ? crop.SeedItemId : crop.HarvestItemId, category, crop);
                if (category == Category.Sell) foreach (var id in new[] { "apple", "egg" }) AddItem(result, id, category, null);
            }
            else if (category == Category.Upgrades)
            {
                if (tool != null) result.Add(new Offer { Id = "tool:" + tool.Level, Name = "Улучшение инструмента", Icon = "tool_hoe", Price = tool.NextPrice, Maximum = 1,
                    Description = "Уровень " + (tool.Level + 1) + " / 3\nУвеличивает радиус работы с грядками.\nТекущий радиус: " + tool.Reach.ToString("0.#") + " м.", Unavailable = tool.Level >= 2 ? "Максимальный уровень" : null });
                if (sector != null) result.Add(new Offer { Id = "sector", Name = "Новый сектор фермы", Icon = "b_sector", Price = sector.PurchasePrice, Maximum = 1,
                    Description = "Открывает дополнительную территорию фермы.", Unavailable = sector.IsUnlocked ? "Уже приобретён" : Application.isPlaying && !sector.CanPurchase ? "Сектор недоступен" : null });
                if (progress != null) result.Add(new Offer { Id = "shepherd", Name = "Нанять пастуха", Icon = "quest", Price = FarmProgression.ShepherdPrice, Maximum = 1,
                    Description = "Кормит кур пшеницей из инвентаря и собирает яйца.", Unavailable = progress.Data.shepherdHired ? "Уже нанят" : null });
                if (buildings != null && Application.isPlaying)
                    foreach (var house in buildings.Capture())
                        if (house.definitionId == "house") result.Add(new Offer { Id = "house:" + house.id + ":" + house.appearanceLevel, ItemId = house.id,
                            Name = "Дом " + house.x + ":" + house.z + " · " + (house.appearanceLevel + 1) + " / 3", Icon = "b_house", Price = FarmDecorationCatalog.HouseUpgradePrice(house.appearanceLevel), Maximum = 1,
                            Description = house.appearanceLevel < 2 ? "Следующий вид: " + FarmDecorationCatalog.HouseLevelName(house.appearanceLevel + 1) + "\nОсвободите территорию дома перед улучшением." : "Усадьба полностью улучшена.",
                            Unavailable = house.appearanceLevel >= 2 ? "Максимальный уровень" : null });
                if (!Application.isPlaying) result.Add(new Offer { Id = "house:preview:0", Name = "Улучшение дома", Icon = "b_house", Price = FarmDecorationCatalog.HouseUpgradePrice(0), Maximum = 1, Description = "Выберите построенный дом в списке рынка." });
            }
            else if (animals != null) result.Add(new Offer { Id = "chicken", Name = "Курица", Icon = "chicken", Price = animals.ChickenPrice,
                Maximum = Mathf.Max(0, animals.MaxAnimals - animals.Count), Description = "После оплаты поселяется в свободном месте загона.\nКорм: пшеница. Продукция: яйца.",
                Unavailable = animals.Count >= animals.MaxAnimals ? "Загон заполнен" : null });
            return result;
        }
        private void AddItem(List<Offer> result, string id, Category category, CropDefinition crop)
        {
            if (!catalog.TryGetItem(id, out var item)) return;
            var sale = category == Category.Sell;
            var rare = catalog.IsRareSeed(id);
            result.Add(new Offer { Id = id, ItemId = id, Name = item.DisplayName, Icon = MyLittleFarm.UI.FarmUITheme.ItemIcon(id), Price = sale ? item.SellPrice : item.BuyPrice,
                Maximum = sale ? Owned(id) : Mathf.Min(999, int.MaxValue - Owned(id)),
                Description = (crop == null ? "Продукция фермы" : "Рост · " + MyLittleFarm.UI.FarmGuideUI.Duration(crop.GrowDurationSeconds) + "\nУрожай: " + crop.YieldAmount + " шт.")
                    + (rare ? "\nРедкие семена можно найти в тайниках." : "") + "\nВ инвентаре: " + Owned(id),
                Unavailable = sale ? (item.SellPrice <= 0 ? "Не продаётся" : Owned(id) == 0 ? "Нет в инвентаре" : null)
                    : rare || !item.CanBuy ? "Только в тайниках" : null });
        }

        public bool Quote(Category category, IReadOnlyDictionary<string, int> cart, out int total, out string reason)
        {
            total = 0; reason = "Добавьте товары в корзину";
            if (cart == null || cart.Count == 0) return false;
            var offers = Offers(category); long sum = 0;
            foreach (var line in cart)
            {
                var offer = offers.Find(o => o.Id == line.Key);
                if (offer == null || !offer.Available || line.Value <= 0 || line.Value > offer.Maximum)
                { reason = "Состав заказа изменился. Удалите недоступные позиции."; return false; }
                sum += (long)offer.Price * line.Value;
                if (sum > int.MaxValue) { reason = "Сумма заказа слишком велика"; return false; }
                if (Application.isPlaying && offer.Id == "chicken" && !animals.CanDeliver(line.Value))
                { reason = "В загоне недостаточно свободных мест"; return false; }
                if (Application.isPlaying && offer.Id.StartsWith("house:") && !buildings.CanUpgradeHouse(offer.ItemId, out reason)) return false;
            }
            total = (int)sum;
            if (category == Category.Sell && sum > int.MaxValue - Balance) { reason = "Кошелёк заполнен"; return false; }
            if (category != Category.Sell && sum > Balance) { reason = "Недостаточно монет"; return false; }
            reason = ""; return true;
        }
        public bool Checkout(Category category, IReadOnlyDictionary<string, int> cart, out string reason)
        {
            reason = "Подойдите к рынку";
            if (_committing || !Application.isPlaying || !CanTrade) return false;
            if (!Quote(category, cart, out var total, out reason)) return false;
            // Snapshot before events fire: HUD observers may refresh their own collections.
            var order = new List<KeyValuePair<string, int>>(cart); var offers = Offers(category);
            _committing = true;
            try
            {
                if (category == Category.Sell)
                {
                    foreach (var line in order) inventory.TryRemove(line.Key, line.Value);
                    wallet.AddCoins(total); GameEvents.Record("sale", value: total);
                    GameEvents.RaiseProgressAction("sale"); GameEvents.RaiseActionFeedback("sale", proximity.TradePosition);
                }
                else
                {
                    if (!wallet.TrySpend(total)) { reason = "Недостаточно монет"; return false; }
                    foreach (var line in order)
                    {
                        var offer = offers.Find(o => o.Id == line.Key);
                        if (category == Category.Seeds) { inventory.Add(offer.ItemId, line.Value); GameEvents.Record("purchase", offer.ItemId, line.Value, offer.Price * line.Value); }
                        else if (line.Key.StartsWith("tool:")) { tool.Restore(tool.Level + 1); MyLittleFarm.Audio.FarmAudio.PlayAtPlayer(MyLittleFarm.Audio.SoundId.ToolUpgrade); }
                        else if (line.Key == "sector") { sector.Restore(true); GameEvents.Record("sector"); }
                        else if (line.Key == "shepherd") progress.GrantShepherd();
                        else if (line.Key.StartsWith("house:")) buildings.CompleteHouseUpgrade(offer.ItemId);
                        else if (line.Key == "chicken") animals.Deliver(line.Value, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                    }
                    GameEvents.RaiseProgressAction("purchase");
                    MyLittleFarm.Audio.FarmAudio.PlayAtPlayer(MyLittleFarm.Audio.SoundId.Purchase);
                }
                reason = category == Category.Sell ? "Продано · +" + total + " монет" : "Заказ оплачен · −" + total + " монет";
                GameEvents.RaiseStatusChanged(reason); Changed?.Invoke(); return true;
            }
            finally { _committing = false; }
        }
    }
}
