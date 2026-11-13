using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Building;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Economy
{
    public sealed class FeederStore
    {
        public const int Capacity=12;
        public const float Radius=6;
        private readonly BuildSystem _build;
        private readonly InventorySystem _inventory;
        private readonly Dictionary<string,int> _wheat=new Dictionary<string,int>();
        private bool _changing;
        public long Revision {get;private set;}
        public bool IsChanging=>_changing;
        public FeederStore(BuildSystem build,InventorySystem inventory){_build=build;_inventory=inventory;}
        public int Amount(string id)=>id!=null && _wheat.TryGetValue(id,out var n)?n:0;
        public bool Transfer(string id,int amount,bool deposit,long inventoryRevision,long feederRevision,out string message)
        {
            message="Действие устарело. Повторите перенос.";
            if(_changing || inventoryRevision!=_inventory.Revision || feederRevision!=Revision || _build.GetBuilding(id)?.definitionId!="feeder")return false;
            var held=_inventory.GetAmount("wheat");var stored=Amount(id);
            if(amount<=0 || (deposit?(amount>held || amount>Capacity-stored):(amount>stored || (long)held+amount>int.MaxValue)))
            {message="Недостаточно пшеницы или места. Ничего не перемещено.";return false;}
            _changing=true;
            try
            {
                _wheat[id]=stored+(deposit?amount:-amount);Revision++;
                _inventory.SetAmountSilently("wheat",held+(deposit?-amount:amount));
                message="Перемещено: "+amount+" пшеницы.";GameEvents.RaiseInventoryChanged();return true;
            }
            finally{_changing=false;}
        }
        // AnimalSystem owns the feeding mutation guard and publishes only after its timer is set.
        public bool CanSupply(Vector3 position)=>Find(position)!=null;
        private string Find(Vector3 position)
        {
            foreach(var b in _build.Capture().Where(b=>b.definitionId=="feeder").OrderBy(b=>b.id,StringComparer.Ordinal))
                if(Amount(b.id)>0 && _build.FeederCovers(b.id,position))return b.id;
            return null;
        }
        public bool Consume(Vector3 position)
        {if(_changing)return false;var id=Find(position);if(id==null)return false;_wheat[id]=Amount(id)-1;Revision++;return true;}
        public void ForgetEmpty(string id)
        {if(_changing || Amount(id)>0)throw new InvalidOperationException("Empty the feeder first.");_wheat.Remove(id);Revision++;}
        public List<FeederData> Capture()=>_build.Capture().Where(b=>b.definitionId=="feeder").Select(b=>new FeederData{buildingId=b.id,wheat=Amount(b.id)}).ToList();
        public void Restore(List<FeederData> data){_wheat.Clear();Revision++;foreach(var f in data)_wheat.Add(f.buildingId,f.wheat);}
        public static void Validate(List<FeederData> data,List<BuildingRuntimeState> buildings)
        {
            if(data==null || data.Count>100000)throw new InvalidDataException("Invalid feeders.");
            var ids=new HashSet<string>(buildings.Where(b=>b.definitionId=="feeder").Select(b=>b.id));
            foreach(var f in data)if(f==null || f.buildingId==null || !ids.Remove(f.buildingId) || f.wheat<0 || f.wheat>Capacity)throw new InvalidDataException("Invalid feeder contents or owner.");
        }
    }
}
