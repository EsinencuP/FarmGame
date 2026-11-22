using System;
using System.Collections.Generic;
using System.Text;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.Gameplay.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace MyLittleFarm.UI
{
    internal sealed class BuildCatalogueScreen : IDisposable
    {
        private const int CardCount = 9;
        private static readonly Vector2 DesignSize = new(1280, 760);
        private readonly RectTransform _host;
        private readonly BuildSystem _buildings;
        private readonly BuildingCataloguePreviews _previews;
        private readonly Action<int> _onPlace;
        private readonly List<int> _buildingIndices = new();
        private readonly List<int> _decorationIndices = new();
        private readonly Button[] _cards = new Button[CardCount];
        private readonly RawImage[] _thumbnails = new RawImage[CardCount];
        private readonly Image[] _icons = new Image[CardCount];
        private readonly Text[] _cardTitles = new Text[CardCount];
        private readonly Text[] _cardPrices = new Text[CardCount];
        private readonly Button _buildingsTab, _decorTab, _previous, _next, _place;
        private readonly Text _pageNumber, _title, _facts, _description, _recipes, _balance, _status;
        private readonly RawImage _picture;
        private readonly Image _pictureIcon;
        private bool _decorations;
        private int _page, _choice, _nextPreview;
        private List<int> VisibleIndices => _decorations ? _decorationIndices : _buildingIndices;
        private int PageStart => _page * CardCount;
        private BuildingDefinition Selected => BuildingDefinition.Catalog[VisibleIndices[_choice]];
        public RectTransform Panel { get; }
        public Button InitialFocus => _cards[_choice - PageStart];

        public BuildCatalogueScreen(RectTransform host, Font font, BuildSystem buildings, Action<int> onPlace,
            Action close, Action move, Action remove, Action garden)
        {
            _host = host;
            _buildings = buildings;
            _onPlace = onPlace;
            _previews = new BuildingCataloguePreviews(buildings);
            for (var i = 0; i < BuildingDefinition.Catalog.Count; i++)
            {
                var definition = BuildingDefinition.Catalog[i];
                (definition.ConstructionSeconds > 0 ? _buildingIndices : _decorationIndices).Add(i);
            }

            Panel = FarmUITheme.Panel(host, "Строительство", font, DesignSize);
            _buildingsTab = AddButton(Panel, font, "Здания", 32, 61, 170, 46, () => ShowCategory(false));
            _decorTab = AddButton(Panel, font, "Декор", 214, 61, 170, 46, () => ShowCategory(true));
            AddButton(Panel, font, "Закрыть · Esc", 1054, 61, 194, 46, close);
            var coin = FarmUITheme.Icon(Panel, "coin");
            At(coin.rectTransform, 878, 66, 34, 34);
            _balance = Label(Panel, font, 23, 921, 61, 118, 46);
            var selection = FarmUITheme.Image(Panel, "Building details", "Skin/slot");
            At(selection.rectTransform, 706, 125, 542, 508);
            var showcase = FarmUITheme.Image(selection.transform, "Model background", "Skin/slot");
            At(showcase.rectTransform, 18, 16, 506, 206);
            showcase.color = new Color(.85f, .76f, .61f, .42f);
            (_picture, _pictureIcon) = Preview(showcase.transform, 82, 1, 342, 204);
            _title = Label(selection.transform, font, 34, 22, 231, 498, 42);
            _facts = Label(selection.transform, font, 21, 22, 278, 498, 35);
            _facts.color = FarmUITheme.Muted;
            _description = Label(selection.transform, font, 23, 22, 324, 498, 53, TextAnchor.UpperLeft);
            _recipes = Label(selection.transform, font, 22, 22, 390, 498, 105, TextAnchor.UpperLeft);
            _recipes.color = new Color32(81, 100, 52, 255);

            for (var i = 0; i < CardCount; i++)
            {
                var slot = i;
                var card = AddButton(Panel, font, "", 32 + i % 3 * 220, 125 + i / 3 * 172, 208, 162,
                    () => Select(PageStart + slot));
                _cards[i] = card;
                card.GetComponentInChildren<Text>().enabled = false;
                (_thumbnails[i], _icons[i]) = Preview(card.transform, 25, 3, 158, 98);
                _cardTitles[i] = Label(card.transform, font, 20, 7, 100, 194, 35, TextAnchor.MiddleCenter);
                _cardPrices[i] = Label(card.transform, font, 17, 7, 134, 194, 23, TextAnchor.MiddleCenter);
            }

            _previous = AddButton(Panel, font, "‹", 225, 641, 48, 36, () => ShowPage(_page - 1));
            _pageNumber = Label(Panel, font, 18, 280, 641, 148, 36, TextAnchor.MiddleCenter);
            _next = AddButton(Panel, font, "›", 435, 641, 48, 36, () => ShowPage(_page + 1));
            AddButton(Panel, font, "Перенести", 32, 691, 181, 44, move);
            AddButton(Panel, font, "Снести", 225, 691, 154, 44, remove);
            if (garden != null) AddButton(Panel, font, "Сделать грядку", 391, 691, 241, 44, garden);
            _status = Label(Panel, font, 21, 706, 640, 542, 41);
            _status.color = new Color32(157, 58, 40, 255);
            _place = AddButton(Panel, font, "Разместить", 706, 687, 542, 52, Place, "secondary");
        }

        public void Show()
        {
            Resize();
            ShowPage(_page);
        }

        public void Resize()
        {
            var area = _host.rect.size - new Vector2(44, 44);
            var scale = Mathf.Clamp(Mathf.Min(area.x / DesignSize.x, area.y / DesignSize.y), .05f, 1);
            Panel.localScale = Vector3.one * scale;
        }

        public void RefreshCoins()
        {
            var coins = _buildings.AvailableCoins;
            var shortfall = Selected.Price - coins;
            _balance.text = coins.ToString();
            _status.text = shortfall > 0 ? FarmText.MissingCoins(shortfall) : string.Empty;
            _place.interactable = shortfall <= 0;
        }

        public void RenderNextPreview()
        {
            if (_nextPreview >= CardCount || PageStart + _nextPreview >= VisibleIndices.Count) return;
            var slot = _nextPreview++;
            var index = VisibleIndices[PageStart + slot];
            var definition = BuildingDefinition.Catalog[index];
            var texture = _previews.Render(definition);
            ApplyPreview(_thumbnails[slot], _icons[slot], definition, texture);
            if (Selected.Id == definition.Id) ApplyPreview(_picture, _pictureIcon, definition, texture);
        }

        private void ShowCategory(bool decorations)
        {
            _decorations = decorations;
            ShowPage(0);
            FarmUITheme.Focus(InitialFocus);
        }

        private void ShowPage(int page)
        {
            var pages = (VisibleIndices.Count + CardCount - 1) / CardCount;
            _page = Mathf.Clamp(page, 0, pages - 1);
            _nextPreview = 0;
            FarmUITheme.Style(_buildingsTab, _decorations ? "ghost" : "secondary");
            FarmUITheme.Style(_decorTab, _decorations ? "secondary" : "ghost");
            for (var i = 0; i < CardCount; i++)
            {
                var index = PageStart + i;
                _cards[i].gameObject.SetActive(index < VisibleIndices.Count);
                if (index >= VisibleIndices.Count) continue;
                var definition = BuildingDefinition.Catalog[VisibleIndices[index]];
                _cardTitles[i].text = definition.Name;
                _cardPrices[i].text = FarmText.Coins(definition.Price);
                ApplyPreview(_thumbnails[i], _icons[i], definition, _previews.Get(definition));
            }
            _previous.gameObject.SetActive(pages > 1);
            _next.gameObject.SetActive(pages > 1);
            _pageNumber.gameObject.SetActive(pages > 1);
            _previous.interactable = _page > 0;
            _next.interactable = _page + 1 < pages;
            _pageNumber.text = $"{_page + 1} / {pages}";
            Select(PageStart);
        }

        private void Select(int index)
        {
            if (index < 0 || index >= VisibleIndices.Count) return;
            _choice = index;
            var definition = Selected;
            for (var i = 0; i < CardCount; i++)
            {
                var selected = PageStart + i == _choice;
                FarmUITheme.Style(_cards[i], selected ? "secondary" : "ghost");
                _cardTitles[i].color = selected ? Color.white : FarmUITheme.Ink;
                _cardPrices[i].color = selected ? FarmUITheme.Cream : FarmUITheme.Muted;
            }
            _title.text = definition.Name;
            var time = definition.ConstructionSeconds == 0 ? "сразу" : FarmText.Duration(definition.ConstructionSeconds);
            _facts.text = $"{FarmText.Coins(definition.Price)}   ·   {definition.Width} × {definition.Depth} кл.   ·   Стройка {time}";
            _description.text = Description(definition.Id);
            _recipes.text = Recipes(definition.Id);
            ApplyPreview(_picture, _pictureIcon, definition, _previews.Get(definition));
            RefreshCoins();
        }

        private void Place()
        {
            RefreshCoins();
            if (_place.interactable) _onPlace(VisibleIndices[_choice]);
        }

        private static string Description(string id) => id switch
        {
            "house" => "Жилой дом. Его внешний вид можно улучшить через рынок.",
            "storage" => "Хранит семена, урожай и продукты отдельно от инвентаря.",
            "market" => "Дополнительная торговая точка на ферме. Здесь можно покупать и продавать товары.",
            "coop" => "Курятник для фермы. Куры дают яйца после кормления.",
            "barn" => $"Коровник с загоном на {BarnHabitat.Capacity} коровы. После кормления коровы дают молоко.",
            "feeder" => $"Кормит кур и коров в радиусе {FeederStore.Radius:g} м. Вмещает {FeederStore.Capacity} пшеницы.",
            "mill" => "Перемалывает зерно в муку. Готовую партию нужно забрать вручную.",
            "apple_press" => "Выжимает сок из яблок. Готовую партию нужно забрать вручную.",
            "cheese_dairy" => "Перерабатывает молоко в сыр. Готовую партию нужно забрать вручную.",
            _ => FarmDecorationCatalog.SupportsText(id) ? "Вывеска с текстом, который можно написать после размещения."
                : "Украшение для фермы."
        };

        private static string Recipes(string id)
        {
            var lines = new StringBuilder();
            foreach (var recipe in ProductionRecipe.All)
            {
                if (recipe.BuildingId != id) continue;
                if (lines.Length == 0) lines.AppendLine("Производство за партию");
                lines.AppendLine($"{recipe.InputAmount} × {recipe.InputName} → 1 × {recipe.OutputName} · {FarmText.Duration(recipe.Seconds)}");
            }
            return lines.ToString().TrimEnd();
        }

        private static (RawImage Image, Image Icon) Preview(Transform parent, int x, int y, int width, int height)
        {
            var preview = FarmUITheme.Rect(parent, "Building preview").gameObject.AddComponent<RawImage>();
            At(preview.rectTransform, x, y, width, height);
            preview.raycastTarget = false;
            var icon = FarmUITheme.Icon(parent, "catalog");
            At(icon.rectTransform, x, y, width, height);
            return (preview, icon);
        }

        private static void ApplyPreview(RawImage image, Image icon, BuildingDefinition definition, Texture texture)
        {
            image.texture = texture;
            image.enabled = texture != null;
            icon.enabled = texture == null;
            if (icon.enabled) icon.sprite = FarmUITheme.Sprite("Icons/b_" + definition.Id) ?? FarmUITheme.Sprite("Icons/catalog");
        }

        private static Button AddButton(Transform parent, Font font, string title, int x, int y, int w, int h,
            Action action, string style = "ghost")
        {
            var button = FarmUITheme.Button(parent, title, font, kind: style);
            At((RectTransform)button.transform, x, y, w, h);
            FarmUITheme.FitCompactButton(button, 22);
            button.onClick.AddListener(() => action());
            return button;
        }

        private static Text Label(Transform parent, Font font, int size, int x, int y, int w, int h,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var label = FarmUITheme.Label(parent, "", font, size);
            label.alignment = alignment;
            At(label.rectTransform, x, y, w, h);
            return label;
        }

        private static void At(RectTransform rect, int x, int y, int width, int height)
        {
            FarmUITheme.Place(rect, Vector2.up, Vector2.up, new Vector2(x, -y), new Vector2(width, height));
        }

        public void Dispose() => _previews.Dispose();
    }
}
