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
        [SerializeField] private BuildPromptUI buildPrompt;

        public void Configure(InteractionSystem interaction, Text promptText, BuildSystem buildings = null,
            BuildPromptUI constructionPrompt = null)
        {
            // BuildingSystem необязателен, чтобы компонент можно было использовать без строительства.
            _interaction = interaction;
            _promptText = promptText;
            _buildings = buildings;
            if (constructionPrompt != null) buildPrompt = constructionPrompt;
        }

        private void Update()
        {
            // Строительная подсказка имеет приоритет; текст назначается только при фактическом изменении.
            if (_promptText != null)
            {
                var building = _buildings != null && _buildings.IsActive;
                if (buildPrompt != null) buildPrompt.Show(building, building ? _buildings.CurrentPrompt : string.Empty);
                _promptText.enabled = !building;
                var prompt = _interaction != null ? _interaction.CurrentPrompt : string.Empty;
                if (_promptText.text != prompt) _promptText.text = prompt;
            }
        }

        private void OnDisable()
        {
            if (buildPrompt != null) buildPrompt.Show(false, string.Empty);
            if (_promptText != null) _promptText.enabled = true;
        }
    }
}
