using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.Farming;
using MyLittleFarm.Gameplay.Animals;
using MyLittleFarm.Gameplay.World;
using UnityEngine;
using UnityEngine.UI;

namespace MyLittleFarm.UI
{
    /// <summary>Синхронизирует показатели экономики и временные сообщения с элементами HUD.</summary>
    public sealed class HUDController : MonoBehaviour
    {
        [Header("Status Message")]
        [Tooltip("Сколько секунд временное сообщение остаётся на экране.")]
        [SerializeField, Min(0.1f)] private float statusDuration = 3.5f;
        [Header("Placeholder Icons")]
        [Tooltip("Необязательная картинка монет вместо цветного прямоугольника.")]
        [SerializeField] private Sprite coinSprite;
        [Tooltip("Необязательная картинка инструмента вместо цветного прямоугольника.")]
        [SerializeField] private Sprite toolSprite;
        [SerializeField, Range(0.1f, 1f)] private float inactiveSlotOpacity = 0.55f;

        // Источники данных и текстовые компоненты назначаются bootstrap-скриптом.
        private InventorySystem _inventory;
        private WalletSystem _wallet;
        private FarmCatalog _catalog;
        private QuickSlotSystem _slots;
        private SectorSystem _sector;
        private ToolUpgradeSystem _upgrades;
        private SeedShopSystem _shop;
        private AnimalSystem _animals;
        private OnboardingSystem _onboarding;
        private Image[] _seedIcons;
        private Image[] _harvestIcons;
        private Image _coinIcon;
        private Image _toolIcon;
        private Text _statsText;
        private Text _statusText;
        private Text _onboardingText;
        private int _shownOnboardingStep = -1;
        // Момент скрытия временного сообщения по независимой от паузы шкале времени.
        private float _hideStatusAt;

        /// <summary>Назначает зависимости, подписывается на события и сразу заполняет интерфейс.</summary>
        public void Configure(InventorySystem inventory, WalletSystem wallet, Text statsText, Text statusText,
            FarmCatalog catalog = null, QuickSlotSystem slots = null, SectorSystem sector = null,
            ToolUpgradeSystem upgrades = null, SeedShopSystem shop = null,
            Image[] seedIcons = null, Image[] harvestIcons = null, Image coinIcon = null, Image toolIcon = null,
            AnimalSystem animals = null,
            OnboardingSystem onboarding = null, Text onboardingText = null)
        {
            Unsubscribe();
            _inventory = inventory;
            _wallet = wallet;
            _catalog = catalog;
            _slots = slots;
            _sector = sector;
            _upgrades = upgrades;
            _shop = shop;
            _animals = animals;
            _onboarding = onboarding;
            _onboardingText = onboardingText;
            _shownOnboardingStep = -1;
            _seedIcons = seedIcons;
            _harvestIcons = harvestIcons;
            _coinIcon = coinIcon;
            _toolIcon = toolIcon;
            if (_coinIcon != null)
            {
                _coinIcon.sprite = coinSprite;
                if (coinSprite != null) _coinIcon.color = Color.white;
            }
            if (_toolIcon != null)
            {
                _toolIcon.sprite = toolSprite;
                if (toolSprite != null) _toolIcon.color = Color.white;
            }
            _statsText = statsText;
            _statusText = statusText;
            GameEvents.InventoryChanged += Refresh;
            GameEvents.MoneyChanged += HandleMoneyChanged;
            GameEvents.StatusChanged += ShowStatus;
            Refresh();
            RefreshOnboarding();
        }

        private void Update()
        {
            // По окончании таймера скрывается только статус, постоянная статистика остаётся видимой.
            if (_statusText != null && _statusText.enabled && Time.unscaledTime >= _hideStatusAt)
            {
                _statusText.enabled = false;
            }
            if (_onboarding != null && _shownOnboardingStep != _onboarding.Step) RefreshOnboarding();
        }

        /// <summary>Держит текущую цель обучения на экране до завершения действия.</summary>
        private void RefreshOnboarding()
        {
            if (_onboardingText == null || _onboarding == null) return;
            _shownOnboardingStep = _onboarding.Step;
            _onboardingText.text = _onboarding.CurrentHint;
        }

        private void Refresh()
        {
            // Показывает деньги, содержимое инвентаря и активный быстрый слот.
            if (_statsText == null || _inventory == null || _wallet == null)
            {
                return;
            }

            if (_catalog == null)
            {
                _statsText.text = $"МОНЕТЫ  {_wallet.Coins}\nСЕМЕНА  {_inventory.GetAmount(InventorySystem.CarrotSeedId)}\nМОРКОВЬ  {_inventory.GetAmount(InventorySystem.CarrotId)}";
                return;
            }
            var lines = $"МОНЕТЫ  {_wallet.Coins}\n";
            for (var i = 0; i < _catalog.Crops.Count; i++)
            {
                var crop = _catalog.Crops[i];
                var marker = _slots != null && _slots.SelectedIndex == i ? ">" : " ";
                lines += $"{marker}{i + 1} {crop.DisplayName}: семена {_inventory.GetAmount(crop.SeedItemId)}, урожай {_inventory.GetAmount(crop.HarvestItemId)}\n";
            }
            lines += _sector == null ? string.Empty : _sector.IsUnlocked
                ? "СЕКТОР: открыт\n" : $"СЕКТОР: L / {_sector.PurchasePrice} монет\n";
            lines += _upgrades == null ? string.Empty : _upgrades.Level >= 2
                ? "ИНСТРУМЕНТ: уровень 2\n" : $"ИНСТРУМЕНТ: уровень {_upgrades.Level}, U / {_upgrades.NextPrice} монет\n";
            var selected = _slots?.SelectedCrop;
            var shopHint = selected != null && _catalog.TryGetItem(selected.SeedItemId, out var seed)
                ? $"P у рынка — {selected.DisplayName}: {_shop?.PurchaseAmount ?? 4} семени × {seed.BuyPrice}"
                : "P у рынка — купить выбранные семена";
            lines += $"ЯБЛОКИ  {_inventory.GetAmount("apple")}   ЯЙЦА  {_inventory.GetAmount("egg")}\n";
            if (_animals != null) lines += $"КУРЫ  {_animals.Count}/{_animals.MaxAnimals}\n";
            _statsText.text = lines + shopHint;
            RefreshIcons();
        }

        /// <summary>Показывает Sprite предмета или цветной плейсхолдер и выделяет активный слот.</summary>
        private void RefreshIcons()
        {
            if (_catalog == null) return;
            for (var index = 0; index < _catalog.Crops.Count; index++)
            {
                var crop = _catalog.Crops[index];
                if (_seedIcons != null && index < _seedIcons.Length && _seedIcons[index] != null)
                {
                    var icon = _seedIcons[index];
                    icon.sprite = _catalog.TryGetItem(crop.SeedItemId, out var seed) ? seed.Icon : null;
                    var color = icon.sprite == null ? crop.MatureColor : Color.white;
                    color.a = _slots != null && _slots.SelectedIndex == index ? 1f : inactiveSlotOpacity;
                    icon.color = color;
                }
                if (_harvestIcons != null && index < _harvestIcons.Length && _harvestIcons[index] != null)
                {
                    var icon = _harvestIcons[index];
                    icon.sprite = _catalog.TryGetItem(crop.HarvestItemId, out var harvest) ? harvest.Icon : null;
                    icon.color = icon.sprite == null ? crop.MatureColor : Color.white;
                }
            }
        }

        private void HandleMoneyChanged(int ignored)
        {
            // Новый баланс уже доступен через WalletSystem, поэтому аргумент события не требуется.
            Refresh();
        }

        private void ShowStatus(string message)
        {
            // Каждое новое сообщение заново запускает таймер отображения.
            if (_statusText == null)
            {
                return;
            }

            _statusText.text = message;
            _statusText.enabled = true;
            _hideStatusAt = Time.unscaledTime + statusDuration;
        }

        private void OnDestroy()
        {
            // Удаление объекта должно снять статические подписки и не оставлять ссылок на уничтоженный HUD.
            Unsubscribe();
        }

        private void Unsubscribe()
        {
            // Метод безопасен при повторном вызове и также используется перед новой конфигурацией.
            GameEvents.InventoryChanged -= Refresh;
            GameEvents.MoneyChanged -= HandleMoneyChanged;
            GameEvents.StatusChanged -= ShowStatus;
        }
    }
}
