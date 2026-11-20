using System;
using System.Collections.Generic;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Animals;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.Gameplay.Farming;
using MyLittleFarm.Gameplay.World;
using UnityEngine;
using MyLittleFarm.UI;

namespace MyLittleFarm.Gameplay.Economy
{
    /// <summary>Quotes and checks an entire order before changing the wallet or owned items.</summary>
    public sealed class FarmMarket : MonoBehaviour
    {
        public enum Category { Seeds, Sell, Upgrades, Animals, Orders }
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
        public DailyMarketOrders Orders=>GetComponentInParent<GameBootstrap>()?.GetComponentInChildren<SaveSystem>(true)?.DailyOrders;
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
                if (category == Category.Sell) foreach (var id in FarmCatalog.FarmProductIds) AddItem(result, id, category, null);
                else foreach (var id in FarmCatalog.SaplingIds)
                    if (catalog.TryGetItem(id, out var sapling) && sapling.CanBuy) AddItem(result, id, category, null);
            }
            else if (category == Category.Upgrades)
            {
                if (tool != null) result.Add(new Offer { Id = "tool:" + tool.Level, Name = "Улучшение инструмента", Icon = "tool_hoe", Price = tool.NextPrice, Maximum = 1,
                    Description = "Мотыга · уровень " + (tool.Level + 1) + " / 3\nСейчас: " + tool.AreaLabel + (tool.Level>=2?"":" → "+(tool.Level==0?"1×3":"3×3")) + "\nПлощадь для обработки, посадки и сбора одной культуры.\nРадиус доступа: " + tool.Reach.ToString("0.#") + " м.", Unavailable = tool.Level >= 2 ? FarmText.Get("market.maxLevel") : null });
                if (sector != null) result.Add(new Offer { Id = "sector", Name = "Новый сектор фермы", Icon = "b_sector", Price = sector.PurchasePrice, Maximum = 1,
                    Description = "Открывает дополнительную территорию фермы.", Unavailable = sector.IsUnlocked ? "Уже приобретён" : Application.isPlaying && !sector.CanPurchase ? "Сектор недоступен" : null });
                if (progress != null) result.Add(new Offer { Id = "shepherd", Name = "Нанять пастуха", Icon = "quest", Price = FarmProgression.ShepherdPrice, Maximum = 1,
                    Description = "Собирает яйца и кормит кур: сначала запас кормушки, затем инвентарь. Коровам нужен ручной сбор молока.", Unavailable = progress.Data.shepherdHired ? FarmText.Get("market.alreadyHired") : null });
                if (buildings != null && Application.isPlaying)
                    foreach (var house in buildings.Capture())
                        if (house.definitionId == "house") result.Add(new Offer { Id = "house:" + house.id + ":" + house.appearanceLevel, ItemId = house.id,
                            Name = "Дом " + house.x + ":" + house.z + " · " + (house.appearanceLevel + 1) + " / 3", Icon = "b_house", Price = FarmDecorationCatalog.HouseUpgradePrice(house.appearanceLevel), Maximum = 1,
                            Description = house.appearanceLevel < 2 ? "Следующий вид: " + FarmDecorationCatalog.HouseLevelName(house.appearanceLevel + 1) + "\nОсвободите территорию дома перед улучшением." : "Усадьба полностью улучшена.",
                            Unavailable = house.appearanceLevel >= 2 ? FarmText.Get("market.maxLevel") : null });
                if (!Application.isPlaying) result.Add(new Offer { Id = "house:preview:0", Name = "Улучшение дома", Icon = "b_house", Price = FarmDecorationCatalog.HouseUpgradePrice(0), Maximum = 1, Description = "Выберите построенный дом в списке рынка." });
            }
            else if(category==Category.Orders)
            {
                if(Orders==null)return result;
                foreach(var order in Orders.Current)
                {
                    var name=catalog.TryGetItem(order.itemId,out var item)?item.DisplayName:order.itemId;
                    result.Add(new Offer{Id=order.id,ItemId=order.itemId,Name=name+" × "+order.amount,Icon=FarmUITheme.ItemIcon(order.itemId),Price=order.unitPrice*order.amount+order.bonus,Maximum=1,
                        Description="День "+(Orders.Day+1)+" · обновление через "+FarmText.Duration((float)Orders.Remaining)+"\nЦена "+(order.unitPrice*order.amount)+" + бонус "+order.bonus+" монет.\nВ инвентаре "+Owned(order.itemId)+" из "+order.amount+".\nБез штрафа. Сутки заказов — 14 минут игры.",Unavailable=order.completed?"Выполнен":null});
                }
            }
            else if (animals != null)
            {
                result.Add(new Offer { Id = "chicken", Name = "Курица", Icon = "chicken", Price = animals.ChickenPrice,
                    Maximum = Mathf.Max(0, animals.MaxAnimals - animals.ChickenCount), Description = "После оплаты поселяется в свободном месте загона.\nКорм: пшеница. Продукция: яйца.",
                    Unavailable = animals.ChickenCount >= animals.MaxAnimals ? FarmText.Get("market.penFull") : null });
                var canDeliverCow = !Application.isPlaying || animals.CanDeliver("cow", 1, out _);
                result.Add(new Offer { Id = "cow", Name = "Корова", Icon = "cow", Price = animals.CowPrice,
                    Maximum = Mathf.Max(0, animals.MaxCows - animals.CowCount),
                    Description = "Нужен коровник с загоном (до 2 коров).\nКорм: пшеница. Продукция: молоко.\nПроизводство: "
                        + FarmGuideUI.Duration(animals.CowDefinition == null ? 60 : animals.CowDefinition.ProductIntervalSeconds),
                    Unavailable = !canDeliverCow ? "Сначала постройте или освободите коровник" : null });
            }
            return result;
        }
        private void AddItem(List<Offer> result, string id, Category category, CropDefinition crop)
        {
            if (!catalog.TryGetItem(id, out var item)) return;
            var sale = category == Category.Sell;
            var rare = catalog.IsRareSeed(id);
            var sapling = id.EndsWith("_sapling", StringComparison.Ordinal);
            result.Add(new Offer { Id = id, ItemId = id, Name = item.DisplayName, Icon = MyLittleFarm.UI.FarmUITheme.ItemIcon(id), Price = sale ? item.SellPrice : item.BuyPrice,
                Maximum = sale ? Owned(id) : Mathf.Min(999, int.MaxValue - Owned(id)),
                Description = (sapling ? "Выберите саженец в инвентаре. Посадка · " + InputReader.KeyLabel(InputReader.Command.Apple)
                    + "\nДерево остаётся после сбора и плодоносит повторно."
                    : crop == null ? "Продукция фермы" : "Рост · " + MyLittleFarm.UI.FarmGuideUI.Duration(crop.GrowDurationSeconds) + "\nУрожай: " + crop.YieldAmount + " шт." + (crop.RegrowSeconds>0?"\nПовтор через "+FarmGuideUI.Duration(crop.RegrowSeconds)+" без семени.":""))
                    + (rare ? "\nРедкие семена можно найти в тайниках." : "") + "\nВ инвентаре: " + Owned(id),
                Unavailable = sale ? (item.SellPrice <= 0 ? FarmText.Get("market.notForSale") : Owned(id) == 0 ? FarmText.Get("market.noStock") : null)
                    : rare || !item.CanBuy ? FarmText.Get("market.cacheOnly") : null });
        }

        public bool Quote(Category category, IReadOnlyDictionary<string, int> cart, out int total, out string reason)
        {
            if(category==Category.Orders && Orders!=null)return Orders.Quote(cart,out total,out reason);
            total = 0; reason = FarmText.Get(category == Category.Sell ? "market.chooseSale" : "market.choosePurchase");
            if (cart == null || cart.Count == 0) return false;
            var offers = Offers(category); long sum = 0;
            foreach (var line in cart)
            {
                var offer = offers.Find(o => o.Id == line.Key);
                if (offer == null || !offer.Available || line.Value <= 0 || line.Value > offer.Maximum)
                { reason = FarmText.Get("market.staleOrder"); return false; }
                sum += (long)offer.Price * line.Value;
                if (sum > int.MaxValue) { reason = FarmText.Get("market.tooLarge"); return false; }
                if (Application.isPlaying && category == Category.Animals && !animals.CanDeliver(offer.Id, line.Value, out _))
                { reason = FarmText.Get("market.noSpace"); return false; }
                if (Application.isPlaying && offer.Id.StartsWith("house:") && !buildings.CanUpgradeHouse(offer.ItemId, out reason)) return false;
            }
            total = (int)sum;
            if (category == Category.Sell && sum > int.MaxValue - Balance) { reason = FarmText.Get("market.walletFull"); return false; }
            if (category != Category.Sell && sum > Balance) { reason = FarmText.MissingCoins(sum - Balance); return false; }
            reason = ""; return true;
        }
        public bool Checkout(Category category, IReadOnlyDictionary<string, int> cart, out string reason)
        {
            reason = FarmText.Get("market.approach");
            if (_committing || !Application.isPlaying || !CanTrade) return false;
            if (!Quote(category, cart, out var total, out reason)) return false;
            if(category==Category.Orders)
            {
                _committing=true;
                try{var done=Orders.Complete(cart,out reason);if(done){GameEvents.RaiseStatusChanged(reason);GameEvents.RaiseActionFeedback("sale",proximity.TradePosition,"coin",total);Changed?.Invoke();}return done;}
                finally{_committing=false;}
            }
            // Snapshot before events fire: HUD observers may refresh their own collections.
            var order = new List<KeyValuePair<string, int>>(cart); var offers = Offers(category);
            _committing = true;
            try
            {
                if (category == Category.Sell)
                {
                    var containsJuice = order.Exists(line => line.Key == ApplePresses.JuiceId);
                    long sold = 0;
                    foreach (var line in order)
                    {
                        inventory.SetAmountSilently(line.Key, Owned(line.Key) - line.Value);
                        sold += line.Value;
                    }
                    wallet.SetCoinsSilently(Balance + total);
                    GameEvents.RaiseInventoryChanged();
                    GameEvents.RaiseMoneyChanged(Balance);
                    GameEvents.Record("sale", containsJuice ? ApplePresses.JuiceId : string.Empty, sold, total);
                    GameEvents.RaiseProgressAction("sale");
                }
                else if (category == Category.Animals)
                {
                    if (!animals.TryPurchase(cart, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), out reason)) return false;
                    MyLittleFarm.Audio.FarmAudio.PlayAtPlayer(MyLittleFarm.Audio.SoundId.Purchase);
                }
                else if (category == Category.Seeds)
                {
                    foreach (var line in order) inventory.SetAmountSilently(line.Key, Owned(line.Key) + line.Value);
                    wallet.SetCoinsSilently(Balance - total);
                    GameEvents.RaiseInventoryChanged();
                    GameEvents.RaiseMoneyChanged(Balance);
                    if (total > 0) GameEvents.Record("spent", value: total);
                    foreach (var line in order)
                    {
                        var offer = offers.Find(o => o.Id == line.Key);
                        GameEvents.Record("purchase", offer.ItemId, line.Value, offer.Price * line.Value);
                    }
                    GameEvents.RaiseProgressAction("purchase");
                    MyLittleFarm.Audio.FarmAudio.PlayAtPlayer(MyLittleFarm.Audio.SoundId.Purchase);
                }
                else
                {
                    if (!wallet.TrySpend(total)) { reason = FarmText.MissingCoins(total - Balance); return false; }
                    foreach (var line in order)
                    {
                        var offer = offers.Find(o => o.Id == line.Key);
                        if (line.Key.StartsWith("tool:")) { tool.Restore(tool.Level + 1); MyLittleFarm.Audio.FarmAudio.PlayAtPlayer(MyLittleFarm.Audio.SoundId.ToolUpgrade); }
                        else if (line.Key == "sector") { sector.Restore(true); GameEvents.Record("sector"); }
                        else if (line.Key == "shepherd") progress.GrantShepherd();
                        else if (line.Key.StartsWith("house:")) buildings.CompleteHouseUpgrade(offer.ItemId);
                    }
                    MyLittleFarm.Audio.FarmAudio.PlayAtPlayer(MyLittleFarm.Audio.SoundId.Purchase);
                }
                GameEvents.RaiseActionFeedback(category == Category.Sell ? "sale" : "purchase", proximity.TradePosition,
                    "coin", category == Category.Sell ? total : -total);
                reason = FarmText.Get(category == Category.Sell ? "market.saleDone" : "market.purchaseDone", FarmText.Coins(total));
                GameEvents.RaiseStatusChanged(reason); Changed?.Invoke(); return true;
            }
            finally { _committing = false; }
        }
    }
}
