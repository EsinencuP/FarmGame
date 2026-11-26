using System;
using System.Collections.Generic;
using System.IO;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Farming;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Economy
{
    [DefaultExecutionOrder(-120)]
    public sealed class QuickSlotSystem : MonoBehaviour
    {
        public enum SelectionMode { Item, Hand, Hoe }
        public const int ItemSlotCount=6;
        [SerializeField, Range(0,5)] private int startingSlot;
        private InputReader_AI _input;
        private FarmCatalog _catalog;
        private InventorySystem_AI _inventory;
        private string[] _items=new string[ItemSlotCount];
        private bool _changing;
        public int SelectedIndex {get;private set;}
        public SelectionMode Mode {get;private set;}
        public long Revision {get;private set;}
        public int SlotCount=>ItemSlotCount;
        public string SelectedItemId=>Mode==SelectionMode.Item?ItemAt(SelectedIndex):null;
        public CropDefinition SelectedCrop
        { get {if(_catalog!=null && Mode==SelectionMode.Item)foreach(var crop in _catalog.Crops)if(crop.SeedItemId==SelectedItemId)return crop;return null;} }
        public string ItemAt(int index)=>index>=0 && index<ItemSlotCount?_items[index]:null;
        internal int SlotFor(string itemId) => Array.IndexOf(_items, itemId);
        public static List<string> DefaultAssignments()
        {var result=new List<string>();foreach(var id in FarmCatalog.CropIds){if(result.Count==ItemSlotCount)break;result.Add(id+"_seed");}return result;}
        public void Configure(InputReader_AI input,FarmCatalog catalog,InventorySystem_AI inventory=null)
        {_input=input;_catalog=catalog;_inventory=inventory;_items=DefaultAssignments().ToArray();SelectedIndex=startingSlot;Mode=SelectionMode.Item;Revision++;}
        private void Update()
        {
            if (_input == null || _input.BuildModeActive || _input.ModalInputActive || _input.SuppressGameplayThisFrame) return;
            if (_input.ToggleToolPressed)
                SelectTool(Mode == SelectionMode.Hoe ? SelectionMode.Hand : SelectionMode.Hoe);
            else if (_input.SeedSelection >= 0)
                Select(_input.SeedSelection);
        }
        public bool Select(int index)
        {
            if(_changing || index<0 || index>=ItemSlotCount)return false;
            if(index==SelectedIndex && Mode==SelectionMode.Item)return true;
            return Commit(()=>{SelectedIndex=index;Mode=SelectionMode.Item;});
        }
        public bool SelectTool(SelectionMode mode)
        {if(_changing || (mode!=SelectionMode.Hand && mode!=SelectionMode.Hoe))return false;if(Mode==mode)return true;return Commit(()=>Mode=mode);}
        public bool Assign(string item,int index)
        {
            if(_changing || index<0 || index>=ItemSlotCount || _catalog==null || string.IsNullOrWhiteSpace(item)
                || (!_catalog.TryGetItem(item,out _) && (_inventory==null || _inventory.GetAmount(item)==0)))return false;
            return Commit(()=>{var previous=Array.IndexOf(_items,item);if(previous>=0 && previous!=index)_items[previous]=_items[index];_items[index]=item;});
        }
        public bool Swap(int a,int b)
        {if(_changing || a<0 || b<0 || a>=ItemSlotCount || b>=ItemSlotCount)return false;return Commit(()=>{var item=_items[a];_items[a]=_items[b];_items[b]=item;});}
        public bool Clear(int index)
        {if(_changing || index<0 || index>=ItemSlotCount)return false;return Commit(()=>_items[index]=null);}
        private bool Commit(Action action)
        {_changing=true;try{action();Revision++;GameEvents.RaiseInventoryChanged();return true;}finally{_changing=false;}}
        public List<string> Capture()=>new List<string>(_items);
        public void Restore(List<string> items,int selected,int mode)
        {
            _items=items.ToArray();
            // JsonUtility serializes null string entries as empty strings.
            for(var i=0;i<_items.Length;i++)if(string.IsNullOrEmpty(_items[i]))_items[i]=null;
            SelectedIndex=selected;Mode=(SelectionMode)mode;Revision++;
        }
        public static void Validate(List<string> items,int mode)
        {
            if(items==null || items.Count!=ItemSlotCount || mode<0 || mode>2)throw new InvalidDataException("Quick slot assignments are invalid.");
            var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var item in items)if(!string.IsNullOrEmpty(item) && (string.IsNullOrWhiteSpace(item) || item.Length>128 || !ids.Add(item)))
                throw new InvalidDataException("Quick slot item is invalid or assigned twice.");
        }
    }
}
