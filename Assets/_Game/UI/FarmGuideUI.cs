using System;
using MyLittleFarm.Gameplay.Animals;
using MyLittleFarm.Gameplay.Farming;
using UnityEngine;
using UnityEngine.UI;

namespace MyLittleFarm.UI
{
    /// <summary>Read-only reference over actual farm definitions, separate from owned inventory.</summary>
    public sealed class FarmGuideUI : MonoBehaviour
    {
        [Serializable] private sealed class PlantCard
        { public Image portrait, seed; public Text title, seedLabel, growth, price, yield; }
        [SerializeField] private FarmCatalog catalog;
        [SerializeField] private AnimalSystem animals;
        [SerializeField] private OrchardSystem orchard;
        [SerializeField] private RectTransform panel;
        [SerializeField] private Button plantsTab, animalsTab, close, journal;
        [SerializeField] private GameObject plantsPage, animalsPage;
        [SerializeField] private ScrollRect plantScroll;
        [SerializeField] private PlantCard[] plantCards;
        [SerializeField] private Text animalName;
        [SerializeField] private Text[] animalValues;
        [SerializeField] private Button upgradesTab;
        [SerializeField] private GameObject upgradesPage;
        [SerializeField] private Text upgradePrices;
        [SerializeField] private MyLittleFarm.Gameplay.Economy.FarmMarket market;
        public RectTransform Panel => panel;
        public Button CloseButton => close;
        public Button JournalButton => journal;
        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            plantsTab.onClick.AddListener(ShowPlants); animalsTab.onClick.AddListener(ShowAnimals);
            if (upgradesTab != null) upgradesTab.onClick.AddListener(ShowUpgrades);
        }
        private void OnDisable()
        { plantsTab.onClick.RemoveListener(ShowPlants); animalsTab.onClick.RemoveListener(ShowAnimals); if (upgradesTab != null) upgradesTab.onClick.RemoveListener(ShowUpgrades); }
        public void Open() { gameObject.SetActive(true); Refresh(); ShowPlants(); }
        public void Hide() => gameObject.SetActive(false);
        public void Preview(bool showAnimals = false, float scrollPosition = 1)
        {
            if (Application.isPlaying) return;
            // Catalog.Configure only builds in-memory definitions. No bootstrap, world or profile IO.
            if (catalog.Crops.Count == 0) catalog.Configure();
            gameObject.SetActive(true); Refresh(); Show(showAnimals);
            Canvas.ForceUpdateCanvases(); plantScroll.verticalNormalizedPosition = Mathf.Clamp01(scrollPosition);
        }
        private void ShowPlants() => Show(false);
        private void ShowAnimals() => Show(true);
        private void Show(bool showAnimals)
        {
            if (upgradesPage != null) { upgradesPage.SetActive(false); FarmUITheme.Style(upgradesTab, "ghost"); }
            plantsPage.SetActive(!showAnimals); animalsPage.SetActive(showAnimals);
            FarmUITheme.Style(plantsTab, showAnimals ? "ghost" : "secondary");
            FarmUITheme.Style(animalsTab, showAnimals ? "secondary" : "ghost");
            if (!showAnimals) { Canvas.ForceUpdateCanvases(); plantScroll.verticalNormalizedPosition = 1; }
            FarmUITheme.Focus(showAnimals ? animalsTab : plantsTab);
        }
        private void ShowUpgrades()
        {
            plantsPage.SetActive(false); animalsPage.SetActive(false); upgradesPage.SetActive(true);
            FarmUITheme.Style(plantsTab, "ghost"); FarmUITheme.Style(animalsTab, "ghost"); FarmUITheme.Style(upgradesTab, "secondary");
            var text = new System.Text.StringBuilder("КАТАЛОГ УЛУЧШЕНИЙ\n\n");
            foreach (var offer in market.Offers(MyLittleFarm.Gameplay.Economy.FarmMarket.Category.Upgrades))
                text.Append(offer.Name).Append(" · ").Append(offer.Price).Append(" монет").Append(offer.Available ? "" : " · " + offer.Unavailable).Append('\n').Append(offer.Description).Append("\n\n");
            text.Append("Покупка улучшений и найм — в окне рынка.\nПодойдите к рынку и нажмите P или E.");
            upgradePrices.text = text.ToString();
        }
        public void ConfigureUpgrades(Font font, MyLittleFarm.Gameplay.Economy.FarmMarket system)
        {
            market = system;
            if (upgradesTab == null)
            {
                upgradesTab = FarmUITheme.Button(panel, "Улучшения", font, "upgrade", "ghost");
                var body = plantsPage.transform.parent;
                var page = FarmUITheme.Image(body, "Upgrade prices"); FarmUITheme.Stretch(page.rectTransform); page.color = Color.clear; upgradesPage = page.gameObject;
                page.gameObject.AddComponent<RectMask2D>(); var scroll = page.gameObject.AddComponent<ScrollRect>();
                upgradePrices = FarmUITheme.Label(page.transform, "", font, 26); upgradePrices.alignment = TextAnchor.UpperLeft; upgradePrices.resizeTextForBestFit = false; upgradePrices.verticalOverflow = VerticalWrapMode.Overflow;
                var content = upgradePrices.rectTransform; content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
                content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                scroll.viewport = page.rectTransform; scroll.content = content; scroll.horizontal = false; scroll.scrollSensitivity = 36; scroll.movementType = ScrollRect.MovementType.Clamped;
            }
            var buttons = new[] { plantsTab, animalsTab, upgradesTab };
            for (var i = 0; i < buttons.Length; i++) FarmUITheme.Place((RectTransform)buttons[i].transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2((i - 1) * 382, -76), new Vector2(362, 64));
            foreach (var label in animalsPage.GetComponentsInChildren<Text>(true))
                if (label.text.Contains("C — купить")) label.text = "1. Купите курицу на рынке\nПосле оплаты она поселится в свободном месте загона.";
            upgradesPage.SetActive(false);
        }
        public static string Duration(float seconds)
        {
            if (seconds < 60) return seconds.ToString("0.#") + " с";
            var minutes = Mathf.FloorToInt(seconds / 60); var rest = Mathf.RoundToInt(seconds % 60);
            return minutes + " мин" + (rest > 0 ? " " + rest + " с" : "");
        }
        private string ItemName(string id) => catalog.TryGetItem(id, out var item) ? item.DisplayName : id;
        private int SalePrice(string id) => catalog.TryGetItem(id, out var item) ? item.SellPrice : 0;
        private void Refresh()
        {
            for (var i = 0; i < plantCards.Length - 1; i++)
            {
                var view = plantCards[i]; var available = i < catalog.Crops.Count;
                view.portrait.transform.parent.gameObject.SetActive(available); if (!available) continue;
                var crop = catalog.Crops[i]; catalog.TryGetItem(crop.SeedItemId, out var seed);
                view.title.text = crop.DisplayName;
                view.portrait.sprite = FarmUITheme.Sprite("Icons/" + FarmUITheme.ItemIcon(crop.HarvestItemId));
                view.seed.sprite = FarmUITheme.Sprite("Icons/" + (crop.RareSeeds ? "seeds_rare" : FarmUITheme.ItemIcon(crop.SeedItemId)));
                view.seedLabel.text = crop.RareSeeds ? "Редкие семена" : "Посадка из семян";
                view.growth.text = "Рост · " + Duration(crop.GrowDurationSeconds);
                view.price.text = (crop.RareSeeds ? "Семена: тайники" : "Семя: " + (seed == null ? 0 : seed.BuyPrice)) + "  ·  Урожай: " + SalePrice(crop.HarvestItemId);
                view.yield.text = "Сбор · " + crop.YieldAmount + " шт. за посадку";
            }
            var tree = plantCards[plantCards.Length - 1]; var definition = orchard.Definition;
            tree.title.text = definition == null ? "Яблоня" : definition.DisplayName;
            tree.portrait.sprite = FarmUITheme.Sprite("Icons/sapling"); tree.seed.sprite = FarmUITheme.Sprite("Icons/crop_apple");
            tree.seedLabel.text = "Сад · повторный урожай";
            tree.growth.text = "Первый сбор · " + Duration(orchard.FirstHarvestSeconds);
            tree.price.text = "Посадка: 0  ·  Плод: " + SalePrice(definition == null ? "apple" : definition.FruitItemId);
            tree.yield.text = orchard.YieldAmount + " шт.  ·  Повтор: " + Duration(orchard.RegrowSeconds);
            var animal = animals.Definition; var feedId = animal == null ? "wheat" : animal.FeedItemId;
            var productId = animal == null ? "egg" : animal.ProductItemId;
            animalName.text = animal == null ? "Курица" : animal.DisplayName;
            animalValues[0].text = "Покупка · " + animals.ChickenPrice + " монет";
            animalValues[1].text = "Корм · 1 × " + ItemName(feedId);
            animalValues[2].text = "Продукция · " + (animal == null ? 1 : animal.ProductYield) + " × " + ItemName(productId);
            animalValues[3].text = "Производство · " + Duration(animals.ProductIntervalSeconds);
            animalValues[4].text = "Продажа продукта · " + SalePrice(productId) + " монет / шт.";
            animalValues[5].text = "Содержание · загон, до " + animals.MaxAnimals + " животных";
        }
        public static FarmGuideUI Create(Transform parent, Font font, FarmCatalog catalog, AnimalSystem animals, OrchardSystem orchard)
        {
            var root = FarmUITheme.Rect(parent, "Farm handbook"); FarmUITheme.Stretch(root);
            var ui = root.gameObject.AddComponent<FarmGuideUI>(); ui.catalog = catalog; ui.animals = animals; ui.orchard = orchard;
            ui.panel = FarmUITheme.Panel(root, "СПРАВОЧНИК", font, new Vector2(1280, 880));
            ui.plantsTab = FarmUITheme.Button(ui.panel, "Растения", font, "growth", "secondary");
            ui.animalsTab = FarmUITheme.Button(ui.panel, "Животные", font, "chicken", "ghost");
            FarmUITheme.Place((RectTransform)ui.plantsTab.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(-272, -76), new Vector2(516, 64));
            FarmUITheme.Place((RectTransform)ui.animalsTab.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(272, -76), new Vector2(516, 64));
            var body = FarmUITheme.Rect(ui.panel, "Handbook pages"); FarmUITheme.Stretch(body);
            body.offsetMin = new Vector2(38, 120); body.offsetMax = new Vector2(-38, -166);
            var plants = FarmUITheme.Rect(body, "Plants"); FarmUITheme.Stretch(plants); ui.plantsPage = plants.gameObject;
            var note = FarmUITheme.Label(plants, "Базовое время роста · цены в монетах за 1 шт.", font, 22);
            FarmUITheme.Place(note.rectTransform, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(1140, 36)); note.alignment = TextAnchor.MiddleLeft;
            var viewport = FarmUITheme.Image(plants, "Plants viewport"); viewport.color = Color.clear;
            FarmUITheme.Stretch(viewport.rectTransform); viewport.rectTransform.offsetMax = new Vector2(-30, -52);
            viewport.gameObject.AddComponent<RectMask2D>(); ui.plantScroll = viewport.gameObject.AddComponent<ScrollRect>();
            var content = FarmUITheme.Rect(viewport.transform, "Plant cards");
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
            var grid = content.gameObject.AddComponent<GridLayoutGroup>(); grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 3;
            grid.cellSize = new Vector2(378, 248); grid.spacing = new Vector2(18, 16); grid.padding = new RectOffset(0, 0, 0, 8);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ui.plantScroll.horizontal = false; ui.plantScroll.viewport = viewport.rectTransform; ui.plantScroll.content = content;
            ui.plantScroll.scrollSensitivity = 38; ui.plantScroll.movementType = ScrollRect.MovementType.Clamped;
            var track = FarmUITheme.Image(plants, "Plants scrollbar", "Skin/slot").rectTransform;
            track.anchorMin = new Vector2(1, 0); track.anchorMax = Vector2.one; track.pivot = new Vector2(1, .5f);
            track.offsetMin = new Vector2(-20, 0); track.offsetMax = new Vector2(0, -52);
            var handle = FarmUITheme.Image(track, "Handle", "Skin/secondary"); FarmUITheme.Stretch(handle.rectTransform);
            var scrollbar = track.gameObject.AddComponent<Scrollbar>(); scrollbar.handleRect = handle.rectTransform; scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop; ui.plantScroll.verticalScrollbar = scrollbar;
            ui.plantCards = new PlantCard[7];
            for (var i = 0; i < ui.plantCards.Length; i++)
            {
                var card = FarmUITheme.Image(content, i == 6 ? "Apple tree" : FarmCatalog.CropIds[i], "Skin/slot").rectTransform;
                var view = ui.plantCards[i] = new PlantCard();
                view.portrait = FarmUITheme.Icon(card, i == 6 ? "sapling" : "crop_" + FarmCatalog.CropIds[i]);
                At(view.portrait.rectTransform, 20, 18, 74, 74);
                view.title = FarmUITheme.Label(card, "", font, 28); At(view.title.rectTransform, 110, 18, 250, 38); view.title.alignment = TextAnchor.MiddleLeft;
                view.seed = FarmUITheme.Icon(card, "seeds_carrot"); At(view.seed.rectTransform, 110, 62, 30, 30);
                view.seedLabel = FarmUITheme.Label(card, "", font, 19); At(view.seedLabel.rectTransform, 148, 60, 212, 34); view.seedLabel.alignment = TextAnchor.MiddleLeft;
                view.growth = Row(card, font, "growth", 108); view.price = Row(card, font, "coin", 152); view.yield = Row(card, font, "tool_sickle", 196);
            }
            var animalPage = FarmUITheme.Rect(body, "Animals"); FarmUITheme.Stretch(animalPage); ui.animalsPage = animalPage.gameObject;
            var animalCard = FarmUITheme.Image(animalPage, "Chicken reference", "Skin/slot").rectTransform;
            FarmUITheme.Place(animalCard, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(586, 580));
            var chicken = FarmUITheme.Icon(animalCard, "chicken"); At(chicken.rectTransform, 26, 20, 100, 100);
            ui.animalName = FarmUITheme.Label(animalCard, "Курица", font, 36); At(ui.animalName.rectTransform, 150, 32, 400, 68); ui.animalName.alignment = TextAnchor.MiddleLeft;
            var animalIcons = new[] { "coin", "feed", "eggs_basket", "growth", "sell", "b_coop" };
            ui.animalValues = new Text[6];
            for (var i = 0; i < 6; i++) ui.animalValues[i] = Row(animalCard, font, animalIcons[i], 146 + 65 * i, 530);
            var care = FarmUITheme.Image(animalPage, "Animal care", "Skin/slot").rectTransform;
            FarmUITheme.Place(care, new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(586, 580));
            var careTitle = FarmUITheme.Label(care, "УХОД ЗА ЖИВОТНЫМИ", font, 30); At(careTitle.rectTransform, 24, 24, 538, 52);
            var icons = new[] { "b_coop", "crop_wheat", "egg" };
            var descriptions = new[] { "1. Поселите курицу в загоне\nC — купить на выбранной клетке.", "2. Дайте корм\nE / ЛКМ рядом с курицей: нужна пшеница.", "3. Соберите продукцию\nПосле кормления дождитесь яйца и нажмите E / ЛКМ." };
            for (var i = 0; i < icons.Length; i++)
            {
                var icon = FarmUITheme.Icon(care, icons[i]); At(icon.rectTransform, 28, 116 + i * 140, 64, 64);
                var label = FarmUITheme.Label(care, descriptions[i], font, 25); label.alignment = TextAnchor.MiddleLeft;
                At(label.rectTransform, 114, 98 + i * 140, 444, 112);
            }
            ui.journal = FarmUITheme.Button(ui.panel, "Статистика и награды", font, "journal", "ghost");
            FarmUITheme.Place((RectTransform)ui.journal.transform, Vector2.zero, Vector2.zero, new Vector2(38, 28), new Vector2(510, 66));
            ui.close = FarmUITheme.Button(ui.panel, "Закрыть · Esc", font, "close", "ghost");
            FarmUITheme.Place((RectTransform)ui.close.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-38, 28), new Vector2(320, 66));
            ui.Preview(); ui.Hide(); return ui;
        }
        private static Text Row(Transform parent, Font font, string icon, float y, float width = 330)
        {
            var image = FarmUITheme.Icon(parent, icon); At(image.rectTransform, 20, y, 30, 32);
            var label = FarmUITheme.Label(parent, "", font, 23); label.alignment = TextAnchor.MiddleLeft;
            At(label.rectTransform, 62, y - 2, width - 42, 38); return label;
        }
        private static void At(RectTransform rect, float x, float y, float width, float height)
        { FarmUITheme.Place(rect, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y), new Vector2(width, height)); }
    }
}
