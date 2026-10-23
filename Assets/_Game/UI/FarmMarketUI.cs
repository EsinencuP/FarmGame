using System.Collections.Generic;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace MyLittleFarm.UI
{
    /// <summary>Kit-style catalogue, selected-item details and editable order, with explicit checkout.</summary>
    public sealed class FarmMarketUI : MonoBehaviour
    {
        [SerializeField] private FarmMarket market;
        [SerializeField] private Font font;
        [SerializeField] private RectTransform panel, cartContent;
        [SerializeField] private Button[] tabs, cards;
        [SerializeField] private Image[] cardIcons;
        [SerializeField] private Text[] cardLabels;
        [SerializeField] private Button close, add, minus, plus, pay, clear;
        [SerializeField] private Text title, description, unitPrice, quantity, balance, status, total, cartTitle;
        [SerializeField] private Image portrait;
        [SerializeField] private InputField quantityInput;
        [SerializeField] private Button maximum;
        private readonly Dictionary<string, int>[] _carts = { new Dictionary<string, int>(), new Dictionary<string, int>(), new Dictionary<string, int>(), new Dictionary<string, int>() };
        private List<FarmMarket.Offer> _offers;
        private FarmMarket.Category _category;
        private string _selected;
        private int _quantity = 1;
        private bool _dirty;
        private float _nextProximityCheck;
        private Dictionary<string, int> Cart => _carts[(int)_category];
        public RectTransform Panel => panel;
        public Button CloseButton => close;
        public void RepairLayout()
        {
            var content = panel.Find("Catalogue viewport/Catalogue items") as RectTransform;
            if (content != null) content.sizeDelta = new Vector2(0, content.sizeDelta.y);
            var detail = panel.Find("Selected item");
            foreach (Transform child in detail)
                if (child != portrait.transform && child.name == "Icon growth") child.name = "Detail metric";
            if (quantityInput == null)
            {
                var image = FarmUITheme.Image(panel, "Order quantity", "Skin/slot"); At(image.rectTransform, 110, 726, 90, 64);
                quantity.transform.SetParent(image.transform, false); FarmUITheme.Stretch(quantity.rectTransform, 6);
                quantityInput = image.gameObject.AddComponent<InputField>(); quantityInput.targetGraphic = image; quantityInput.textComponent = quantity;
                quantityInput.contentType = InputField.ContentType.IntegerNumber; quantityInput.characterLimit = 10;
                maximum = FarmUITheme.Button(panel, "Макс.", font, null, "ghost"); At((RectTransform)maximum.transform, 284, 726, 106, 64);
                At((RectTransform)add.transform, 402, 726, 288, 64);
            }
        }
        public void Open()
        {
            if (!market.CanTrade) return;
            foreach (var cart in _carts) cart.Clear();
            gameObject.SetActive(true); SelectCategory(0); status.text = "Выберите товары, добавьте в корзину и оплатите заказ.";
        }
        public void Hide() => gameObject.SetActive(false);
        public void Preview(int category = 0, bool sampleCart = false)
        {
            if (Application.isPlaying) return;
            foreach (var cart in _carts) cart.Clear();
            gameObject.SetActive(true); SelectCategory(category);
            if (sampleCart && category == 0) { Cart["carrot_seed"] = 4; Cart["wheat_seed"] = 2; Basket(); }
            status.text = "Предпросмотр · запасы фермы не загружены";
        }
        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            for (var i = 0; i < tabs.Length; i++) { var index = i; tabs[i].onClick.AddListener(() => SelectCategory(index)); }
            for (var i = 0; i < cards.Length; i++) { var index = i; cards[i].onClick.AddListener(() => Select(index)); }
            add.onClick.AddListener(Add); minus.onClick.AddListener(Less); plus.onClick.AddListener(More); pay.onClick.AddListener(Checkout); clear.onClick.AddListener(Clear);
            quantityInput.onEndEdit.AddListener(EnterQuantity); maximum.onClick.AddListener(Maximum);
            GameEvents.InventoryChanged += Dirty; GameEvents.MoneyChanged += MoneyChanged; market.Changed += Dirty;
        }
        private void OnDisable()
        {
            if (!Application.isPlaying) return;
            foreach (var tab in tabs) tab.onClick.RemoveAllListeners(); foreach (var card in cards) card.onClick.RemoveAllListeners();
            add.onClick.RemoveListener(Add); minus.onClick.RemoveListener(Less); plus.onClick.RemoveListener(More); pay.onClick.RemoveListener(Checkout); clear.onClick.RemoveListener(Clear);
            quantityInput.onEndEdit.RemoveListener(EnterQuantity); maximum.onClick.RemoveListener(Maximum);
            GameEvents.InventoryChanged -= Dirty; GameEvents.MoneyChanged -= MoneyChanged; market.Changed -= Dirty;
        }
        private void Dirty() => _dirty = true;
        private void MoneyChanged(int ignored) => Dirty();
        private void LateUpdate()
        {
            if (_dirty) { _dirty = false; Refresh(); }
            if (Time.unscaledTime < _nextProximityCheck) return;
            _nextProximityCheck = Time.unscaledTime + .2f;
            if (!market.CanTrade) { pay.interactable = false; add.interactable = false; status.text = "Подойдите к рынку для торговли."; }
        }
        private void SelectCategory(int index)
        {
            _category = (FarmMarket.Category)index; _selected = null; _quantity = 1; status.text = "";
            for (var i = 0; i < tabs.Length; i++) FarmUITheme.Style(tabs[i], i == index ? "secondary" : "ghost");
            Refresh(); FarmUITheme.Focus(cards[0]);
        }
        private void Select(int index)
        { if (_offers == null || index >= _offers.Count) return; _selected = _offers[index].Id; _quantity = 1; Details(); Paint(); }
        private FarmMarket.Offer Selected => _offers == null ? null : _offers.Find(o => o.Id == _selected);
        private int Remaining(FarmMarket.Offer offer) => Mathf.Max(0, offer.Maximum - (Cart.TryGetValue(offer.Id, out var value) ? value : 0));
        private void Less() { _quantity = Mathf.Max(1, _quantity - 1); Details(); }
        private void More() { if (Selected != null) _quantity = Mathf.Min(_quantity + 1, Mathf.Max(1, Remaining(Selected))); Details(); }
        private void EnterQuantity(string value) { if (int.TryParse(value, out var amount)) _quantity = amount; Details(); }
        private void Maximum()
        {
            var offer = Selected; if (offer == null) return;
            var limit = Remaining(offer);
            if (_category != FarmMarket.Category.Sell && offer.Price > 0)
            {
                long reserved = 0;
                foreach (var line in Cart) { var existing = _offers.Find(o => o.Id == line.Key); if (existing != null) reserved += (long)existing.Price * line.Value; }
                limit = (int)System.Math.Min(limit, System.Math.Max(0L, market.Balance - reserved) / offer.Price);
            }
            _quantity = Mathf.Max(1, limit); Details();
        }
        private void Add()
        {
            var offer = Selected;
            if (offer == null || !offer.Available || !market.CanTrade || _quantity > Remaining(offer)) return;
            Cart.TryGetValue(offer.Id, out var owned); Cart[offer.Id] = owned + _quantity;
            status.text = "Добавлено в корзину: " + offer.Name; Details(); Basket();
        }
        private void Clear() { Cart.Clear(); status.text = "Корзина очищена"; Details(); Basket(); }
        private void Checkout()
        {
            if (market.Checkout(_category, Cart, out var message)) { Cart.Clear(); _quantity = 1; }
            else MyLittleFarm.Audio.FarmAudio.Play(MyLittleFarm.Audio.SoundId.UiError);
            Refresh(); status.text = message;
        }
        private void Refresh()
        {
            _offers = market.Offers(_category);
            EnsureCardCapacity(_offers.Count);
            if (!_offers.Exists(o => o.Id == _selected)) _selected = _offers.Count == 0 ? null : _offers[0].Id;
            balance.text = market.Balance.ToString("N0");
            for (var i = 0; i < cards.Length; i++)
            {
                var exists = i < _offers.Count; cards[i].gameObject.SetActive(exists); if (!exists) continue;
                var offer = _offers[i]; cardIcons[i].sprite = FarmUITheme.Sprite("Icons/" + offer.Icon);
                cardLabels[i].text = offer.Name.Replace("Улучшение инструмента", "Инструмент") + "\n" + (offer.Available ? offer.Price + " монет" : offer.Unavailable);
                cardIcons[i].color = offer.Available ? Color.white : new Color(1, 1, 1, .5f);
            }
            Details(); Paint(); Basket();
        }
        private void EnsureCardCapacity(int count)
        {
            if (count <= cards.Length) return;
            var previous = cards.Length;
            System.Array.Resize(ref cards, count); System.Array.Resize(ref cardIcons, count); System.Array.Resize(ref cardLabels, count);
            for (var i = previous; i < count; i++)
            {
                var clone = Instantiate(cards[0].gameObject, cards[0].transform.parent); clone.name = "Offer " + i;
                cards[i] = clone.GetComponent<Button>(); cardIcons[i] = clone.transform.GetChild(0).GetComponent<Image>(); cardLabels[i] = clone.GetComponentInChildren<Text>(true);
                var index = i; if (Application.isPlaying) cards[i].onClick.AddListener(() => Select(index));
            }
        }
        private void Paint()
        { for (var i = 0; i < cards.Length && i < _offers.Count; i++) cards[i].GetComponent<Image>().color = _offers[i].Id == _selected ? new Color(1, .87f, .47f) : Color.white; }
        private void Details()
        {
            var offer = Selected; var exists = offer != null;
            if (quantityInput != null) { quantityInput.interactable = exists; maximum.interactable = exists && offer.Available && Remaining(offer) > 0; }
            portrait.gameObject.SetActive(exists); add.interactable = minus.interactable = plus.interactable = exists;
            if (!exists) { title.text = "Нет доступных позиций"; description.text = "Постройте дом, чтобы увидеть его улучшения."; unitPrice.text = ""; quantity.text = "—"; return; }
            _quantity = Mathf.Clamp(_quantity, 1, Mathf.Max(1, Remaining(offer)));
            title.text = offer.Name; portrait.sprite = FarmUITheme.Sprite("Icons/" + offer.Icon);
            var metric = panel.Find("Selected item/Detail metric").GetComponent<Image>();
            metric.sprite = FarmUITheme.Sprite("Icons/" + (_category == FarmMarket.Category.Upgrades ? "upgrade" : _category == FarmMarket.Category.Animals ? "clock" : "growth"));
            description.text = offer.Description;
            unitPrice.text = offer.Available ? "Цена за 1 шт. · " + offer.Price : offer.Unavailable;
            quantityInput.SetTextWithoutNotify(_quantity.ToString()); minus.interactable = _quantity > 1; plus.interactable = _quantity < Remaining(offer);
            add.interactable = offer.Available && Remaining(offer) > 0 && (!Application.isPlaying || market.CanTrade);
            add.GetComponentInChildren<Text>().text = _category == FarmMarket.Category.Sell ? "К продаже" : "В корзину";
        }
        private void Basket()
        {
            // Rebuild from the actual children, including any rows left by an Edit Mode preview
            // before a domain reload. Nonserialized row caches cannot own authored objects.
            for (var i = cartContent.childCount - 1; i >= 0; i--)
            { var row = cartContent.GetChild(i).gameObject; row.SetActive(false); if (Application.isPlaying) Destroy(row); else DestroyImmediate(row); }
            var heading = _category == FarmMarket.Category.Sell ? "К ПРОДАЖЕ" : "КОРЗИНА";
            cartTitle.text = heading + " · " + Cart.Count + " поз.";
            var index = 0; long sum = 0;
            foreach (var line in Cart)
            {
                var id = line.Key; var offer = _offers.Find(o => o.Id == id); var price = offer == null ? 0 : offer.Price;
                sum += (long)price * line.Value;
                var row = FarmUITheme.Rect(cartContent, id);
                At(row, 0, index++ * 48, 590, 44);
                var icon = FarmUITheme.Icon(row, offer == null ? "warning" : offer.Icon); At(icon.rectTransform, 2, 4, 36, 36);
                var label = FarmUITheme.Label(row, (offer == null ? "Недоступно" : offer.Name) + " ×" + line.Value + " · " + (long)price * line.Value, font, 21);
                label.alignment = TextAnchor.MiddleLeft; At(label.rectTransform, 48, 0, 420, 44);
                var less = FarmUITheme.Button(row, "−", font, null, "ghost"); At((RectTransform)less.transform, 476, 0, 48, 44);
                var remove = FarmUITheme.Button(row, "×", font, null, "ghost"); At((RectTransform)remove.transform, 534, 0, 48, 44);
                if (Application.isPlaying)
                {
                    less.onClick.AddListener(() => { if (--Cart[id] <= 0) Cart.Remove(id); Details(); Basket(); });
                    remove.onClick.AddListener(() => { Cart.Remove(id); Details(); Basket(); });
                }
            }
            cartContent.sizeDelta = new Vector2(0, Mathf.Max(200, index * 48));
            var valid = market.Quote(_category, Cart, out _, out var reason);
            total.text = (_category == FarmMarket.Category.Sell ? "Вы получите: " : "Итого: ") + sum.ToString("N0");
            pay.GetComponentInChildren<Text>().text = _category == FarmMarket.Category.Sell ? "Продать" : "Оплатить";
            pay.interactable = Application.isPlaying && market.CanTrade && valid; clear.interactable = Cart.Count > 0;
            if (!valid && Cart.Count > 0) status.text = reason;
        }
        public static FarmMarketUI Create(Transform parent, Font font, FarmMarket market)
        {
            var root = FarmUITheme.Rect(parent, "Market"); FarmUITheme.Stretch(root);
            var ui = root.gameObject.AddComponent<FarmMarketUI>(); ui.font = font; ui.market = market;
            ui.panel = FarmUITheme.Panel(root, "РЫНОК", font, new Vector2(1440, 900));
            var names = new[] { "Семена", "Продать", "Улучшения", "Животные" }; var keys = new[] { "seeds_carrot", "sell", "upgrade", "chicken" };
            ui.tabs = new Button[4];
            for (var i = 0; i < 4; i++) { ui.tabs[i] = FarmUITheme.Button(ui.panel, names[i], font, keys[i], "ghost"); At((RectTransform)ui.tabs[i].transform, 40 + i * 344, 78, 328, 64); }
            var viewport = FarmUITheme.Image(ui.panel, "Catalogue viewport"); viewport.color = Color.clear; At(viewport.rectTransform, 40, 170, 650, 518);
            viewport.gameObject.AddComponent<RectMask2D>(); var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            var content = FarmUITheme.Rect(viewport.transform, "Catalogue items"); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
            var grid = content.gameObject.AddComponent<GridLayoutGroup>(); grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 3; grid.cellSize = new Vector2(206, 162); grid.spacing = new Vector2(14, 14);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport.rectTransform; scroll.content = content; scroll.horizontal = false; scroll.scrollSensitivity = 36; scroll.movementType = ScrollRect.MovementType.Clamped;
            ui.cards = new Button[24]; ui.cardIcons = new Image[24]; ui.cardLabels = new Text[24];
            for (var i = 0; i < ui.cards.Length; i++)
            {
                var image = FarmUITheme.Image(content, "Offer " + i, "Skin/slot"); var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; MyLittleFarm.Audio.UIAudioFeedback.Install(button);
                ui.cards[i] = button; ui.cardIcons[i] = FarmUITheme.Icon(button.transform, "buy"); At(ui.cardIcons[i].rectTransform, 61, 12, 84, 84);
                ui.cardLabels[i] = FarmUITheme.Label(button.transform, "", font, 23); At(ui.cardLabels[i].rectTransform, 8, 98, 190, 58);
            }
            var detail = FarmUITheme.Image(ui.panel, "Selected item", "Skin/slot").rectTransform; At(detail, 730, 170, 670, 232);
            ui.portrait = FarmUITheme.Icon(detail, "growth"); At(ui.portrait.rectTransform, 24, 26, 92, 92);
            ui.title = FarmUITheme.Label(detail, "", font, 30); ui.title.alignment = TextAnchor.MiddleLeft; At(ui.title.rectTransform, 138, 18, 506, 50);
            ui.description = FarmUITheme.Label(detail, "", font, 23); ui.description.alignment = TextAnchor.UpperLeft; At(ui.description.rectTransform, 138, 78, 506, 104);
            var growth = FarmUITheme.Icon(detail, "growth"); growth.name = "Detail metric"; At(growth.rectTransform, 48, 142, 44, 44);
            var coin = FarmUITheme.Icon(detail, "coin"); At(coin.rectTransform, 140, 190, 30, 30);
            ui.unitPrice = FarmUITheme.Label(detail, "", font, 23); ui.unitPrice.alignment = TextAnchor.MiddleLeft; At(ui.unitPrice.rectTransform, 184, 184, 460, 42);
            var basket = FarmUITheme.Image(ui.panel, "Order", "Skin/slot").rectTransform; At(basket, 730, 420, 670, 288);
            ui.cartTitle = FarmUITheme.Label(basket, "КОРЗИНА", font, 26); ui.cartTitle.alignment = TextAnchor.MiddleLeft; At(ui.cartTitle.rectTransform, 24, 12, 430, 44);
            ui.clear = FarmUITheme.Button(basket, "Очистить", font, null, "ghost"); At((RectTransform)ui.clear.transform, 474, 12, 170, 44);
            var cartView = FarmUITheme.Image(basket, "Order viewport"); cartView.color = Color.clear; At(cartView.rectTransform, 24, 70, 622, 200); cartView.gameObject.AddComponent<RectMask2D>();
            var cartScroll = cartView.gameObject.AddComponent<ScrollRect>(); ui.cartContent = FarmUITheme.Rect(cartView.transform, "Order lines");
            ui.cartContent.anchorMin = new Vector2(0, 1); ui.cartContent.anchorMax = Vector2.one; ui.cartContent.pivot = new Vector2(.5f, 1); ui.cartContent.sizeDelta = new Vector2(0, 200);
            cartScroll.viewport = cartView.rectTransform; cartScroll.content = ui.cartContent; cartScroll.horizontal = false; cartScroll.scrollSensitivity = 32; cartScroll.movementType = ScrollRect.MovementType.Clamped;
            ui.minus = FarmUITheme.Button(ui.panel, "−", font, null, "ghost"); At((RectTransform)ui.minus.transform, 40, 726, 64, 64);
            ui.quantity = FarmUITheme.Label(ui.panel, "1", font, 30); At(ui.quantity.rectTransform, 110, 726, 90, 64);
            ui.plus = FarmUITheme.Button(ui.panel, "+", font, null, "ghost"); At((RectTransform)ui.plus.transform, 206, 726, 64, 64);
            ui.add = FarmUITheme.Button(ui.panel, "В корзину", font, "buy"); At((RectTransform)ui.add.transform, 294, 726, 396, 64);
            ui.total = FarmUITheme.Label(ui.panel, "Итого: 0", font, 28); ui.total.alignment = TextAnchor.MiddleLeft; At(ui.total.rectTransform, 730, 726, 370, 64);
            ui.pay = FarmUITheme.Button(ui.panel, "Оплатить", font, "confirm", "secondary"); At((RectTransform)ui.pay.transform, 1110, 726, 290, 64);
            ui.close = FarmUITheme.Button(ui.panel, "Закрыть · Esc", font, "close", "ghost"); At((RectTransform)ui.close.transform, 40, 818, 320, 58);
            var money = FarmUITheme.Icon(ui.panel, "coin"); At(money.rectTransform, 404, 824, 42, 42);
            ui.balance = FarmUITheme.Label(ui.panel, "0", font, 28); ui.balance.alignment = TextAnchor.MiddleLeft; At(ui.balance.rectTransform, 458, 818, 212, 58);
            ui.status = FarmUITheme.Label(ui.panel, "", font, 22); ui.status.alignment = TextAnchor.MiddleLeft; At(ui.status.rectTransform, 730, 806, 670, 76);
            ui.RepairLayout(); ui.Preview(); ui.Hide(); return ui;
        }
        private static void At(RectTransform rect, float x, float y, float width, float height)
        { FarmUITheme.Place(rect, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y), new Vector2(width, height)); }
    }
}
