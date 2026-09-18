using System;

namespace MyLittleFarm.Core
{
    public static class GameEvents
    {
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            InventoryChanged = null;
            MoneyChanged = null;
            StatusChanged = null;
        }
        public static event Action InventoryChanged;
        public static event Action<int> MoneyChanged;
        public static event Action<string> StatusChanged;

        public static void RaiseInventoryChanged()
        {
            InventoryChanged?.Invoke();
        }

        public static void RaiseMoneyChanged(int coins)
        {
            MoneyChanged?.Invoke(coins);
        }

        public static void RaiseStatusChanged(string message)
        {
            StatusChanged?.Invoke(message);
        }
    }
}
