using System;
using System.Collections.Generic;
using System.Linq;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.Farming;
using UnityEngine;
using UnityEngine.UI;

namespace MyLittleFarm.UI
{
    public sealed partial class FarmInventoryUI_AI
    {
        private ItemWorkspace _workspace;
        private void OpenWorkspace(string warehouse,Action back)
        {
            _market=false;
            SetOriginalInventoryVisible(false);
            if(_workspace==null)
            {
                _workspace=new ItemWorkspace(this);
            }
            _workspace.SetVisible(true);_workspace.Open(warehouse);gameObject.SetActive(true);Refresh();
            heading.text=warehouse==null?"Инвентарь":"Склад";
            FarmUIFocus.Show(panel,_workspace.First,back);
        }
        private void SetOriginalInventoryVisible(bool visible)
        {
            foreach(var tab in tabs)tab.gameObject.SetActive(visible);
            gridRect.gameObject.SetActive(visible);detailIcon.transform.parent.gameObject.SetActive(visible);
            balance.gameObject.SetActive(visible);hint.gameObject.SetActive(visible);
            var coin=panel.Find("Inventory coin");if(coin!=null)coin.gameObject.SetActive(visible);
        }
        public void OpenWarehouse(string id,Action back=null)=>OpenWorkspace(id,back);

        /// <summary>One quantity per item ID; grids are projections and quick slots contain references.</summary>
        private sealed class ItemWorkspace
        {
            private readonly FarmInventoryUI_AI ui;
            private readonly RectTransform root,left,right;
            private readonly ScrollRect leftScroll,rightScroll;
            private readonly Text selected,detail,status,rightTitle;
            private readonly Image detailArt;
            private readonly InputField amount;
            private readonly Button[] filters=new Button[4],refs=new Button[6];
            private readonly Image[] refIcons=new Image[6];
            private readonly Text[] refAmounts=new Text[6];
            private readonly Button put,take,all,assignItem,clear,hand,hoe;
            private readonly List<Button> leftCells=new List<Button>(),rightCells=new List<Button>();
            private List<string> leftIds=new List<string>(),rightIds=new List<string>();
            private string warehouse,item;
            private bool fromWarehouse;
            private int filter;
            private int lastTransferFrame=-1;
            private readonly Font font;
            private BuildSystem Build=>ui.inventory.GetComponentInParent<GameBootstrap>()?.GetComponentInChildren<BuildSystem>(true);
            private WarehouseStore Storage=>Build?.Warehouses;
            public Button First=>filters[0];
            public ItemWorkspace(FarmInventoryUI_AI owner)
            {
                ui=owner;font=ui.heading.font;
                root=FarmUITheme.Rect(ui.panel,"Item workspace");FarmUITheme.Stretch(root);
                var names=new[]{"Все","Семена и сад","Урожай","Продукты"};
                for(var i=0;i<4;i++){var n=i;filters[i]=Button(names[i],28+i*306,64,294,54,()=>{filter=n;Refresh();},"ghost");}
                Label("ИНВЕНТАРЬ",28,124,560,34,21);
                rightTitle=Label("ПРЕДМЕТ",670,124,580,34,21);
                leftScroll=Grid("Inventory",28,out left);rightScroll=Grid("Warehouse",670,out right);
                detailArt=FarmUITheme.Icon(root,"inventory");Place(detailArt.rectTransform,714,180,120,120);
                detail=Label("",858,176,352,320,25);detail.alignment=TextAnchor.UpperLeft;
                selected=Label("Выберите предмет",28,516,1220,38,26);selected.alignment=TextAnchor.MiddleLeft;
                Button("−",28,564,54,52,()=>Change(-1),"ghost");
                var field=FarmUITheme.Image(root,"Количество","Skin/slot");Place(field.rectTransform,90,564,164,52);
                amount=field.gameObject.AddComponent<InputField>();var value=FarmUITheme.Label(field.transform,"1",font,26);
                FarmUITheme.Stretch(value.rectTransform,6);amount.textComponent=value;amount.contentType=InputField.ContentType.IntegerNumber;amount.characterLimit=10;amount.text="1";
                Button("+",262,564,54,52,()=>Change(1),"ghost");Button("Все",324,564,100,52,()=>amount.text=Maximum().ToString(),"ghost");
                put=Button("В склад →",444,564,242,52,()=>Transfer(false));take=Button("← В инвентарь",696,564,242,52,()=>Transfer(true));
                all=Button("Переместить всё в склад",444,624,494,52,TransferAll,"secondary");
                assignItem=Button("Назначить в выбранный слот",444,564,494,52,Assign);
                clear=Button("Освободить слот",950,564,298,52,()=>ui.quickSlots.Clear(ui.quickSlots.SelectedIndex),"ghost");
                Label("БЫСТРЫЕ СЛОТЫ · перетащите предмет или поменяйте слоты местами",28,630,900,34,19);
                for(var i=0;i<6;i++)
                {
                    var n=i;refs[i]=Button("",28+i*106,682,96,80,()=>ui.quickSlots.Select(n),"ghost");
                    refIcons[i]=FarmUITheme.Icon(refs[i].transform,"inventory");Place(refIcons[i].rectTransform,24,5,48,44);
                    var key=FarmUITheme.Label(refs[i].transform,InputReader_AI.KeyLabel(InputReader_AI.Command.Seed1+i),font,16);Place(key.rectTransform,4,2,26,22);
                    refAmounts[i]=FarmUITheme.Label(refs[i].transform,"",font,18);Place(refAmounts[i].rectTransform,4,52,88,24);
                    FarmItemDrag_AI.Install(refs[i].gameObject,()=>SlotPayload(n),p=>FarmItemDrag_AI.DropOnSlot(p,n,ui.inventory,ui.quickSlots));
                }
                hand=Button("Руки",700,682,150,48,()=>ui.quickSlots.SelectTool(QuickSlotSystem.SelectionMode.Hand),"secondary");
                hoe=Button("Мотыга",862,682,150,48,()=>ui.quickSlots.SelectTool(QuickSlotSystem.SelectionMode.Hoe),"secondary");

                status=Label("",28,768,850,46,19);status.alignment=TextAnchor.MiddleLeft;
            }
            private static void Place(RectTransform rect,float x,float y,float w,float h)=>FarmUITheme.Place(rect,new Vector2(0,1),new Vector2(0,1),new Vector2(x,-y),new Vector2(w,h));
            private Text Label(string title,float x,float y,float w,float h,int size)
            {var label=FarmUITheme.Label(root,title,font,size);Place(label.rectTransform,x,y,w,h);return label;}
            private Button Button(string title,float x,float y,float w,float h,Action action,string style="primary")
            {var button=FarmUITheme.Button(root,title,font,null,style);Place((RectTransform)button.transform,x,y,w,h);FarmUITheme.FitCompactButton(button,24);button.onClick.AddListener(()=>action());return button;}
            private ScrollRect Grid(string name,float x,out RectTransform content)
            {
                var viewport=FarmUITheme.Image(root,name,"Skin/slot");Place(viewport.rectTransform,x,164,580,340);
                viewport.gameObject.AddComponent<RectMask2D>();
                var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport.rectTransform;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
                content=FarmUITheme.Rect(viewport.transform,"Stacks");content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.sizeDelta=new Vector2(0,340);
                scroll.content=content;
                var grid=content.gameObject.AddComponent<GridLayoutGroup>();grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=5;
                grid.cellSize=new Vector2(100,100);grid.spacing=new Vector2(10,10);grid.padding=new RectOffset(18,18,12,12);
                FarmItemDrag_AI.Install(viewport.gameObject,null,p=>DropStorage(p,name=="Warehouse"));return scroll;
            }
            public void SetVisible(bool visible)=>root.gameObject.SetActive(visible);
            public void Open(string id){warehouse=id;item=null;fromWarehouse=false;amount.text="1";status.text="Перетащите предмет: на склад, обратно или в быстрый слот. Количество задаётся выше.";FarmItemDrag_AI.Cancel();}
            private int Category(string id)
            {id=HarvestQuality.ParseStack(id).itemId;if(id.EndsWith("_seed")||id.EndsWith("_sapling"))return 1;return ui.catalog.Crops.Any(c=>c.HarvestItemId==id)?2:3;}
            private string Name(string stackId)
            {
                var (id, size) = HarvestQuality.ParseStack(stackId);
                var name = ui.catalog.TryGetItem(id, out var definition) ? definition.DisplayName : id;
                return HarvestQuality.IsSized(id) ? name + " · " + size : name;
            }
            private Sprite Icon(string id)
            {
                id = HarvestQuality.ParseStack(id).itemId;
                return ui.catalog.TryGetItem(id,out var d)&&d.Icon!=null?d.Icon:FarmUITheme.Sprite("Icons/"+FarmUITheme.ItemIcon(id))??FarmUITheme.Sprite("Icons/inventory");
            }
            private string Describe(string stackId)
            {
                var (id, size) = HarvestQuality.ParseStack(stackId);
                if(id=="apple_sapling")return "Яблоня доступна бесплатно. Назначьте саженец на слот или выберите дерево для посадки клавишей "+InputReader_AI.KeyLabel(InputReader_AI.Command.Apple)+".";
                var text=Name(stackId)+"\n";
                if(ui.catalog.TryGetItem(id,out var definition))text+="Продажа по базовой котировке: "+HarvestQuality.Price(definition.SellPrice, 100, size)+" монет за шт.\n";
                var crop=ui.catalog.Crops.FirstOrDefault(c=>c.SeedItemId==id || c.HarvestItemId==id);
                if(crop!=null)text+="Рост: "+FarmGuideUI.Duration(crop.GrowDurationSeconds)+" · урожай: "+crop.YieldAmount+" шт.\n";
                if(crop!=null && crop.RegrowSeconds>0)text+="Повторный урожай: "+FarmGuideUI.Duration(crop.RegrowSeconds)+" без нового семени.\n";
                if(ui.orchard!=null && ui.orchard.TryGetSapling(id,out var tree))text+="Первый урожай: "+FarmGuideUI.Duration(tree.FirstHarvestSeconds)+". Дерево плодоносит повторно.\n";
                foreach(var recipe in ProductionRecipe.All)if(recipe.OutputId==id)
                    text+=recipe.InputName+" × "+recipe.InputAmount+" → "+recipe.OutputName+" · "+FarmText.Duration(recipe.Seconds)+".\n";
                return text+"\nПеретащите предмет в быстрый слот или нажмите «Назначить». Слот показывает запас инвентаря.";
            }
            private List<string> Items(bool stored)
            {
                var items=(stored?Storage?.Items(warehouse):ui.inventory.Capture())??new List<InventoryEntryData>();
                var ids=items.Where(e=>e.amount>0).Select(e=>e.StackId).ToList();
                if(!stored&&!ids.Contains("apple_sapling"))ids.Add("apple_sapling");
                return ids.Where(id=>filter==0||Category(id)==filter).OrderBy(Category).ThenBy(Name,StringComparer.CurrentCulture).ThenBy(id=>id,StringComparer.Ordinal).ToList();
            }
            private int Count(string id,bool stored)=>stored?Storage?.StackAmount(warehouse,id)??0:ui.inventory.GetStackAmount(id);
            private string CountLabel(string id,bool stored)=>!stored&&id=="apple_sapling"?"∞":"×"+Count(id,stored);
            public void Refresh()
            {
                leftIds=Items(false);rightIds=warehouse==null?new List<string>():Items(true);
                PaintGrid(left,leftCells,leftIds,false);PaintGrid(right,rightCells,rightIds,true);
                rightScroll.gameObject.SetActive(warehouse!=null);rightTitle.text=warehouse==null?"ПРЕДМЕТ":"СКЛАД · отдельное хранилище";
                detailArt.gameObject.SetActive(warehouse==null);detail.gameObject.SetActive(warehouse==null);
                put.gameObject.SetActive(warehouse!=null);take.gameObject.SetActive(warehouse!=null);all.gameObject.SetActive(warehouse!=null);
                assignItem.gameObject.SetActive(warehouse==null);
                // Bulk control and quick-slot caption occupy separate rows.
                var caption=root.GetComponentsInChildren<Text>().FirstOrDefault(t=>t.text.StartsWith("БЫСТРЫЕ СЛОТЫ"));
                if(caption!=null){caption.text=warehouse==null?"БЫСТРЫЕ СЛОТЫ · перетащите предмет или поменяйте слоты местами":"БЫСТРЫЕ СЛОТЫ";Place(caption.rectTransform,28,warehouse==null?630:648,warehouse==null?900:405,30);}
                for(var i=0;i<4;i++)FarmUITheme.Style(filters[i],filter==i?"secondary":"ghost");
                for(var i=0;i<6;i++)
                {
                    var id=ui.quickSlots.ItemAt(i);refIcons[i].gameObject.SetActive(id!=null);if(id!=null)refIcons[i].sprite=Icon(id);
                    refAmounts[i].text=id==null?"Пусто":id=="apple_sapling"?"∞":"×"+ui.inventory.GetAmount(id);
                    refs[i].GetComponent<Image>().color=ui.quickSlots.Mode==QuickSlotSystem.SelectionMode.Item&&ui.quickSlots.SelectedIndex==i?new Color(1,.87f,.43f):Color.white;
                }
                hand.GetComponent<Image>().color=ui.quickSlots.Mode==QuickSlotSystem.SelectionMode.Hand?new Color(1,.87f,.43f):Color.white;
                var tools=Build?.GetComponentInParent<GameBootstrap>()?.GetComponentInChildren<MyLittleFarm.Gameplay.World.ToolUpgradeSystem>(true);
                hoe.GetComponentInChildren<Text>().text="Мотыга · "+(tools?.AreaLabel??"1 клетка");
                hoe.GetComponent<Image>().color=ui.quickSlots.Mode==QuickSlotSystem.SelectionMode.Hoe?new Color(1,.87f,.43f):Color.white;
                selected.text=item==null?"Выберите предмет":Name(item)+" · "+(fromWarehouse?"на складе: ":"в инвентаре: ")+CountLabel(item,fromWarehouse);
                if(item!=null){detailArt.sprite=Icon(item);detail.text=Describe(item);}
                else detail.text="Выберите предмет.\n\nФильтры и сортировка по категории сохраняют порядок списка.\nДля прокрутки используйте колесо мыши.";
                assignItem.interactable=item!=null&&!fromWarehouse;clear.interactable=ui.quickSlots.ItemAt(ui.quickSlots.SelectedIndex)!=null;
                put.interactable=item!=null&&!fromWarehouse&&Count(item,false)>0;take.interactable=item!=null&&fromWarehouse&&Count(item,true)>0;
            }
            private void PaintGrid(RectTransform content,List<Button> cells,List<string> ids,bool stored)
            {
                while(cells.Count<ids.Count)
                {
                    var index=cells.Count;var image=FarmUITheme.Image(content,"Stack "+index,"Skin/slot");var b=image.gameObject.AddComponent<Button>();b.targetGraphic=image;
                    var icon=FarmUITheme.Icon(image.transform,"inventory");Place(icon.rectTransform,21,7,58,58);
                    var count=FarmUITheme.Label(image.transform,"",font,19);Place(count.rectTransform,3,69,94,27);
                    b.onClick.AddListener(()=>Select(index,stored));FarmItemDrag_AI.Install(b.gameObject,()=>ItemPayload(index,stored),p=>DropStorage(p,stored));cells.Add(b);
                }
                for(var i=0;i<cells.Count;i++)
                {
                    var active=i<ids.Count;cells[i].gameObject.SetActive(active);if(!active)continue;
                    var id=ids[i];cells[i].GetComponentsInChildren<Image>()[1].sprite=Icon(id);
                    var (itemId, size) = HarvestQuality.ParseStack(id);
                    cells[i].GetComponentInChildren<Text>().text=(HarvestQuality.IsSized(itemId) ? size+" · " : "")+CountLabel(id,stored);
                    cells[i].GetComponent<Image>().color=item==id&&fromWarehouse==stored?new Color(1,.87f,.43f):Color.white;
                }
                content.sizeDelta=new Vector2(0,Mathf.Max(340,((ids.Count+4)/5)*110+14));
            }
            private void Select(int index,bool stored)
            {var list=stored?rightIds:leftIds;if(index>=list.Count)return;item=list[index];fromWarehouse=stored;amount.text="1";Refresh();}
            private int Maximum()=>item==null?0:Count(item,fromWarehouse);
            private void Change(int delta){if(!int.TryParse(amount.text,out var n))n=1;amount.text=Math.Max(1,Math.Min((long)Math.Max(1,Maximum()),(long)n+delta)).ToString();}
            private bool Quantity(out int n){if(int.TryParse(amount.text,out n)&&n>0)return true;status.text="Введите целое положительное количество.";return false;}
            private FarmItemDrag_AI.Payload ItemPayload(int index,bool stored)
            {
                var list=stored?rightIds:leftIds;if(index>=list.Count)return null;var id=list[index];
                var n=item==id&&fromWarehouse==stored?(Quantity(out var selectedAmount)?selectedAmount:0):1;
                if(n<1)return null;
                return FarmItemDrag_AI.Snapshot(stored?FarmItemDrag_AI.Source.Warehouse:FarmItemDrag_AI.Source.Inventory,id,n,0,warehouse,ui.inventory,ui.quickSlots,Storage,Icon(id));
            }
            private FarmItemDrag_AI.Payload SlotPayload(int i)
            {var id=ui.quickSlots.ItemAt(i);return id==null?null:FarmItemDrag_AI.Snapshot(FarmItemDrag_AI.Source.QuickSlot,id,0,i,warehouse,ui.inventory,ui.quickSlots,Storage,Icon(id));}
            private bool Near()
            {
                // Player is a sibling of systems in the baked hierarchy.
                var bootstrap=ui.GetComponentInParent<GameBootstrap>();
                var motor=bootstrap?.GetComponentInChildren<MyLittleFarm.Gameplay.World.PlayerController>();
                if(warehouse!=null && motor!=null && Build.WarehouseInReach(warehouse,motor.transform.position))return true;
                status.text="Подойдите к складу и откройте его снова.";return false;
            }
            private void DropStorage(FarmItemDrag_AI.Payload p,bool destinationStored)
            {
                if(warehouse==null || p.From==FarmItemDrag_AI.Source.QuickSlot || (p.From==FarmItemDrag_AI.Source.Warehouse)==destinationStored)return;
                if(!FarmItemDrag_AI.Current(p,ui.inventory,ui.quickSlots,Storage) || p.Warehouse!=warehouse || !Near()){status.text="Действие устарело. Повторите перенос.";return;}
                Storage.Transfer(warehouse,p.Item,p.Amount,destinationStored,p.Token,p.InventoryRevision,p.StorageRevision,out var message);status.text=message;
            }
            private void Transfer(bool stored)
            {
                if(lastTransferFrame==Time.frameCount || item==null || fromWarehouse!=stored || !Quantity(out var n) || !Near())return;
                lastTransferFrame=Time.frameCount;
                Storage.Transfer(warehouse,item,n,!stored,Guid.NewGuid().ToString("N"),ui.inventory.Revision,Storage.Revision,out var message);status.text=message;
            }
            private void TransferAll()
            {if(lastTransferFrame==Time.frameCount || !Near())return;lastTransferFrame=Time.frameCount;Storage.TransferAll(warehouse,Guid.NewGuid().ToString("N"),ui.inventory.Revision,Storage.Revision,out var message);status.text=message;}
            private void Assign()
            {
                if(item==null||fromWarehouse)return;
                if(ui.quickSlots.Assign(HarvestQuality.ParseStack(item).itemId,ui.quickSlots.SelectedIndex))
                {if(ui.orchard!=null&&ui.orchard.TryGetSapling(item,out var tree))ui.orchard.SelectTree(tree.TreeId);status.text="Назначено в слот "+(ui.quickSlots.SelectedIndex+1)+". Количество хранится в инвентаре.";}
            }
        }
    }
}
