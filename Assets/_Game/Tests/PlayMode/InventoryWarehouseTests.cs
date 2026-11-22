using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.Gameplay.Economy;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MyLittleFarm.Tests.PlayMode
{
    // Prepared in U1-5. Execute only at major phase closure, per AGENTS.md.
    public sealed class InventoryWarehouseTests : FarmPlayModeFixture
    {
        private T System<T>() where T:Component=>Farm.GetComponentInChildren<T>(true);
        private string Place()
        {
            System<WalletSystem>().SetCoins(1000);var build=System<BuildSystem>();
            Assert.That(build.TryPlace("storage",new Vector2Int(1,1),0,out var reason),Is.True,reason);
            return build.Capture().Find(b=>b.definitionId=="storage").id;
        }
        [UnityTest]
        public IEnumerator TransfersRejectReplayReentryAndBulkOverflowWithoutPartialChanges()
        {
            yield return null;
            var id=Place();var inv=System<InventorySystem>();var storage=System<BuildSystem>().Warehouses;
            inv.Add("wheat",20);var invRevision=inv.Revision;var storageRevision=storage.Revision;
            var token=Guid.NewGuid().ToString("N");var reentered=false;
            Action observer=()=>reentered=storage.Transfer(id,"wheat",7,true,"reentry",inv.Revision,storage.Revision,out _);
            GameEvents.InventoryChanged+=observer;
            try{Assert.That(storage.Transfer(id,"wheat",7,true,token,invRevision,storageRevision,out _),Is.True);}
            finally{GameEvents.InventoryChanged-=observer;}
            Assert.That(reentered,Is.False);
            Assert.That(storage.Transfer(id,"wheat",7,true,token,inv.Revision,storage.Revision,out _),Is.False);
            Assert.That(inv.GetAmount("wheat"),Is.EqualTo(13));Assert.That(storage.Amount(id,"wheat"),Is.EqualTo(7));
            storage.Restore(new List<WarehouseData>{new WarehouseData{buildingId=id,items=new List<InventoryEntryData>{new InventoryEntryData{itemId="wheat",amount=int.MaxValue-5}}}});
            var carrots=inv.GetAmount("carrot_seed");
            Assert.That(storage.TransferAll(id,"overflow",inv.Revision,storage.Revision,out _),Is.False);
            Assert.That(inv.GetAmount("carrot_seed"),Is.EqualTo(carrots));Assert.That(storage.Amount(id,"carrot_seed"),Is.Zero);
            Assert.That(inv.GetAmount("wheat"),Is.EqualTo(13));Assert.That(storage.Amount(id,"wheat"),Is.EqualTo(int.MaxValue-5));
        }
        [UnityTest]
        public IEnumerator ReferencesToolsAndEmptySlotRoundTripAndV15KeepsStock()
        {
            yield return null;
            Place();var inv=System<InventorySystem>();var slots=System<QuickSlotSystem>();var save=System<SaveSystem>();
            inv.Add("wheat",11);var before=JsonUtility.ToJson(new WarehouseData{items=inv.Capture()});
            Assert.That(slots.Assign("wheat",0),Is.True);Assert.That(slots.Swap(0,4),Is.True);slots.Clear(1);slots.SelectTool(QuickSlotSystem.SelectionMode.Hoe);
            save.Restore(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save.Capture())));
            Assert.That(slots.ItemAt(4),Is.EqualTo("wheat"));Assert.That(slots.ItemAt(1),Is.Null);Assert.That(slots.Mode,Is.EqualTo(QuickSlotSystem.SelectionMode.Hoe));
            Assert.That(JsonUtility.ToJson(new WarehouseData{items=inv.Capture()}),Is.EqualTo(before));
            var old=save.Capture();old.saveVersion=15;old.quickSlotItems=null;old.warehouses=null;
            var path=Path.Combine(Application.temporaryCachePath,"U15-"+Guid.NewGuid().ToString("N")+".json");File.WriteAllText(path,JsonUtility.ToJson(old));
            try{Assert.That(save.LoadFromPath(path),Is.True);}finally{File.Delete(path);}
            CollectionAssert.AreEqual(QuickSlotSystem.DefaultAssignments(),slots.Capture());
            Assert.That(inv.GetAmount("wheat"),Is.EqualTo(11));Assert.That(save.Capture().warehouses[0].items,Is.Empty);
        }
        [UnityTest]
        public IEnumerator FilledBuildingMovesAndLoadsByIdAndOnlyEmptyBuildingCanBeRemoved()
        {
            yield return null;
            var id=Place();var build=System<BuildSystem>();var inv=System<InventorySystem>();var save=System<SaveSystem>();
            inv.Add("milk",5);Assert.That(build.Warehouses.Transfer(id,"milk",5,true,"put",inv.Revision,build.Warehouses.Revision,out _),Is.True);
            Assert.That(build.TryRemove(id,out _),Is.False);Assert.That(build.TryMove(id,new Vector2Int(4,3),1,out var reason),Is.True,reason);
            save.Restore(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save.Capture())));
            Assert.That(build.Warehouses.Amount(id,"milk"),Is.EqualTo(5));Assert.That(build.GetBuilding(id).quarterTurns,Is.EqualTo(1));
            Assert.That(build.Warehouses.Transfer(id,"milk",5,false,"take",inv.Revision,build.Warehouses.Revision,out _),Is.True);
            Assert.That(build.TryRemove(id,out _),Is.True);Assert.That(inv.GetAmount("milk"),Is.EqualTo(5));
        }
    }
}
