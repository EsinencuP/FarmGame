using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Farming;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Economy
{
    /// <summary>Хранит выбранную культуру в шести быстрых слотах семян.</summary>
    [DefaultExecutionOrder(-120)]
    public sealed class QuickSlotSystem : MonoBehaviour
    {
        [SerializeField, Range(0, 5)] private int startingSlot;

        // Порядок слотов берётся из каталога; индекс сохраняется отдельно от количества семян.
        private InputReader _input;
        private FarmCatalog _catalog;
        public int SelectedIndex { get; private set; }
        /// <summary>Количество доступных слотов определяется текущим каталогом культур.</summary>
        public int SlotCount => _catalog == null ? 0 : _catalog.Crops.Count;
        public CropDefinition SelectedCrop => _catalog == null || _catalog.Crops.Count == 0
            ? null : _catalog.Crops[Mathf.Clamp(SelectedIndex, 0, _catalog.Crops.Count - 1)];

        /// <summary>Назначает ввод и каталог, затем выбирает стартовый слот.</summary>
        public void Configure(InputReader input, FarmCatalog catalog)
        {
            _input = input;
            _catalog = catalog;
            Select(startingSlot);
        }

        private void Update()
        {
            // Клавиши 1–6 в режиме строительства принадлежат постройкам или семенам вне него.
            if (_input != null && !_input.BuildModeActive && !_input.SuppressGameplayThisFrame
                && _input.SeedSelection >= 0) Select(_input.SeedSelection);
        }

        /// <summary>Выбирает существующий слот без изменения инвентаря.</summary>
        public bool Select(int index)
        {
            if (_catalog == null || index < 0 || index >= _catalog.Crops.Count) return false;
            SelectedIndex = index;
            GameEvents.RaiseInventoryChanged();
            return true;
        }
    }
}
