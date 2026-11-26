using System;
using System.Collections.Generic;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Animals;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.Farming;
using MyLittleFarm.Gameplay.World;
using UnityEngine;
using UnityEngine.UI;

namespace MyLittleFarm.UI
{
    internal sealed class FarmDemoPanel
    {
        internal static bool IsAvailable { get; } = Application.isEditor || Debug.isDebugBuild
            || Array.Exists(Environment.GetCommandLineArgs(), arg => arg == "-farm-demo");
        private static readonly Vector2 PanelSize = new Vector2(880, 704);
        private static readonly Vector2 RightCenter = new Vector2(1f, .5f);
        private readonly Font _font;
        private readonly SaveSystem_AI _saves;
        private readonly WalletSystem _wallet;
        private readonly InventorySystem_AI _inventory;
        private readonly FarmCatalog _catalog;
        private readonly DayNightCycle _clock;
        private readonly WeatherController_AI _weather;
        private readonly AnimalSystem_AI _animals;
        private readonly CropSystem_AI _crops;
        private readonly OrchardSystem_AI _orchard;
        private readonly BuildSystem _buildings;
        private readonly ToolUpgradeSystem _tools;
        private readonly SectorSystem _sector;
        private readonly Text _overview;
        private readonly Text _status;

        public RectTransform Panel { get; }
        public Button InitialFocus { get; }

        public FarmDemoPanel(Transform parent, Font font, SaveSystem_AI saves, Action close,
            Action<FarmMenuUI.Page> openScreen, Action openCatalogue, Action<FishPond> startFishing)
        {
            _font = font;
            _saves = saves;
            var farm = saves.GetComponentInParent<GameBootstrap>();
            _wallet = farm.GetComponentInChildren<WalletSystem>();
            _inventory = farm.GetComponentInChildren<InventorySystem_AI>();
            _catalog = farm.GetComponentInChildren<FarmCatalog>();
            _clock = farm.GetComponentInChildren<DayNightCycle>();
            _weather = farm.GetComponentInChildren<WeatherController_AI>();
            _animals = farm.GetComponentInChildren<AnimalSystem_AI>();
            _crops = farm.GetComponentInChildren<CropSystem_AI>();
            _orchard = farm.GetComponentInChildren<OrchardSystem_AI>();
            _buildings = farm.GetComponentInChildren<BuildSystem>();
            _tools = farm.GetComponentInChildren<ToolUpgradeSystem>();
            _sector = farm.GetComponentInChildren<SectorSystem>();
            var pond = farm.GetComponentInChildren<FishPond>(true);

            Panel = FarmUITheme.Panel(parent, "ПАНЕЛЬ АДМИНА", font, PanelSize);
            var closeButton = FarmUITheme.Button(Panel, "Закрыть · F1", font, kind: "ghost");
            FarmUITheme.FitCompactButton(closeButton, 21);
            FarmUITheme.Place((RectTransform)closeButton.transform, Vector2.one, Vector2.one,
                new Vector2(-26, -28), new Vector2(158, 46));
            closeButton.onClick.AddListener(() => close());
            _overview = PlaceLabel(Panel, "", 23, -80);
            var left = CreateColumn("Мир и ожидание", false);
            AddHeading(left, "ПОГОДА И ВРЕМЯ");
            var row = AddRow(left);
            InitialFocus = AddAction(row, "Ясно", () => SetWeather(WeatherState.Sunny), _weather != null);
            AddAction(row, "Облачно", () => SetWeather(WeatherState.Cloudy), _weather != null);
            AddAction(row, "Дождь", () => SetWeather(WeatherState.LightRain), _weather != null);
            row = AddRow(left);
            AddAction(row, "Утро", () => SetHour(6f), _clock != null);
            AddAction(row, "День", () => SetHour(12f), _clock != null);
            AddAction(row, "Закат", () => SetHour(18f), _clock != null);
            AddAction(row, "Ночь", () => SetHour(23f), _clock != null);
            row = AddRow(left);
            AddAction(row, "−1 игровой час", () => SetHour(_clock.Hour - 1f), _clock != null);
            AddAction(row, "+1 игровой час", () => SetHour(_clock.Hour + 1f), _clock != null);

            AddHeading(left, "ПРОПУСТИТЬ ОЖИДАНИЕ");
            row = AddRow(left);
            AddAction(row, "Ускорить созревание", RipenFarm);
            AddAction(row, "Готовые яйца и молоко", FinishAnimalProduction);
            row = AddRow(left);
            AddAction(row, "Завершить партии", FinishBatches);
            AddAction(row, "Достроить здания", FinishConstruction);
            AddAction(AddRow(left), "Следующий день рынка", NextMarketDay);

            var right = CreateColumn("Ресурсы и переходы", true);
            AddHeading(right, "РЕСУРСЫ И РАЗВИТИЕ");
            row = AddRow(right);
            AddAction(row, "+100 монет", () => GrantCoins(100));
            AddAction(row, "+1 000 монет", () => GrantCoins(1000));
            row = AddRow(right);
            AddAction(row, "Семена и саженцы", GrantSeeds);
            AddAction(row, "Урожай и продукты", GrantProducts);
            row = AddRow(right);
            AddAction(row, "Купить курицу", () => BuyAnimal(_animals.AnimalDefinitionId));
            AddAction(row, "Купить корову", () => BuyAnimal("cow"));
            row = AddRow(right);
            AddAction(row, "Улучшить мотыгу", UpgradeHoe);
            AddAction(row, "Купить сектор", BuySector);

            AddHeading(right, "ОТКРЫТЬ ЭКРАН");
            row = AddRow(right);
            AddAction(row, "Инвентарь", () => openScreen(FarmMenuUI.Page.Inventory));
            AddAction(row, "Рынок", () => openScreen(FarmMenuUI.Page.Shop));
            row = AddRow(right);
            AddAction(row, "Строительство", openCatalogue);
            AddAction(row, "Журнал фермы", () => openScreen(FarmMenuUI.Page.Journal));
            row = AddRow(right);
            AddAction(row, "Справочник", () => openScreen(FarmMenuUI.Page.Guide));
            AddAction(row, "Рыбалка", () => startFishing(pond), pond != null);

            var footer = FarmUITheme.Image(Panel, "Результат действия", "Skin/slot").rectTransform;
            footer.anchorMin = Vector2.zero;
            footer.anchorMax = Vector2.right;
            footer.offsetMin = new Vector2(30, 26);
            footer.offsetMax = new Vector2(-30, 82);
            _status = FarmUITheme.Label(footer, "Действия применяются к текущей ферме.", font, 22);
            FarmUITheme.Stretch(_status.rectTransform, 8);
            Panel.gameObject.SetActive(false);
        }

        public void Resize(Vector2 area)
        {
            var scale = Mathf.Clamp(Mathf.Min((area.x - 32) / PanelSize.x, (area.y - 40) / PanelSize.y), .1f, 1f);
            FarmUITheme.Place(Panel, RightCenter, RightCenter, new Vector2(-16, 0), PanelSize);
            Panel.localScale = Vector3.one * scale;
        }

        public void Refresh()
        {
            var time = _clock == null ? "—" : ClockText();
            var weather = _weather == null ? "—" : WeatherName(_weather.TargetState);
            _overview.text = $"{time}   ·   {weather}   ·   {FarmText.Coins(_wallet.Coins)}";
        }

        public void Report(string message)
        {
            _status.text = message;
            Refresh();
        }

        private string ClockText()
        {
            var minutes = Mathf.FloorToInt(_clock.Hour * 60f);
            return $"{minutes / 60:00}:{minutes % 60:00}";
        }

        private static string WeatherName(WeatherState state) => state switch
        {
            WeatherState.Cloudy => "Облачно",
            WeatherState.LightRain => "Дождь",
            _ => "Ясно"
        };

        private void SetWeather(WeatherState state)
        {
            _weather.SetDemoWeather(state);
            Report("Погода изменена · " + WeatherName(state));
        }

        private void SetHour(float hour)
        {
            _clock.SetHour(hour);
            Report("Установлено время " + ClockText());
        }

        private void GrantCoins(int amount)
        {
            var granted = Math.Min(amount, int.MaxValue - _wallet.Coins);
            if (granted > 0) _wallet.AddCoins(granted);
            Report(granted > 0 ? "Начислено " + FarmText.Coins(granted) : "Кошелёк достиг предела");
        }

        private void GrantSeeds()
        {
            var count = 0;
            foreach (var crop in _catalog.Crops) count += Stock(crop.SeedItemId, 20);
            foreach (var id in FarmCatalog.SaplingIds) count += Stock(id, 5);
            ReportStock(count, "Добавлено семян и саженцев");
        }

        private void GrantProducts()
        {
            var count = 0;
            foreach (var crop in _catalog.Crops) count += Stock(crop.CropId, 20);
            foreach (var id in FarmCatalog.FarmProductIds) count += Stock(id, 20);
            ReportStock(count, "Добавлено единиц урожая и продуктов");
        }

        private int Stock(string id, int amount)
        {
            if (!_catalog.TryGetItem(id, out _)) return 0;
            var current = _inventory.GetAmount(id);
            var added = Math.Min(amount, int.MaxValue - current);
            if (added > 0) _inventory.SetAmountSilently(id, current + added);
            return added;
        }

        private void ReportStock(int count, string label)
        {
            if (count > 0) GameEvents.RaiseInventoryChanged();
            Report(count > 0 ? $"{label} · {count}" : "В этих стопках нет свободного места");
        }

        private void BuyAnimal(string species)
        {
            _animals.TryPurchase(new Dictionary<string, int> { [species] = 1 }, Now(), out var message);
            Report(message);
        }

        private void UpgradeHoe()
        {
            if (_tools.Level >= 2) Report("Мотыга уже улучшена до 3×3");
            else Report(_tools.TryUpgrade() ? "Мотыга улучшена · " + _tools.AreaLabel : "Недостаточно монет");
        }

        private void BuySector()
        {
            if (_sector.AllLocationsOpen) Report("Все локации открыты");
            else if (!_sector.CanPurchase) Report("Сектор сейчас недоступен");
            else Report(_sector.TryPurchase() ? "Сектор открыт" : "Недостаточно монет");
        }

        private void RipenFarm()
        {
            var now = Now();
            var count = _crops.RipenForDemo(now) + _orchard.RipenForDemo(now);
            Report(count > 0 ? $"Готово к сбору · {count}" : "Нет созревающих посевов или деревьев");
        }

        private void FinishAnimalProduction()
        {
            var count = _animals.FinishProductionForDemo(Now());
            Report(count > 0 ? $"Можно собрать яйца или молоко · {count}" : "Сначала покормите животных");
        }

        private void FinishBatches()
        {
            var count = _buildings.Production?.FinishForDemo(Now()) ?? 0;
            Report(count > 0 ? $"Готовых партий · {count}" : "Нет партий в работе");
        }

        private void FinishConstruction()
        {
            var count = _buildings.FinishConstructionForDemo();
            Report(count > 0 ? $"Построек завершено · {count}" : "Нет площадок, готовых к завершению");
        }

        private void NextMarketDay()
        {
            var orders = _saves.DailyOrders;
            var day = orders.Day;
            orders.Advance(orders.Remaining);
            Report(orders.Day > day ? "Рынок получил новые заказы" : "Достигнут последний день рынка");
        }

        private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        private RectTransform CreateColumn(string name, bool right)
        {
            var column = FarmUITheme.Rect(Panel, name);
            var edge = new Vector2(right ? 1f : 0f, 1f);
            FarmUITheme.Place(column, edge, edge, new Vector2(right ? -32 : 32, -126), new Vector2(396, 494));
            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            return column;
        }

        private void AddHeading(Transform parent, string title)
        {
            var label = FarmUITheme.Label(parent, title, _font, 21);
            label.alignment = TextAnchor.MiddleLeft;
            label.color = FarmUITheme.Muted;
            label.gameObject.AddComponent<LayoutElement>().preferredHeight = 29;
        }

        private static RectTransform AddRow(Transform parent)
        {
            var row = FarmUITheme.Rect(parent, "Действия");
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 51;
            return row;
        }

        private Button AddAction(Transform parent, string title, Action action, bool available = true)
        {
            var button = FarmUITheme.Button(parent, title, _font, kind: "ghost");
            FarmUITheme.FitCompactButton(button, 20);
            button.interactable = available;
            button.onClick.AddListener(() => action());
            return button;
        }

        private Text PlaceLabel(Transform parent, string text, int size, float y)
        {
            var label = FarmUITheme.Label(parent, text, _font, size);
            var rect = label.rectTransform;
            rect.anchorMin = Vector2.up;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(32, y - 36);
            rect.offsetMax = new Vector2(-32, y);
            return label;
        }
    }
}
