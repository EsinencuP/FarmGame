using System;

namespace MyLittleFarm.Core
{
    /// <summary>
    /// Общая шина простых событий интерфейса. Игровые системы сообщают об изменениях,
    /// не сохраняя прямых ссылок на конкретные UI-компоненты.
    /// </summary>
    public static class GameEvents
    {
        // События уведомляют HUD об инвентаре, деньгах и коротких статусных сообщениях.
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            // Очищает статические подписки при новом запуске, включая режим без перезагрузки домена.
            InventoryChanged = null;
            MoneyChanged = null;
            StatusChanged = null;
        }
        public static event Action InventoryChanged;
        public static event Action<int> MoneyChanged;
        public static event Action<string> StatusChanged;

        public static void RaiseInventoryChanged()
        {
            // Безопасный вызов: если подписчиков нет, ничего не происходит.
            InventoryChanged?.Invoke();
        }

        public static void RaiseMoneyChanged(int coins)
        {
            // Передаёт подписчикам уже рассчитанный остаток монет.
            MoneyChanged?.Invoke(coins);
        }

        public static void RaiseStatusChanged(string message)
        {
            // Передаёт текст временного уведомления для HUD.
            StatusChanged?.Invoke(message);
        }
    }
}
