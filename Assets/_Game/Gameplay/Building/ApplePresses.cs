using System.Collections.Generic;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Economy;

namespace MyLittleFarm.Gameplay.Building
{
    // Compatibility surface for the existing juice goal and press callers; all transfers use the shared service.
    internal sealed class ApplePresses
    {
        public const string BuildingId="apple_press", FruitId="apple", JuiceId="apple_juice";
        public const int BuildingPrice=80, AppleCost=3, BatchSeconds=60, JuicePrice=65;
        private readonly ProductionBatches _production;
        public ApplePresses(InventorySystem inventory) : this(new ProductionBatches(inventory)) { }
        public ApplePresses(ProductionBatches production) => _production=production;
        public bool HasBatches => _production.HasPressBatches;
        public bool IsOccupied(string id) => _production.Get(id)?.recipeId==ProductionRecipe.Press.Id;
        public string Prompt(string id,long now,string hint) => _production.Prompt(id,ProductionRecipe.Press,now,hint);
        public bool TryUse(string id,long now,out string message,out bool collected)
        {
            collected=false;
            if(IsOccupied(id)) { var ok=_production.TryCollect(id,now,out message); collected=ok; return ok; }
            return _production.TryStart(id,ProductionRecipe.Press,now,out message);
        }
        public List<ApplePressBatchData> Capture()
        {
            var result=new List<ApplePressBatchData>();
            foreach(var batch in _production.Capture()) if(batch.recipeId==ProductionRecipe.Press.Id)
                result.Add(new ApplePressBatchData { buildingId=batch.buildingId,readyAtUnixMs=batch.readyAtUnixMs });
            return result;
        }
        public void Restore(List<ApplePressBatchData> batches)
        {
            var result=_production.Capture();result.RemoveAll(b=>b.recipeId==ProductionRecipe.Press.Id);
            result.AddRange(ProductionBatches.MigratePresses(batches));_production.Restore(result);
        }
        public static void Validate(List<ApplePressBatchData> batches,List<BuildingRuntimeState> buildings)
            => ProductionBatches.Validate(ProductionBatches.MigratePresses(batches),buildings);
    }
}
