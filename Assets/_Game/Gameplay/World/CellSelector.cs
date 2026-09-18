using UnityEngine;

namespace MyLittleFarm.Gameplay.World
{
    /// <summary>Находит клетку перед лицом игрока и передаёт её GridSystem для подсветки.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class CellSelector : MonoBehaviour
    {
        // Grid переводит мировую точку в координаты клетки; player даёт позицию и направление взгляда.
        private GridSystem _grid;
        private Transform _player;

        // Публичные значения читает InteractionSystem при нажатии действия.
        public bool HasSelection { get; private set; }
        public Vector2Int SelectedPosition { get; private set; }

        public void Configure(GridSystem grid, Transform player)
        {
            // Зависимости назначаются bootstrap-скриптом один раз.
            _grid = grid;
            _player = player;
        }

        private void Update()
        {
            // Выбор пересчитывается после перемещения игрока благодаря порядку выполнения -100.
            RefreshSelection();
        }

        public void RefreshSelection()
        {
            // В режиме строительства обычная подсветка уступает место предпросмотру под курсором.
            if (_grid == null || _player == null)
            {
                return;
            }

            if (_grid.Buildings != null && _grid.Buildings.IsActive)
            {
                HasSelection = false;
                _grid.Select(null);
                return;
            }

            // Target находится на небольшом расстоянии перед игроком в мировом пространстве.
            var target = _player.position + _player.forward * 1.35f;
            HasSelection = _grid.TryWorldToCell(target, out var position);
            if (HasSelection)
            {
                SelectedPosition = position;
                _grid.Select(position);
            }
            else
            {
                _grid.Select(null);
            }
        }
    }
}
