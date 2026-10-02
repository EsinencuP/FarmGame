using MyLittleFarm.Core;
using UnityEngine;

namespace MyLittleFarm.Gameplay.World
{
    /// <summary>Проводит игрока через короткий сценарий дом → сундук → ферма → рынок.</summary>
    [DefaultExecutionOrder(-40)]
    public sealed class OnboardingSystem : MonoBehaviour
    {
        [Header("Onboarding")]
        [SerializeField] private bool enabledInNewGame = true;
        [SerializeField] private string[] stepHints =
        {
            "Постройте дом (B → 1 → ЛКМ)",
            "Постройте склад-сундук (B → 2 → ЛКМ)",
            "Обработайте клетку и посадите семя",
            "Соберите созревший урожай",
            "Продайте урожай у ящика",
            "Купите первую новую пачку семян у рынка",
            "Обучение завершено — ферма готова к развитию"
        };

        // Номер шага сохраняется в SaveData, поэтому подсказка не возвращается после загрузки.
        private int _step;
        public int Step => _step;
        public string CurrentHint => stepHints != null && _step >= 0 && _step < stepHints.Length
            ? stepHints[_step] : string.Empty;

        /// <summary>Подписывается на завершённые действия игрока.</summary>
        private void OnEnable()
        {
            GameEvents.ProgressAction += HandleProgress;
        }

        /// <summary>Публикует стартовую подсказку после подключения интерфейса.</summary>
        private void Start()
        {
            if (enabledInNewGame) PublishHint();
        }

        /// <summary>Возвращает номер следующего действия для SaveSystem.</summary>
        public int Capture() => _step;

        /// <summary>Восстанавливает ограниченный номер шага из проверенного снимка.</summary>
        public void Restore(int step)
        {
            _step = Mathf.Clamp(step, 0, Mathf.Max(0, (stepHints?.Length ?? 7) - 1));
            PublishHint();
        }

        /// <summary>Продвигает обучение только при ожидаемом действии, пропуская дубликаты.</summary>
        private void HandleProgress(string action)
        {
            if (!enabledInNewGame || _step >= 6) return;
            var expected = _step == 0 ? "house" : _step == 1 ? "chest" : _step == 2 ? "plant"
                : _step == 3 ? "harvest" : _step == 4 ? "sale" : "purchase";
            if (action != expected) return;
            _step++;
            PublishHint();
        }

        /// <summary>Показывает текущую подсказку через общий HUD без прямой ссылки на UI.</summary>
        private void PublishHint()
        {
            if (enabledInNewGame && !string.IsNullOrWhiteSpace(CurrentHint))
                GameEvents.RaiseStatusChanged(CurrentHint);
        }

        /// <summary>Убирает подписку при отключении или выгрузке сцены.</summary>
        private void OnDisable() => GameEvents.ProgressAction -= HandleProgress;
    }
}
