using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    // Prepared during U1-7; execute with the phase suites at U1-8, not individually.
    public sealed class ExtraFarmSystemsTests:FarmPlayModeFixture
    {
        private T System<T>() where T:Component=>Farm.GetComponentInChildren<T>(true);
        [UnityTest]
        public IEnumerator RepeatPlantsKeepTheirCellAndTimerWithoutSpendingAnotherSeed()
        {
            yield return null;
            var catalog=System<FarmCatalog>();Assert.That(catalog.Crops.Count,Is.EqualTo(8));
            var cells=new[]{new Vector2Int(1,1),new Vector2Int(2,1),new Vector2Int(3,1)};
            var ids=new[]{"strawberry","tomato","pumpkin"};var items=System<InventorySystem_AI>();var slots=System<QuickSlotSystem>();
            var grid=System<GridSystem>();System<PlayerController>().Teleport(grid.CellToWorld(cells[1])+Vector3.up*1.1f);
            var actions=System<InteractionSystem_AI>();var now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            for(var i=0;i<3;i++)
            {System<SoilSystem>().Till(cells[i]);items.Add(ids[i]+"_seed",1);slots.Assign(ids[i]+"_seed",i);slots.Select(i);Assert.That(actions.InteractWithCell(cells[i],now-120000),Is.True);}
            yield return new WaitForSecondsRealtime(.2f);
            for(var i=0;i<3;i++)Assert.That(actions.InteractWithCell(cells[i],now),Is.True);
            var crops=System<CropSystem_AI>();var snapshot=System<SaveSystem_AI>().Capture();
            Assert.That(snapshot.crops.Count(c=>c.isRegrowing),Is.EqualTo(2));Assert.That(crops.Contains(cells[2]),Is.False);Assert.That(grid.CanPlant(cells[2]),Is.True);
            foreach(var c in snapshot.crops.Where(c=>c.isRegrowing)){Assert.That(c.GetStage(now),Is.EqualTo(1));Assert.That(c.IsMature(now),Is.False);}
            System<SaveSystem_AI>().Restore(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(snapshot)));
            Assert.That(actions.InteractWithCell(cells[0],now),Is.False);
            var before=items.GetAmount("strawberry");yield return new WaitForSecondsRealtime(.2f);
            Assert.That(actions.InteractWithCell(cells[0],now+30000),Is.True);
            Assert.That(items.GetAmount("strawberry"),Is.EqualTo(before+3));Assert.That(items.GetAmount("strawberry_seed"),Is.Zero);
            Assert.That(actions.InteractWithCell(cells[0],now+30000),Is.False);
        }
        [UnityTest]
        public IEnumerator FeederAndShepherdChargeOneRationAndContentsMoveAndRestore()
        {
            yield return null;
            var build=System<BuildSystem>();System<WalletSystem>().SetCoins(1000);
            Assert.That(build.TryPlace("feeder",new Vector2Int(3,2),0,out var reason),Is.True,reason);
            var id=build.Capture().Find(b=>b.definitionId=="feeder").id;var animals=System<AnimalSystem_AI>();var now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Assert.That(animals.AddChicken(new Vector2Int(2,3),now),Is.True);var chicken=animals.Capture().First(a=>a.definitionId=="chicken").animalId;
            var items=System<InventorySystem_AI>();items.Add("wheat",12);
            Assert.That(build.Feeders.Transfer(id,12,true,items.Revision,build.Feeders.Revision,out _),Is.True);
            Assert.That(animals.Feed(chicken,now),Is.True);Assert.That(animals.Feed(chicken,now),Is.False);animals.AutomaticCare(now);
            Assert.That(build.Feeders.Amount(id),Is.EqualTo(11));Assert.That(items.GetAmount("wheat"),Is.Zero);
            Assert.That(build.TryRemove(id,out _),Is.False);Assert.That(build.TryMove(id,new Vector2Int(4,2),0,out reason),Is.True,reason);
            var save=System<SaveSystem_AI>();save.Restore(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save.Capture())));
            Assert.That(build.Feeders.Amount(id),Is.EqualTo(11));
            Assert.That(build.Feeders.Transfer(id,11,false,items.Revision,build.Feeders.Revision,out _),Is.True);
            Assert.That(build.TryRemove(id,out _),Is.True);Assert.That(items.GetAmount("wheat"),Is.EqualTo(11));
        }
        [UnityTest]
        public IEnumerator DailyOrdersSurviveReloadRejectReentryAndAdvanceOnlyTheirOwnClock()
        {
            yield return null;
            var save=System<SaveSystem_AI>();var orders=save.DailyOrders;var items=System<InventorySystem_AI>();var money=System<WalletSystem>();
            var order=orders.Current[0];items.Add(order.itemId,order.amount);var cart=new Dictionary<string,int>{{order.id,1}};
            var before=money.Coins;var reentered=false;Action observer=()=>reentered|=orders.Complete(cart,out _);
            GameEvents.InventoryChanged+=observer;
            try{Assert.That(orders.Complete(cart,out _),Is.True);}finally{GameEvents.InventoryChanged-=observer;}
            Assert.That(reentered,Is.False);Assert.That(money.Coins,Is.EqualTo(before+order.unitPrice*order.amount+order.bonus));
            var saved=save.Capture();save.Restore(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(saved)));
            Assert.That(orders.Complete(cart,out _),Is.False);orders.Advance(839);Assert.That(orders.Day,Is.Zero);
            var partial=save.Capture();orders.Advance(1);var next=JsonUtility.ToJson(orders.Capture());
            save.Restore(partial);orders.Advance(1);Assert.That(JsonUtility.ToJson(orders.Capture()),Is.EqualTo(next));
            Assert.That(orders.Day,Is.EqualTo(1));Assert.That(orders.Current.Count,Is.InRange(1,3));Assert.That(orders.Complete(cart,out _),Is.False);
            var legacy=save.Capture();legacy.saveVersion=17;legacy.marketOrders=null;save.Restore(legacy);var first=JsonUtility.ToJson(orders.Capture());
            save.Restore(legacy);Assert.That(JsonUtility.ToJson(orders.Capture()),Is.EqualTo(first));
        }
    }
}
