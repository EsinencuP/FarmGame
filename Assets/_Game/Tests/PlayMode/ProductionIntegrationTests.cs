using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MyLittleFarm.Tests.PlayMode
{
    // Prepared during U1-3. Run only at major phase closure under AGENTS.md.
    public sealed class ProductionIntegrationTests : FarmPlayModeFixture
    {
        private T System<T>() where T:Component => Farm.GetComponentInChildren<T>(true);
        private BuildingRuntimeState Place(string definition,int x,int z)
        {
            var build=System<BuildSystem>();System<WalletSystem>().SetCoins(1000);
            Assert.That(build.TryPlace(definition,new Vector2Int(x,z),0,out var reason),Is.True,reason);
            return build.Capture().Find(b=>b.definitionId==definition);
        }
        [UnityTest]
        public IEnumerator IndependentBatchesMoveAndRoundTripWithoutDoubleCollection()
        {
            yield return null;
            var build=System<BuildSystem>();var items=System<InventorySystem>();var save=System<SaveSystem>();
            System<SectorSystem>().Restore(true);
            var press=Place("apple_press",1,1);var mill=Place("mill",4,3);var dairy=Place("cheese_dairy",11,1);
            items.Add("apple",3);items.Add("wheat",3);items.Add("milk",2);
            var now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            foreach(var b in new[]{press,mill,dairy}) Assert.That(build.TryStartProduction(b.id,now,out _),Is.True);
            Assert.That(build.TryStartProduction(mill.id,now,out _),Is.False);
            Assert.That(build.TryRemove(dairy.id,out _),Is.False);
            Assert.That(build.TryMove(press.id,new Vector2Int(1,4),1,out _),Is.True);
            var copy=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save.Capture()));
            save.Restore(copy);
            Assert.That(save.Capture().productionBatches.Count,Is.EqualTo(3));
            Assert.That(build.TryCollectProduction(dairy.id,now+89999,out _),Is.False);
            foreach(var b in new[]{press,mill,dairy})
            { Assert.That(build.TryCollectProduction(b.id,now+90000,out _),Is.True);Assert.That(build.TryCollectProduction(b.id,now+90000,out _),Is.False); }
            Assert.That(items.GetAmount("flour"),Is.EqualTo(1));Assert.That(items.GetAmount("cheese"),Is.EqualTo(1));Assert.That(items.GetAmount("apple_juice"),Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator CapacityFailureAndReentrantObserverKeepExactlyOneBatch()
        {
            yield return null;
            var build=System<BuildSystem>();var items=System<InventorySystem>();var mill=Place("mill",1,1);
            var now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Assert.That(build.TryStartProduction(mill.id,now,out _),Is.False);
            items.Add("wheat",6);
            Action observer=()=>build.TryStartProduction(mill.id,now,out _);
            GameEvents.InventoryChanged+=observer;
            try { Assert.That(build.TryStartProduction(mill.id,now,out _),Is.True); }
            finally { GameEvents.InventoryChanged-=observer; }
            Assert.That(items.GetAmount("wheat"),Is.EqualTo(3));items.Add("flour",int.MaxValue);
            Assert.That(build.TryCollectProduction(mill.id,now+30000,out _),Is.False);
            Assert.That(System<SaveSystem>().Capture().productionBatches.Count,Is.EqualTo(1));
            items.TryRemove("flour",int.MaxValue);observer=()=>build.TryCollectProduction(mill.id,now+30000,out _);
            GameEvents.InventoryChanged+=observer;
            try { Assert.That(build.TryCollectProduction(mill.id,now+30000,out _),Is.True); }
            finally { GameEvents.InventoryChanged-=observer; }
            Assert.That(items.GetAmount("flour"),Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator CornMillUsesItsRecipeAcrossSaveAndLoad()
        {
            yield return null;
            var build = System<BuildSystem>();
            var items = System<InventorySystem>();
            var save = System<SaveSystem>();
            var mill = Place("mill", 1, 1);
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            items.Add("wheat", 3);
            items.Add("corn", 2);
            Assert.That(build.TryStartProduction(mill.id, ProductionRecipe.Dairy.Id, now, out _), Is.False);
            Assert.That(build.TryStartProduction(mill.id, ProductionRecipe.CornMill.Id, now, out _), Is.True);
            Assert.That(items.GetAmount("corn"), Is.Zero);
            Assert.That(items.GetAmount("wheat"), Is.EqualTo(3));
            Assert.That(build.TryStartProduction(mill.id, ProductionRecipe.Mill.Id, now, out _), Is.False);
            save.Restore(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save.Capture())));
            Assert.That(save.Capture().productionBatches[0].recipeId, Is.EqualTo(ProductionRecipe.CornMill.Id));
            Assert.That(build.TryCollectProduction(mill.id, now + 30000, out _), Is.True);
            Assert.That(build.TryCollectProduction(mill.id, now + 30000, out _), Is.False);
            Assert.That(items.GetAmount("flour"), Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator LegacyV12PressKeepsDeadlineAndOutputAndRejectsDuplicates()
        {
            yield return null;
            var build=System<BuildSystem>();var press=Place("apple_press",1,1);var save=System<SaveSystem>();
            var old=save.Capture();old.saveVersion=12;old.selectedTreeId=null;
            var deadline=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()+12000;
            old.applePressBatches=new List<ApplePressBatchData>{new ApplePressBatchData{buildingId=press.id,readyAtUnixMs=deadline}};
            old.productionBatches=null;
            var file=Path.Combine(Application.temporaryCachePath,"U13-"+Guid.NewGuid().ToString("N")+".json");
            File.WriteAllText(file,JsonUtility.ToJson(old));
            try { Assert.That(save.LoadFromPath(file),Is.True); }
            finally { File.Delete(file); }
            Assert.That(save.Capture().productionBatches[0].readyAtUnixMs,Is.EqualTo(deadline));
            Assert.That(build.TryCollectProduction(press.id,deadline-1,out _),Is.False);
            Assert.That(build.TryCollectProduction(press.id,deadline,out _),Is.True);
            Assert.That(System<InventorySystem>().GetAmount("apple_juice"),Is.EqualTo(1));
            var bad=save.Capture();bad.productionBatches.Add(new ProductionBatchData {buildingId=press.id,recipeId="wheat_to_flour",outputItemId="flour",outputAmount=1,readyAtUnixMs=deadline,startedAtUnixMs=deadline-30000});
            Assert.Throws<InvalidDataException>(()=>save.Restore(bad));
            Assert.That(System<InventorySystem>().GetAmount("apple_juice"),Is.EqualTo(1));
        }
    }
}
