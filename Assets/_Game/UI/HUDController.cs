using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace MyLittleFarm.UI
{
    /// <summary>Синхронизирует показатели экономики и временные сообщения с элементами HUD.</summary>
    public sealed class HUDController : MonoBehaviour
    {
        // Источники данных и текстовые компоненты назначаются bootstrap-скриптом.
        private InventorySystem _inventory;
        private WalletSystem _wallet;
        private Text _statsText;
        private Text _statusText;
        // Момент скрытия временного сообщения по независимой от паузы шкале времени.
        private float _hideStatusAt;

        /// <summary>Назначает зависимости, подписывается на события и сразу заполняет интерфейс.</summary>
        public void Configure(InventorySystem inventory, WalletSystem wallet, Text statsText, Text statusText)
        {
            Unsubscribe();
            _inventory = inventory;
            _wallet = wallet;
            _statsText = statsText;
            _statusText = statusText;
            GameEvents.InventoryChanged += Refresh;
            GameEvents.MoneyChanged += HandleMoneyChanged;
            GameEvents.StatusChanged += ShowStatus;
            Refresh();
        }

        private void Update()
        {
            // По окончании таймера скрывается только статус, постоянная статистика остаётся видимой.
            if (_statusText != null && _statusText.enabled && Time.unscaledTime >= _hideStatusAt)
            {
                _statusText.enabled = false;
            }
        }

        private void Refresh()
        {
            // Собирает единый текст из актуального кошелька и двух предметов прототипа.
            if (_statsText == null || _inventory == null || _wallet == null)
            {
                return;
            }

            _statsText.text =
                $"МОНЕТЫ  {_wallet.Coins}\n" +
                $"СЕМЕНА  {_inventory.GetAmount(InventorySystem.CarrotSeedId)}\n" +
                $"МОРКОВЬ  {_inventory.GetAmount(InventorySystem.CarrotId)}";
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
            _hideStatusAt = Time.unscaledTime + 3.5f;
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
