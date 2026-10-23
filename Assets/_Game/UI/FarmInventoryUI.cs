using System.Collections.Generic;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.Farming;
using UnityEngine;
using UnityEngine.UI;

namespace MyLittleFarm.UI
{
    /// <summary>Event-driven inventory and market projection over the existing farm economy.</summary>
    public sealed class FarmInventoryUI : MonoBehaviour
    {
        [SerializeField] private InventorySystem inventory;
        [SerializeField] private WalletSystem wallet;
        [SerializeField] private FarmCatalog catalog;
        [SerializeField] private QuickSlotSystem quickSlots;
        [SerializeField] private SeedShopSystem shop;
        [SerializeField] private SellingSystem selling;
        [SerializeField] private RectTransform panel, gridRect;
        [SerializeField] private GridLayoutGroup grid;
        [SerializeField] private Button[] cells, tabs;
        [SerializeField] private Image[] icons;
        [SerializeField] private Text[] amounts;
        [SerializeField] private Text title, description, count, balance, heading, hint;
        [SerializeField] private Image detailIcon;
        [SerializeField] private Button minus, plus, transact, assign, close;
        [SerializeField] private Image growthIcon;
        [SerializeField] private Text growthText;
        [SerializeField] private OrchardSystem orchard;
        [SerializeField] private MyLittleFarm.Gameplay.Animals.AnimalSystem animals;
        private readonly List<string> _items = new List<string>();
        private string _selected;
        private int _filter, _quantity = 1;
        private bool _market;
        public RectTransform Panel => panel;
        public Button CloseButton => close;
        public void ApplyTheme()
        {
            FarmUITheme.Style(transact, "secondary"); FarmUITheme.Style(assign, "ghost"); FarmUITheme.Style(close, "ghost");
            foreach (var cell in cells) { FarmUITheme.Skin(cell.GetComponent<Image>(), "slot"); var colors = cell.colors; colors.disabledColor = Color.white; cell.colors = colors; }
        }
        public void Open(bool market)
        { if (market && !shop.CanTrade) return; _market = market; _filter = 0; _selected = null; _quantity = 1; gameObject.SetActive(true); Refresh(); FarmUITheme.Focus(cells[0]); }
        public void Preview(bool market)
        {
            if (Application.isPlaying) return;
            gameObject.SetActive(true); heading.text = market ? "МАГАЗИН СЕМЯН" : "ИНВЕНТАРЬ";
            var example = new[] { "carrot", "corn", "wheat", "tomato", "cucumber", "potato", "apple", "egg", "carrot_seed", "wheat_seed", "tomato_seed", "corn_seed" };
            for (var i = 0; i < cells.Length; i++)
            {
                var filled = i < (market ? 6 : example.Length); icons[i].gameObject.SetActive(filled); cells[i].interactable = filled;
                if (filled) { var id = market ? example[i] + "_seed" : example[i]; icons[i].sprite = FarmUITheme.Sprite("Icons/" + FarmUITheme.ItemIcon(id)); }
                amounts[i].text = filled ? "—" : "";
            }
            foreach (var tab in tabs) tab.gameObject.SetActive(!market);
            title.text = market ? "Семена моркови" : "Томат"; detailIcon.sprite = FarmUITheme.Sprite("Icons/" + (market ? "seeds_carrot" : "crop_tomato"));
            description.text = "Предпросмотр оформления\n\nВ игре здесь показаны реальные запасы и цены выбранной фермы.";
            if (growthText != null)
            {
                if (catalog.Crops.Count == 0) catalog.Configure();
                var id = market ? "carrot" : "tomato";
                growthText.text = catalog.TryGetCrop(id, out var crop) ? "Рост · " + FarmGuideUI.Duration(crop.GrowDurationSeconds) : "";
                growthText.gameObject.SetActive(true); growthIcon.gameObject.SetActive(true);
            }
            count.text = "—"; balance.text = "—"; hint.text = "Предпросмотр · игровые данные не загружены"; assign.gameObject.SetActive(false);
            transact.gameObject.SetActive(false); minus.gameObject.SetActive(false); plus.gameObject.SetActive(false); count.gameObject.SetActive(false);
        }
        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            GameEvents.InventoryChanged += Refresh; GameEvents.MoneyChanged += MoneyChanged;
            for (var i = 0; i < cells.Length; i++) { var index = i; cells[i].onClick.AddListener(() => Select(index)); }
            for (var i = 0; i < tabs.Length; i++) { var index = i; tabs[i].onClick.AddListener(() => { _filter = index; _selected = null; Refresh(); }); }
            minus.onClick.AddListener(() => ChangeAmount(-1)); plus.onClick.AddListener(() => ChangeAmount(1));
            assign.onClick.AddListener(Assign);
        }
        private void OnDisable()
        {
            if (!Application.isPlaying) return;
            GameEvents.InventoryChanged -= Refresh; GameEvents.MoneyChanged -= MoneyChanged;
            foreach (var cell in cells) cell.onClick.RemoveAllListeners(); foreach (var tab in tabs) tab.onClick.RemoveAllListeners();
            foreach (var button in new[] { minus, plus, transact, assign }) button.onClick.RemoveAllListeners();
        }
        private void MoneyChanged(int ignored) => Refresh();
        private void Select(int index) { if (index >= _items.Count) return; _selected = _items[index]; _quantity = 1; RefreshDetails(); PaintSelection(); }
        private void ChangeAmount(int delta) { _quantity = Mathf.Clamp(_quantity + delta, 1, Maximum()); RefreshDetails(); }
        private int Maximum()
        {
            if (_selected == null || !catalog.TryGetItem(_selected, out var item)) return 1;
            return _market ? Mathf.Max(1, Mathf.Min(999, item.BuyPrice > 0 ? wallet.Coins / item.BuyPrice : 999)) : Mathf.Max(1, inventory.GetAmount(_selected));
        }
        private void Refresh()
        {
            if (!gameObject.activeInHierarchy || catalog == null || inventory == null) return;
            _items.Clear();
            foreach (var crop in catalog.Crops)
            {
                if (_market) { _items.Add(crop.SeedItemId); continue; }
                if ((_filter == 0 || _filter == 1) && inventory.GetAmount(crop.SeedItemId) > 0) _items.Add(crop.SeedItemId);
                if ((_filter == 0 || _filter == 2) && inventory.GetAmount(crop.HarvestItemId) > 0) _items.Add(crop.HarvestItemId);
            }
            if (!_market && (_filter == 0 || _filter == 3)) foreach (var id in new[] { "apple", "egg" }) if (inventory.GetAmount(id) > 0) _items.Add(id);
            heading.text = _market ? "МАГАЗИН СЕМЯН" : "ИНВЕНТАРЬ";
            for (var i = 0; i < tabs.Length; i++) { tabs[i].gameObject.SetActive(!_market); FarmUITheme.Style(tabs[i], i == _filter ? "secondary" : "ghost"); }
            for (var i = 0; i < cells.Length; i++)
            {
                var filled = i < _items.Count; cells[i].interactable = filled; icons[i].gameObject.SetActive(filled);
                if (!filled) { amounts[i].text = ""; continue; }
                var id = _items[i]; icons[i].sprite = FarmUITheme.Sprite("Icons/" + FarmUITheme.ItemIcon(id));
                amounts[i].text = _market && catalog.TryGetItem(id, out var item) ? (catalog.IsRareSeed(id) ? "Тайники" : item.BuyPrice + " м.") : "×" + inventory.GetAmount(id);
            }
            if (!_items.Contains(_selected)) _selected = _items.Count > 0 ? _items[0] : null;
            balance.text = wallet.Coins.ToString("N0");
            hint.text = _market ? "Покупка у рынка · редкие семена — в тайниках" : "I / Esc — закрыть · выберите предмет";
            RefreshDetails(); PaintSelection();
        }
        private void PaintSelection()
        {
            for (var i = 0; i < cells.Length; i++)
            { var selected = i < _items.Count && _items[i] == _selected; cells[i].GetComponent<Image>().color = selected ? new Color(1, .88f, .48f) : Color.white; }
        }
        private void RefreshDetails()
        {
            var found = _selected != null && catalog.TryGetItem(_selected, out _);
            detailIcon.gameObject.SetActive(found); minus.interactable = plus.interactable = found;
            if (growthIcon != null) { growthIcon.gameObject.SetActive(found); growthText.gameObject.SetActive(found); }
            transact.gameObject.SetActive(false); minus.gameObject.SetActive(false); plus.gameObject.SetActive(false); count.gameObject.SetActive(false); assign.gameObject.SetActive(false);
            if (!found) { title.text = "Пока пусто"; description.text = "Посадите семена и соберите первый урожай."; count.text = "—"; return; }
            catalog.TryGetItem(_selected, out var item);
            _quantity = Mathf.Clamp(_quantity, 1, Maximum());
            title.text = item.DisplayName; detailIcon.sprite = item.Icon ?? FarmUITheme.Sprite("Icons/" + FarmUITheme.ItemIcon(_selected));
            var seed = _selected.EndsWith("_seed");
            if (growthText != null)
            {
                var cropId = seed ? _selected.Substring(0, _selected.Length - 5) : _selected;
                if (catalog.TryGetCrop(cropId, out var crop)) growthText.text = "Рост · " + FarmGuideUI.Duration(crop.GrowDurationSeconds);
                else if (_selected == "apple" && orchard != null) growthText.text = "Повторный сбор · " + FarmGuideUI.Duration(orchard.RegrowSeconds);
                else if (_selected == "egg" && animals != null) growthText.text = "Производство · " + FarmGuideUI.Duration(animals.ProductIntervalSeconds);
                else growthText.text = "";
                growthIcon.sprite = FarmUITheme.Sprite("Icons/" + (_selected == "egg" ? "clock" : "growth"));
            }
            description.text = (_market ? "В наличии: " : "Количество: ") + inventory.GetAmount(_selected)
                + "\n" + (seed ? "Семена · выберите в быстром слоте для посадки." : "Урожай · можно продать у рынка.")
                + "\n\n" + (catalog.IsRareSeed(_selected) ? "Редкие семена: ищите тайники у пруда, в лесу и на холме." : (_market ? "Цена покупки: " + item.BuyPrice : "Цена продажи: " + item.SellPrice) + " монет / шт.");
            count.text = _quantity.ToString(); minus.interactable = _quantity > 1; plus.interactable = _quantity < Maximum();
            var available = _market ? item.CanBuy && !catalog.IsRareSeed(_selected) && (long)item.BuyPrice * _quantity <= wallet.Coins : !seed && item.SellPrice > 0;
            transact.interactable = available && shop.CanTrade;
            transact.GetComponentInChildren<Text>().text = !shop.CanTrade ? "Подойдите к рынку" : (_market ? "Купить · " : "Продать · ") + (long)(_market ? item.BuyPrice : item.SellPrice) * _quantity;
            if (!_market && seed) { assign.gameObject.SetActive(true); assign.interactable = inventory.GetAmount(_selected) > 0; }
        }
        private void Assign()
        {
            for (var i = 0; i < catalog.Crops.Count; i++)
                if (catalog.Crops[i].SeedItemId == _selected) { quickSlots.Select(i); hint.text = "Выбран быстрый слот " + (i + 1) + " · закройте инвентарь для посадки"; break; }
        }
        public void ConfigureReferenceDetails(Font font, OrchardSystem orchardSystem, MyLittleFarm.Gameplay.Animals.AnimalSystem animalSystem)
        {
            orchard = orchardSystem; animals = animalSystem;
            var detail = detailIcon.transform.parent;
            if (growthIcon == null)
            {
                growthIcon = FarmUITheme.Icon(detail, "growth");
                growthText = FarmUITheme.Label(detail, "", font, 24); growthText.alignment = TextAnchor.MiddleLeft;
            }
            FarmUITheme.Place(growthIcon.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -152), new Vector2(34, 36));
            FarmUITheme.Place(growthText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(72, -150), new Vector2(390, 42));
            FarmUITheme.Place(description.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -206), new Vector2(440, 126));
            var coin = panel.Find("Inventory coin") as RectTransform;
            if (coin == null) { coin = FarmUITheme.Icon(panel, "coin").rectTransform; coin.name = "Inventory coin"; }
            FarmUITheme.Place(coin, Vector2.zero, Vector2.zero, new Vector2(38, 116), new Vector2(44, 44));
            FarmUITheme.Place(balance.rectTransform, Vector2.zero, Vector2.zero, new Vector2(94, 112), new Vector2(598, 54));
            balance.alignment = TextAnchor.MiddleLeft;
        }
        public static FarmInventoryUI Create(Transform parent, Font font, InventorySystem inventory, WalletSystem wallet, FarmCatalog catalog, QuickSlotSystem slots, SeedShopSystem shop, SellingSystem selling)
        {
            var root = FarmUITheme.Rect(parent, "Inventory and market"); FarmUITheme.Stretch(root); var ui = root.gameObject.AddComponent<FarmInventoryUI>();
            ui.inventory = inventory; ui.wallet = wallet; ui.catalog = catalog; ui.quickSlots = slots; ui.shop = shop; ui.selling = selling;
            ui.panel = FarmUITheme.Panel(root, "ИНВЕНТАРЬ", font, new Vector2(1280, 830)); ui.heading = ui.panel.Find("Title ribbon").GetComponentInChildren<Text>();
            ui.tabs = new Button[4]; var names = new[] { "Всё", "Семена", "Урожай", "Продукты" }; var keys = new[] { "inventory", "seeds_carrot", "crop_tomato", "egg" };
            for (var i = 0; i < 4; i++)
            { ui.tabs[i] = FarmUITheme.Button(ui.panel, names[i], font, keys[i], "ghost"); FarmUITheme.Place((RectTransform)ui.tabs[i].transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(38 + i * 234, -66), new Vector2(220, 62)); }
            var grid = FarmUITheme.Rect(ui.panel, "Item grid"); ui.gridRect = grid;
            FarmUITheme.Place(grid, new Vector2(0, 1), new Vector2(0, 1), new Vector2(38, -162), new Vector2(654, 450));
            ui.grid = grid.gameObject.AddComponent<GridLayoutGroup>(); ui.grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; ui.grid.constraintCount = 6;
            ui.grid.cellSize = new Vector2(96, 96); ui.grid.spacing = new Vector2(12, 14);
            ui.cells = new Button[24]; ui.icons = new Image[24]; ui.amounts = new Text[24];
            for (var i = 0; i < ui.cells.Length; i++)
            {
                var image = FarmUITheme.Image(grid, "Item " + i, "Skin/slot"); ui.cells[i] = image.gameObject.AddComponent<Button>(); ui.cells[i].targetGraphic = image;
                ui.icons[i] = FarmUITheme.Icon(image.transform, "inventory"); FarmUITheme.Stretch(ui.icons[i].rectTransform, 12); ui.icons[i].rectTransform.offsetMin = new Vector2(12, 28);
                ui.amounts[i] = FarmUITheme.Label(image.transform, "", font, 21);
                FarmUITheme.Place(ui.amounts[i].rectTransform, Vector2.zero, Vector2.zero, new Vector2(6, 3), new Vector2(84, 26));
            }
            var detail = FarmUITheme.Image(ui.panel, "Item details", "Skin/slot").rectTransform;
            FarmUITheme.Place(detail, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-38, -162), new Vector2(490, 498));
            ui.detailIcon = FarmUITheme.Icon(detail, "crop_carrot"); FarmUITheme.Place(ui.detailIcon.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -24), new Vector2(112, 112));
            ui.title = FarmUITheme.Label(detail, "Предмет", font, 34); FarmUITheme.Place(ui.title.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-22, -24), new Vector2(308, 112));
            ui.description = FarmUITheme.Label(detail, "", font, 24); ui.description.alignment = TextAnchor.UpperLeft;
            FarmUITheme.Place(ui.description.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -154), new Vector2(440, 178));
            ui.minus = FarmUITheme.Button(detail, "−", font, null, "ghost"); ui.plus = FarmUITheme.Button(detail, "+", font, null, "ghost"); ui.count = FarmUITheme.Label(detail, "1", font, 28);
            FarmUITheme.Place((RectTransform)ui.minus.transform, Vector2.zero, Vector2.zero, new Vector2(24, 96), new Vector2(56, 58));
            FarmUITheme.Place(ui.count.rectTransform, Vector2.zero, Vector2.zero, new Vector2(82, 96), new Vector2(72, 58));
            FarmUITheme.Place((RectTransform)ui.plus.transform, Vector2.zero, Vector2.zero, new Vector2(156, 96), new Vector2(56, 58));
            ui.transact = FarmUITheme.Button(detail, "Продать", font, null, "secondary"); FarmUITheme.Place((RectTransform)ui.transact.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 96), new Vector2(230, 58));
            ui.assign = FarmUITheme.Button(detail, "В быстрый слот", font, "play", "ghost"); FarmUITheme.Place((RectTransform)ui.assign.transform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 22), new Vector2(440, 58));
            ui.balance = FarmUITheme.Label(ui.panel, "", font, 28); FarmUITheme.Place(ui.balance.rectTransform, Vector2.zero, Vector2.zero, new Vector2(38, 112), new Vector2(654, 54));
            ui.hint = FarmUITheme.Label(ui.panel, "", font, 21); FarmUITheme.Place(ui.hint.rectTransform, Vector2.zero, Vector2.zero, new Vector2(38, 36), new Vector2(860, 58));
            ui.close = FarmUITheme.Button(ui.panel, "Закрыть", font, "close", "ghost"); FarmUITheme.Place((RectTransform)ui.close.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-38, 30), new Vector2(270, 68));
            root.gameObject.SetActive(false); return ui;
        }
    }
}
