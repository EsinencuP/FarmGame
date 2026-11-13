using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Animals;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.Gameplay.Farming;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Economy
{
    /// <summary>Persisted economic days count active play, independently of the lighting clock.</summary>
    public sealed class DailyMarketOrders
    {
        public const double DaySeconds=840;
        private readonly FarmCatalog _catalog;
        private readonly InventorySystem _inventory;
        private readonly WalletSystem _wallet;
        private readonly BuildSystem _build;
        private readonly AnimalSystem _animals;
        private readonly OrchardSystem _orchard;
        private MarketOrdersData _data;
        private bool _changing;
        public double Remaining=>DaySeconds-_data.elapsedSeconds;
        public int Day=>_data.day;
        public IReadOnlyList<MarketOrderData> Current=>Capture().orders;
        public DailyMarketOrders(FarmCatalog catalog,InventorySystem inventory,WalletSystem wallet,BuildSystem build,AnimalSystem animals,OrchardSystem orchard)
        {
            _catalog=catalog;_inventory=inventory;_wallet=wallet;_build=build;_animals=animals;_orchard=orchard;
            _data=Initial(catalog,Live(),Guid.NewGuid().GetHashCode()&int.MaxValue);
        }
        private SaveData Live()=>new SaveData{buildings=_build?.Capture()??new List<BuildingRuntimeState>(),animals=_animals?.Capture()??new List<AnimalRuntimeState>(),trees=_orchard?.Capture()??new List<TreeRuntimeState>()};
        public static MarketOrdersData Initial(FarmCatalog catalog,SaveData farm,int seed)
        {
            var data=new MarketOrdersData{seed=Math.Max(1,seed)};Generate(data,catalog,farm);return data;
        }
        public static MarketOrdersData Legacy(FarmCatalog catalog,SaveData farm)
            =>Initial(catalog,farm,unchecked((int)(farm.savedAtUnixMs^farm.worldBaseHash))&int.MaxValue);
        private static void Generate(MarketOrdersData data,FarmCatalog catalog,SaveData farm)
        {
            var pool=new List<string>{"carrot","potato","wheat","tomato","cucumber","strawberry","pumpkin","apple"};
            if(farm.animals.Any(a=>a.definitionId=="chicken"))pool.Add("egg");
            var cows=farm.animals.Any(a=>a.definitionId=="cow");if(cows)pool.Add("milk");
            if(farm.buildings.Any(b=>b.definitionId=="mill"))pool.Add("flour");
            if(cows && farm.buildings.Any(b=>b.definitionId=="cheese_dairy"))pool.Add("cheese");
            if(farm.trees.Any(t=>t.treeId=="pear"))pool.Add("pear");
            pool.RemoveAll(id=>catalog!=null && !catalog.TryGetItem(id,out _));
            if(catalog==null)pool=new List<string>{"carrot"};
            var random=new System.Random(unchecked(data.seed+data.day*7919));var count=1+random.Next(3);data.orders.Clear();
            for(var i=0;i<count && pool.Count>0;i++)
            {
                var index=random.Next(pool.Count);var id=pool[index];pool.RemoveAt(index);
                var crop=catalog!=null && catalog.TryGetCrop(id,out _);var amount=crop?2+random.Next(4):1+random.Next(2);
                var price=catalog!=null && catalog.TryGetItem(id,out var item)?item.SellPrice:3;
                var bonus=Math.Max(2,(int)Math.Ceiling(price*amount*.25));
                data.orders.Add(new MarketOrderData{id="day:"+data.day+":"+i,itemId=id,amount=amount,unitPrice=price,bonus=bonus});
            }
        }
        public void Advance(double seconds)
        {
            if(_changing || double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds<0)return;
            if(_data.day>=1000000){_data.elapsedSeconds=Math.Min(DaySeconds-.001,_data.elapsedSeconds+seconds);return;}
            _data.elapsedSeconds+=seconds;
            if(_data.elapsedSeconds<DaySeconds)return;
            var days=(int)Math.Min(1000000-_data.day,Math.Floor(_data.elapsedSeconds/DaySeconds));
            _data.day+=days;_data.elapsedSeconds%=DaySeconds;Generate(_data,_catalog,Live());GameEvents.RaiseInventoryChanged();
        }
        public MarketOrdersData Capture()=>JsonUtility.FromJson<MarketOrdersData>(JsonUtility.ToJson(_data));
        public void Restore(MarketOrdersData data)=>_data=JsonUtility.FromJson<MarketOrdersData>(JsonUtility.ToJson(data));
        public bool Quote(IReadOnlyDictionary<string,int> cart,out int reward,out string message)
        {
            reward=0;message="Выберите заказ. Штрафа за отказ нет.";
            if(cart==null || cart.Count==0)return false;long sum=0;var required=new Dictionary<string,int>();
            foreach(var line in cart)
            {
                var order=_data.orders.Find(o=>o.id==line.Key);
                if(order==null || order.completed || line.Value!=1){message="Заказ уже выполнен или сменился день.";return false;}
                required.TryGetValue(order.itemId,out var count);required[order.itemId]=count+order.amount;
                sum+=(long)order.unitPrice*order.amount+order.bonus;
            }
            foreach(var pair in required)if(_inventory.GetAmount(pair.Key)<pair.Value){message="Не хватает товара в инвентаре. Верните запасы со склада.";return false;}
            if(sum>int.MaxValue-_wallet.Coins){message="Кошелёк не вмещает награду. Товар не списан.";return false;}
            reward=(int)sum;message="";return true;
        }
        public bool Complete(IReadOnlyDictionary<string,int> cart,out string message)
        {
            message="Заказ уже обрабатывается.";if(_changing)return false;
            if(!Quote(cart,out var reward,out message))return false;
            _changing=true;
            try
            {
                foreach(var line in cart)
                {var order=_data.orders.Find(o=>o.id==line.Key);order.completed=true;_inventory.SetAmountSilently(order.itemId,_inventory.GetAmount(order.itemId)-order.amount);}
                _wallet.SetCoinsSilently(_wallet.Coins+reward);
                GameEvents.RaiseInventoryChanged();GameEvents.RaiseMoneyChanged(_wallet.Coins);
                GameEvents.Record("sale",value:reward);GameEvents.RaiseProgressAction("sale");
                message="Заказы выполнены · +"+reward+" монет";return true;
            }
            finally{_changing=false;}
        }
        public static void Validate(MarketOrdersData data,FarmCatalog catalog)
        {
            if(data==null || data.seed<1 || data.day<0 || data.day>1000000 || double.IsNaN(data.elapsedSeconds) || double.IsInfinity(data.elapsedSeconds)
                || data.elapsedSeconds<0 || data.elapsedSeconds>=DaySeconds || data.orders==null || data.orders.Count<1 || data.orders.Count>3)throw new InvalidDataException("Invalid market day.");
            var ids=new HashSet<string>();var items=new HashSet<string>();
            foreach(var order in data.orders)
                if(order==null || order.id==null || !order.id.StartsWith("day:"+data.day+":",StringComparison.Ordinal) || !ids.Add(order.id)
                    || string.IsNullOrWhiteSpace(order.itemId) || !items.Add(order.itemId) || catalog!=null && !catalog.TryGetItem(order.itemId,out _)
                    || order.amount<1 || order.amount>12 || order.unitPrice<1 || order.unitPrice>100000 || order.bonus<1 || order.bonus>100000)
                    throw new InvalidDataException("Invalid daily order.");
        }
    }
}
