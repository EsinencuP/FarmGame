using System;
using System.Collections.Generic;
using System.Text;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MyLittleFarm.UI
{
    public sealed class FarmJournalUI : MonoBehaviour
    {
        [SerializeField] private FarmProgression progress;
        [SerializeField] private InputReader input;
        [SerializeField] private GameObject shade;
        [SerializeField] private RectTransform panel,body;
        [SerializeField] private GameObject farmTab,badgesTab,discoveriesTab;
        [SerializeField] private Text stats,discoveries,pageTitle,toast;
        [SerializeField] private Text clock;
        [SerializeField] private Text[] badgeLabels;
        [SerializeField] private GridLayoutGroup badgesGrid;
        [SerializeField] private Button farmButton,badgesButton,discoveriesButton,hire,close,previous,next;
        private bool _bound;
        private bool _previewingBadges;
        private static readonly List<string> PreviewEarned=new List<string>{"first_harvest","coins_100","house","storage","sun_bonus","rare_seed"};
        private int _tab,_page,_pageSize=10;
        private float _nextRefresh,_toastUntil;
        private float _nextClock;
        public Text ClockText=>clock;
        private readonly Queue<string> _toasts=new Queue<string>();
        public bool IsOpen=>shade!=null&&shade.activeSelf;
        public void Configure(FarmProgression system,InputReader reader){progress=system;input=reader;Bind();}
        private void OnEnable()=>Bind();
        private void Bind()
        {
            if(_bound||!Application.isPlaying||progress==null||input==null)return;
            progress.Changed+=Refresh;progress.AchievementUnlocked+=EnqueueToast;progress.Restored+=ResetToasts;
            farmButton.onClick.AddListener(ShowFarm);badgesButton.onClick.AddListener(ShowBadges);discoveriesButton.onClick.AddListener(ShowDiscoveries);
            hire.onClick.AddListener(Hire);close.onClick.AddListener(Close);previous.onClick.AddListener(Previous);next.onClick.AddListener(Next);_bound=true;
        }
        private void OnDisable()
        {
            if(!_bound)return;
            progress.Changed-=Refresh;progress.AchievementUnlocked-=EnqueueToast;progress.Restored-=ResetToasts;
            foreach(var button in new[]{farmButton,badgesButton,discoveriesButton,hire,close,previous,next})button.onClick.RemoveAllListeners();
            if(IsOpen&&input!=null){input.ModalInputActive=false;input.SuppressGameplayThisFrame=true;}_bound=false;
        }
        public void Open()
        {
            if(!Application.isPlaying||input==null||progress==null||(input.ModalInputActive&&!IsOpen))return;
            Bind();shade.SetActive(true);input.ModalInputActive=true;input.SuppressGameplayThisFrame=true;
            _tab=0;_page=0;Resize();Refresh();Focus(farmButton.gameObject);
        }
        public void Close()
        {
            var owned=IsOpen;if(shade!=null)shade.SetActive(false);
            if(owned&&Application.isPlaying&&input!=null){input.ModalInputActive=false;input.SuppressGameplayThisFrame=true;Focus(null);}
        }
        private static void Focus(GameObject target){if(Application.isPlaying&&EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(target);}
        private void ShowFarm(){_tab=0;Refresh();}
        private void ShowBadges(){_tab=1;Refresh();}
        private void ShowDiscoveries(){_tab=2;Refresh();}
        private void Previous(){_page--;Refresh();}
        private void Next(){_page++;Refresh();}
        private void Hire(){if(progress.HireShepherd())GameEvents.RaiseStatusChanged("Пастух нанят: держите пшеницу в инвентаре для кормления кур");Refresh();}
        private void Refresh()
        {
            if(!IsOpen||progress==null)return;
            farmTab.SetActive(_tab==0);badgesTab.SetActive(_tab==1);discoveriesTab.SetActive(_tab==2);
            var data=progress.Data;
            pageTitle.gameObject.SetActive(_tab==1);
            stats.text="ПРИБЫЛЬ  "+progress.Profit+" монет   ·   В кошельке  "+progress.Balance+"\nВыручка  "+data.revenue+"   ·   Покупки  "+data.expenses+"   ·   Возвраты  "+data.refunds
                +"\nПосадок  "+data.plantings+"   ·   Сборов  "+data.harvests+"   ·   Урожая  "+data.harvestedUnits
                +"\nЛюбимая культура  "+progress.FavouriteCrop+"\nДостижения  "+data.achievements.Count+" / 20"
                +"\n\nБОНУСЫ ВРЕМЕНИ СУТОК — при сборе\n"+BonusHints()
                +"\n\nПастух  "+(data.shepherdHired?"нанят · собирает яйца и кормит кур":"80 монет · найм в окне рынка")
                +"\nРасходует обычную пшеницу из инвентаря. Работает во время игры.\nПрибыль = продажи + возвраты − покупки; стартовые монеты не учитываются.";
            hire.gameObject.SetActive(false);
            var text=new StringBuilder("РЕДКИЕ СЕМЕНА КУКУРУЗЫ\nТри тайника дают по 2 семени и пополняются через 15 реальных минут.\nКукуруза: обычный слот семян 5. В магазине семян кукурузы нет.\n\n");
            var names=new[]{"Западный пруд","Северный лес","Восточный холм"};
            var now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            for(var i=0;i<3;i++)
            {
                var cache=progress.Cache(FarmProgression.CacheIds[i]);var remaining=cache.nextAvailableUnixMs-now;
                text.Append(names[i]).Append("  ·  ").Append(!cache.discovered?"не исследован":remaining>0?"новые семена через "+Mathf.CeilToInt(remaining/60000f)+" мин":"семена доступны").Append('\n');
            }
            text.Append("\nНайдите мешочки с золотыми семенами в этих зонах.\nПодойдите и нажмите E / ЛКМ на тайник.\n\nНайдено семян: ").Append(data.rareSeeds);
            discoveries.text=text.ToString();PaintBadges(data.achievements);
        }
        private string BonusHints()
        {
            var text=new StringBuilder();if(progress.Catalog!=null)foreach(var crop in progress.Catalog.Crops)
                if(crop.BonusHarvestYield>0)text.AppendLine(crop.BonusDescription);
            return text.ToString().TrimEnd();
        }
        private void PaintBadges(List<string> earned)
        {
            var pages=(FarmAchievements.All.Length+_pageSize-1)/_pageSize;_page=Mathf.Clamp(_page,0,pages-1);
            pageTitle.text="ДОСТИЖЕНИЯ  "+earned.Count+" / 20   ·   "+(_page+1)+" / "+pages;
            previous.interactable=_page>0;next.interactable=_page+1<pages;
            for(var i=0;i<badgeLabels.Length;i++)
            {
                var index=_page*_pageSize+i;var visible=i<_pageSize&&index<FarmAchievements.All.Length;
                badgeLabels[i].transform.parent.gameObject.SetActive(visible);if(!visible)continue;
                var item=FarmAchievements.All[index];var unlocked=earned.Contains(item.Id);
                badgeLabels[i].text=(unlocked?"[ОТКРЫТО] ":"[  ] ")+item.Name+"\n"+item.Description;
                badgeLabels[i].color=unlocked?new Color(.24f,.49f,.15f):FarmUITheme.Muted;
            }
        }
        private void EnqueueToast(string name){if(_toasts.Count<20)_toasts.Enqueue(name);}
        private void ResetToasts(){_toasts.Clear();if(toast!=null)toast.gameObject.SetActive(false);_toastUntil=0;}
        private void Update()
        {
            if(!Application.isPlaying)return;
            if(clock!=null&&progress!=null&&Time.unscaledTime>=_nextClock){_nextClock=Time.unscaledTime+.5f;clock.text=progress.ClockTime;}
            if(IsOpen&&input!=null&&input.ModalCancelPressed)Close();
            if(IsOpen&&Time.unscaledTime>=_nextRefresh){_nextRefresh=Time.unscaledTime+1;Refresh();}
            if(Time.unscaledTime>=_toastUntil)
            {
                if(_toasts.Count>0){toast.text="ДОСТИЖЕНИЕ: "+_toasts.Dequeue();toast.gameObject.SetActive(true);_toastUntil=Time.unscaledTime+3.5f;}
                else toast.gameObject.SetActive(false);
            }
        }
        private void OnRectTransformDimensionsChange()=>Resize();
        public void Resize()
        {
            if(panel==null||badgesGrid==null||badgeLabels==null)return;
            var canvas=GetComponentInParent<Canvas>();if(canvas==null)return;var area=((RectTransform)canvas.transform).rect.size;
            var width=Mathf.Max(340,Mathf.Min(1060,area.x-48));var height=Mathf.Max(480,Mathf.Min(790,area.y-48));
            panel.sizeDelta=new Vector2(width,height);var oldPageSize=_pageSize;_pageSize=width<680?5:10;
            var columns=_pageSize==5?1:2;badgesGrid.constraintCount=columns;
            badgesGrid.cellSize=new Vector2((width-48-(columns-1)*12)/columns,Mathf.Max(42,(height-338)/5));
            if(farmButton!=null)foreach(var button in new[]{farmButton,badgesButton,discoveriesButton})((RectTransform)button.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,(width-72)/3);
            if(badgesButton!=null)badgesButton.GetComponentInChildren<Text>().text=width<600?"Награды":"Достижения";
            if(previous!=null&&next!=null)foreach(var button in new[]{previous,next})((RectTransform)button.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,Mathf.Min(220,(width-72)/2));
            if(hire!=null)((RectTransform)hire.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,Mathf.Min(430,width-48));
            if(oldPageSize!=_pageSize)
            {
                if(Application.isPlaying&&IsOpen)Refresh();
                else if(_previewingBadges)PaintBadges(PreviewEarned);
            }
        }
        public void PreviewAchievements(int page)
        {
            if(Application.isPlaying)return;_previewingBadges=true;shade.SetActive(true);farmTab.SetActive(false);badgesTab.SetActive(true);discoveriesTab.SetActive(false);pageTitle.gameObject.SetActive(true);_page=page;Resize();PaintBadges(PreviewEarned);
        }
        public void ClearPreview(){if(Application.isPlaying)return;_previewingBadges=false;shade.SetActive(false);ResetToasts();stats.text="";discoveries.text="";foreach(var label in badgeLabels)label.text="";}
        public void PreviewFarm(string text)
        {if(Application.isPlaying)return;_previewingBadges=false;shade.SetActive(true);farmTab.SetActive(true);badgesTab.SetActive(false);discoveriesTab.SetActive(false);pageTitle.gameObject.SetActive(false);stats.text=text;hire.gameObject.SetActive(true);hire.interactable=false;Resize();}
        public static FarmJournalUI Create(Transform parent,FarmProgression system,InputReader reader,Font font)
        {
            var root=new GameObject("Farm Journal",typeof(RectTransform),typeof(FarmJournalUI));root.transform.SetParent(parent,false);Stretch((RectTransform)root.transform);
            var ui=root.GetComponent<FarmJournalUI>();ui.progress=system;ui.input=reader;
            ui.clock=SaveSlotsUI.Label(root.transform,"",font,21);ui.clock.raycastTarget=false;
            ui.clock.gameObject.name="World Clock";
            ui.shade=Image(parent:root.transform,name:"Journal backdrop",new Color(.01f,.025f,.015f,.93f));Stretch((RectTransform)ui.shade.transform);
            var panel=Image(ui.shade.transform,"Journal panel",new Color(.045f,.095f,.065f));ui.panel=(RectTransform)panel.transform;ui.panel.anchorMin=ui.panel.anchorMax=ui.panel.pivot=new Vector2(.5f,.5f);
            var title=SaveSlotsUI.Label(ui.panel,"ЖУРНАЛ ФЕРМЫ",font,30);Top(title.rectTransform,24,18,42);
            ui.farmButton=SaveSlotsUI.Button(ui.panel,"Ферма",font);Tab(ui.farmButton,0);
            ui.badgesButton=SaveSlotsUI.Button(ui.panel,"Достижения",font);Tab(ui.badgesButton,.5f);
            ui.discoveriesButton=SaveSlotsUI.Button(ui.panel,"Открытия",font);Tab(ui.discoveriesButton,1);
            ui.body=new GameObject("Journal body",typeof(RectTransform)).GetComponent<RectTransform>();ui.body.SetParent(ui.panel,false);Stretch(ui.body);ui.body.offsetMin=new Vector2(24,110);ui.body.offsetMax=new Vector2(-24,-124);
            ui.farmTab=Section(ui.body,"Farm");ui.badgesTab=Section(ui.body,"Achievements");ui.discoveriesTab=Section(ui.body,"Discoveries");
            ui.stats=ScrollableText(ui.farmTab.transform,font,68);
            ui.hire=SaveSlotsUI.Button(ui.farmTab.transform,"Нанять пастуха · 80 монет",font);Bottom(ui.hire,0,4,.5f);
            ui.discoveries=ScrollableText(ui.discoveriesTab.transform,font,0);
            var badgeGrid=new GameObject("Achievement list",typeof(RectTransform),typeof(GridLayoutGroup));badgeGrid.transform.SetParent(ui.badgesTab.transform,false);Stretch((RectTransform)badgeGrid.transform);((RectTransform)badgeGrid.transform).offsetMin=new Vector2(0,64);
            ui.badgesGrid=badgeGrid.GetComponent<GridLayoutGroup>();ui.badgesGrid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;ui.badgesGrid.spacing=new Vector2(12,10);ui.badgeLabels=new Text[10];
            for(var i=0;i<10;i++){var card=Image(badgeGrid.transform,"Achievement",new Color(.09f,.17f,.12f));var label=SaveSlotsUI.Label(card.transform,"",font,19);Stretch(label.rectTransform);label.rectTransform.offsetMin=new Vector2(12,8);label.rectTransform.offsetMax=new Vector2(-12,-8);label.alignment=TextAnchor.MiddleLeft;Fit(label);ui.badgeLabels[i]=label;}
            ui.previous=SaveSlotsUI.Button(ui.badgesTab.transform,"Назад",font);Bottom(ui.previous,0,0,0);
            ui.next=SaveSlotsUI.Button(ui.badgesTab.transform,"Далее",font);Bottom(ui.next,0,0,1);
            ui.pageTitle=SaveSlotsUI.Label(ui.panel,"",font,19);var page=ui.pageTitle.rectTransform;page.anchorMin=new Vector2(0,0);page.anchorMax=new Vector2(1,0);page.pivot=new Vector2(.5f,0);page.offsetMin=new Vector2(24,70);page.offsetMax=new Vector2(-24,102);
            ui.close=SaveSlotsUI.Button(ui.panel,"Вернуться · Esc",font);Bottom(ui.close,0,20,.5f);
            ui.toast=SaveSlotsUI.Label(root.transform,"",font,24);var toast=ui.toast.rectTransform;toast.anchorMin=toast.anchorMax=toast.pivot=new Vector2(.5f,1);toast.anchoredPosition=new Vector2(0,-16);toast.sizeDelta=new Vector2(900,54);ui.toast.color=new Color(1,.85f,.32f);ui.toast.gameObject.SetActive(false);
            ui.Resize();ui.ClearPreview();return ui;
        }
        private static GameObject Image(Transform parent,string name,Color color){var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);go.GetComponent<Image>().color=color;return go;}
        private static Text ScrollableText(Transform parent,Font font,float bottom)
        {
            var go=new GameObject("Text viewport",typeof(RectTransform),typeof(Image),typeof(RectMask2D),typeof(ScrollRect));go.transform.SetParent(parent,false);var rect=(RectTransform)go.transform;Stretch(rect);rect.offsetMin=new Vector2(0,bottom);go.GetComponent<Image>().color=Color.clear;
            var text=SaveSlotsUI.Label(go.transform,"",font,22);text.alignment=TextAnchor.UpperLeft;text.verticalOverflow=VerticalWrapMode.Overflow;
            text.rectTransform.anchorMin=new Vector2(0,1);text.rectTransform.anchorMax=Vector2.one;text.rectTransform.pivot=new Vector2(.5f,1);text.rectTransform.sizeDelta=Vector2.zero;
            text.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var scroll=go.GetComponent<ScrollRect>();scroll.horizontal=false;scroll.viewport=rect;scroll.content=text.rectTransform;scroll.scrollSensitivity=26;scroll.movementType=ScrollRect.MovementType.Clamped;
            return text;
        }
        private static GameObject Section(Transform parent,string name){var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);Stretch((RectTransform)go.transform);return go;}
        private static void Fit(Text label){label.resizeTextForBestFit=true;label.resizeTextMinSize=14;label.resizeTextMaxSize=label.fontSize;}
        private static void Stretch(RectTransform rect){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
        private static void Top(RectTransform rect,float inset,float y,float height){rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,1);rect.offsetMin=new Vector2(inset,-y-height);rect.offsetMax=new Vector2(-inset,-y);}
        private static void Tab(Button button,float anchor){var rect=(RectTransform)button.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(anchor,1);rect.anchoredPosition=new Vector2(anchor==0?24:anchor==1?-24:0,-72);rect.sizeDelta=new Vector2(250,40);}
        private static void Bottom(Button button,float x,float y,float anchor){var rect=(RectTransform)button.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(anchor,0);rect.anchoredPosition=new Vector2(x,y);rect.sizeDelta=new Vector2(220,44);}
    }
}
