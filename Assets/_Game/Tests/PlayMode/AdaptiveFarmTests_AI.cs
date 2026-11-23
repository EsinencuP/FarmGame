using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.Animals;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.Farming;
using MyLittleFarm.Gameplay.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MyLittleFarm.Tests.PlayMode
{
    public sealed class AdaptiveFarmTests : FarmPlayModeFixture
    {
        private T Component<T>() where T : Component => Farm.GetComponentInChildren<T>(true);

        [UnityTest]
        public IEnumerator MarketPricesUseSuccessfulTradesAndRejectStaleQuotes()
        {
            yield return null;
            var market = Farm.gameObject.AddComponent<FarmMarket_AI>();
            var items = Component<InventorySystem_AI>();
            var wallet = Component<WalletSystem>();
            var save = Component<SaveSystem_AI>();
            market.Configure(Component<FarmCatalog>(), items, wallet, Component<SeedShopSystem>(),
                Component<ToolUpgradeSystem>(), Component<SectorSystem>(), Component<AnimalSystem_AI>(),
                Component<BuildSystem>(), Component<FarmProgression_AI>());
            Component<OnboardingSystem>().Restore(OnboardingSystem.StepCount, true, true, default, true);
            var booth = Farm.GetComponentsInChildren<InteractionTarget>().First(target => target.Kind == InteractionTargetKind.Market);
            Component<PlayerController>().Teleport(booth.transform.position + new Vector3(1.2f, 1, 0));
            wallet.SetCoins(1000);
            items.Add("carrot", 14, HarvestSize.XL);
            items.Add("tomato", 2);
            items.Add("pumpkin", 1);

            var carrotStack = HarvestQuality.StackId("carrot", HarvestSize.XL);
            var cart = new Dictionary<string, int> { [carrotStack] = 12 };
            Assert.That(market.Quote(FarmMarket_AI.Category.Sell, cart, out var original, out _), Is.True);
            Assert.That(market.Checkout(FarmMarket_AI.Category.Sell, cart, out var reason), Is.True, reason);
            Assert.That(wallet.Coins, Is.EqualTo(1000 + original));
            Assert.That(save.DailyOrders.SalePriceRule("carrot").percent, Is.EqualTo(75));
            Assert.That(save.DailyOrders.SalePriceRule("tomato").percent, Is.EqualTo(120));

            cart[carrotStack] = 1;
            Assert.That(market.Checkout(FarmMarket_AI.Category.Sell, cart, out _,
                new Dictionary<string, int> { [carrotStack] = original / 12 }), Is.False);
            Assert.That(items.GetAmount("carrot"), Is.EqualTo(2));
            Assert.That(market.Checkout(FarmMarket_AI.Category.Seeds,
                new Dictionary<string, int> { ["pumpkin_seed"] = 10 }, out reason), Is.True, reason);
            Assert.That(save.DailyOrders.SalePriceRule("pumpkin").percent, Is.EqualTo(90));
            save.Restore(RoundTrip(save.Capture()));
            Assert.That(save.DailyOrders.SalePriceRule("carrot").percent, Is.EqualTo(75));
            save.DailyOrders.Advance(DailyMarketOrders.DaySeconds);
            Assert.That(save.DailyOrders.SalePriceRule("carrot").percent, Is.EqualTo(100));
        }

        [UnityTest]
        public IEnumerator WateringAndSizedStacksSurviveTransfersAndMigration()
        {
            yield return null;
            var items = Component<InventorySystem_AI>();
            var build = Component<BuildSystem>();
            var save = Component<SaveSystem_AI>();
            var crop = Component<CropSystem_AI>();
            var soil = Component<SoilSystem>();
            Component<WalletSystem>().SetCoins(1000);
            Assert.That(build.TryPlace("storage", new Vector2Int(3, 3), 0, out var reason), Is.True, reason);
            var warehouse = build.Capture().Find(row => row.definitionId == "storage");
            var legacy = save.Capture();
            legacy.saveVersion = 19;
            legacy.inventory.Add(new InventoryEntryData { itemId = "carrot", amount = 4 });
            save.Restore(RoundTrip(legacy));
            var cell = new Vector2Int(1, 1);
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Assert.That(soil.Till(cell), Is.True);
            Assert.That(crop.Plant(cell, now), Is.True);
            Assert.That(crop.QualityHint(cell, now), Does.Contain("0%"));
            Assert.That(crop.TryWater(cell, now), Is.True);
            Assert.That(crop.TryWater(cell, now), Is.False);
            Assert.That(crop.QualityHint(cell, now), Does.Contain("70%"));
            save.Restore(RoundTrip(save.Capture()));
            Assert.That(crop.QualityHint(cell, now), Does.Contain("70%"));

            items.Add("carrot", 2, HarvestSize.XL);
            var key = HarvestQuality.StackId("carrot", HarvestSize.XL);
            var storage = build.Warehouses;
            Assert.That(storage.Transfer(warehouse.id, key, 2, true, "sized-deposit", items.Revision, storage.Revision, out reason), Is.True, reason);
            save.Restore(RoundTrip(save.Capture()));
            Assert.That(storage.Transfer(warehouse.id, key, 1, false, "sized-return", items.Revision, storage.Revision, out reason), Is.True, reason);
            Assert.That(storage.StackAmount(warehouse.id, key), Is.EqualTo(1));
            Assert.That(items.GetStackAmount(key), Is.EqualTo(1));
            Assert.That(items.GetStackAmount("carrot"), Is.EqualTo(4));
            Assert.That(HarvestQuality.Price(20, 75, HarvestSize.XL), Is.EqualTo(23));
        }

        [UnityTest]
        public IEnumerator TwoCollectedBatchesChooseAContractAndCannotBeClaimedTwice()
        {
            yield return null;
            var build = Component<BuildSystem>();
            var items = Component<InventorySystem_AI>();
            var save = Component<SaveSystem_AI>();
            Component<WalletSystem>().SetCoins(1000);
            Assert.That(build.TryPlace("mill", new Vector2Int(4, 3), 0, out var reason), Is.True, reason);
            var mill = build.Capture().Find(row => row.definitionId == "mill");
            var start = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 60000;
            items.Add("wheat", 3, HarvestSize.S);
            items.Add("wheat", 3, HarvestSize.XL);
            for (var batch = 0; batch < 2; batch++)
            {
                Assert.That(build.TryStartProduction(mill.id, start, out reason), Is.True, reason);
                Assert.That(build.TryCollectProduction(mill.id, start + 30000, out reason), Is.True, reason);
                Assert.That(build.TryCollectProduction(mill.id, start + 30000, out _), Is.False);
            }
            Assert.That(items.GetAmount("wheat"), Is.Zero);
            save.Restore(RoundTrip(save.Capture()));
            save.DailyOrders.Advance(DailyMarketOrders.DaySeconds);
            var order = save.DailyOrders.Current.Single(candidate => candidate.personalized);
            Assert.That(order.itemId, Is.EqualTo("flour"));
            Assert.That(order.amount, Is.LessThanOrEqualTo(items.GetAmount("flour")));
            var cart = new Dictionary<string, int> { [order.id] = 1 };
            Assert.That(save.DailyOrders.Complete(cart, out reason), Is.True, reason);
            save.Restore(RoundTrip(save.Capture()));
            Assert.That(save.DailyOrders.Complete(cart, out _), Is.False);
        }

        [UnityTest]
        public IEnumerator ShepherdPrefersDifferentTasksAfterObservedManualAndMissedEggs()
        {
            yield return null;
            var progress = Component<FarmProgression_AI>();
            var animals = Component<AnimalSystem_AI>();
            var items = Component<InventorySystem_AI>();
            var save = Component<SaveSystem_AI>();
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Component<WalletSystem>().SetCoins(1000);
            var booth = Farm.GetComponentsInChildren<InteractionTarget>().First(target => target.Kind == InteractionTargetKind.Market);
            Component<PlayerController>().Teleport(booth.transform.position + new Vector3(1.2f, 1, 0));
            Assert.That(progress.HireShepherd(), Is.True);
            Assert.That(animals.AddChicken(new Vector2Int(2, 3), now), Is.True);
            Assert.That(animals.AddChicken(new Vector2Int(3, 3), now), Is.True);
            Assert.That(animals.AddChicken(new Vector2Int(4, 3), now), Is.True);
            items.Add("wheat", 3, HarvestSize.S);
            var state = animals.Capture();
            MakeReady(state[0], now);
            MakeReady(state[1], now);
            animals.Restore(state);
            var observe = typeof(AnimalSystem_AI).GetMethod("ObserveReadyEggs", BindingFlags.Instance | BindingFlags.NonPublic);
            observe.Invoke(animals, new object[] { 12f });
            observe.Invoke(animals, new object[] { 12f });
            Assert.That(progress.Data.shepherdObservations, Is.EqualTo(new[] { false, false }));
            Assert.That(progress.Data.shepherdPriority, Is.EqualTo(ShepherdPriority.CollectFirst));
            save.Restore(RoundTrip(save.Capture()));
            var ready = animals.Capture();
            animals.AutomaticCare(now, ShepherdPriority.FeedFirst);
            Assert.That(items.GetAmount("wheat"), Is.EqualTo(2));
            Assert.That(items.GetAmount("egg"), Is.Zero);
            animals.Restore(ready);
            animals.AutomaticCare(now, progress.Data.shepherdPriority);
            Assert.That(items.GetAmount("egg"), Is.EqualTo(1));
            Assert.That(items.GetAmount("wheat"), Is.EqualTo(2));
            Assert.That(progress.Data.shepherdObservations, Is.EqualTo(new[] { false, false }));
            var refreshed = animals.Capture();
            MakeReady(refreshed[0], now + 1000);
            MakeReady(refreshed[1], now + 1000);
            animals.Restore(refreshed);
            Assert.That(animals.TryCollect(refreshed[0].animalId, out _), Is.True);
            Assert.That(animals.TryCollect(refreshed[1].animalId, out _), Is.True);
            Assert.That(progress.Data.shepherdPriority, Is.EqualTo(ShepherdPriority.FeedFirst));
        }

        private static void MakeReady(AnimalRuntimeState animal, long now)
        {
            animal.lastFedAtUnixMs = now - 60000;
            animal.nextProductAtUnixMs = now;
            animal.readyProductCount = 1;
            animal.shepherdWaitSeconds = 0;
            animal.shepherdObserved = false;
        }

        private static SaveData RoundTrip(SaveData data) => JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
    }
}
