using System;
using System.Collections.Generic;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Animals;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.Farming;
using MyLittleFarm.UI;
using UnityEngine;

namespace MyLittleFarm.Gameplay.World
{
    [DefaultExecutionOrder(-30)]
    public sealed class FarmProgression : MonoBehaviour
    {
        public const int ShepherdPrice=80;
        public const long CounterLimit=1_000_000_000_000_000;
        public static readonly string[] CacheIds={"pond","forest","hill"};
        [SerializeField] private FarmJournalUI journal;
        [SerializeField] private FarmMenuUI gameMenus;
        public void ConfigureGuideNavigation(FarmMenuUI menu) => gameMenus = menu;
        [SerializeField] private RareSeedCache[] seedCaches;
        [SerializeField] private GameObject shepherdVisual;
        private FarmProgressData _data=new FarmProgressData();
        private WalletSystem _wallet;
        private InventorySystem _inventory;
        private FarmCatalog _catalog;
        private BuildSystem _buildings;
        private OrchardSystem _orchard;
        private AnimalSystem _animals;
        private SectorSystem _sectors;
        private InputReader _input;
        private Transform _player,_market;
        private DayNightCycle _dayNight;
        private QuickSlotSystem _slots;
        private bool _configured,_restoring;
        private float _nextCareAt;
        public event Action Changed;
        public event Action<string> AchievementUnlocked;
        public event Action Restored;
        // Internal consumers are trusted; persisted snapshots are always detached copies.
        public FarmProgressData Data=>_data;
        public FarmCatalog Catalog=>_catalog;
        public long Profit=>_data.revenue+_data.refunds-_data.expenses;
        public int Balance=>_wallet==null?0:_wallet.Coins;
        public float Hour=>_dayNight==null?8:_dayNight.Hour;
        public string ClockTime
        {
            get
            {
                var minutes = Mathf.FloorToInt(Hour * 60);
                return (minutes / 60).ToString("00") + ":" + (minutes % 60).ToString("00") + " · "
                    + (Hour >= 6 && Hour < 12 ? "Утро" : Hour >= 12 && Hour < 18 ? "День" : Hour >= 18 && Hour < 22 ? "Вечер" : "Ночь");
            }
        }
        public string ClockHint
        {
            get
            {
                var minutes=Mathf.FloorToInt(Hour*60);var crop=_slots?.SelectedCrop;
                var hint=crop==null?"J — журнал фермы":crop.RareSeeds?"Кукуруза: редкие семена · J"
                    :crop.BonusHarvestYield>0?crop.BonusDescription:"J — статистика и достижения";
                return (minutes/60).ToString("00")+":"+(minutes%60).ToString("00")+"   ·   "+(Hour>=6&&Hour<12?"Утро":Hour>=12&&Hour<18?"День":Hour>=18&&Hour<22?"Вечер":"Ночь")+"\n"+hint;
            }
        }
        public int TreeCount=>_orchard==null?0:_orchard.Capture().Count;
        public int AnimalCount=>_animals==null?0:_animals.Count;
        public bool AllSectorsOpen=>_sectors!=null&&_sectors.IsUnlocked;
        public int DiscoveredCaches=>_data.caches.FindAll(c=>c.discovered).Count;
        public string FavouriteCrop
        {
            get
            {
                CropHistory best=null;
                foreach(var id in FarmCatalog.CropIds)
                {
                    var item=_data.crops.Find(c=>c.cropId==id);
                    if(item!=null&&(best==null||item.planted>best.planted))best=item;
                }
                return best==null?"—":_catalog!=null&&_catalog.TryGetCrop(best.cropId,out var crop)?crop.DisplayName:best.cropId;
            }
        }
        public bool HasBuilding(string id)=>_buildings!=null&&_buildings.FindFirstView(id)!=null;
        public bool HasManor
        {
            get{if(_buildings!=null)foreach(var b in _buildings.Capture())if(b.definitionId=="house"&&b.appearanceLevel==2)return true;return false;}
        }
        public void Configure(InputReader input,WalletSystem wallet,InventorySystem inventory,FarmCatalog catalog,
            BuildSystem buildings,OrchardSystem orchard,AnimalSystem animals,SectorSystem sectors,Transform player,Transform market)
        {
            _input=input;_wallet=wallet;_inventory=inventory;_catalog=catalog;_buildings=buildings;
            _orchard=orchard;_animals=animals;_sectors=sectors;_player=player;_market=market;
            var root=GetComponentInParent<GameBootstrap>();_dayNight=root==null?null:root.GetComponentInChildren<DayNightCycle>(true);
            _slots=root==null?null:root.GetComponentInChildren<QuickSlotSystem>(true);
            _configured=true;_data=new FarmProgressData();_data.highestBalance=wallet.Coins;
            if(seedCaches!=null)foreach(var cache in seedCaches)if(cache!=null)cache.Configure(this,player);
            if(journal!=null)journal.Configure(this,input);
            ApplyVisuals();
        }
        private void OnEnable(){GameEvents.FarmAction+=OnAction;GameEvents.MoneyChanged+=OnMoney;}
        private void OnDisable(){GameEvents.FarmAction-=OnAction;GameEvents.MoneyChanged-=OnMoney;}
        private static long Add(long total,long value)=>total+Math.Min(CounterLimit-total,Math.Max(0,value));
        private void OnMoney(int coins)
        {
            if(!_configured||_restoring)return;
            _data.highestBalance=Math.Max(_data.highestBalance,coins);Evaluate(true);Changed?.Invoke();
        }
        private void OnAction(FarmActivity action)
        {
            if(!_configured||_restoring)return;
            switch(action.Kind)
            {
                case "plant":
                    _data.plantings=Add(_data.plantings,action.Amount);
                    var crop=_data.crops.Find(c=>c.cropId==action.ItemId);
                    if(crop==null){crop=new CropHistory{cropId=action.ItemId};_data.crops.Add(crop);}
                    crop.planted=Add(crop.planted,action.Amount);break;
                case "harvest":_data.harvests=Add(_data.harvests,1);_data.harvestedUnits=Add(_data.harvestedUnits,action.Amount);
                    if(action.Value>0)_data.bonusHarvests=Add(_data.bonusHarvests,1);break;
                case "sale":_data.revenue=Add(_data.revenue,action.Value);break;
                case "spent":_data.expenses=Add(_data.expenses,action.Value);break;
                case "refund":_data.refunds=Add(_data.refunds,action.Value);break;
                case "apples":_data.apples=Add(_data.apples,action.Amount);break;
                case "eggs":_data.eggs=Add(_data.eggs,action.Amount);break;
                case "build":
                    if(IsDecoration(action.ItemId)&&!_data.decorations.Contains(action.ItemId))_data.decorations.Add(action.ItemId);
                    break;
            }
            Evaluate(true);Changed?.Invoke();
        }
        private static bool IsDecoration(string id)
        {if(id=="flowerbed")return true;foreach(var item in FarmDecorationCatalog.Items)if(item.Id==id)return true;return false;}
        private void Evaluate(bool announce)
        {
            foreach(var achievement in FarmAchievements.All)
                if(!_data.achievements.Contains(achievement.Id)&&achievement.IsEarned(this))
                {_data.achievements.Add(achievement.Id);if(announce)AchievementUnlocked?.Invoke(achievement.Name);}
        }
        public FarmProgressData Capture()=>JsonUtility.FromJson<FarmProgressData>(JsonUtility.ToJson(_data));
        public void BeginRestore()=>_restoring=true;
        public void EndRestore(){_restoring=false;Changed?.Invoke();}
        public void Restore(FarmProgressData data)
        {
            _data=data==null?new FarmProgressData():JsonUtility.FromJson<FarmProgressData>(JsonUtility.ToJson(data));
            _data.highestBalance=Math.Max(_data.highestBalance,_wallet.Coins);
            Evaluate(false);ApplyVisuals();_nextCareAt=Time.unscaledTime+5;Restored?.Invoke();Changed?.Invoke();
        }
        private void ApplyVisuals()
        {
            if(shepherdVisual!=null)shepherdVisual.SetActive(_data.shepherdHired);
            if(seedCaches!=null)foreach(var cache in seedCaches)if(cache!=null)cache.Refresh();
        }
        public SeedCacheState Cache(string id)
        {
            var state=_data.caches.Find(c=>c.id==id);
            return state??new SeedCacheState{id=id};
        }
        public bool CollectCache(string id)
        {
            if(!_configured||_restoring||Array.IndexOf(CacheIds,id)<0)return false;
            var cache=seedCaches==null?null:Array.Find(seedCaches,c=>c!=null&&c.Id==id);
            var now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if(cache==null||!cache.WithinReach(_player.position)||Cache(id).nextAvailableUnixMs>now
                ||_inventory.GetAmount("corn_seed")>int.MaxValue-2)return false;
            var state=_data.caches.Find(c=>c.id==id);
            if(state==null){state=new SeedCacheState{id=id};_data.caches.Add(state);}
            state.discovered=true;state.nextAvailableUnixMs=now+15*60*1000;
            _inventory.Add("corn_seed",2);_data.rareSeeds=Add(_data.rareSeeds,2);
            cache.Refresh();Evaluate(true);Changed?.Invoke();
            MyLittleFarm.Audio.FarmAudio.Play(MyLittleFarm.Audio.SoundId.RareSeedCollect, cache.transform.position);
            GameEvents.RaiseStatusChanged("Найдено: 2 редких семени кукурузы · слот семян 5");return true;
        }
        public bool NearMarket
        {
            get{if(_player==null||_market==null)return false;var delta=_player.position-_market.position;delta.y=0;
                return delta.sqrMagnitude<=2.4f*2.4f||(_buildings!=null&&_buildings.IsNearMarket(_player.position,2.4f));}
        }
        public bool HireShepherd()
        {
            if(!_configured||_restoring||_data.shepherdHired||!NearMarket||!_wallet.TrySpend(ShepherdPrice))return false;
            GrantShepherd();return true;
        }
        internal void GrantShepherd() { _data.shepherdHired=true;ApplyVisuals();Evaluate(true);Changed?.Invoke(); }
        public bool CanHireShepherd=>!_data.shepherdHired&&NearMarket&&Balance>=ShepherdPrice;
        private void Update()
        {
            if (Time.timeScale <= 0) return;
            if(!_configured)return;
            if(_input!=null&&_input.JournalPressed&&journal!=null)
            { if (gameMenus != null) gameMenus.OpenGuide(); else journal.Open(); }
            if(_restoring||!_data.shepherdHired||_animals==null||Time.unscaledTime<_nextCareAt)return;
            _nextCareAt=Time.unscaledTime+5;
            _animals.AutomaticCare(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }
        public static bool IsValid(FarmProgressData data)
        {
            if(data==null)return true; // v1–10 have no historical counters.
            foreach(var value in new[]{data.revenue,data.expenses,data.refunds,data.plantings,data.harvests,data.harvestedUnits,data.apples,data.eggs,data.bonusHarvests,data.rareSeeds})
                if(value<0||value>CounterLimit)return false;
            if(data.highestBalance<0||data.crops==null||data.crops.Count>6||data.achievements==null||data.achievements.Count>20
                ||data.decorations==null||data.decorations.Count>35||data.caches==null||data.caches.Count>3)return false;
            var ids=new HashSet<string>();
            foreach(var c in data.crops)if(c==null||Array.IndexOf(FarmCatalog.CropIds,c.cropId)<0||c.planted<=0||c.planted>CounterLimit||!ids.Add(c.cropId))return false;
            ids.Clear();foreach(var id in data.achievements)if(!Array.Exists(FarmAchievements.All,a=>a.Id==id)||!ids.Add(id))return false;
            ids.Clear();foreach(var id in data.decorations)if(!IsDecoration(id)||!ids.Add(id))return false;
            ids.Clear();foreach(var c in data.caches)if(c==null||Array.IndexOf(CacheIds,c.id)<0||!ids.Add(c.id)||c.nextAvailableUnixMs<0||c.nextAvailableUnixMs>253402300799999)return false;
            return true;
        }
    }
}
