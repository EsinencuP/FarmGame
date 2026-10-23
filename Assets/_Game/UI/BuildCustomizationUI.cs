using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Building;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MyLittleFarm.UI
{
    /// <summary>Baked construction catalogue and modal editing; no gameplay initialization in Edit Mode.</summary>
    public sealed class BuildCustomizationUI : MonoBehaviour
    {
        [SerializeField] private BuildSystem buildings;
        [SerializeField] private InputReader input;
        [SerializeField] private Button openCatalogue;
        [SerializeField] private GameObject shade;
        [SerializeField] private RectTransform cataloguePanel,signPanel,housePanel;
        [SerializeField] private Text catalogueTitle,houseDescription,feedback;
        [SerializeField] private Button[] itemButtons;
        [SerializeField] private Text[] itemLabels;
        [SerializeField] private Button previous,next,closeCatalogue,saveText,closeText,upgradeHouse,closeHouse;
        [SerializeField] private Text upgradeLabel;
        [SerializeField] private InputField signInput;
        private string _detailId;
        private int _page;
        private bool _bound;

        private void OnEnable()
        {
            if(!Application.isPlaying||buildings==null||input==null)return;
            buildings.ActiveChanged+=OnBuildMode;
            openCatalogue.onClick.AddListener(ToggleCatalogue);
            previous.onClick.AddListener(()=>ShowPage(_page-1));next.onClick.AddListener(()=>ShowPage(_page+1));
            closeCatalogue.onClick.AddListener(Close);closeText.onClick.AddListener(Close);closeHouse.onClick.AddListener(Close);
            saveText.onClick.AddListener(SaveText);upgradeHouse.onClick.AddListener(Upgrade);
            for(var i=0;i<itemButtons.Length;i++){var slot=i;itemButtons[i].onClick.AddListener(()=>Choose(slot));}
            _bound=true;OnBuildMode(buildings.IsActive);
        }
        private void OnDisable()
        {
            if(!_bound)return;
            buildings.ActiveChanged-=OnBuildMode;
            foreach(var button in itemButtons)button.onClick.RemoveAllListeners();
            foreach(var button in new[]{openCatalogue,previous,next,closeCatalogue,saveText,closeText,upgradeHouse,closeHouse})button.onClick.RemoveAllListeners();
            Close();_bound=false;
        }
        private void Update()
        {
            if(input==null||!Application.isPlaying)return;
            if(input.ModalCancelPressed&&shade.activeSelf){Close();return;}
            if(input.CatalogPressed&&buildings.IsActive&&(!shade.activeSelf||cataloguePanel.gameObject.activeSelf))ToggleCatalogue();
            if(input.ModalSubmitPressed&&signPanel.gameObject.activeSelf)SaveText();
        }
        private void OnBuildMode(bool active)
        {openCatalogue.gameObject.SetActive(active);if(!active)Close();}

        public bool BlocksWorldPointer(Vector2 position)=>isActiveAndEnabled
            &&(shade.activeSelf||(openCatalogue.gameObject.activeInHierarchy
                &&RectTransformUtility.RectangleContainsScreenPoint(openCatalogue.GetComponent<RectTransform>(),position)));

        private void ToggleCatalogue()
        {
            if(cataloguePanel.gameObject.activeSelf){Close();return;}
            Close();shade.SetActive(true);cataloguePanel.gameObject.SetActive(true);input.ModalInputActive=true;
            ShowPage(_page);Focus(itemButtons[0].gameObject);
        }
        public void OpenCatalogue()
        {
            if (!Application.isPlaying || input == null || input.ModalInputActive || buildings == null || !buildings.IsActive) return;
            if (!shade.activeSelf) ToggleCatalogue();
        }
        private void ShowPage(int page)
        {
            var count=BuildingDefinition.Catalog.Count;var pages=(count+itemButtons.Length-1)/itemButtons.Length;
            _page=Mathf.Clamp(page,0,pages-1);catalogueTitle.text="КАТАЛОГ ФЕРМЫ   "+(_page+1)+" / "+pages;
            previous.interactable=_page>0;next.interactable=_page+1<pages;
            for(var i=0;i<itemButtons.Length;i++)
            {
                var index=_page*itemButtons.Length+i;itemButtons[i].gameObject.SetActive(index<count);
                if(index>=count)continue;var definition=BuildingDefinition.Catalog[index];
                itemLabels[i].text=definition.Name+"\n"+definition.Price+" монет · "+definition.Width+"×"+definition.Depth;
                var icon = itemButtons[i].transform.Find("Catalogue icon");
                if (icon != null)
                {
                    var sprite = FarmUITheme.Sprite("Icons/b_" + definition.Id);
                    if (sprite == null) sprite = FarmUITheme.Sprite("Icons/catalog");
                    icon.GetComponent<Image>().sprite = sprite;
                }
            }
        }
        private void Choose(int slot){buildings.SelectDefinition(_page*itemButtons.Length+slot);Close();}
        public void ShowDetails(BuildingRuntimeState state)
        {
            if(!Application.isPlaying||state==null)return;
            if(state.definitionId!="house"&&!FarmDecorationCatalog.SupportsText(state.definitionId))
            {GameEvents.RaiseStatusChanged("Этот предмет можно перенести (M) или удалить (Delete)");return;}
            Close();_detailId=state.id;shade.SetActive(true);input.ModalInputActive=true;
            if(FarmDecorationCatalog.SupportsText(state.definitionId))
            {
                signPanel.gameObject.SetActive(true);signInput.text=state.customText??string.Empty;
                Focus(signInput.gameObject);signInput.ActivateInputField();
            }
            else
            {
                housePanel.gameObject.SetActive(true);var level=state.appearanceLevel;
                houseDescription.text="ДОМ · УРОВЕНЬ "+(level+1)+" / 3\n"+FarmDecorationCatalog.HouseLevelName(level)
                    +(level<2?"\nСледующий: "+FarmDecorationCatalog.HouseLevelName(level+1):"\nУсадьба полностью улучшена");
                upgradeHouse.gameObject.SetActive(level<2);
                upgradeLabel.text="На рынке · "+FarmDecorationCatalog.HouseUpgradePrice(level)+" монет";
                upgradeHouse.interactable=false;
                feedback.text=string.Empty;Focus(level<2?upgradeHouse.gameObject:closeHouse.gameObject);
            }
        }
        private void SaveText()
        {
            if(_detailId==null)return;
            if(buildings.TrySetSignText(_detailId,signInput.text,out var reason))Close();
            GameEvents.RaiseStatusChanged(reason);
        }
        private void Upgrade()
        {
            if(_detailId==null)return;
            var id=_detailId;
            if(buildings.TryUpgradeHouse(id,out var reason))ShowDetails(buildings.GetBuilding(id));
            feedback.text=reason;GameEvents.RaiseStatusChanged(reason);
        }
        private void Close()
        {
            if(shade==null)return;
            var ownedModal=shade.activeSelf;
            shade.SetActive(false);cataloguePanel.gameObject.SetActive(false);signPanel.gameObject.SetActive(false);housePanel.gameObject.SetActive(false);
            _detailId=null;if(input!=null&&ownedModal){input.ModalInputActive=false;input.SuppressGameplayThisFrame=true;}
            if(ownedModal&&EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(null);
        }
        private static void Focus(GameObject target){if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(target);}

        private void OnRectTransformDimensionsChange()=>Resize();
        private void Resize()
        {
            if(cataloguePanel==null||signPanel==null||housePanel==null||previous==null)return;
            var canvas=GetComponentInParent<Canvas>();if(canvas==null)return;
            var area=canvas.GetComponent<RectTransform>().rect.size;
            var width=Mathf.Max(260,Mathf.Min(640,area.x-40));var height=Mathf.Max(320,Mathf.Min(590,area.y-40));
            cataloguePanel.sizeDelta=new Vector2(width,height);signPanel.sizeDelta=new Vector2(width,310);housePanel.sizeDelta=new Vector2(width,340);
            var grid=cataloguePanel.GetComponentInChildren<GridLayoutGroup>(true);
            grid.cellSize=new Vector2((width-52)/2,(height-225)/5);
            foreach(var button in new[]{previous,next,closeCatalogue})button.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,(width-64)/3);
            foreach(var button in new[]{saveText,closeText,upgradeHouse,closeHouse})button.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,(width-72)/2);
        }

        // Called solely by explicit Editor setup. All references are serialized into Prototype.
        public static BuildCustomizationUI Create(Transform parent,BuildSystem build,InputReader reader,Font font)
        {
            var root=new GameObject("Farm Customization",typeof(RectTransform),typeof(BuildCustomizationUI));root.transform.SetParent(parent,false);
            Stretch(root.GetComponent<RectTransform>());var ui=root.GetComponent<BuildCustomizationUI>();ui.buildings=build;ui.input=reader;
            ui.openCatalogue=Button(root.transform,"Каталог · Tab",font,new Vector2(220,46));
            var open=ui.openCatalogue.GetComponent<RectTransform>();open.anchorMin=open.anchorMax=open.pivot=Vector2.one;open.anchoredPosition=new Vector2(-22,-20);
            ui.shade=new GameObject("Modal Backdrop",typeof(RectTransform),typeof(Image));ui.shade.transform.SetParent(root.transform,false);Stretch(ui.shade.GetComponent<RectTransform>());
            ui.shade.GetComponent<Image>().color=new Color(.015f,.03f,.02f,.8f);
            ui.cataloguePanel=Panel(ui.shade.transform,"Catalogue",new Vector2(640,590));
            ui.catalogueTitle=Label(ui.cataloguePanel,"КАТАЛОГ ФЕРМЫ",font,24);Top(ui.catalogueTitle.rectTransform,20,18,40);
            var hint=Label(ui.cataloguePanel,"Выберите предмет, затем разместите его на сетке",font,17);Top(hint.rectTransform,20,60,30);
            var gridObject=new GameObject("Items",typeof(RectTransform),typeof(GridLayoutGroup));gridObject.transform.SetParent(ui.cataloguePanel,false);
            var rect=gridObject.GetComponent<RectTransform>();Stretch(rect);rect.offsetMin=new Vector2(20,85);rect.offsetMax=new Vector2(-20,-100);
            var grid=gridObject.GetComponent<GridLayoutGroup>();grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=2;grid.spacing=new Vector2(12,10);
            ui.itemButtons=new Button[10];ui.itemLabels=new Text[10];
            for(var i=0;i<10;i++){ui.itemButtons[i]=Button(gridObject.transform,"",font,new Vector2(294,80));ui.itemLabels[i]=ui.itemButtons[i].GetComponentInChildren<Text>();}
            ui.previous=Button(ui.cataloguePanel,"Назад",font,new Vector2(140,44));Bottom(ui.previous,20,20,false);
            ui.next=Button(ui.cataloguePanel,"Далее",font,new Vector2(140,44));Bottom(ui.next,-20,20,true);
            ui.closeCatalogue=Button(ui.cataloguePanel,"Закрыть · Esc",font,new Vector2(190,44));Bottom(ui.closeCatalogue,0,20,null);
            ui.signPanel=Panel(ui.shade.transform,"Sign Text",new Vector2(640,310));
            var title=Label(ui.signPanel,"ТЕКСТ ВЫВЕСКИ",font,24);Top(title.rectTransform,20,20,42);
            var caption=Label(ui.signPanel,"До 48 символов · текст появится с обеих сторон",font,18);Top(caption.rectTransform,20,65,38);
            var field=new GameObject("Sign Input",typeof(RectTransform),typeof(Image),typeof(InputField));field.transform.SetParent(ui.signPanel,false);Top(field.GetComponent<RectTransform>(),26,120,62);
            field.GetComponent<Image>().color=new Color(.96f,.94f,.85f);ui.signInput=field.GetComponent<InputField>();
            var content=Label(field.transform,"",font,24);content.color=new Color(.07f,.12f,.08f);Stretch(content.rectTransform);content.rectTransform.offsetMin=new Vector2(14,8);content.rectTransform.offsetMax=new Vector2(-14,-8);content.alignment=TextAnchor.MiddleLeft;
            ui.signInput.textComponent=content;ui.signInput.characterLimit=FarmDecorationCatalog.MaximumSignLength;ui.signInput.lineType=InputField.LineType.SingleLine;
            var placeholder=Label(field.transform,"Моя ферма",font,24);placeholder.color=new Color(.4f,.44f,.38f);Stretch(placeholder.rectTransform);placeholder.rectTransform.offsetMin=new Vector2(14,8);placeholder.rectTransform.offsetMax=new Vector2(-14,-8);placeholder.alignment=TextAnchor.MiddleLeft;ui.signInput.placeholder=placeholder;
            ui.saveText=Button(ui.signPanel,"Сохранить · Enter",font,new Vector2(250,46));Bottom(ui.saveText,24,26,false);
            ui.closeText=Button(ui.signPanel,"Отмена · Esc",font,new Vector2(210,46));Bottom(ui.closeText,-24,26,true);
            ui.housePanel=Panel(ui.shade.transform,"House Upgrade",new Vector2(640,340));
            ui.houseDescription=Label(ui.housePanel,"",font,23);Top(ui.houseDescription.rectTransform,24,20,140);
            ui.feedback=Label(ui.housePanel,"",font,18);Top(ui.feedback.rectTransform,24,165,62);
            ui.upgradeHouse=Button(ui.housePanel,"Улучшить",font,new Vector2(290,46));ui.upgradeLabel=ui.upgradeHouse.GetComponentInChildren<Text>();Bottom(ui.upgradeHouse,24,28,false);
            ui.closeHouse=Button(ui.housePanel,"Закрыть · Esc",font,new Vector2(210,46));Bottom(ui.closeHouse,-24,28,true);
            ui.ShowPage(0);ui.Resize();ui.Close();ui.openCatalogue.gameObject.SetActive(false);return ui;
        }
        private static RectTransform Panel(Transform parent,string name,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
            go.GetComponent<Image>().color=new Color(.055f,.105f,.075f,.99f);var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=size;return rect;
        }
        private static Text Label(Transform parent,string content,Font font,int size)
        {
            var go=new GameObject("Label",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var text=go.GetComponent<Text>();text.font=font;text.fontSize=size;text.color=new Color(.98f,.96f,.87f);text.text=content;text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;text.supportRichText=false;return text;
        }
        private static Button Button(Transform parent,string title,Font font,Vector2 size)
        {
            var go=new GameObject(title,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);go.GetComponent<RectTransform>().sizeDelta=size;go.GetComponent<Image>().color=new Color(.18f,.30f,.21f);
            var button=go.GetComponent<Button>();var colors=button.colors;colors.highlightedColor=new Color(.72f,.86f,.59f);colors.selectedColor=colors.highlightedColor;colors.pressedColor=new Color(.48f,.65f,.37f);button.colors=colors;
            var text=Label(go.transform,title,font,19);Stretch(text.rectTransform);text.rectTransform.offsetMin=new Vector2(8,3);text.rectTransform.offsetMax=new Vector2(-8,-3);text.resizeTextForBestFit=true;text.resizeTextMinSize=14;text.resizeTextMaxSize=19;return button;
        }
        private static void Stretch(RectTransform rect){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
        private static void Top(RectTransform rect,float inset,float y,float height)
        {rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,1);rect.offsetMin=new Vector2(inset,-y-height);rect.offsetMax=new Vector2(-inset,-y);}
        private static void Bottom(Button button,float x,float y,bool? right)
        {var rect=button.GetComponent<RectTransform>();var anchor=right==null?.5f:right.Value?1f:0f;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(anchor,0);rect.anchoredPosition=new Vector2(x,y);}
    }
}
