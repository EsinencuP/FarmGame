using MyLittleFarm.Core;
using System.Text;
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
        [SerializeField] private bool compactPresentation;

        public void UseCompactPresentation() { compactPresentation = true; Refresh(); RefreshOnboarding(); }

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
        // Момент скрытия временного сообщения по независимой от паузы шкале времени.
        private float _hideStatusAt;
        private bool _refreshPending;
        private readonly StringBuilder _statsBuilder = new StringBuilder(512);

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
            if (isActiveAndEnabled) Subscribe();
            Refresh();
            RefreshOnboarding();
        }

        private void LateUpdate()
        {
            if (_refreshPending) Refresh();
            // По окончании таймера скрывается только статус, постоянная статистика остаётся видимой.
            if (_statusText != null && _statusText.enabled && Time.unscaledTime >= _hideStatusAt)
            {
                _statusText.enabled = false;
            }
        }

        private void OnEnable()
        {
            Subscribe();
            Refresh();
            RefreshOnboarding();
        }

        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            Unsubscribe();
            GameEvents.InventoryChanged += RequestRefresh;
            GameEvents.MoneyChanged += HandleMoneyChanged;
            GameEvents.StatusChanged += ShowStatus;
            GameEvents.OnboardingChanged += HandleOnboardingChanged;
        }

        private void RequestRefresh() => _refreshPending = true;
        private void HandleOnboardingChanged(int ignored) => RefreshOnboarding();

        /// <summary>Держит текущую цель обучения на экране до завершения действия.</summary>
        private void RefreshOnboarding()
        {
            if (compactPresentation) { if (_onboardingText != null) _onboardingText.gameObject.SetActive(false); return; }
            if (_onboardingText == null || _onboarding == null) return;
            if (_onboarding.UsesOverlay) return;
            _onboardingText.text = _onboarding.CurrentHint;
            _onboardingText.enabled = _onboarding.IsActive;
        }

        private void Refresh()
        {
            _refreshPending = false;
            if (compactPresentation)
            {
                if (_statsText != null && _wallet != null) _statsText.text = _wallet.Coins.ToString("N0");
                return; // The hotbar owns its item sprites/counts; no large inventory report is built here.
            }
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
            var lines = _statsBuilder;
            lines.Clear();
            lines.Append("<b>МОНЕТЫ  ").Append(_wallet.Coins).Append("</b>\nСЕМЕНА / УРОЖАЙ\n");
            for (var i = 0; i < _catalog.Crops.Count; i++)
            {
                var crop = _catalog.Crops[i];
                var active = _slots != null && _slots.SelectedIndex == i;
                if (active) lines.Append("<color=#3E8A27><b>");
                lines.Append(i + 1).Append("  ").Append(crop.DisplayName).Append("  ")
                    .Append(_inventory.GetAmount(crop.SeedItemId)).Append(" / ")
                    .Append(_inventory.GetAmount(crop.HarvestItemId));
                if (active) lines.Append("</b></color>");
                lines.Append('\n');
            }
            lines.Append(_sector == null ? string.Empty : _sector.IsUnlocked
                ? "СЕКТОР: открыт\n" : $"СЕКТОР  L: {_sector.PurchasePrice}\n");
            lines.Append(_upgrades == null ? string.Empty : _upgrades.Level >= 2
                ? "ИНСТРУМЕНТ: ур. 2\n" : $"ИНСТРУМЕНТ  U: {_upgrades.NextPrice}\n");
            var selected = _slots?.SelectedCrop;
            var shopHint = selected != null && _catalog.TryGetItem(selected.SeedItemId, out var seed)
                ? selected.RareSeeds ? "Редкие семена: тайники · J — журнал" : $"P: {_shop?.PurchaseAmount ?? 4} сем. × {seed.BuyPrice}"
                : "P: купить семена";
            lines.Append("ЯБЛОКИ ").Append(_inventory.GetAmount("apple"))
                .Append("   ЯЙЦА ").Append(_inventory.GetAmount("egg"));
            if (_animals != null) lines.Append("   КУРЫ ").Append(_animals.Count).Append('/').Append(_animals.MaxAnimals);
            lines.Append('\n').Append(shopHint);
            _statsText.text = lines.ToString();
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
                    if (icon.sprite == null) icon.sprite = FarmUITheme.Sprite("Icons/" + FarmUITheme.ItemIcon(crop.SeedItemId));
                    var color = icon.sprite == null ? crop.MatureColor : Color.white;
                    color.a = _slots != null && _slots.SelectedIndex == index ? 1f : inactiveSlotOpacity;
                    icon.color = color;
                }
                if (_harvestIcons != null && index < _harvestIcons.Length && _harvestIcons[index] != null)
                {
                    var icon = _harvestIcons[index];
                    icon.sprite = _catalog.TryGetItem(crop.HarvestItemId, out var harvest) ? harvest.Icon : null;
                    if (icon.sprite == null) icon.sprite = FarmUITheme.Sprite("Icons/" + FarmUITheme.ItemIcon(crop.HarvestItemId));
                    icon.color = icon.sprite == null ? crop.MatureColor : Color.white;
                }
            }
        }

        private void HandleMoneyChanged(int ignored)
        {
            // Новый баланс уже доступен через WalletSystem, поэтому аргумент события не требуется.
            RequestRefresh();
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
            GameEvents.InventoryChanged -= RequestRefresh;
            GameEvents.MoneyChanged -= HandleMoneyChanged;
            GameEvents.StatusChanged -= ShowStatus;
            GameEvents.OnboardingChanged -= HandleOnboardingChanged;
        }
    }
}
