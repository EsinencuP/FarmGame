using System;
using System.Collections.Generic;
using System.Text;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.World;
using UnityEngine;
using UnityEngine.UI;

namespace MyLittleFarm.UI
{
    public sealed class FarmJournalUI_AI : MonoBehaviour
    {
        [SerializeField] private FarmProgression_AI progress;
        [SerializeField] private InputReader_AI input;
        [SerializeField] private GameObject shade;
        [SerializeField] private RectTransform panel,body;
        [SerializeField] private GameObject farmTab,badgesTab,discoveriesTab;
        [SerializeField] private Text stats,discoveries,pageTitle,toast;
        [SerializeField] private Text clock;
        [SerializeField] private Text[] badgeLabels;
        [SerializeField] private GridLayoutGroup badgesGrid;
        [SerializeField] private Button farmButton,badgesButton,discoveriesButton,hire,close,previous,next;
        private bool _bound;
        private bool _managed;
        private Button _guide, _skip;
        private OnboardingSystem _onboarding;
        private GameObject _previousFocus;
        public event Action Closed, GuideRequested;
        private bool _previewingBadges;
        private static readonly List<string> PreviewEarned=new List<string>{"first_harvest","coins_100","house","storage","sun_bonus","rare_seed"};
        private static readonly (string Name, string Route)[] CacheRoutes =
        {
            (FarmText.Get("journal.pond"), FarmText.Get("journal.pondRoute")),
            (FarmText.Get("journal.forest"), FarmText.Get("journal.forestRoute")),
            (FarmText.Get("journal.hill"), FarmText.Get("journal.hillRoute"))
        };
        private int _tab,_page,_pageSize=10;
        private float _nextRefresh,_toastUntil;
        private float _nextClock;
        public Text ClockText=>clock;
        private readonly Queue<string> _toasts=new Queue<string>();
        public bool IsOpen=>shade!=null&&shade.activeSelf;
        public void Configure(FarmProgression_AI system,InputReader_AI reader){progress=system;input=reader;Bind();}
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
        public void Open(bool managed = false)
        {
            if (!Application.isPlaying || input == null || progress == null || (!managed && input.ModalInputActive && !IsOpen)) return;
            _managed = managed;
            _previousFocus = FarmUIFocus.Selected;
            Bind(); EnsureActions(); shade.SetActive(true);
            input.ModalInputActive = true; input.SuppressGameplayThisFrame = true;
            _tab = 0; _page = 0; Resize(); Refresh();
            FarmUIFocus.Show(panel, farmButton, Close);
        }
        public void Close()
        {
            if (!IsOpen) return;
            Hide();
            if (!Application.isPlaying || input == null) return;
            if (_managed) Closed?.Invoke();
            else
            {
                input.ModalInputActive = false; input.SuppressGameplayThisFrame = true;
                FarmUIFocus.Show(null, null, null);
                FarmUIFocus.Restore(_previousFocus);
            }
        }
        public void Hide() { if (shade != null) shade.SetActive(false); }

        private void EnsureActions()
        {
            if (_guide != null) return;
            _onboarding = FindAnyObjectByType<OnboardingSystem>();
            var font = stats.font;
            _guide = FarmUITheme.Button(panel, FarmText.Get("journal.guide"), font, "help", "ghost");
            _guide.onClick.AddListener(() => GuideRequested?.Invoke());
            Bottom(_guide, -24, 20, 1);
            FarmUITheme.FitCompactButton(_guide, 24);
            Bottom(close, 24, 20, 0);
            close.GetComponentInChildren<Text>().text = FarmText.Get("journal.close");
            _skip = FarmUITheme.Button(farmTab.transform, FarmText.Get("journal.skip"), font, null, "ghost");
            Bottom(_skip, 0, 4, .5f);
            ((RectTransform)_skip.transform).sizeDelta = new Vector2(390, 44);
            FarmUITheme.FitCompactButton(_skip, 24);
            _skip.onClick.AddListener(() => { _onboarding?.Skip(); Refresh(); });
        }
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
            if (_guide != null) _guide.gameObject.SetActive(_managed);
            var learning = _onboarding != null && _onboarding.IsActive;
            if (_skip != null) _skip.gameObject.SetActive(learning && _onboarding.CanSkip);
            var objective = learning ? FarmText.Get("journal.objective", _onboarding.CurrentHint) : FarmText.Get("journal.freePlay");
            stats.text = objective + "\n\n" + FarmText.Get("press.goal.title", progress.JuiceGoal())
                + "\n\n" + FarmText.Get("journal.farmStats",
                FarmText.Coins(progress.Profit), FarmText.Coins(progress.Balance), FarmText.Coins(data.revenue),
                FarmText.Coins(data.expenses), FarmText.Coins(data.refunds), data.plantings, data.harvests,
                data.harvestedUnits, progress.FavouriteCrop, data.achievements.Count)
                + "\n\n" + FarmText.Get("journal.bonuses", BonusHints())
                + "\n\n" + FarmText.Get("journal.shepherd", data.shepherdHired
                    ? FarmText.Get("journal.shepherdHired") + "\n" + progress.ShepherdStatus
                    : FarmText.Get("journal.shepherdMarket", FarmText.Coins(FarmProgression_AI.ShepherdPrice)));
            hire.gameObject.SetActive(false);
            var text=new StringBuilder(FarmText.Get("journal.rareInfo", InputReader_AI.KeyLabel(InputReader_AI.Command.Seed5)));
            var now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            for(var i=0;i<CacheRoutes.Length;i++)
            {
                var cache=progress.Cache(FarmProgression_AI.CacheIds[i]);var remaining=cache.nextAvailableUnixMs-now;
                text.Append(CacheRoutes[i].Name).Append("  ·  ").Append(!cache.discovered?FarmText.Get("journal.unknownCache"):remaining>0?FarmText.Get("journal.refill", Mathf.CeilToInt(remaining / 60000f)):FarmText.Get("journal.cacheReady")).Append('\n');
                text.AppendLine(CacheRoutes[i].Route);
            }
            text.Append(FarmText.Get("journal.rareHint", InputReader_AI.ActionHint, data.rareSeeds));
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
            pageTitle.text=FarmText.Get("journal.badgesPage", earned.Count, _page + 1, pages);
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
            if(badgesButton!=null)badgesButton.GetComponentInChildren<Text>().text=width<600?"Награды":FarmText.Get("journal.tabBadges");
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
        public static FarmJournalUI_AI Create(Transform parent,FarmProgression_AI system,InputReader_AI reader,Font font)
        {
            var root=new GameObject("Farm Journal",typeof(RectTransform),typeof(FarmJournalUI_AI));root.transform.SetParent(parent,false);Stretch((RectTransform)root.transform);
            var ui=root.GetComponent<FarmJournalUI_AI>();ui.progress=system;ui.input=reader;
            ui.clock=SaveSlotsUI.Label(root.transform,"",font,21);ui.clock.raycastTarget=false;
            ui.clock.gameObject.name="World Clock";
            ui.shade=Image(parent:root.transform,name:"Journal backdrop",new Color(.01f,.025f,.015f,.93f));Stretch((RectTransform)ui.shade.transform);
            var panel=Image(ui.shade.transform,"Journal panel",new Color(.045f,.095f,.065f));ui.panel=(RectTransform)panel.transform;ui.panel.anchorMin=ui.panel.anchorMax=ui.panel.pivot=new Vector2(.5f,.5f);
            var title=SaveSlotsUI.Label(ui.panel,FarmText.Get("journal.title"),font,30);Top(title.rectTransform,24,18,42);
            ui.farmButton=SaveSlotsUI.Button(ui.panel,FarmText.Get("journal.tabFarm"),font);Tab(ui.farmButton,0);
            ui.badgesButton=SaveSlotsUI.Button(ui.panel,FarmText.Get("journal.tabBadges"),font);Tab(ui.badgesButton,.5f);
            ui.discoveriesButton=SaveSlotsUI.Button(ui.panel,FarmText.Get("journal.tabDiscoveries"),font);Tab(ui.discoveriesButton,1);
            ui.body=new GameObject("Journal body",typeof(RectTransform)).GetComponent<RectTransform>();ui.body.SetParent(ui.panel,false);Stretch(ui.body);ui.body.offsetMin=new Vector2(24,110);ui.body.offsetMax=new Vector2(-24,-124);
            ui.farmTab=Section(ui.body,"Farm");ui.badgesTab=Section(ui.body,"Achievements");ui.discoveriesTab=Section(ui.body,"Discoveries");
            ui.stats=ScrollableText(ui.farmTab.transform,font,68);
            ui.hire=SaveSlotsUI.Button(ui.farmTab.transform,"Нанять пастуха · 80 монет",font);Bottom(ui.hire,0,4,.5f);
            ui.discoveries=ScrollableText(ui.discoveriesTab.transform,font,0);
            var badgeGrid=new GameObject("Achievement list",typeof(RectTransform),typeof(GridLayoutGroup));badgeGrid.transform.SetParent(ui.badgesTab.transform,false);Stretch((RectTransform)badgeGrid.transform);((RectTransform)badgeGrid.transform).offsetMin=new Vector2(0,64);
            ui.badgesGrid=badgeGrid.GetComponent<GridLayoutGroup>();ui.badgesGrid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;ui.badgesGrid.spacing=new Vector2(12,10);ui.badgeLabels=new Text[10];
            for(var i=0;i<10;i++){var card=Image(badgeGrid.transform,"Achievement",new Color(.09f,.17f,.12f));var label=SaveSlotsUI.Label(card.transform,"",font,19);Stretch(label.rectTransform);label.rectTransform.offsetMin=new Vector2(12,8);label.rectTransform.offsetMax=new Vector2(-12,-8);label.alignment=TextAnchor.MiddleLeft;Fit(label);ui.badgeLabels[i]=label;}
            ui.previous=SaveSlotsUI.Button(ui.badgesTab.transform,FarmText.Get("journal.previous"),font);Bottom(ui.previous,0,0,0);
            ui.next=SaveSlotsUI.Button(ui.badgesTab.transform,FarmText.Get("journal.next"),font);Bottom(ui.next,0,0,1);
            ui.pageTitle=SaveSlotsUI.Label(ui.panel,"",font,19);var page=ui.pageTitle.rectTransform;page.anchorMin=new Vector2(0,0);page.anchorMax=new Vector2(1,0);page.pivot=new Vector2(.5f,0);page.offsetMin=new Vector2(24,70);page.offsetMax=new Vector2(-24,102);
            ui.close=SaveSlotsUI.Button(ui.panel,FarmText.Get("journal.close"),font);Bottom(ui.close,0,20,.5f);
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
