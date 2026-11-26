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
    public sealed class FarmProgression_AI : MonoBehaviour
    {
        public const int ShepherdPrice=120;
        public const long CounterLimit=1_000_000_000_000_000;
        public static readonly string[] CacheIds={"pond","forest","hill"};
        [SerializeField] private FarmJournalUI_AI journal;
        [SerializeField] private FarmMenuUI gameMenus;
        public void ConfigureGuideNavigation(FarmMenuUI menu) => gameMenus = menu;
        [SerializeField] private RareSeedCache[] seedCaches;
        [SerializeField] private GameObject shepherdVisual;
        private FarmProgressData _data=new FarmProgressData();
        private WalletSystem _wallet;
        private InventorySystem_AI _inventory;
        private FarmCatalog _catalog;
        private BuildSystem _buildings;
        private OrchardSystem_AI _orchard;
        private AnimalSystem_AI _animals;
        private SectorSystem _sectors;
        private InputReader_AI _input;
        private Transform _player,_market;
        private DayNightCycle _dayNight;
        private QuickSlotSystem _slots;
        private bool _configured,_restoring;
        private float _nextCareAt;
        private bool _shepherdChanged;
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
                var guide=InputReader_AI.KeyLabel(InputReader_AI.Command.Guide);
                var hint=crop==null?$"{guide} — справочник":crop.RareSeeds?$"Кукуруза: редкие семена · {guide}"
                    :crop.BonusHarvestYield>0?crop.BonusDescription:$"{guide} — справочник";
                return (minutes/60).ToString("00")+":"+(minutes%60).ToString("00")+"   ·   "+(Hour>=6&&Hour<12?"Утро":Hour>=12&&Hour<18?"День":Hour>=18&&Hour<22?"Вечер":"Ночь")+"\n"+hint;
            }
        }
        public int TreeCount=>_orchard==null?0:_orchard.Capture().Count;
        public int AnimalCount=>_animals==null?0:_animals.Count;
        public int ChickenCount=>_animals==null?0:_animals.ChickenCount;
        public bool AllSectorsOpen=>_sectors!=null&&_sectors.AllLocationsOpen;
        internal bool UsesLocationProgression => _sectors?.UsesLegacySector == false;
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

        public string JuiceGoal()
        {
            if (_data.firstJuiceSold) return FarmText.Get("press.goal.done");
            if (_inventory.GetAmount(ApplePresses.JuiceId) > 0)
                return FarmText.Get("press.goal.sell", InputReader_AI.MarketHint, FarmText.Coins(ApplePresses.JuicePrice));
            if (_buildings.Presses?.HasBatches == true)
                return FarmText.Get("press.goal.collect", InputReader_AI.ActionHint);
            if (_inventory.GetAmount(ApplePresses.FruitId) < ApplePresses.AppleCost)
            {
                if (_orchard.SelectedDefinition != null && _orchard.SelectedDefinition.FruitItemId != ApplePresses.FruitId)
                    return "Для сока нужны яблоки. Выберите бесплатную яблоню в инвентаре · " + InputReader_AI.KeyLabel(InputReader_AI.Command.Inventory);
                return FarmText.Get("press.goal.apples", ApplePresses.AppleCost,
                    InputReader_AI.KeyLabel(InputReader_AI.Command.Apple), InputReader_AI.ActionHint);
            }
            if (!HasBuilding(ApplePresses.BuildingId))
                return FarmText.Get("press.goal.build", FarmText.Coins(ApplePresses.BuildingPrice),
                    InputReader_AI.KeyLabel(InputReader_AI.Command.Build));
            return FarmText.Get("press.goal.start", InputReader_AI.ActionHint, FarmText.Duration(ApplePresses.BatchSeconds));
        }
        public bool HasManor
        {
            get{if(_buildings!=null)foreach(var b in _buildings.CaptureReady())if(b.definitionId=="house"&&b.appearanceLevel==2)return true;return false;}
        }
        public void Configure(InputReader_AI input,WalletSystem wallet,InventorySystem_AI inventory,FarmCatalog catalog,
            BuildSystem buildings,OrchardSystem_AI orchard,AnimalSystem_AI animals,SectorSystem sectors,Transform player,Transform market)
        {
            _input=input;_wallet=wallet;_inventory=inventory;_catalog=catalog;_buildings=buildings;
            _orchard=orchard;_animals=animals;_sectors=sectors;_player=player;_market=market;
            // Подключаю наблюдение за яйцами. Оно работает после найма пастуха.
            if (_animals != null)
            {
                _animals.ShepherdObserving = () => _configured && !_restoring && ShepherdWorking;
                _animals.RecordShepherdObservation = ObserveEggCollection;
            }
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
                case "sale":
                    _data.revenue=Add(_data.revenue,action.Value);
                    if (action.ItemId == ApplePresses.JuiceId) _data.firstJuiceSold = true;
                    break;
                case "spent":_data.expenses=Add(_data.expenses,action.Value);break;
                case "refund":_data.refunds=Add(_data.refunds,action.Value);break;
                case "apples":_data.apples=Add(_data.apples,action.Amount);break;
                case "eggs":_data.eggs=Add(_data.eggs,action.Amount);break;
                case "build":
                    if(IsDecoration(action.ItemId)&&!_data.decorations.Contains(action.ItemId))_data.decorations.Add(action.ItemId);
                    break;
            }
            if (action.ItemId == "house" && (action.Kind == "build" || action.Kind == "refund"))
                RefreshShepherdVisual();
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
            _shepherdChanged = false;
            Evaluate(false);ApplyVisuals();_nextCareAt=Time.unscaledTime+5;Restored?.Invoke();Changed?.Invoke();
        }
        private void ApplyVisuals()
        {
            RefreshShepherdVisual();
            if(seedCaches!=null)foreach(var cache in seedCaches)if(cache!=null)cache.Refresh();
        }
        private void RefreshShepherdVisual()
        {
            if (shepherdVisual != null) shepherdVisual.SetActive(ShepherdWorking);
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
            if(cache==null||!cache.isActiveAndEnabled||!cache.WithinReach(_player.position)||Cache(id).nextAvailableUnixMs>now
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
        internal bool HasWorkerHousing => _buildings?.HasWorkerHousing == true;
        private bool ShepherdWorking => _data.shepherdHired && HasWorkerHousing;
        internal string ShepherdHireUnavailable => _data.shepherdHired ? FarmText.Get("market.alreadyHired")
            : !HasWorkerHousing ? FarmText.Get("market.shepherdHousing") : null;

        public bool HireShepherd() => NearMarket && TryHireShepherd(ShepherdPrice, out _);
        public bool CanHireShepherd => _configured && !_restoring && NearMarket && Balance >= ShepherdPrice
            && ShepherdHireUnavailable == null;

        internal bool TryHireShepherd(int total, out string reason)
        {
            reason = !_configured || _restoring || total < ShepherdPrice
                ? FarmText.Get("market.staleOrder") : ShepherdHireUnavailable;
            if (reason != null) return false;
            if (total > Balance) { reason = FarmText.MissingCoins(total - Balance); return false; }

            // A cart can include other upgrades. Commit the hiring and the full debit before publishing either.
            _wallet.SetCoinsSilently(Balance - total);
            _data.shepherdHired = true;
            RefreshShepherdVisual();
            GameEvents.RaiseMoneyChanged(Balance);
            GameEvents.Record("spent", value: total);
            return true;
        }
        // Подписываю в журнале текущий режим и объясняю, почему он выбран.
        public string ShepherdStatus => _data.shepherdHired && !ShepherdWorking ? FarmText.Get("journal.shepherdWaiting")
            : _data.shepherdPriority == ShepherdPriority.CollectFirst
            ? "Сначала сбор · вы часто оставляли готовые яйца"
            : _data.shepherdAdapted ? "Сначала кормление · вы снова собираете яйца сами"
            : "Сначала кормление · наблюдает, как часто вы собираете яйца";

        // Храню последние три случая: игрок успел забрать яйцо или пропустил окно.
        private void ObserveEggCollection(bool manual)
        {
            var observations = _data.shepherdObservations;
            observations.Add(manual);
            if (observations.Count > 3) observations.RemoveAt(0);
            var timely = 0;
            foreach (var observed in observations) if (observed) timely++;
            var next = _data.shepherdPriority;
            // Два ручных сбора — сначала кормим. Два пропуска — сначала собираем.
            if (timely >= 2) next = ShepherdPriority.FeedFirst;
            else if (observations.Count - timely >= 2) next = ShepherdPriority.CollectFirst;
            if (_data.shepherdPriority == next) return;
            _data.shepherdPriority = next;
            _data.shepherdAdapted = true;
            _shepherdChanged = true;
        }

        // Когда ферма активна, даю пастуху выбрать дело примерно раз в пять секунд.
        private void Update()
        {
            if (Time.timeScale <= 0) return;
            if(!_configured)return;
            if(_input!=null&&_input.JournalPressed&&journal!=null)
            { if (gameMenus != null) gameMenus.OpenJournal(); else journal.Open(); }
            if (_shepherdChanged) { _shepherdChanged = false; Changed?.Invoke(); }
            if(_restoring||_animals==null||!Application.isFocused||Time.unscaledTime<_nextCareAt||!ShepherdWorking)return;
            _nextCareAt=Time.unscaledTime+5;
            _animals.AutomaticCare(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), _data.shepherdPriority);
        }
        // Проверяю статистику и данные пастуха перед загрузкой.
        public static bool IsValid(FarmProgressData data)
        {
            if(data==null)return true; // v1–10 have no historical counters.
            foreach(var value in new[]{data.revenue,data.expenses,data.refunds,data.plantings,data.harvests,data.harvestedUnits,data.apples,data.eggs,data.bonusHarvests,data.rareSeeds})
                if(value<0||value>CounterLimit)return false;
            if(data.shepherdObservations==null || data.shepherdObservations.Count>3 ||
                (data.shepherdPriority != ShepherdPriority.FeedFirst && data.shepherdPriority != ShepherdPriority.CollectFirst) ||
                (!data.shepherdHired && (data.shepherdAdapted || data.shepherdObservations.Count > 0 ||
                    data.shepherdPriority != ShepherdPriority.FeedFirst))) return false;
            if(data.highestBalance<0||data.crops==null||data.crops.Count>FarmCatalog.CropIds.Length||data.achievements==null||data.achievements.Count>20
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
