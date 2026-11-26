using MyLittleFarm.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MyLittleFarm.UI
{
    /// <summary>Three farm slots, live only when explicitly opened. Edit previews use supplied DTOs.</summary>
    public sealed class SaveSlotsUI : MonoBehaviour
    {
        [SerializeField] private SaveSystem_AI saves;
        [SerializeField] private InputReader_AI input;
        [SerializeField] private GameObject shade, confirmation;
        [SerializeField] private RectTransform panel, viewport, content;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private GridLayoutGroup grid;
        [SerializeField] private CanvasGroup panelGroup;
        [SerializeField] private SaveSlotCard[] cards;
        [SerializeField] private Button save, close, confirm, cancel;
        [SerializeField] private Text message, confirmationText;
        private SaveSlotInfo[] _slots;
        private bool _bound;
        private int _pendingSlot;
        private bool _pendingManual, _pendingNew;
        private bool _pendingDelete;
        private GameObject _choiceFocus;
        [SerializeField] private FarmMenuUI mainMenu;
        [SerializeField] private bool selectionScene;
        public event System.Action<bool> Closed;
        private bool _selectedFarm;
        private bool _loading;
        private ComicPrologue _prologue;
        public bool IsOpen => shade != null && shade.activeSelf;
        private bool HasActiveFarm => saves?.HasActiveFarm == true;

        public void Configure(SaveSystem_AI system, InputReader_AI reader)
        { saves = system; input = reader; Bind(); }
        private void OnEnable() => Bind();
        private void Start()
        {
            if (selectionScene && Application.isPlaying) Open();
        }
        private void Bind()
        {
            if (_bound || !Application.isPlaying || (!selectionScene && (saves == null || input == null))) return;
            for (var i = 0; i < cards.Length; i++)
            {
                var index = i;
                cards[i].Primary.onClick.AddListener(() => Choose(index, false));
                cards[i].Manual.onClick.AddListener(() => Choose(index, true));
                if (cards[i].Delete != null) cards[i].Delete.onClick.AddListener(() => ConfirmDelete(index));
            }
            save.onClick.AddListener(Save); close.onClick.AddListener(Close);
            confirm.onClick.AddListener(ApplyChoice); cancel.onClick.AddListener(CancelChoice);
            if (saves != null) saves.SlotsChanged += RefreshIfOpen;
            _bound = true;
        }
        private void OnDisable()
        {
            if (!_bound) return;
            if (_loading)
            {
                StopAllCoroutines();
                HidePrologue();
                if (mainMenu != null) mainMenu.SetLoading(false);
                _loading = false;
            }
            if (saves != null) saves.SlotsChanged -= RefreshIfOpen;
            foreach (var card in cards) { card.Primary.onClick.RemoveAllListeners(); card.Manual.onClick.RemoveAllListeners(); if (card.Delete != null) card.Delete.onClick.RemoveAllListeners(); }
            foreach (var button in new[] { save,close,confirm,cancel }) button.onClick.RemoveAllListeners();
            if (IsOpen && input != null) { input.ModalInputActive = false; input.SuppressGameplayThisFrame = true; }
            _bound = false;
        }

        public void Open()
        {
            if (!Application.isPlaying || (!selectionScene
                && (saves == null || input == null || (input.ModalInputActive && !IsOpen)))) return;
            Bind();
            if (input != null) { input.ModalInputActive = true; input.SuppressGameplayThisFrame = true; }
            _selectedFarm = false;
            _pendingDelete = false;
            _choiceFocus = null; scroll.StopMovement(); content.anchoredPosition = Vector2.zero;
            shade.SetActive(true); confirmation.SetActive(false); panelGroup.interactable = true;
            save.GetComponentInChildren<Text>().text = FarmText.Get("saves.save");
            close.GetComponentInChildren<Text>().text = FarmText.Get("saves.close");
            var subtitle = panel.Find("ТРИ СЛОТА · ТРИ ИСТОРИИ ФЕРМЫ");
            if (subtitle != null) subtitle.GetComponent<Text>().text = FarmText.Get("saves.subtitle");
            RefreshIfOpen(); Resize();
            message.text = selectionScene
                ? (FarmSceneFlow.TakeLaunchFailure() ? FarmText.Get("saves.loadFailed") : string.Empty)
                : saves.LegacyUnavailable ? FarmText.Get("saves.legacyUnavailable")
                : HasActiveFarm ? FarmText.Get("saves.autosave", InputReader_AI.KeyLabel(InputReader_AI.Command.Save)) : FarmText.Get("saves.chooseFarm");
            FocusFirst();
        }
        private void RefreshIfOpen()
        {
            if (!IsOpen || (!selectionScene && saves == null)) return;
            _slots = selectionScene ? SaveSystem_AI.ReadSelectionSlots() : saves.ReadSlots();
            for (var i = 0; i < cards.Length; i++) cards[i].Show(_slots[i], HasActiveFarm && saves.ActiveSlot == i,
                i == 0 && saves?.LegacyUnavailable == true);
            save.interactable = HasActiveFarm;
            close.interactable = selectionScene || HasActiveFarm || mainMenu != null;
        }
        private void Save()
        {
            message.text = saves.SaveActivePoint(false) ? FarmText.Get("saves.saved", saves.ActiveSlot + 1)
                : FarmText.Get("saves.saveFailed");
        }
        private void Choose(int slot, bool manual)
        {
            if (_loading || FarmSceneFlow.IsLoading) return;
            _choiceFocus = FarmUIFocus.Selected;
            _pendingSlot = slot; _pendingManual = manual; _pendingNew = _slots[slot].IsEmpty;
            _pendingDelete = false;
            confirm.GetComponentInChildren<Text>().text = FarmText.Get(_pendingNew ? "saves.new" : "saves.continue"); FarmUITheme.Style(confirm, "primary");
            if (!HasActiveFarm) { ApplyChoice(); return; }
            confirmation.SetActive(true);
            panelGroup.interactable = false;
            confirmationText.text = _pendingNew ? FarmText.Get("saves.confirmNew", slot + 1)
                : slot != saves.ActiveSlot ? FarmText.Get("saves.confirmSwitch", slot + 1)
                : FarmText.Get("saves.confirmReload", FarmText.Get(manual ? "saves.manualAdjective" : "saves.latestAdjective"), slot + 1);
            FarmUIFocus.Show(confirmation.transform, cancel, CancelChoice);
        }
        private void ApplyChoice()
        {
            if (_loading) return;
            if (_pendingDelete)
            {
                var removed = selectionScene ? SaveSystem_AI.ArchiveClosedSlot(_pendingSlot) : saves.ArchiveSlot(_pendingSlot);
                CancelChoice(); RefreshIfOpen();
                message.text = removed ? FarmText.Get("saves.archiveDone") : FarmText.Get("saves.archiveFailed");
                return;
            }
            StartCoroutine(LoadChoice());
        }
        private void ConfirmDelete(int slot)
        {
            if (_loading || FarmSceneFlow.IsLoading || (HasActiveFarm && saves.ActiveSlot == slot)) return;
            _choiceFocus = FarmUIFocus.Selected;
            _pendingSlot = slot; _pendingDelete = true; confirmation.SetActive(true); panelGroup.interactable = false;
            confirmationText.text = FarmText.Get("saves.confirmDelete", slot + 1);
            confirm.GetComponentInChildren<Text>().text = FarmText.Get("saves.delete"); FarmUITheme.Style(confirm, "danger"); FarmUIFocus.Show(confirmation.transform, cancel, CancelChoice);
        }
        private System.Collections.IEnumerator LoadChoice()
        {
            var slot = _pendingSlot;
            var newFarm = _pendingNew;
            var manual = _pendingManual;
            _loading = true; panelGroup.interactable = false;
            confirmation.SetActive(false);
            FarmUIFocus.Show(null, null, null);
            if (newFarm)
            {
                _prologue = ComicPrologue.Open(transform, message.font);
                if (_prologue != null) yield return _prologue.Play();
            }
            if (selectionScene)
            {
                HidePrologue();
                if (FarmSceneFlow.LoadFarm(slot, manual, newFarm))
                    while (FarmSceneFlow.IsLoading) yield return null;
                _loading = false;
                panelGroup.interactable = true;
                RefreshIfOpen();
                message.text = FarmText.Get("saves.loadFailed");
                FocusFirst();
                yield break;
            }
            if (mainMenu != null) mainMenu.SetLoading(true);
            HidePrologue();
            // Present a real loading frame before synchronous validation and restore.
            yield return null;
            yield return null;
            // Keep the slot empty if the player exits during the story.
            var success = newFarm ? saves.NewFarm(slot) : saves.LoadSlot(slot, manual);
            if (mainMenu != null) mainMenu.SetLoading(false);
            _loading = false;
            confirmation.SetActive(false);
            panelGroup.interactable = true;
            if (success) { _selectedFarm = true; Close(); }
            else { RefreshIfOpen(); message.text = FarmText.Get("saves.loadFailed"); FocusFirst(); }
        }
        private void HidePrologue()
        {
            if (_prologue == null) return;
            _prologue.gameObject.SetActive(false);
            Destroy(_prologue.gameObject);
            _prologue = null;
        }
        private void CancelChoice()
        {
            confirmation.SetActive(false); panelGroup.interactable = true; FocusFirst();
            FarmUIFocus.Restore(_choiceFocus, FarmUIFocus.Selected == null ? null : FarmUIFocus.Selected.GetComponent<Selectable>());
            _choiceFocus = null;
        }
        public void Close()
        {
            if (Application.isPlaying && selectionScene)
            {
                if (_loading || FarmSceneFlow.IsLoading) return;
                if (!FarmSceneFlow.LoadMenu()) message.text = FarmText.Get("pause.menuFailed");
                return;
            }
            if (Application.isPlaying && (_loading || (saves == null || (!saves.HasActiveFarm && mainMenu == null)))) return;
            var owned = IsOpen;
            confirmation.SetActive(false); shade.SetActive(false);
            if (panelGroup != null) panelGroup.interactable = true;
            if (cards != null) for (var i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null) continue;
                cards[i].ReleasePreview();
                if (!Application.isPlaying) cards[i].Show(new SaveSlotInfo { Index = i, Manual = new SavePointInfo(), Autosave = new SavePointInfo() }, false, false);
            }
            if (owned && Application.isPlaying && input != null) { input.ModalInputActive = false; input.SuppressGameplayThisFrame = true; }
            if (owned && Application.isPlaying) FarmUIFocus.Show(null, null, null);
            if (owned && Application.isPlaying) Closed?.Invoke(_selectedFarm);
        }
        private void FocusFirst()
        {
            var first = close;
            foreach (var card in cards)
                if (card.Primary.interactable) { first = card.Primary; break; }
            if (HasActiveFarm && cards[saves.ActiveSlot].Primary.interactable)
                first = cards[saves.ActiveSlot].Primary;
            FarmUIFocus.Show(panel, first, Close);
        }

        public void ConfigureSelectionScene()
        {
            selectionScene = true;
            shade.GetComponent<Image>().color = new Color(.07f, .11f, .07f, .42f);
            FarmUITheme.Skin(panel.GetComponent<Image>(), "panel");
            FarmUITheme.Skin(confirmation.transform.GetChild(0).GetComponent<Image>(), "panel");
            foreach (var card in cards) FarmUITheme.Skin(card.GetComponent<Image>(), "slot");
            foreach (var label in shade.GetComponentsInChildren<Text>(true))
                if (label.GetComponentInParent<Button>() == null) label.color = FarmUITheme.Ink;
            foreach (var card in cards) card.EnsureDeleteButton(message.font);
            FarmUITheme.Style(close, "ghost");
            FarmUITheme.Style(cancel, "ghost");
            save.gameObject.SetActive(false);
        }

        private void OnRectTransformDimensionsChange() => Resize();
        public void Resize()
        {
            if (panel == null || grid == null || cards == null) return;
            var canvas = GetComponentInParent<Canvas>(); if (canvas == null) return;
            var size = ((RectTransform)canvas.transform).rect.size;
            var width = Mathf.Max(300, Mathf.Min(1320, size.x - 48));
            var height = Mathf.Max(380, Mathf.Min(selectionScene ? 704 : 760, size.y - 48));
            panel.sizeDelta = new Vector2(width,height);
            var columns = width >= 1000;
            grid.constraintCount = columns ? 3 : 1;
            var cardWidth = columns ? (width - 72) / 3 : width - 40;
            grid.cellSize = new Vector2(cardWidth, columns ? 430 : 290);
            foreach (var card in cards) if (card != null) card.Resize(cardWidth,columns);
            if (save != null && close != null)
                foreach (var button in new[] { save, close }) ((RectTransform)button.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,Mathf.Min(240,(width-72)/2));
            if (confirmation != null)
            {
                var dialog = confirmation.transform.GetChild(0) as RectTransform;
                if (dialog != null)
                {
                    var dialogWidth = Mathf.Min(620,width-24); dialog.sizeDelta = new Vector2(dialogWidth,290);
                    if (confirm != null && cancel != null)
                        foreach (var button in new[] { confirm, cancel }) ((RectTransform)button.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,Mathf.Min(240,(dialogWidth-72)/2));
                }
            }
        }

        public void Preview(SaveSlotInfo[] slots)
        {
            if (Application.isPlaying) return;
            shade.SetActive(true); confirmation.SetActive(false);
            panelGroup.interactable = true;
            for (var i = 0; i < cards.Length; i++) cards[i].Show(slots[i],i == 0,false);
            save.interactable = true; close.interactable = true;
            message.text = "Автосохранение каждые 5 минут и при выходе. F5 — ручная точка активной фермы.";
            Resize();
        }

        public void EnsureScrollbar()
        {
            if (scroll == null || scroll.verticalScrollbar != null) return;
            var bar = new GameObject("Slot scrollbar",typeof(RectTransform),typeof(Image),typeof(Scrollbar));bar.transform.SetParent(panel,false);
            var barRect = (RectTransform)bar.transform;barRect.anchorMin = new Vector2(1,0);barRect.anchorMax = Vector2.one;barRect.pivot = new Vector2(1,.5f);barRect.offsetMin = new Vector2(-12,135);barRect.offsetMax = new Vector2(-4,-112);
            bar.GetComponent<Image>().color = new Color(.10f,.18f,.12f);
            var handle = new GameObject("Handle",typeof(RectTransform),typeof(Image));handle.transform.SetParent(bar.transform,false);Stretch((RectTransform)handle.transform);handle.GetComponent<Image>().color = new Color(.56f,.66f,.39f);
            var scrollbar = bar.GetComponent<Scrollbar>();scrollbar.handleRect = (RectTransform)handle.transform;scrollbar.targetGraphic = handle.GetComponent<Image>();scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = scrollbar;scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        public static SaveSlotsUI Create(Transform parent, SaveSystem_AI system, InputReader_AI reader, Font font)
        {
            var root = new GameObject("Save Slots UI",typeof(RectTransform),typeof(SaveSlotsUI)); root.transform.SetParent(parent,false);
            Stretch((RectTransform)root.transform);
            var ui = root.GetComponent<SaveSlotsUI>(); ui.saves = system; ui.input = reader;
            ui.shade = new GameObject("Slot menu backdrop",typeof(RectTransform),typeof(Image));ui.shade.transform.SetParent(root.transform,false);
            Stretch((RectTransform)ui.shade.transform);ui.shade.GetComponent<Image>().color = new Color(.015f,.035f,.02f,.94f);
            ui.panel = Panel(ui.shade.transform,"Farm slots",new Vector2(1320,760));
            ui.panelGroup = ui.panel.gameObject.AddComponent<CanvasGroup>();
            var title = Label(ui.panel,FarmText.Get("saves.title"),font,30); Top(title.rectTransform,24,20,42);
            var subtitle = Label(ui.panel,FarmText.Get("saves.subtitle"),font,18); Top(subtitle.rectTransform,24,66,30);
            var viewport = new GameObject("Slot list",typeof(RectTransform),typeof(Image),typeof(Mask),typeof(ScrollRect));viewport.transform.SetParent(ui.panel,false);
            ui.viewport = (RectTransform)viewport.transform; Stretch(ui.viewport);ui.viewport.offsetMin = new Vector2(20,135);ui.viewport.offsetMax = new Vector2(-20,-112);
            viewport.GetComponent<Image>().color = Color.white;viewport.GetComponent<Mask>().showMaskGraphic = false;
            ui.scroll = viewport.GetComponent<ScrollRect>();ui.scroll.horizontal = false;ui.scroll.movementType = ScrollRect.MovementType.Clamped;ui.scroll.scrollSensitivity = 28;
            var content = new GameObject("Slots",typeof(RectTransform),typeof(GridLayoutGroup),typeof(ContentSizeFitter));content.transform.SetParent(viewport.transform,false);
            ui.content = (RectTransform)content.transform;ui.content.anchorMin = new Vector2(0,1);ui.content.anchorMax = Vector2.one;ui.content.pivot = new Vector2(.5f,1);ui.content.anchoredPosition = Vector2.zero;
            ui.content.sizeDelta = Vector2.zero;
            ui.grid = content.GetComponent<GridLayoutGroup>();ui.grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;ui.grid.spacing = new Vector2(16,16);
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ui.scroll.viewport = ui.viewport;ui.scroll.content = ui.content;
            ui.EnsureScrollbar();
            ui.cards = new SaveSlotCard[SaveSystem_AI.SlotCount];for(var i = 0;i < ui.cards.Length;i++) {ui.cards[i] = SaveSlotCard.Create(ui.content,font);ui.cards[i].name = "Slot " + (i+1);}
            ui.message = Label(ui.panel,"",font,18);ui.message.alignment = TextAnchor.MiddleLeft;
            var messageRect = ui.message.rectTransform;messageRect.anchorMin = Vector2.zero;messageRect.anchorMax = new Vector2(1,0);messageRect.pivot = new Vector2(.5f,0);messageRect.offsetMin = new Vector2(24,74);messageRect.offsetMax = new Vector2(-24,125);
            ui.save = Button(ui.panel,FarmText.Get("saves.save"),font);Bottom(ui.save,24,false);
            ui.close = Button(ui.panel,FarmText.Get("saves.close"),font);Bottom(ui.close,-24,true);
            ui.confirmation = new GameObject("Confirm load",typeof(RectTransform),typeof(Image));ui.confirmation.transform.SetParent(ui.shade.transform,false);Stretch((RectTransform)ui.confirmation.transform);ui.confirmation.GetComponent<Image>().color = new Color(0,0,0,.75f);
            var dialog = Panel(ui.confirmation.transform,"Load confirmation",new Vector2(620,290));
            ui.confirmationText = Label(dialog,"",font,23);Top(ui.confirmationText.rectTransform,24,24,175);
            ui.confirm = Button(dialog,"Продолжить",font);Bottom(ui.confirm,24,false);
            ui.cancel = Button(dialog,FarmText.Get("saves.cancel"),font);Bottom(ui.cancel,-24,true);
            ui.Resize(); ui.Close(); return ui;
        }
        public static Text Label(Transform parent,string text,Font font,int size)
        {
            var go = new GameObject(text,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
            var label = go.GetComponent<Text>();label.text = text;label.font = font;label.fontSize = size;label.color = new Color(.98f,.96f,.85f);label.raycastTarget = false;label.supportRichText = false;label.alignment = TextAnchor.MiddleCenter;
            return label;
        }
        public static Button Button(Transform parent,string text,Font font)
        {
            var go = new GameObject(text,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);
            var graphic = go.GetComponent<Image>();graphic.color = new Color(.23f,.36f,.25f);
            var button = go.GetComponent<Button>();button.targetGraphic = graphic;
            var colors = button.colors;colors.highlightedColor = colors.selectedColor = new Color(.83f,.93f,.64f);colors.pressedColor = new Color(.55f,.70f,.4f);colors.disabledColor = new Color(.4f,.4f,.4f);button.colors = colors;
            var label = Label(go.transform,text,font,19);Stretch(label.rectTransform);label.rectTransform.offsetMin = new Vector2(8,4);label.rectTransform.offsetMax = new Vector2(-8,-4);label.resizeTextForBestFit = true;label.resizeTextMinSize = 14;label.resizeTextMaxSize = 19;
            return button;
        }
        private static RectTransform Panel(Transform parent,string name,Vector2 size)
        {
            var go = new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);go.GetComponent<Image>().color = new Color(.045f,.095f,.065f);
            var rect = (RectTransform)go.transform;rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f);rect.sizeDelta = size;return rect;
        }
        private static void Stretch(RectTransform rect) {rect.anchorMin = Vector2.zero;rect.anchorMax = Vector2.one;rect.offsetMin = rect.offsetMax = Vector2.zero;}
        private static void Top(RectTransform rect,float inset,float y,float height)
        {rect.anchorMin = new Vector2(0,1);rect.anchorMax = Vector2.one;rect.pivot = new Vector2(.5f,1);rect.offsetMin = new Vector2(inset,-y-height);rect.offsetMax = new Vector2(-inset,-y);}
        private static void Bottom(Button button,float x,bool right)
        {var rect = (RectTransform)button.transform;rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(right?1:0,0);rect.anchoredPosition = new Vector2(x,20);rect.sizeDelta = new Vector2(240,44);}
    }
}
