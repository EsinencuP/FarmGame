using System;
using System.Collections;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.Farming;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MyLittleFarm.Tests.PlayMode
{
    // Prepared in U1-2; execution is deferred to the major U1 closure.
    public sealed class OrchardVarietyTests : FarmPlayModeFixture
    {
        private T System<T>() where T : Component => Farm.GetComponentInChildren<T>(true);

        [UnityTest]
        public IEnumerator MixedOrchardRestoresSpeciesTimersAndSelectedSapling()
        {
            yield return null;
            var orchard=System<OrchardSystem_AI>();var items=System<InventorySystem_AI>();
            var apple=new Vector2Int(1,1);var pear=new Vector2Int(2,1);
            var now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();items.Add("pear_sapling",1);
            Assert.That(orchard.PlantApple(apple,now-46000),Is.True);
            Assert.That(orchard.SelectTree("pear"),Is.True);
            Assert.That(orchard.PlantSelected(pear,now-46000),Is.True);
            Assert.That(orchard.TryHarvest(apple,now,out var apples),Is.True);
            Assert.That(orchard.TryHarvest(pear,now,out var pears),Is.True);
            Assert.That(apples,Is.EqualTo(3));Assert.That(pears,Is.EqualTo(3));
            var snapshot=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(System<SaveSystem_AI>().Capture()));
            System<SaveSystem_AI>().Restore(snapshot);
            Assert.That(orchard.SelectedTreeId,Is.EqualTo("pear"));
            Assert.That(orchard.Capture().Find(t=>t.Position==pear).lastHarvestAtUnixMs,Is.EqualTo(now));
            Assert.That(orchard.TryHarvest(apple,now+45000,out _),Is.True);
            Assert.That(orchard.TryHarvest(pear,now+45000,out _),Is.False);
            Assert.That(orchard.TryHarvest(pear,now+60000,out _),Is.True);
            Assert.That(items.GetAmount("pear_sapling"),Is.Zero);
        }

        [UnityTest]
        public IEnumerator OccupiedPlantingDoesNotConsumeSaplingAndOldAppleAliasSurvives()
        {
            yield return null;
            var orchard=System<OrchardSystem_AI>();var items=System<InventorySystem_AI>();
            var now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();var cell=new Vector2Int(1,1);
            Assert.That(orchard.PlantApple(cell,now),Is.True);
            items.Add("pear_sapling",1);
            Assert.That(orchard.Plant(cell,"pear",now),Is.False);
            Assert.That(items.GetAmount("pear_sapling"),Is.EqualTo(1));
            var old=System<SaveSystem_AI>().Capture();old.saveVersion=13;old.selectedTreeId=null;
            old.trees[0].treeId="apple_tree";
            System<SaveSystem_AI>().Restore(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(old)));
            Assert.That(orchard.Capture()[0].treeId,Is.EqualTo("apple_tree"));
            Assert.That(orchard.SelectedDefinition.FruitItemId,Is.EqualTo("apple"));
            Assert.That(orchard.TryHarvest(cell,now+30000,out _),Is.True);
            Assert.That(items.GetAmount("apple"),Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator FullInventoryKeepsReadyPearAndInventoryObserverCannotHarvestTwice()
        {
            yield return null;
            var orchard=System<OrchardSystem_AI>();var items=System<InventorySystem_AI>();
            var now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();var cell=new Vector2Int(1,1);
            items.Add("pear_sapling",1);Assert.That(orchard.Plant(cell,"pear",now-46000),Is.True);
            items.Add("pear",int.MaxValue);
            Assert.That(orchard.TryHarvest(cell,now,out _),Is.False);
            Assert.That(orchard.Capture()[0].lastHarvestAtUnixMs,Is.Zero);
            items.TryRemove("pear",int.MaxValue);
            Action observer=()=>orchard.TryHarvest(cell,now,out _);
            GameEvents.InventoryChanged+=observer;
            try{Assert.That(orchard.TryHarvest(cell,now,out _),Is.True);}
            finally{GameEvents.InventoryChanged-=observer;}
            Assert.That(items.GetAmount("pear"),Is.EqualTo(3));
            Assert.That(orchard.TryHarvest(cell,now,out _),Is.False);
        }
    }
}
