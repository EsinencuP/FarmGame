using System.Collections.Generic;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.Farming;
using UnityEngine;
using UnityEngine.UI;

namespace MyLittleFarm.UI
{
    /// <summary>Baked screen stack. The top screen owns input; profile IO happens only in Play Mode.</summary>
    [DefaultExecutionOrder(-180)]
    public sealed class FarmMenuUI : MonoBehaviour
    {
        public enum Page { Game, Main, Pause, Settings, Inventory, Shop, Quit, Guide, Journal }
        [SerializeField] private InputReader input;
        [SerializeField] private SaveSystem saves;
        [SerializeField] private SaveSlotsUI slots;
        [SerializeField] private FarmJournalUI journal;
        [SerializeField] private BuildCustomizationUI catalogue;
        [SerializeField] private BuildSystem buildings;
        [SerializeField] private RectTransform safeArea, pausePanel, quitPanel, navigation;
        [SerializeField] private GameObject overlay, shade, loading;
        [SerializeField] private FarmSettingsUI settings;
        [SerializeField] private FarmInventoryUI inventory;
        [SerializeField] private FarmGuideUI guide;
        [SerializeField] private FarmMarketUI market;
        [SerializeField] private SeedShopSystem shop;
        [SerializeField] private RectTransform marketPrompt;
        [SerializeField] private Button resume, pauseSettings, pauseSlots, home, pauseExit, quitConfirm, quitCancel;
        [SerializeField] private Button journalShortcut, pauseShortcut;
        [SerializeField] private Text pauseMessage;
        private readonly Stack<(Page Page, GameObject Focus)> _history = new Stack<(Page, GameObject)>();
        private GameObject _slotFocus;
        private Page _page = Page.Game, _slotReturn = Page.Main;
        private float _previousTimeScale = 1;
        private bool _ownsPause, _waitingForSlots, _bound;
        private Rect _lastSafeArea;
        private Vector2 _lastSize;
        public bool IsOpen => _page != Page.Game || _waitingForSlots;

        private void Awake()
        {
            if (!Application.isPlaying) return;
            settings.Initialize();
            // Prevent one frame of world interaction before SaveSystem opens the startup screen.
            input.ModalInputActive = true;
        }
        private void OnEnable()
        {
            if (!Application.isPlaying || _bound) return;
            resume.onClick.AddListener(Resume); pauseSettings.onClick.AddListener(OpenSettings); pauseSlots.onClick.AddListener(OpenSlots); home.onClick.AddListener(ReturnHome); pauseExit.onClick.AddListener(AskQuit);
            quitConfirm.onClick.AddListener(Quit); quitCancel.onClick.AddListener(Back);
            settings.BackButton.onClick.AddListener(Back); inventory.CloseButton.onClick.AddListener(Back);
            journalShortcut.onClick.AddListener(OpenJournal);
            if (journal != null) { journal.Closed += Back; journal.GuideRequested += OpenGuideFromJournal; } pauseShortcut.onClick.AddListener(OpenPause);
            if (guide != null) { guide.CloseButton.onClick.AddListener(Back); guide.JournalButton.onClick.AddListener(OpenJournalFromGuide); }
            if (market != null) market.CloseButton.onClick.AddListener(Back);
            slots.Closed += SlotsClosed; _bound = true;
        }
        private void OnDisable()
        {
            if (!_bound) return;
            foreach (var button in new[] { resume, pauseSettings, pauseSlots, home, pauseExit, quitConfirm, quitCancel,
                settings.BackButton, inventory.CloseButton, journalShortcut, pauseShortcut }) button.onClick.RemoveAllListeners();
            if (guide != null) { guide.CloseButton.onClick.RemoveListener(Back); guide.JournalButton.onClick.RemoveListener(OpenJournalFromGuide); }
            if (market != null) market.CloseButton.onClick.RemoveListener(Back);
            if (journal != null) { journal.Closed -= Back; journal.GuideRequested -= OpenGuideFromJournal; }
            slots.Closed -= SlotsClosed; _bound = false;
            ReleasePause();
            if (IsOpen && input != null) { input.ModalInputActive = false; input.SuppressGameplayThisFrame = true; }
        }
        private void Update()
        {
            Resize();
            if (!Application.isPlaying || input == null || _waitingForSlots || FarmSceneFlow.IsLoading) return;
            RefreshShortcutLabels();
            if (marketPrompt != null) marketPrompt.gameObject.SetActive(CanOpenFromGame() && shop != null && shop.CanTrade);
            if (_page == Page.Game)
            {
                if (navigation.gameObject.activeSelf == input.ModalInputActive) navigation.gameObject.SetActive(!input.ModalInputActive);
                if (input.ModalInputActive || input.BuildModeActive || input.SuppressGameplayThisFrame) return;
                if (input.PausePressed) OpenPause();
                else if (input.InventoryPressed) OpenInventory();
                else if (input.MarketPressed) OpenMarket();
                else if (input.JournalPressed) OpenJournal();
            }
            else if ((_page == Page.Inventory || _page == Page.Shop) && input.InventoryPressed
                && !FarmUIFocus.EditingText && !FarmUIFocus.TabPressed) Back();
        }
        private void PauseTime()
        { if (_ownsPause) return; _previousTimeScale = Time.timeScale; Time.timeScale = 0; _ownsPause = true; }
        private void ReleasePause() { if (!_ownsPause) return; Time.timeScale = _previousTimeScale; _ownsPause = false; }
        public void ShowMain() => ReturnHome();
        public void OpenStartupBrowser()
        { _history.Clear(); _page = Page.Main; OpenSlots(); }
        public void OpenPause() { if (CanOpenFromGame()) Show(Page.Pause); }
        public void OpenInventory() { if (CanOpenFromGame()) Show(Page.Inventory); }
        public void OpenMarket() { if (CanOpenFromGame() && shop != null && shop.CanTrade) Show(Page.Shop); }
        public void OpenGuide()
        { if (CanOpenFromGame()) { if (guide != null) Show(Page.Guide); else journal.Open(); } }
        public void OpenJournal()
        { if (CanOpenFromGame() && journal != null) Show(Page.Journal); }
        private void OpenGuideFromJournal()
        { if (_page == Page.Journal && guide != null) Show(Page.Guide); }
        private bool CanOpenFromGame() => _page == Page.Game && !input.ModalInputActive && !input.BuildModeActive && saves.HasActiveFarm;
        private void OpenSettings() => Show(Page.Settings);
        private void AskQuit() => Show(Page.Quit);
        private void Show(Page page, bool push = true)
        {
            if (page == Page.Main) { ReturnHome(); return; }
            MyLittleFarm.Audio.FarmAudio.Play(page == Page.Game ? MyLittleFarm.Audio.SoundId.UiClose : MyLittleFarm.Audio.SoundId.UiOpen);
            if (push) _history.Push((_page, FarmUIFocus.Selected)); _page = page;
            overlay.SetActive(page != Page.Game && page != Page.Journal);
            navigation.gameObject.SetActive(page == Page.Game);
            shade.SetActive(true);
            pausePanel.gameObject.SetActive(page == Page.Pause); quitPanel.gameObject.SetActive(page == Page.Quit);
            if (journal != null && page != Page.Journal) journal.Hide();
            settings.Hide(); inventory.gameObject.SetActive(false); loading.SetActive(false); if (guide != null) guide.Hide();
            if (marketPrompt != null) marketPrompt.gameObject.SetActive(false);
            if (market != null) market.Hide();
            if (page == Page.Game) { input.ModalInputActive = false; input.SuppressGameplayThisFrame = true; ReleasePause(); FarmUIFocus.Show(null, null, null); return; }
            PauseTime(); input.ModalInputActive = true; input.SuppressGameplayThisFrame = true;
            switch (page)
            {
                case Page.Pause: pauseMessage.text = FarmText.Get("pause.shortcuts", InputReader.KeyLabel(InputReader.Command.Save), InputReader.KeyLabel(InputReader.Command.Load)); FarmUIFocus.Show(pausePanel, resume, Back); break;
                case Page.Settings: settings.Open(Back); break;
                case Page.Inventory: inventory.Open(false, Back); break;
                case Page.Shop: if (market != null) market.Open(Back); break;
                case Page.Guide: guide.Open(); FarmUIFocus.Show(guide.Panel, guide.JournalButton, Back); break;
                case Page.Journal: journal.Open(true); break;
                case Page.Quit: FarmUIFocus.Show(quitPanel, quitCancel, Back); break;
            }
            Resize(true);
        }

        private int _shortcutRevision = -1;
        private void RefreshShortcutLabels()
        {
            if (_shortcutRevision == InputReader.BindingRevision) return;
            _shortcutRevision = InputReader.BindingRevision;
            if (marketPrompt != null) marketPrompt.GetComponentInChildren<Text>(true).text = FarmText.Get("market.openHint", InputReader.MarketHint);
            var guideLabel = journalShortcut.GetComponentInChildren<Text>(true);
            if (guideLabel != null) guideLabel.text = FarmText.Get("journal.shortcut", InputReader.KeyLabel(InputReader.Command.Guide));
        }
        private void Back()
        {
            if (_page == Page.Main) return;
            MyLittleFarm.Audio.FarmAudio.Play(MyLittleFarm.Audio.SoundId.UiBack);
            var previous = _history.Count > 0 ? _history.Pop() : (Page.Game, (GameObject)null);
            Show(previous.Item1, false);
            FarmUIFocus.Restore(previous.Item2, FarmUIFocus.Selected == null ? null : FarmUIFocus.Selected.GetComponent<Selectable>());
        }
        private void Resume()
        {
            while (_history.Count > 1) _history.Pop();
            var previous = _history.Count == 0 ? null : _history.Pop().Focus;
            Show(Page.Game, false);
            FarmUIFocus.Restore(previous);
        }
        private void OpenSlots()
        {
            if (marketPrompt != null) marketPrompt.gameObject.SetActive(false);
            PauseTime();
            _slotReturn = _page; _slotFocus = FarmUIFocus.Selected; _waitingForSlots = true;
            navigation.gameObject.SetActive(false);
            overlay.SetActive(false); input.ModalInputActive = false;
            slots.Open();
        }
        public void OpenSaveBrowser()
        { if (_page == Page.Game && !input.ModalInputActive) OpenSlots(); }
        private void SlotsClosed(bool selectedFarm)
        {
            if (!_waitingForSlots) return;
            _waitingForSlots = false;
            if (selectedFarm) Resume();
            else
            {
                Show(_slotReturn, false);
                FarmUIFocus.Restore(_slotFocus, FarmUIFocus.Selected == null ? null : FarmUIFocus.Selected.GetComponent<Selectable>());
            }
            _slotFocus = null;
        }
        private void ReturnHome()
        {
            if (saves.HasActiveFarm && !saves.SaveActivePoint(true)) { pauseMessage.text = FarmText.Get("pause.homeSaveFailed"); return; }
            if (!FarmSceneFlow.LoadMenu()) { pauseMessage.text = FarmText.Get("pause.menuFailed"); return; }
            input.ModalInputActive = true;
        }
        private void Quit()
        {
            if (saves.HasActiveFarm && !saves.SaveActivePoint(true))
            { Back(); pauseMessage.text = FarmText.Get("pause.quitSaveFailed"); return; }
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
        private void OpenJournalFromGuide()
        {
            if (_page != Page.Guide) return;
            if (_history.Count > 0 && _history.Peek().Page == Page.Journal) Back();
            else if (journal != null) Show(Page.Journal);
        }
        public void SetLoading(bool visible) { loading.SetActive(visible); if (visible) loading.transform.SetAsLastSibling(); }
        public void ApplyTheme()
        {
            FarmUITheme.Style(resume);
            foreach (var button in new[] { pauseSettings, pauseSlots, home, quitCancel }) FarmUITheme.Style(button, "ghost");
            FarmUITheme.Style(pauseExit, "danger"); FarmUITheme.Style(quitConfirm, "danger");
            settings.ApplyTheme(); inventory.ApplyTheme();
        }
        private void Resize(bool force = false)
        {
            if (safeArea == null) return; var canvas = (RectTransform)transform.parent; var size = canvas.rect.size;
            var safe = Screen.safeArea;
            if (!force && safe == _lastSafeArea && size == _lastSize) return;
            _lastSafeArea = safe; _lastSize = size;
            // In Edit Mode a render-texture preview can change Screen dimensions while
            // safeArea still refers to the small Game View. Preview the full target canvas.
            safeArea.anchorMin = Application.isPlaying ? safe.min / new Vector2(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height)) : Vector2.zero;
            safeArea.anchorMax = Application.isPlaying ? safe.max / new Vector2(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height)) : Vector2.one; safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
            Canvas.ForceUpdateCanvases(); var area = safeArea.rect.size;
            foreach (var panel in new[] { pausePanel, quitPanel, settings.Panel, inventory.Panel })
            { var factor = Mathf.Min(1, (area.x - 48) / panel.sizeDelta.x, (area.y - 80) / panel.sizeDelta.y); panel.localScale = Vector3.one * Mathf.Max(.3f, factor); }
            if (guide != null) guide.Panel.localScale = Vector3.one * Mathf.Max(.3f, Mathf.Min(1, (area.x - 48) / guide.Panel.sizeDelta.x, (area.y - 80) / guide.Panel.sizeDelta.y));
            if (market != null) market.Panel.localScale = Vector3.one * Mathf.Max(.3f, Mathf.Min(1, (area.x - 48) / market.Panel.sizeDelta.x, (area.y - 80) / market.Panel.sizeDelta.y));
            FarmUITheme.Place(navigation, Vector2.one, Vector2.one, new Vector2(-20, -18), new Vector2(138, 64));
            if (marketPrompt != null) FarmUITheme.Place(marketPrompt, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, area.x < 1500 ? 248 : 122), new Vector2(286, 60));
        }
        public void Preview(Page page)
        {
            if (Application.isPlaying) return;
            if (page == Page.Main) page = Page.Game;
            overlay.SetActive(page != Page.Game); navigation.gameObject.SetActive(page == Page.Game);
            shade.SetActive(true);
            pausePanel.gameObject.SetActive(page == Page.Pause); quitPanel.gameObject.SetActive(page == Page.Quit);
            settings.gameObject.SetActive(page == Page.Settings); if (page == Page.Settings) settings.PreviewPage(0);
            inventory.gameObject.SetActive(page == Page.Inventory);
            if (page == Page.Inventory) inventory.Preview(false);
            if (market != null) { if (page == Page.Shop) market.Preview(); else market.Hide(); }
            if (guide != null) { if (page == Page.Guide) guide.Preview(); else guide.Hide(); }
            if (marketPrompt != null) marketPrompt.gameObject.SetActive(false);
            loading.SetActive(false); Resize(true);
        }
        public static FarmMenuUI Create(Transform parent, Font font, InputReader input, SaveSystem saves, SaveSlotsUI slots,
            FarmJournalUI journal, BuildCustomizationUI catalogue, BuildSystem buildings, InventorySystem items, WalletSystem wallet, FarmCatalog catalog, QuickSlotSystem quickSlots, SeedShopSystem shop, SellingSystem selling)
        {
            var root = FarmUITheme.Rect(parent, "Farm UI"); FarmUITheme.Stretch(root); var ui = root.gameObject.AddComponent<FarmMenuUI>();
            ui.input = input; ui.saves = saves; ui.slots = slots; ui.journal = journal; ui.catalogue = catalogue; ui.buildings = buildings;
            var nav = FarmUITheme.Rect(root, "HUD navigation"); ui.navigation = nav;
            var row = nav.gameObject.AddComponent<HorizontalLayoutGroup>(); row.spacing = 10; row.childControlHeight = row.childControlWidth = true; row.childForceExpandHeight = row.childForceExpandWidth = true;
            ui.journalShortcut = Shortcut(nav, "help", "Справочник · J", font); ui.pauseShortcut = Shortcut(nav, "pause", "Пауза · Esc", font);
            var overlay = FarmUITheme.Rect(root, "Screen overlay"); FarmUITheme.Stretch(overlay); ui.overlay = overlay.gameObject;
            var dim = FarmUITheme.Image(overlay, "Dimmer"); FarmUITheme.Stretch(dim.rectTransform); dim.color = new Color(.09f, .13f, .05f, .58f); ui.shade = dim.gameObject;
            ui.safeArea = FarmUITheme.Rect(overlay, "Safe area"); FarmUITheme.Stretch(ui.safeArea);
            ui.pausePanel = FarmUITheme.Panel(ui.safeArea, "ПАУЗА", font, new Vector2(650, 720));
            ui.resume = PauseButton(ui.pausePanel, "Продолжить", "continue", font, 0); ui.pauseSettings = PauseButton(ui.pausePanel, "Настройки", "settings", font, 1, "ghost");
            ui.pauseSlots = PauseButton(ui.pausePanel, "Сохранения", "save", font, 2, "ghost"); ui.home = PauseButton(ui.pausePanel, "Главное меню", "home", font, 3, "ghost"); ui.pauseExit = PauseButton(ui.pausePanel, "Выйти из игры", "exit", font, 4, "danger");
            ui.pauseMessage = FarmUITheme.Label(ui.pausePanel, "", font, 20); FarmUITheme.Place(ui.pauseMessage.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 28), new Vector2(560, 64));
            ui.settings = FarmSettingsUI.Create(ui.safeArea, font);
            ui.inventory = FarmInventoryUI.Create(ui.safeArea, font, items, wallet, catalog, quickSlots, shop, selling);
            ui.quitPanel = FarmUITheme.Panel(ui.safeArea, "ВЫЙТИ ИЗ ИГРЫ?", font, new Vector2(740, 340));
            var quitText = FarmUITheme.Label(ui.quitPanel, "Текущая ферма будет сохранена.", font, 28); FarmUITheme.Place(quitText.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -86), new Vector2(660, 84));
            ui.quitCancel = FarmUITheme.Button(ui.quitPanel, "Отмена", font, "back", "ghost"); ui.quitConfirm = FarmUITheme.Button(ui.quitPanel, "Выйти", font, "exit", "danger");
            FarmUITheme.Place((RectTransform)ui.quitCancel.transform, Vector2.zero, Vector2.zero, new Vector2(36, 40), new Vector2(310, 76));
            FarmUITheme.Place((RectTransform)ui.quitConfirm.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-36, 40), new Vector2(310, 76));
            var loading = FarmUITheme.Image(root, "Loading farm"); FarmUITheme.Stretch(loading.rectTransform); loading.color = new Color(.08f, .12f, .04f, .96f); ui.loading = loading.gameObject;
            var loadingPanel = FarmUITheme.Panel(loading.transform, "ЗАГРУЗКА ФЕРМЫ", font, new Vector2(720, 260));
            var loadingText = FarmUITheme.Label(loadingPanel, "Подготавливаем грядки и постройки…", font, 28); FarmUITheme.Stretch(loadingText.rectTransform, 54);
            ui.ConfigureGameplayReference(font, catalog, input.GetComponentInChildren<MyLittleFarm.Gameplay.Animals.AnimalSystem>(true), input.GetComponentInChildren<MyLittleFarm.Gameplay.Farming.OrchardSystem>(true), shop);
            ui.Preview(Page.Game); return ui;
        }
        public void ConfigureGameplayReference(Font font, FarmCatalog catalog, MyLittleFarm.Gameplay.Animals.AnimalSystem animals,
            MyLittleFarm.Gameplay.Farming.OrchardSystem orchard, SeedShopSystem shopSystem)
        {
            shop = shopSystem;
            if (guide == null) guide = FarmGuideUI.Create(safeArea, font, catalog, animals, orchard);
            journalShortcut.name = "Справочник · J";
            var icon = journalShortcut.GetComponentsInChildren<Image>(true);
            foreach (var image in icon) if (image.transform != journalShortcut.transform) image.sprite = FarmUITheme.Sprite("Icons/help");
            if (marketPrompt == null)
            {
                var image = FarmUITheme.Image(transform, "Nearby market prompt", "Skin/slot"); image.raycastTarget = false; marketPrompt = image.rectTransform;
                FarmUITheme.Label(marketPrompt, "Рынок · P / E", font, 26);
            }
            marketPrompt.GetComponentInChildren<Text>(true).text = "Рынок · P / E";
            marketPrompt.gameObject.SetActive(false); Resize(true);
        }
        public void ConfigureMarket(Font font, FarmMarket system)
        {
            if (market == null) market = FarmMarketUI.Create(safeArea, font, system);
            market.RepairLayout();
            guide.ConfigureUpgrades(font, system);
            Resize(true);
        }
        private static Button Shortcut(Transform parent, string icon, string name, Font font)
        {
            var button = FarmUITheme.Button(parent, "", font, null, "ghost"); button.name = name;
            var image = FarmUITheme.Icon(button.transform, icon); FarmUITheme.Stretch(image.rectTransform, 12); return button;
        }
        private static Button PauseButton(Transform parent, string name, string icon, Font font, int index, string kind = "primary")
        { var button = FarmUITheme.Button(parent, name, font, icon, kind); FarmUITheme.Place((RectTransform)button.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -84 - index * 100), new Vector2(530, 84)); return button; }
    }
}
