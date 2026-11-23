using System;
using System.Collections;
using System.IO;
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
    // Prepared for U1 phase closure. Do not run individual points under AGENTS.md.
    public sealed class AreaToolTests:FarmPlayModeFixture
    {
        private T System<T>() where T:Component=>Farm.GetComponentInChildren<T>(true);
        [UnityTest]
        public IEnumerator PreviewBudgetMatchesPlantingAndRepeatDoesNotDuplicateHarvest()
        {
            yield return null;
            var c=new Vector2Int(3,2);var grid=System<GridSystem>();var actions=System<InteractionSystem_AI>();
            var player = System<PlayerController>();
            var slots=System<QuickSlotSystem>();var tools=System<ToolUpgradeSystem>();tools.Restore(2);
            var now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            slots.Select(0);
            var status = string.Empty;
            void CaptureStatus(string text) => status = text;
            GameEvents.StatusChanged += CaptureStatus;
            try
            {
                player.Teleport(grid.CellToWorld(c + Vector2Int.left * 8) + Vector3.up * 1.1f);
                Assert.That(actions.InteractWithCell(c, now), Is.False);
                Assert.That(status, Does.Contain("слишком далеко"));
                player.Teleport(grid.CellToWorld(c) + Vector3.up * 1.1f);
                Assert.That(actions.InteractWithCell(c, now), Is.False, "Seeds must not till grass");
                Assert.That(status, Does.Contain("Сначала обработайте землю мотыгой"));
            }
            finally { GameEvents.StatusChanged -= CaptureStatus; }
            slots.SelectTool(QuickSlotSystem.SelectionMode.Hoe);
            Assert.That(actions.PlanField(c,now).Allowed,Is.EqualTo(9));Assert.That(actions.InteractWithCell(c,now),Is.True);
            yield return new WaitForSecondsRealtime(.2f);
            var items=System<InventorySystem_AI>();items.TryRemove("carrot_seed",items.GetAmount("carrot_seed"));items.Add("carrot_seed",4);slots.Select(0);
            var preview=actions.PlanField(c,now);Assert.That(preview.Allowed,Is.EqualTo(4));
            var reentered=false;Action observer=()=>reentered|=actions.InteractWithCell(c,now);
            GameEvents.InventoryChanged+=observer;
            try{Assert.That(actions.InteractWithCell(c,now),Is.True);}finally{GameEvents.InventoryChanged-=observer;}
            Assert.That(reentered,Is.False);Assert.That(items.GetAmount("carrot_seed"),Is.Zero);
            Assert.That(System<CropSystem_AI>().Capture().Count,Is.EqualTo(4));Assert.That(actions.InteractWithCell(c,now),Is.False);
            yield return new WaitForSecondsRealtime(.2f);
            var mature=now+60000;Assert.That(actions.PlanField(c,mature).Allowed,Is.EqualTo(4));
            Assert.That(actions.InteractWithCell(c,mature),Is.True);var collected=items.GetAmount("carrot");
            Assert.That(collected, Is.InRange(4 * CropSystem_AI.MinHarvestYield, 4 * CropSystem_AI.MaxHarvestYield));
            Assert.That(actions.InteractWithCell(c,mature),Is.False);Assert.That(items.GetAmount("carrot"),Is.EqualTo(collected));
        }
        [UnityTest]
        public IEnumerator RotationAndProtectedCellsMatchTheCommittedMask()
        {
            yield return null;
            var grid=System<GridSystem>();var actions=System<InteractionSystem_AI>();var c=new Vector2Int(3,4);
            System<PlayerController>().Teleport(grid.CellToWorld(c)+Vector3.up*1.1f);
            System<QuickSlotSystem>().SelectTool(QuickSlotSystem.SelectionMode.Hoe);System<ToolUpgradeSystem>().Restore(1);
            var now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();var initial=actions.PlanField(c,now);actions.RotateField();var rotated=actions.PlanField(c,now);
            Assert.That(initial.Cells.Count,Is.EqualTo(3));Assert.That(initial.Direction.x*rotated.Direction.x+initial.Direction.y*rotated.Direction.y,Is.Zero);
            System<ToolUpgradeSystem>().Restore(2);System<WalletSystem>().SetCoins(1000);
            Assert.That(System<BuildSystem>().TryPlace("storage",new Vector2Int(4,3),0,out var reason),Is.True,reason);
            grid.SetCellType(new Vector2Int(2,3),CellType.Water);
            Assert.That(System<AnimalSystem_AI>().AddChicken(new Vector2Int(2,4),now),Is.True);
            var plan=actions.PlanField(c,now);Assert.That(plan.Allowed,Is.LessThan(9));
            Assert.That(plan.Cells.First(p=>p.Position==new Vector2Int(2,4)).SkipReason,Is.EqualTo("животное"));
            Assert.That(actions.InteractWithCell(c,now),Is.True);
            foreach(var cell in plan.Cells)if(cell.Allowed)Assert.That(grid.CanPlant(cell.Position),Is.True);
            Assert.That(grid.GetCellType(new Vector2Int(2,3)),Is.EqualTo(CellType.Water));
            Assert.That(grid.GetCellType(new Vector2Int(2,4)),Is.EqualTo(CellType.Grass));
            Assert.That(grid.GetCellType(new Vector2Int(4,3)),Is.EqualTo(CellType.Building));
        }
        [UnityTest]
        public IEnumerator OlderPaidLevelsConvertToAreasAndPermanentToolGrantsOnce()
        {
            yield return null;
            var save=System<SaveSystem_AI>();var tools=System<ToolUpgradeSystem>();var items=System<InventorySystem_AI>();
            var old=save.Capture();old.saveVersion=16;old.toolUpgradeLevel=2;old.baseHoeGranted=false;
            var path=Path.Combine(Application.temporaryCachePath,"U16-"+Guid.NewGuid().ToString("N")+".json");
            File.WriteAllText(path,JsonUtility.ToJson(old));
            try{Assert.That(save.LoadFromPath(path),Is.True);Assert.That(save.LoadFromPath(path),Is.True);}finally{File.Delete(path);}
            Assert.That(tools.Level,Is.EqualTo(2));Assert.That(tools.AreaLabel,Is.EqualTo("3×3"));Assert.That(tools.HasBaseHoe,Is.True);
            CollectionAssert.AreEqual(old.inventory.Select(i=>i.amount),items.Capture().Select(i=>i.amount));
            System<QuickSlotSystem>().SelectTool(QuickSlotSystem.SelectionMode.Hoe);
            save.Restore(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save.Capture())));
            Assert.That(tools.Level,Is.EqualTo(2));Assert.That(save.Capture().baseHoeGranted,Is.True);
            Assert.That(System<QuickSlotSystem>().Mode,Is.EqualTo(QuickSlotSystem.SelectionMode.Hoe));
        }
    }
}
