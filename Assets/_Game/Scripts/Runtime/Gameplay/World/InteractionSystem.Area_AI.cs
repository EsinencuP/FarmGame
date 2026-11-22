using System;
using System.Collections.Generic;
using System.Linq;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.Economy;
using UnityEngine;

namespace MyLittleFarm.Gameplay.World
{
    public sealed partial class InteractionSystem_AI
    {
        private const string RequiresTilledSoil = "сначала обработайте землю мотыгой";

        public enum FieldAction { None, Till, Plant, Harvest }
        public sealed class FieldCell
        {
            public Vector2Int Position;
            public string SkipReason;
            public bool Allowed=>SkipReason==null;
        }
        public sealed class FieldPlan
        {
            public FieldAction Action;
            public Vector2Int Center,Direction;
            public string CropId,SeedId;
            public readonly List<FieldCell> Cells=new List<FieldCell>(9);
            public int Allowed=>Cells.Count(c=>c.Allowed);
            public string Skips=>string.Join("; ",Cells.Where(c=>!c.Allowed).GroupBy(c=>c.SkipReason).Select(g=>g.Key+": "+g.Count()));
        }
        private int _fieldTurns;
        private bool _applyingField;
        private readonly Dictionary<Vector2Int,float> _workedUntil=new Dictionary<Vector2Int,float>();
        private HeldHoeView _heldHoe;
        public void RotateField()=>_fieldTurns=(_fieldTurns+1)%2;
        public void ResetFieldInteraction(){_workedUntil.Clear();_fieldTurns=0;}
        public FieldPlan PlanField(Vector2Int center,long nowUnixMs)
        {
            var plan=new FieldPlan{Center=center,Direction=Vector2Int.up};
            if(_grid==null || _inventory==null || _crops==null)return plan;
            if(_orchard?.Contains(center)==true)return plan;
            if(_slots?.Mode==QuickSlotSystem.SelectionMode.Item && _orchard!=null && _orchard.TryGetSapling(_slots.SelectedItemId,out _))return plan;
            if(_crops.TryGet(center,out var crop)){plan.Action=FieldAction.Harvest;plan.CropId=crop.cropId;}
            else if(_slots?.Mode==QuickSlotSystem.SelectionMode.Hoe && (_upgrades==null || _upgrades.HasBaseHoe))plan.Action=FieldAction.Till;
            else if(_slots?.SelectedCrop!=null){plan.Action=FieldAction.Plant;plan.CropId=_slots.SelectedCrop.CropId;plan.SeedId=_slots.SelectedCrop.SeedItemId;}
            // Compatibility for small fixtures that configure no slot system at all.
            else if(_slots==null){plan.Action=_grid.CanTill(center)?FieldAction.Till:FieldAction.Plant;plan.CropId="carrot";plan.SeedId="carrot_seed";}
            if(plan.Action==FieldAction.None)return plan;
            var delta=_player==null?Vector3.forward:_grid.CellToWorld(center)-_player.position;
            if(delta.x*delta.x+delta.z*delta.z<.01f)delta=_player==null?Vector3.forward:_player.forward;
            plan.Direction=Mathf.Abs(delta.x)>Mathf.Abs(delta.z)?new Vector2Int(delta.x>=0?1:-1,0):new Vector2Int(0,delta.z>=0?1:-1);
            if(_fieldTurns==1)plan.Direction=new Vector2Int(-plan.Direction.y,plan.Direction.x);
            var side=new Vector2Int(plan.Direction.y,-plan.Direction.x);
            var offsets=new List<Vector2Int>{Vector2Int.zero};
            var level=_upgrades?.Level??0;
            if(level>=1){offsets.Add(plan.Direction);offsets.Add(-plan.Direction);}
            if(level>=2)
                for(var across=-1;across<=1;across+=2)
                for(var along=-1;along<=1;along++)offsets.Add(side*across+plan.Direction*along);
            var seeds=_inventory.GetAmount(plan.SeedId);
            long room=long.MaxValue;
            var expectedYield=0;
            if(plan.Action==FieldAction.Harvest)
            {
                var item=plan.CropId;
                if(_catalog!=null && _catalog.TryGetCrop(plan.CropId,out var definition))item=definition.HarvestItemId;
                room=int.MaxValue-(long)_inventory.GetAmount(item);expectedYield=_crops.HarvestYield(plan.CropId);
            }
            foreach(var offset in offsets)
            {
                var position=center+offset;var entry=new FieldCell{Position=position};plan.Cells.Add(entry);
                if(!_grid.TryGetCell(position,out var cell)){entry.SkipReason="нет доступной клетки";continue;}
                if(!IsWithinReach(position)){entry.SkipReason="слишком далеко";continue;}
                if(_orchard?.Contains(position)==true || cell.type==CellType.Tree){entry.SkipReason="дерево";continue;}
                if(_animals?.OccupiesHomeCell(position)==true){entry.SkipReason="животное";continue;}
                if(cell.type==CellType.Building || cell.type==CellType.BuildingEdge){entry.SkipReason="постройка";continue;}
                if(cell.type==CellType.Water){entry.SkipReason="вода";continue;}
                if(cell.type==CellType.Rock){entry.SkipReason="камень или дерево";continue;}
                if(cell.type==CellType.Locked){entry.SkipReason="участок ещё закрыт";continue;}
                if(_workedUntil.TryGetValue(position,out var until) && Time.unscaledTime<until){entry.SkipReason="пауза после действия";continue;}
                switch(plan.Action)
                {
                    case FieldAction.Till:
                        if(!_grid.CanTill(position))entry.SkipReason="нельзя обработать";
                        break;
                    case FieldAction.Plant:
                        if (!_grid.CanPlant(position))
                            entry.SkipReason = _grid.CanTill(position) ? RequiresTilledSoil : "здесь нельзя посадить семена";
                        else if(seeds<=0)entry.SkipReason="не хватает семян";
                        else seeds--;
                        break;
                    case FieldAction.Harvest:
                        if(!_crops.TryGet(position,out var candidate))entry.SkipReason="нет культуры";
                        else if(candidate.cropId!=plan.CropId)entry.SkipReason="другая культура";
                        else if(!candidate.IsMature(nowUnixMs))entry.SkipReason=candidate.isRegrowing?"восстанавливается":"ещё не созрело";
                        else if(room<expectedYield)entry.SkipReason="нет места в стеке";
                        else room-=expectedYield;
                        break;
                }
            }
            return plan;
        }
        private string FieldPrompt(FieldPlan plan)
        {
            if(plan.Action==FieldAction.None)return "Выберите мотыгу, семена или зрелую культуру · "+InputReader_AI.KeyLabel(InputReader_AI.Command.Inventory);
            var verb=plan.Action==FieldAction.Till?"Обработать":plan.Action==FieldAction.Plant?"Посадить: "+CropName(plan.CropId):"Собрать: "+CropName(plan.CropId);
            var allowed = plan.Allowed;
            var text=InputReader_AI.ActionHint+" — "+verb+" · "+allowed+" / "+plan.Cells.Count+" клеток";
            if (allowed > 0 && !plan.Cells[0].Allowed) text += " · соседние клетки";
            if(plan.Cells.Count==3 && InputReader_AI.CanRotateField)text+=" · "+InputReader_AI.KeyLabel(InputReader_AI.Command.Rotate)+" — повернуть";
            return allowed==plan.Cells.Count?text:text+"\nПропуск: "+plan.Skips;
        }
        // Выполняю действие на выбранных клетках. При сборе складываю партии в сумку с их размерами.
        private bool ApplyField(Vector2Int center,long nowUnixMs)
        {
            if(_applyingField)return false;
            _applyingField=true;
            try
            {
                var plan=PlanField(center,nowUnixMs);var done=0;var totalYield=0;
                foreach(var cell in plan.Cells)
                {
                    if(!cell.Allowed)continue;
                    var success=false;
                    if(plan.Action==FieldAction.Till)success=_soil.Till(cell.Position);
                    else if(plan.Action==FieldAction.Plant)
                    {
                        if(_inventory.TryRemove(plan.SeedId,1))
                        {success=_crops.Plant(cell.Position,plan.CropId,nowUnixMs);if(!success)_inventory.Add(plan.SeedId,1);}
                    }
                    else if(plan.Action==FieldAction.Harvest)
                    {
                        // Recheck live capacity in case a synchronous event observer changed inventory.
                        var itemId=_catalog!=null && _catalog.TryGetCrop(plan.CropId,out var definition)?definition.HarvestItemId:plan.CropId;
                        if((long)_inventory.GetAmount(itemId)+_crops.HarvestYield(plan.CropId)<=int.MaxValue && _crops.TryHarvest(cell.Position,nowUnixMs,out var harvested,out var yield,out var size))
                        {
                            // Размер идёт в сумку вместе с количеством, чтобы потом продать именно эту стопку.
                            _inventory.Add(harvested,yield,size);totalYield+=yield;success=true;
                            var normal=_catalog!=null && _catalog.TryGetCrop(plan.CropId,out var d)?d.YieldAmount:_crops.YieldAmount;
                            GameEvents.Record("harvest",plan.CropId,yield,yield>normal?1:0,_crops.HarvestHour);
                            GameEvents.RaiseActionFeedback("harvest",_grid.CellToWorld(cell.Position),harvested,yield);
                            GameEvents.RaiseProgressAction("harvest");
                        }
                    }
                    if(!success){cell.SkipReason="состояние изменилось";continue;}
                    done++;_workedUntil[cell.Position]=Time.unscaledTime+.18f;
                    if(plan.Action!=FieldAction.Harvest)
                    {
                        GameEvents.RaiseActionFeedback(plan.Action==FieldAction.Till?"till":"plant",_grid.CellToWorld(cell.Position));
                        if(plan.Action==FieldAction.Plant)GameEvents.RaiseProgressAction("plant");
                    }
                }
                if(done>0)
                {
                    if(plan.Action==FieldAction.Till)MyLittleFarm.Audio.FarmAudio.Play(MyLittleFarm.Audio.SoundId.Till,_grid.CellToWorld(center));
                    if(_slots?.Mode==QuickSlotSystem.SelectionMode.Hoe)_heldHoe?.Work();
                }
                var verb=plan.Action==FieldAction.Till?"Обработано":plan.Action==FieldAction.Plant?"Засеяно":"Собрано";
                var message=plan.Action==FieldAction.None?FieldPrompt(plan):verb+": "+done+" / "+plan.Cells.Count+" клеток"+(totalYield>0?" · "+totalYield+" шт.":"");
                if (done == 0 && plan.Action == FieldAction.Plant && plan.Cells[0].SkipReason == RequiresTilledSoil)
                    message = "Сначала обработайте землю мотыгой. Выберите «Мотыга» на панели · " + InputReader_AI.ActionHint;
                else if(plan.Cells.Count>done)message+=". Пропуск: "+plan.Skips;
                GameEvents.RaiseStatusChanged(message);return done>0;
            }
            finally{_applyingField=false;}
        }
    }
}
