using System;
using System.Collections;
using System.Collections.Generic;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Animals;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MyLittleFarm.Tests.PlayMode
{
    // Prepared during U1-1. Run only at closure of U1 per AGENTS.md.
    public sealed class CowIntegrationTests : FarmPlayModeFixture
    {
        private T System<T>() where T : Component => Farm.GetComponentInChildren<T>(true);
        private string PrepareBarn()
        {
            System<SectorSystem>().Restore(true);
            System<WalletSystem>().SetCoins(500);
            Assert.That(System<BuildSystem>().TryPlace("barn", new Vector2Int(10,0),0,out var reason), Is.True,reason);
            return System<BuildSystem>().GetAt(new Vector2Int(10,0)).id;
        }

        [UnityTest]
        public IEnumerator CowAndPendingMilkKeepIdentityThroughBarnMoveAndJsonRestore()
        {
            yield return null;
            var home=PrepareBarn(); var animals=System<AnimalSystem_AI>();
            var now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Assert.That(animals.TryPurchase(new Dictionary<string,int>{{"cow",1}},now,out var reason),Is.True,reason);
            var cow=animals.Capture().Find(a=>a.definitionId=="cow");
            System<InventorySystem_AI>().Add("wheat",2);
            Assert.That(animals.Feed(cow.animalId,now),Is.True);
            Assert.That(animals.Feed(cow.animalId,now),Is.False);
            Assert.That(System<InventorySystem_AI>().GetAmount("wheat"),Is.EqualTo(1));
            var pending=animals.Capture()[0].nextProductAtUnixMs;
            Assert.That(System<BuildSystem>().TryMove(home,new Vector2Int(10,0),1,out reason),Is.True,reason);
            var snapshot=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(System<SaveSystem_AI>().Capture()));
            System<SaveSystem_AI>().Restore(snapshot);
            var restored=animals.Capture().Find(a=>a.animalId==cow.animalId);
            Assert.That(restored.homeBuildingId,Is.EqualTo(home));
            Assert.That(restored.nextProductAtUnixMs,Is.EqualTo(pending));
            Assert.That(BarnHabitat.TryCell(System<BuildSystem>().GetBuilding(home),restored.homeSlot,out var position),Is.True);
            Assert.That(restored.Position,Is.EqualTo(position));
            Assert.That(System<BuildSystem>().TryRemove(home,out _),Is.False);
        }

        [UnityTest]
        public IEnumerator MissingOrFullBarnNeverChargesAndMilkCannotBeCollectedTwice()
        {
            yield return null;
            var animals=System<AnimalSystem_AI>(); var wallet=System<WalletSystem>();wallet.SetCoins(500);
            var now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Assert.That(animals.TryPurchase(new Dictionary<string,int>{{"cow",1}},now,out _),Is.False);
            Assert.That(wallet.Coins,Is.EqualTo(500));
            PrepareBarn();
            Assert.That(animals.TryPurchase(new Dictionary<string,int>{{"cow",2}},now,out var reason),Is.True,reason);
            var balance=wallet.Coins;
            Assert.That(animals.TryPurchase(new Dictionary<string,int>{{"cow",1}},now,out _),Is.False);
            Assert.That(wallet.Coins,Is.EqualTo(balance));
            var ready=animals.Capture();ready[0].readyProductCount=1;ready[0].lastFedAtUnixMs=now-61000;ready[0].nextProductAtUnixMs=now-1000;
            animals.Restore(ready);
            var id=ready[0].animalId;
            Action listener=()=>animals.TryCollect(id,out _);
            GameEvents.InventoryChanged+=listener;
            try { Assert.That(animals.TryCollect(id,out var amount),Is.True);Assert.That(amount,Is.EqualTo(1)); }
            finally {GameEvents.InventoryChanged-=listener;}
            Assert.That(animals.TryCollect(id,out _),Is.False);
            Assert.That(System<InventorySystem_AI>().GetAmount("milk"),Is.EqualTo(1));
            Assert.That(System<SellingSystem>().SellAllCrops(),Is.EqualTo(20));
        }

        [UnityTest]
        public IEnumerator V12ChickenSaveStillRestoresOriginalIdAndEggProduction()
        {
            yield return null;
            var animals=System<AnimalSystem_AI>();var now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Assert.That(animals.AddChicken(new Vector2Int(1,3),now),Is.True);
            System<InventorySystem_AI>().Add("wheat",1);
            var chicken=animals.Capture()[0];Assert.That(animals.Feed(chicken.animalId,now),Is.True);
            var legacy=System<SaveSystem_AI>().Capture();legacy.saveVersion=12;
            System<SaveSystem_AI>().Restore(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(legacy)));
            var restored=animals.Capture()[0];
            Assert.That(restored.animalId,Is.EqualTo(chicken.animalId));
            Assert.That(restored.definitionId,Is.EqualTo("chicken"));
            Assert.That(restored.nextProductAtUnixMs,Is.EqualTo(now+40000));
            Assert.That(animals.CowCount,Is.Zero);
        }
    }
}
