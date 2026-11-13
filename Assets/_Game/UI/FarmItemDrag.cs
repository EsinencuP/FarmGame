using System;
using MyLittleFarm.Gameplay.Economy;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MyLittleFarm.UI
{
    /// <summary>A gesture owns one immutable snapshot. Consuming it precedes any mutation.</summary>
    public sealed class FarmItemDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        public enum Source { Inventory, Warehouse, QuickSlot }
        public sealed class Payload
        {
            public Source From;
            public string Item, Warehouse, Token;
            public int Amount, Slot;
            public long InventoryRevision, StorageRevision, SlotRevision;
            public InventorySystem Inventory;
            public QuickSlotSystem Slots;
            public WarehouseStore Storage;
            public Sprite Icon;
        }
        private Func<Payload> _begin;
        private Action<Payload> _drop;
        private static Payload _active;
        private static GameObject _ghost;
        private static FarmItemDrag _owner;
        public void Configure(Func<Payload> begin, Action<Payload> drop) { _begin=begin; _drop=drop; }
        public static Payload Snapshot(Source from,string item,int amount,int slot,string warehouse,InventorySystem inventory,QuickSlotSystem slots,WarehouseStore storage,Sprite icon)
            => new Payload{From=from,Item=item,Amount=amount,Slot=slot,Warehouse=warehouse,Inventory=inventory,Slots=slots,Storage=storage,Icon=icon,
                Token=Guid.NewGuid().ToString("N"),InventoryRevision=inventory.Revision,StorageRevision=storage?.Revision??0,SlotRevision=slots.Revision};
        public static bool Current(Payload p,InventorySystem inventory,QuickSlotSystem slots,WarehouseStore storage=null)
            => p!=null && p.Inventory==inventory && p.Slots==slots && p.InventoryRevision==inventory.Revision && p.SlotRevision==slots.Revision
                && (storage==null || p.Storage==storage && p.StorageRevision==storage.Revision);
        public static void DropOnSlot(Payload p,int index,InventorySystem inventory,QuickSlotSystem slots)
        {
            if(!Current(p,inventory,slots) || p.From==Source.Warehouse)return;
            if(p.From==Source.QuickSlot)slots.Swap(p.Slot,index);else slots.Assign(p.Item,index);
        }
        public void OnBeginDrag(PointerEventData e)
        {
            Cancel(); if(e.button!=PointerEventData.InputButton.Left)return;
            _active=_begin?.Invoke(); if(_active==null || string.IsNullOrEmpty(_active.Item)){_active=null;return;}
            e.eligibleForClick=false; _owner=this;
            var canvas=GetComponentInParent<Canvas>().rootCanvas;
            var icon=FarmUITheme.Image(canvas.transform,"Dragged item"); icon.sprite=_active.Icon;icon.preserveAspect=true;icon.raycastTarget=false;
            icon.rectTransform.sizeDelta=new Vector2(64,64);_ghost=icon.gameObject;
            var group=_ghost.AddComponent<CanvasGroup>();group.blocksRaycasts=false;group.interactable=false;group.alpha=.9f;
            OnDrag(e);
        }
        public void OnDrag(PointerEventData e)
        {
            if(_ghost==null)return;
            var parent=(RectTransform)_ghost.transform.parent;
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,e.position,e.pressEventCamera,out var point))
                ((RectTransform)_ghost.transform).localPosition=point;
        }
        public void OnDrop(PointerEventData e)
        {
            if(e.button!=PointerEventData.InputButton.Left || _active==null)return;
            var payload=_active;Cancel();_drop?.Invoke(payload);
        }
        public void OnEndDrag(PointerEventData e)=>Cancel();
        private void OnDisable(){if(_owner==this)Cancel();}
        public static void Cancel(){_active=null;_owner=null;if(_ghost!=null)Destroy(_ghost);_ghost=null;}
        public static FarmItemDrag Install(GameObject target,Func<Payload> begin,Action<Payload> drop)
        {var hook=target.GetComponent<FarmItemDrag>()??target.AddComponent<FarmItemDrag>();hook.Configure(begin,drop);return hook;}
    }
}
