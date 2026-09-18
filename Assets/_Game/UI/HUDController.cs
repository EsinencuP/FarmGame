using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace MyLittleFarm.UI
{
    public sealed class HUDController : MonoBehaviour
    {
        private InventorySystem _inventory;
        private WalletSystem _wallet;
        private Text _statsText;
        private Text _statusText;
        private float _hideStatusAt;

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
            if (_statusText != null && _statusText.enabled && Time.unscaledTime >= _hideStatusAt)
            {
                _statusText.enabled = false;
            }
        }

        private void Refresh()
        {
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
            Refresh();
        }

        private void ShowStatus(string message)
        {
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
            Unsubscribe();
        }

        private void Unsubscribe()
        {
            GameEvents.InventoryChanged -= Refresh;
            GameEvents.MoneyChanged -= HandleMoneyChanged;
            GameEvents.StatusChanged -= ShowStatus;
        }
    }
}
