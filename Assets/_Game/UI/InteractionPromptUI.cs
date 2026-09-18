using MyLittleFarm.Gameplay.World;
using MyLittleFarm.Gameplay.Building;
using UnityEngine;
using UnityEngine.UI;

namespace MyLittleFarm.UI
{
    public sealed class InteractionPromptUI : MonoBehaviour
    {
        private InteractionSystem _interaction;
        private Text _promptText;
        private BuildSystem _buildings;

        public void Configure(InteractionSystem interaction, Text promptText, BuildSystem buildings = null)
        {
            _interaction = interaction;
            _promptText = promptText;
            _buildings = buildings;
        }

        private void Update()
        {
            if (_interaction != null && _promptText != null)
            {
                var prompt = _buildings != null && _buildings.IsActive ? _buildings.CurrentPrompt : _interaction.CurrentPrompt;
                if (_promptText.text != prompt) _promptText.text = prompt;
            }
        }
    }
}
