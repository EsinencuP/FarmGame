using System;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.World;
using UnityEngine;
using UnityEngine.UI;

namespace MyLittleFarm.UI
{
    public sealed class FarmFeederUI:MonoBehaviour
    {
        private BuildSystem _build;
        private InventorySystem _inventory;
        private PlayerController _player;
        private string _id;
        private Text _state,_note;
        private Button _one,_five,_fill,_take;
        private int _lastFrame=-1;
        public RectTransform Panel=>(RectTransform)transform;
        public Button CloseButton {get;private set;}
        public void Open(string id){_id=id;gameObject.SetActive(true);_note.text="Сначала кормушка, затем запас инвентаря. Пастух собирает яйца; молоко — вручную.";Refresh();}
        public void Hide(){gameObject.SetActive(false);_id=null;}
        private void OnEnable()=>GameEvents.InventoryChanged+=Refresh;
        private void OnDisable()=>GameEvents.InventoryChanged-=Refresh;
        private void Transfer(int amount,bool deposit)
        {
            if(_lastFrame==Time.frameCount || _player==null || !_build.FeederInReach(_id,_player.transform.position))return;
            _lastFrame=Time.frameCount;
            _build.Feeders.Transfer(_id,amount,deposit,_inventory.Revision,_build.Feeders.Revision,out var message);_note.text=message;Refresh();
        }
        private void Refresh()
        {
            if(_state==null)return;var count=_build.Feeders.Amount(_id);var owned=_inventory.GetAmount("wheat");
            var near=_player!=null && _build.FeederInReach(_id,_player.transform.position);
            _state.text="Пшеница в кормушке: "+count+" / "+FeederStore.Capacity+"\nВ инвентаре: "+owned+"\nОбласть кормления: 6 м. Один корм — одно производство.";
            _one.interactable=near&&owned>0&&count<FeederStore.Capacity;
            _five.interactable=near&&owned>=5&&count<=FeederStore.Capacity-5;
            _fill.interactable=_one.interactable;_take.interactable=near&&count>0&&(long)owned+count<=int.MaxValue;
        }
        public static FarmFeederUI Create(Transform parent,Font font,BuildSystem build)
        {
            var panel=FarmUITheme.Panel(parent,"КОРМУШКА",font,new Vector2(760,450));panel.gameObject.SetActive(false);
            var ui=panel.gameObject.AddComponent<FarmFeederUI>();ui._build=build;
            var farm=build.GetComponentInParent<GameBootstrap>();ui._inventory=farm.GetComponentInChildren<InventorySystem>(true);ui._player=farm.GetComponentInChildren<PlayerController>(true);
            ui._state=FarmUITheme.Label(panel,"",font,26);Place(ui._state.rectTransform,35,65,690,130);
            ui._note=FarmUITheme.Label(panel,"",font,21);Place(ui._note.rectTransform,35,205,690,70);
            ui._one=Button(panel,"+1",font,30,290,100,()=>ui.Transfer(1,true));ui._five=Button(panel,"+5",font,140,290,100,()=>ui.Transfer(5,true));
            ui._fill=Button(panel,"Пополнить",font,250,290,225,()=>ui.Transfer(Math.Min(ui._inventory.GetAmount("wheat"),FeederStore.Capacity-ui._build.Feeders.Amount(ui._id)),true));
            ui._take=Button(panel,"Забрать корм",font,485,290,245,()=>ui.Transfer(ui._build.Feeders.Amount(ui._id),false));
            ui.CloseButton=Button(panel,"Закрыть",font,250,365,260,()=>{},"ghost");return ui;
        }
        private static void Place(RectTransform rect,float x,float y,float w,float h)=>FarmUITheme.Place(rect,new Vector2(0,1),new Vector2(0,1),new Vector2(x,-y),new Vector2(w,h));
        private static Button Button(Transform parent,string text,Font font,float x,float y,float width,Action action,string style="primary")
        {var b=FarmUITheme.Button(parent,text,font,null,style);Place((RectTransform)b.transform,x,y,width,58);FarmUITheme.FitCompactButton(b,24);b.onClick.AddListener(()=>action());return b;}
    }
}
