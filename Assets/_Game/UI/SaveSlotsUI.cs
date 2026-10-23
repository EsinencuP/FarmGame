using MyLittleFarm.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MyLittleFarm.UI
{
    /// <summary>Three farm slots, live only when explicitly opened. Edit previews use supplied DTOs.</summary>
    public sealed class SaveSlotsUI : MonoBehaviour
    {
        [SerializeField] private SaveSystem saves;
        [SerializeField] private InputReader input;
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
        private GameObject _lastSelection;
        [SerializeField] private FarmMenuUI mainMenu;
        public event System.Action<bool> Closed;
        private bool _selectedFarm;
        private bool _loading;
        public bool IsOpen => shade != null && shade.activeSelf;

        public void Configure(SaveSystem system, InputReader reader)
        { saves = system; input = reader; Bind(); }
        private void OnEnable() => Bind();
        private void Bind()
        {
            if (_bound || !Application.isPlaying || saves == null || input == null) return;
            for (var i = 0; i < cards.Length; i++)
            {
                var index = i;
                cards[i].Primary.onClick.AddListener(() => Choose(index, false));
                cards[i].Manual.onClick.AddListener(() => Choose(index, true));
                if (cards[i].Delete != null) cards[i].Delete.onClick.AddListener(() => ConfirmDelete(index));
            }
            save.onClick.AddListener(Save); close.onClick.AddListener(Close);
            confirm.onClick.AddListener(ApplyChoice); cancel.onClick.AddListener(CancelChoice);
            saves.SlotsChanged += RefreshIfOpen; _bound = true;
        }
        private void OnDisable()
        {
            if (!_bound) return;
            saves.SlotsChanged -= RefreshIfOpen;
            foreach (var card in cards) { card.Primary.onClick.RemoveAllListeners(); card.Manual.onClick.RemoveAllListeners(); if (card.Delete != null) card.Delete.onClick.RemoveAllListeners(); }
            foreach (var button in new[] { save,close,confirm,cancel }) button.onClick.RemoveAllListeners();
            if (IsOpen && input != null) { input.ModalInputActive = false; input.SuppressGameplayThisFrame = true; }
            _bound = false;
        }

        public void Open()
        {
            if (!Application.isPlaying || saves == null || input == null || (input.ModalInputActive && !IsOpen)) return;
            Bind(); input.ModalInputActive = true; input.SuppressGameplayThisFrame = true;
            _selectedFarm = false;
            _pendingDelete = false;
            _lastSelection = null; scroll.StopMovement(); content.anchoredPosition = Vector2.zero;
            shade.SetActive(true); confirmation.SetActive(false); panelGroup.interactable = true;
            RefreshIfOpen(); Resize();
            message.text = saves.LegacyUnavailable ? "Старое сохранение не удалось перенести; слот 1 защищён. Исходные файлы сохранены."
                : "Автосохранение каждые 5 минут и при выходе. F5 — ручная точка активной фермы.";
            FocusFirst();
        }
        private void RefreshIfOpen()
        {
            if (!IsOpen || saves == null) return;
            _slots = saves.ReadSlots();
            for (var i = 0; i < cards.Length; i++) cards[i].Show(_slots[i], saves.HasActiveFarm && saves.ActiveSlot == i,
                i == 0 && saves.LegacyUnavailable);
            save.interactable = saves.HasActiveFarm; close.interactable = saves.HasActiveFarm || mainMenu != null;
        }
        private void Save()
        {
            message.text = saves.SaveActivePoint(false) ? "Ручная точка сохранена · слот " + (saves.ActiveSlot + 1)
                : "Запись не удалась. Предыдущие файлы сохранены.";
        }
        private void Choose(int slot, bool manual)
        {
            _pendingSlot = slot; _pendingManual = manual; _pendingNew = _slots[slot].IsEmpty;
            _pendingDelete = false;
            confirm.GetComponentInChildren<Text>().text = "Продолжить"; FarmUITheme.Style(confirm, "primary");
            if (!saves.HasActiveFarm) { ApplyChoice(); return; }
            confirmation.SetActive(true);
            panelGroup.interactable = false;
            confirmationText.text = _pendingNew ? "Начать новую ферму в слоте " + (slot + 1) + "?\nТекущая ферма будет автоматически сохранена в своём слоте."
                : slot != saves.ActiveSlot ? "Загрузить слот " + (slot + 1) + "?\nТекущая ферма будет автоматически сохранена в своём слоте."
                : "Загрузить " + (manual ? "ручную точку" : "последнюю точку") + " слота " + (slot + 1)
                    + "?\nПрогресс после выбранного сохранения будет заменён.";
            Focus(cancel.gameObject);
        }
        private void ApplyChoice()
        {
            if (_loading) return;
            if (_pendingDelete)
            {
                var removed = saves.ArchiveSlot(_pendingSlot); CancelChoice(); RefreshIfOpen();
                message.text = removed ? "Слот освобождён. Копия файлов сохранена в папке Saves/Removed." : "Не удалось удалить слот. Файлы сохранены.";
                return;
            }
            StartCoroutine(LoadChoice());
        }
        private void ConfirmDelete(int slot)
        {
            if (saves.HasActiveFarm && saves.ActiveSlot == slot) return;
            _pendingSlot = slot; _pendingDelete = true; confirmation.SetActive(true); panelGroup.interactable = false;
            confirmationText.text = "Удалить ферму из слота " + (slot + 1) + "?\nСлот станет свободным. Копия файлов останется в папке восстановления.";
            confirm.GetComponentInChildren<Text>().text = "Удалить"; FarmUITheme.Style(confirm, "danger"); Focus(cancel.gameObject);
        }
        private System.Collections.IEnumerator LoadChoice()
        {
            _loading = true; panelGroup.interactable = false;
            if (mainMenu != null) mainMenu.SetLoading(true);
            // Present a real loading frame before synchronous validation and restore.
            yield return null;
            yield return null;
            var success = _pendingNew ? saves.NewFarm(_pendingSlot) : saves.LoadSlot(_pendingSlot, _pendingManual);
            if (mainMenu != null) mainMenu.SetLoading(false);
            _loading = false;
            confirmation.SetActive(false);
            panelGroup.interactable = true;
            if (success) { _selectedFarm = true; Close(); }
            else { RefreshIfOpen(); message.text = "Не удалось открыть ферму. Проверьте доступ к файлам; данные сохранены."; FocusFirst(); }
        }
        private void CancelChoice() { confirmation.SetActive(false); panelGroup.interactable = true; FocusFirst(); }
        public void Close()
        {
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
            Focus(null);
            if (owned && Application.isPlaying) Closed?.Invoke(_selectedFarm);
        }
        private void Update()
        {
            if (!Application.isPlaying || !IsOpen || input == null || _loading) return;
            if (input.ModalCancelPressed) { if (confirmation.activeSelf) CancelChoice(); else Close(); }
            ScrollToSelection();
        }
        private void FocusFirst()
        {
            if (saves != null && cards[saves.ActiveSlot].Primary.interactable)
            { Focus(cards[saves.ActiveSlot].Primary.gameObject); return; }
            for (var i = 0; i < cards.Length; i++) if (cards[i].Primary.interactable) { Focus(cards[i].Primary.gameObject); return; }
            Focus(close.interactable ? close.gameObject : null);
        }
        private static void Focus(GameObject target)
        { if (Application.isPlaying && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(target); }
        private void ScrollToSelection()
        {
            var selected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
            if (selected == null || selected == _lastSelection || !selected.transform.IsChildOf(content)) return;
            _lastSelection = selected;
            Canvas.ForceUpdateCanvases();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, selected.transform);
            var offset = bounds.min.y < viewport.rect.yMin ? viewport.rect.yMin - bounds.min.y
                : bounds.max.y > viewport.rect.yMax ? viewport.rect.yMax - bounds.max.y : 0f;
            var position = content.anchoredPosition;
            position.y = Mathf.Clamp(position.y + offset, 0, Mathf.Max(0, content.rect.height - viewport.rect.height));
            content.anchoredPosition = position;
        }

        private void OnRectTransformDimensionsChange() => Resize();
        public void Resize()
        {
            if (panel == null || grid == null || cards == null) return;
            var canvas = GetComponentInParent<Canvas>(); if (canvas == null) return;
            var size = ((RectTransform)canvas.transform).rect.size;
            var width = Mathf.Max(300, Mathf.Min(1320, size.x - 48));
            var height = Mathf.Max(380, Mathf.Min(760, size.y - 48));
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

        public static SaveSlotsUI Create(Transform parent, SaveSystem system, InputReader reader, Font font)
        {
            var root = new GameObject("Save Slots UI",typeof(RectTransform),typeof(SaveSlotsUI)); root.transform.SetParent(parent,false);
            Stretch((RectTransform)root.transform);
            var ui = root.GetComponent<SaveSlotsUI>(); ui.saves = system; ui.input = reader;
            ui.shade = new GameObject("Slot menu backdrop",typeof(RectTransform),typeof(Image));ui.shade.transform.SetParent(root.transform,false);
            Stretch((RectTransform)ui.shade.transform);ui.shade.GetComponent<Image>().color = new Color(.015f,.035f,.02f,.94f);
            ui.panel = Panel(ui.shade.transform,"Farm slots",new Vector2(1320,760));
            ui.panelGroup = ui.panel.gameObject.AddComponent<CanvasGroup>();
            var title = Label(ui.panel,"СОХРАНЕНИЯ",font,30); Top(title.rectTransform,24,20,42);
            var subtitle = Label(ui.panel,"ТРИ СЛОТА · ТРИ ИСТОРИИ ФЕРМЫ",font,18); Top(subtitle.rectTransform,24,66,30);
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
            ui.cards = new SaveSlotCard[SaveSystem.SlotCount];for(var i = 0;i < ui.cards.Length;i++) {ui.cards[i] = SaveSlotCard.Create(ui.content,font);ui.cards[i].name = "Slot " + (i+1);}
            ui.message = Label(ui.panel,"",font,18);ui.message.alignment = TextAnchor.MiddleLeft;
            var messageRect = ui.message.rectTransform;messageRect.anchorMin = Vector2.zero;messageRect.anchorMax = new Vector2(1,0);messageRect.pivot = new Vector2(.5f,0);messageRect.offsetMin = new Vector2(24,74);messageRect.offsetMax = new Vector2(-24,125);
            ui.save = Button(ui.panel,"Сохранить текущую",font);Bottom(ui.save,24,false);
            ui.close = Button(ui.panel,"Вернуться · Esc",font);Bottom(ui.close,-24,true);
            ui.confirmation = new GameObject("Confirm load",typeof(RectTransform),typeof(Image));ui.confirmation.transform.SetParent(ui.shade.transform,false);Stretch((RectTransform)ui.confirmation.transform);ui.confirmation.GetComponent<Image>().color = new Color(0,0,0,.75f);
            var dialog = Panel(ui.confirmation.transform,"Load confirmation",new Vector2(620,290));
            ui.confirmationText = Label(dialog,"",font,23);Top(ui.confirmationText.rectTransform,24,24,175);
            ui.confirm = Button(dialog,"Продолжить",font);Bottom(ui.confirm,24,false);
            ui.cancel = Button(dialog,"Отмена",font);Bottom(ui.cancel,-24,true);
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
