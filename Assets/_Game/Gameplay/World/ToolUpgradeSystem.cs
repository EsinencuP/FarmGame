using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Economy;
using UnityEngine;

namespace MyLittleFarm.Gameplay.World
{
    /// <summary>Два улучшения инструмента увеличивают доступный радиус работы с грядками.</summary>
    [DefaultExecutionOrder(-85)]
    public sealed class ToolUpgradeSystem : MonoBehaviour
    {
        [SerializeField, Min(0)] private int firstUpgradePrice = 15;
        [SerializeField, Min(0)] private int secondUpgradePrice = 35;
        [SerializeField, Min(0.5f)] private float baseReach = 2.2f;
        [SerializeField, Min(0.5f)] private float firstReach = 3.2f;
        [SerializeField, Min(0.5f)] private float secondReach = 4.5f;

        // Уровень 0 является базовым, уровни 1 и 2 покупаются последовательно.
        private InputReader _input;
        private WalletSystem _wallet;
        public int Level { get; private set; }
        public float Reach => Level == 0 ? baseReach : Level == 1 ? firstReach : secondReach;
        public int NextPrice => Level == 0 ? firstUpgradePrice : Level == 1 ? secondUpgradePrice : 0;

        private void OnValidate()
        {
            // Настройки Inspector сохраняют последовательное увеличение дальности.
            firstReach = Mathf.Max(baseReach, firstReach);
            secondReach = Mathf.Max(firstReach, secondReach);
        }

        /// <summary>Подключает кошелёк и ввод для покупки на клавишу U.</summary>
        public void Configure(InputReader input, WalletSystem wallet)
        {
            _input = input;
            _wallet = wallet;
            Level = 0;
        }

        private void Update()
        {
            if (_input != null && _input.UpgradePressed && !_input.BuildModeActive
                && !_input.SuppressGameplayThisFrame) TryUpgrade();
        }

        /// <summary>Покупает только следующий уровень и сообщает о нехватке средств.</summary>
        public bool TryUpgrade()
        {
            if (Level >= 2) return false;
            var price = NextPrice;
            if (!_wallet.TrySpend(price))
            {
                GameEvents.RaiseStatusChanged("Недостаточно монет для улучшения");
                return false;
            }
            Level++;
            GameEvents.RaiseInventoryChanged();
            GameEvents.RaiseStatusChanged($"Инструмент улучшен до уровня {Level}");
            return true;
        }

        /// <summary>Восстанавливает проверенный уровень из сохранения.</summary>
        public void Restore(int level)
        {
            Level = Mathf.Clamp(level, 0, 2);
            GameEvents.RaiseInventoryChanged();
        }
    }
}
