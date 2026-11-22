using System;
using System.Collections.Generic;
using System.IO;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.UI;

namespace MyLittleFarm.Gameplay.Building
{
    /// <summary>Fixed, single-input recipes for the farm workshops.</summary>
    public sealed class ProductionRecipe
    {
        public string Id { get; }
        public string BuildingId { get; }
        public string Name { get; }
        public string InputId { get; }
        public string InputName { get; }
        public int InputAmount { get; }
        public string OutputId { get; }
        public string OutputName { get; }
        public int Seconds { get; }
        public int SellPrice { get; }
        private ProductionRecipe(string id, string building, string name, string input, string inputName,
            int amount, string output, string outputName, int seconds, int price)
        { Id=id; BuildingId=building; Name=name; InputId=input; InputName=inputName; InputAmount=amount;
            OutputId=output; OutputName=outputName; Seconds=seconds; SellPrice=price; }
        public static readonly ProductionRecipe Press = new ProductionRecipe("apple_to_juice", "apple_press", "Яблочный пресс", "apple", "Яблоко", 3, "apple_juice", "Яблочный сок", 60, 65);
        public static readonly ProductionRecipe Mill = new ProductionRecipe("wheat_to_flour", "mill", "Мельница", "wheat", "Пшеница", 3, "flour", "Мука", 30, 22);
        public static readonly ProductionRecipe CornMill = new ProductionRecipe("corn_to_flour", "mill", "Мельница", "corn", "Кукуруза", 2, "flour", "Мука", 30, Mill.SellPrice);
        public static readonly ProductionRecipe Dairy = new ProductionRecipe("milk_to_cheese", "cheese_dairy", "Сыроварня", "milk", "Молоко", 2, "cheese", "Сыр", 90, 80);
        public static IReadOnlyList<ProductionRecipe> All { get; } = Array.AsReadOnly(new[] { Press, Mill, CornMill, Dairy });
        public static ProductionRecipe ForBuilding(string definitionId)
        { foreach(var recipe in All) if(recipe.BuildingId==definitionId) return recipe; return null; }
        public static ProductionRecipe Find(string recipeId)
        { foreach (var recipe in All) if (recipe.Id == recipeId) return recipe; return null; }
    }

    /// <summary>Authoritative batches keyed by stable building instance ID, using real UTC like the old press.</summary>
    internal sealed class ProductionBatches
    {
        private const long MaxUnixMs = 253402300799999L;
        private readonly InventorySystem _inventory;
        private readonly Dictionary<string, ProductionBatchData> _batches = new Dictionary<string, ProductionBatchData>(StringComparer.Ordinal);
        private bool _changing;
        internal Action<string> RecordCollection { get; set; }
        public ProductionBatches(InventorySystem inventory) => _inventory=inventory;
        public bool IsOccupied(string id) => id != null && _batches.ContainsKey(id);
        public bool HasPressBatches
        { get { foreach(var batch in _batches.Values) if(batch.recipeId==ProductionRecipe.Press.Id) return true; return false; } }
        public ProductionBatchData Get(string id) => id != null && _batches.TryGetValue(id,out var batch) ? Copy(batch) : null;
        public int Amount(string item) => _inventory.GetAmount(item);
        public string RemovalHint => "В постройке есть партия. Заберите продукт перед удалением.";
        public string Prompt(string id, ProductionRecipe recipe, long now, string hint)
        {
            var batch=Get(id);
            var state=batch==null ? "нет партии" : now<batch.readyAtUnixMs
                ? "готовность через "+FarmText.Duration((float)Math.Ceiling((batch.readyAtUnixMs-now)/1000d)) : "продукт готов";
            return recipe.Name+" · "+state+"\n"+hint+" — открыть производство";
        }
        public bool TryStart(string id, ProductionRecipe recipe, long now, out string message)
        {
            message="Партия уже находится в постройке. Сначала заберите продукт.";
            if(_changing || IsOccupied(id) || string.IsNullOrEmpty(id) || recipe==null || now<=0 || now>MaxUnixMs-recipe.Seconds*1000L) return false;
            if(_inventory.GetAmount(recipe.InputId)<recipe.InputAmount)
            { message="Недостаточно сырья: "+recipe.InputName+" × "+recipe.InputAmount; return false; }
            _changing=true;
            try
            {
                _batches.Add(id,new ProductionBatchData { buildingId=id, recipeId=recipe.Id, startedAtUnixMs=now,
                    readyAtUnixMs=now+recipe.Seconds*1000L, outputItemId=recipe.OutputId, outputAmount=1 });
                _inventory.TryRemove(recipe.InputId,recipe.InputAmount);
                message="Партия запущена · "+FarmText.Duration(recipe.Seconds); return true;
            }
            finally { _changing=false; }
        }
        public bool TryCollect(string id, long now, out string message)
        {
            message="Нет готового продукта.";
            if(_changing || now<=0 || now>MaxUnixMs || !_batches.TryGetValue(id,out var batch)) return false;
            if(now<batch.readyAtUnixMs) { message="Партия ещё в работе."; return false; }
            if(_inventory.GetAmount(batch.outputItemId)>int.MaxValue-batch.outputAmount)
            { message="Инвентарь полон. Продукт остался в постройке."; return false; }
            _changing=true;
            try
            {
                _batches.Remove(id);
                _inventory.SetAmountSilently(batch.outputItemId, _inventory.GetAmount(batch.outputItemId) + batch.outputAmount);
                RecordCollection?.Invoke(batch.outputItemId);
                GameEvents.RaiseInventoryChanged();
                message="Продукт получен · "+batch.outputAmount+" шт."; return true;
            }
            finally { _changing=false; }
        }
        internal int FinishForDemo(long now)
        {
            if (_changing) return 0;
            var count = 0;
            foreach (var batch in _batches.Values)
            {
                if (batch.readyAtUnixMs <= now) continue;
                var remaining = batch.readyAtUnixMs - now;
                batch.startedAtUnixMs = Math.Max(0, batch.startedAtUnixMs - remaining);
                batch.readyAtUnixMs = now;
                count++;
            }
            return count;
        }

        public List<ProductionBatchData> Capture()
        { var result=new List<ProductionBatchData>(_batches.Count); foreach(var batch in _batches.Values) result.Add(Copy(batch)); return result; }
        public void Restore(List<ProductionBatchData> batches)
        { _batches.Clear(); if(batches!=null) foreach(var batch in batches) _batches.Add(batch.buildingId,Copy(batch)); }
        private static ProductionBatchData Copy(ProductionBatchData b) => new ProductionBatchData { buildingId=b.buildingId,
            recipeId=b.recipeId,startedAtUnixMs=b.startedAtUnixMs,readyAtUnixMs=b.readyAtUnixMs,outputItemId=b.outputItemId,outputAmount=b.outputAmount };
        public static List<ProductionBatchData> MigratePresses(List<ApplePressBatchData> old)
        {
            var result=new List<ProductionBatchData>();
            foreach(var b in old) result.Add(b==null ? null : new ProductionBatchData { buildingId=b.buildingId,
                recipeId=ProductionRecipe.Press.Id,startedAtUnixMs=Math.Max(0,b.readyAtUnixMs-60000),
                readyAtUnixMs=b.readyAtUnixMs,outputItemId=ProductionRecipe.Press.OutputId,outputAmount=1 });
            return result;
        }
        public static void Validate(List<ProductionBatchData> batches, List<BuildingRuntimeState> buildings)
        {
            var owners=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(var building in buildings)
                if(ProductionRecipe.ForBuilding(building.definitionId)!=null) owners.Add(building.id,building.definitionId);
            foreach(var batch in batches)
            {
                var recipe = ProductionRecipe.Find(batch?.recipeId);
                if(batch==null || string.IsNullOrEmpty(batch.buildingId) || !owners.TryGetValue(batch.buildingId,out var buildingType)
                    || recipe==null || recipe.BuildingId!=buildingType || batch.outputItemId!=recipe.OutputId || batch.outputAmount!=1
                    || batch.startedAtUnixMs<0 || batch.readyAtUnixMs<=batch.startedAtUnixMs || batch.readyAtUnixMs>MaxUnixMs)
                    throw new InvalidDataException("Invalid production batch, recipe or building.");
                owners.Remove(batch.buildingId);
            }
        }
    }
}
