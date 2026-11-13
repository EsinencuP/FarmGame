using System;
using System.Collections.Generic;
using System.IO;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Building;

namespace MyLittleFarm.Gameplay.Economy
{
    /// <summary>One unbounded-by-gameplay item store per stable warehouse building ID.</summary>
    public sealed class WarehouseStore
    {
        private readonly InventorySystem _inventory;
        private readonly BuildSystem _build;
        private readonly Dictionary<string,Dictionary<string,int>> _stores=new Dictionary<string,Dictionary<string,int>>(StringComparer.Ordinal);
        private readonly HashSet<string> _completed=new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<string> _recent=new Queue<string>();
        private bool _changing;
        public long Revision { get; private set; }
        public WarehouseStore(InventorySystem inventory,BuildSystem build){_inventory=inventory;_build=build;}
        public int Amount(string warehouse,string item)
            => warehouse!=null && item!=null && _stores.TryGetValue(warehouse,out var items) && items.TryGetValue(item,out var count)?count:0;
        public bool HasContents(string warehouse)
        {if(warehouse==null || !_stores.TryGetValue(warehouse,out var items))return false;foreach(var count in items.Values)if(count>0)return true;return false;}
        public List<InventoryEntryData> Items(string warehouse)
        {
            var result=new List<InventoryEntryData>();
            if(warehouse!=null && _stores.TryGetValue(warehouse,out var items))foreach(var pair in items)
                if(pair.Value>0)result.Add(new InventoryEntryData{itemId=pair.Key,amount=pair.Value});
            return result;
        }
        private bool Begin(string warehouse,string token,long inventoryRevision,long storageRevision,out string message)
        {
            message="Действие устарело. Повторите перенос.";
            if(_changing || string.IsNullOrEmpty(token) || _completed.Contains(token) || inventoryRevision!=_inventory.Revision || storageRevision!=Revision)return false;
            if(_build.GetBuilding(warehouse)?.definitionId!="storage"){message="Склад не найден.";return false;}return true;
        }
        private Dictionary<string,int> Store(string warehouse)
        {if(!_stores.TryGetValue(warehouse,out var items))_stores.Add(warehouse,items=new Dictionary<string,int>(StringComparer.Ordinal));return items;}
        private void Complete(string token)
        {_completed.Add(token);_recent.Enqueue(token);while(_recent.Count>128)_completed.Remove(_recent.Dequeue());Revision++;}
        public bool Transfer(string warehouse,string item,int amount,bool toWarehouse,string token,long inventoryRevision,long storageRevision,out string message)
        {
            if(!Begin(warehouse,token,inventoryRevision,storageRevision,out message))return false;
            if(string.IsNullOrWhiteSpace(item) || amount<1){message="Укажите количество.";return false;}
            var carried=_inventory.GetAmount(item);var stored=Amount(warehouse,item);
            if(amount>(toWarehouse?carried:stored)){message="Недостаточно предметов.";return false;}
            if((long)(toWarehouse?stored:carried)+amount>int.MaxValue){message="Стек не вмещает выбранное количество. Перенос отменён.";return false;}
            _changing=true;
            try
            {
                // Commit both stores before publishing a single inventory event.
                var items=Store(warehouse);
                _inventory.SetAmountSilently(item,carried+(toWarehouse?-amount:amount));
                items[item]=stored+(toWarehouse?amount:-amount);
                if(items[item]==0)items.Remove(item);
                Complete(token);message="Перемещено: "+amount+" шт.";
                GameEvents.RaiseInventoryChanged();return true;
            }
            finally{_changing=false;}
        }
        public bool TransferAll(string warehouse,string token,long inventoryRevision,long storageRevision,out string message)
        {
            if(!Begin(warehouse,token,inventoryRevision,storageRevision,out message))return false;
            var snapshot=_inventory.Capture();var any=false;
            foreach(var item in snapshot)if(item.amount>0)
            {any=true;if((long)Amount(warehouse,item.itemId)+item.amount>int.MaxValue){message="Один из стеков не вмещает запас. Ничего не перемещено.";return false;}}
            if(!any){message="Инвентарь пуст.";return false;}
            _changing=true;
            try
            {
                var items=Store(warehouse);
                foreach(var item in snapshot)if(item.amount>0)
                {items[item.itemId]=Amount(warehouse,item.itemId)+item.amount;_inventory.SetAmountSilently(item.itemId,0);}
                Complete(token);message="Все предметы перемещены в склад.";GameEvents.RaiseInventoryChanged();return true;
            }
            finally{_changing=false;}
        }
        public void ForgetEmpty(string warehouse)
        {if(_changing || HasContents(warehouse))throw new InvalidOperationException("Warehouse transfer is active or contents remain.");_stores.Remove(warehouse);Revision++;}
        public bool IsChanging=>_changing;
        public List<WarehouseData> Capture()
        {
            var result=new List<WarehouseData>();
            foreach(var b in _build.Capture())if(b.definitionId=="storage")result.Add(new WarehouseData{buildingId=b.id,items=Items(b.id)});
            return result;
        }
        public void Restore(List<WarehouseData> data)
        {
            _stores.Clear();_completed.Clear();_recent.Clear();Revision++;
            foreach(var warehouse in data){var items=Store(warehouse.buildingId);foreach(var item in warehouse.items)if(item.amount>0)items.Add(item.itemId,item.amount);}
        }
        public static void Validate(List<WarehouseData> data,List<BuildingRuntimeState> buildings)
        {
            if(data==null || data.Count>100000)throw new InvalidDataException("Warehouse list is invalid.");
            var owners=new HashSet<string>(StringComparer.Ordinal);foreach(var b in buildings)if(b.definitionId=="storage")owners.Add(b.id);
            foreach(var warehouse in data)
            {
                if(warehouse==null || string.IsNullOrWhiteSpace(warehouse.buildingId) || !owners.Remove(warehouse.buildingId)
                    || warehouse.items==null || warehouse.items.Count>128)throw new InvalidDataException("Warehouse is duplicated or has no building.");
                var ids=new HashSet<string>(StringComparer.Ordinal);
                foreach(var item in warehouse.items)if(item==null || string.IsNullOrWhiteSpace(item.itemId) || item.amount<0 || !ids.Add(item.itemId))
                    throw new InvalidDataException("Warehouse item is invalid or duplicated.");
            }
        }
    }
}
