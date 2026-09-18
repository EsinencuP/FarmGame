using System;
using MyLittleFarm.Core;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Economy
{
    /// <summary>Хранит баланс монет и выполняет безопасные начисления и списания.</summary>
    public sealed class WalletSystem : MonoBehaviour
    {
        // Изменение доступно только методам кошелька, чтение разрешено всем системам.
        public int Coins { get; private set; }

        /// <summary>Устанавливает стартовый баланс новой игры.</summary>
        public void Configure(int startingCoins)
        {
            SetCoins(startingCoins);
        }

        public void AddCoins(int amount)
        {
            // Отрицательное начисление запрещено, переполнение вызывает исключение без смены баланса.
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            SetCoins(checked(Coins + amount));
        }

        public bool TrySpend(int amount)
        {
            // Неудачная покупка является безопасной: баланс остаётся прежним.
            if (amount < 0 || Coins < amount)
            {
                return false;
            }

            SetCoins(Coins - amount);
            return true;
        }

        public void SetCoins(int amount)
        {
            // Используется загрузкой и настройкой; отрицательные значения приводятся к нулю.
            Coins = Mathf.Max(0, amount);
            GameEvents.RaiseMoneyChanged(Coins);
        }
    }
}
