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
            ActionFeedback = null;
            ProgressAction = null;
        }
        public static event Action InventoryChanged;
        public static event Action<int> MoneyChanged;
        public static event Action<string> StatusChanged;
        // action — тип эффекта, позиция указывает место его появления в мире.
        public static event Action<string, UnityEngine.Vector3> ActionFeedback;
        // ProgressAction используется онбордингом и не зависит от визуальных эффектов.
        public static event Action<string> ProgressAction;

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

        /// <summary>Просит систему эффектов показать короткий маркер игрового действия.</summary>
        public static void RaiseActionFeedback(string action, UnityEngine.Vector3 worldPosition)
        {
            ActionFeedback?.Invoke(action, worldPosition);
        }

        /// <summary>Сообщает прогресс сценария обучения без жёсткой связи с игровыми системами.</summary>
        public static void RaiseProgressAction(string action)
        {
            ProgressAction?.Invoke(action);
        }
    }
}
