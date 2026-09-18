using System;
using MyLittleFarm.Core;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Economy
{
    public sealed class WalletSystem : MonoBehaviour
    {
        public int Coins { get; private set; }

        public void Configure(int startingCoins)
        {
            SetCoins(startingCoins);
        }

        public void AddCoins(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            SetCoins(checked(Coins + amount));
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0 || Coins < amount)
            {
                return false;
            }

            SetCoins(Coins - amount);
            return true;
        }

        public void SetCoins(int amount)
        {
            Coins = Mathf.Max(0, amount);
            GameEvents.RaiseMoneyChanged(Coins);
        }
    }
}
