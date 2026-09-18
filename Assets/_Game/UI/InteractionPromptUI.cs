using MyLittleFarm.Gameplay.World;
using MyLittleFarm.Gameplay.Building;
using UnityEngine;
using UnityEngine.UI;

namespace MyLittleFarm.UI
{
    /// <summary>Показывает контекстную подсказку обычного взаимодействия или режима строительства.</summary>
    public sealed class InteractionPromptUI : MonoBehaviour
    {
        // Источники формируют текст, promptText является единственным отображаемым компонентом.
        private InteractionSystem _interaction;
        private Text _promptText;
        private BuildSystem _buildings;

        public void Configure(InteractionSystem interaction, Text promptText, BuildSystem buildings = null)
        {
            // BuildingSystem необязателен, чтобы компонент можно было использовать без строительства.
            _interaction = interaction;
            _promptText = promptText;
            _buildings = buildings;
        }

        private void Update()
        {
            // Строительная подсказка имеет приоритет; текст назначается только при фактическом изменении.
            if (_interaction != null && _promptText != null)
            {
                var prompt = _buildings != null && _buildings.IsActive ? _buildings.CurrentPrompt : _interaction.CurrentPrompt;
                if (_promptText.text != prompt) _promptText.text = prompt;
            }
        }
    }
}
