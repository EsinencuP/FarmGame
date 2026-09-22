using UnityEngine;
using MyLittleFarm.Core;

namespace MyLittleFarm.Gameplay.World
{
    /// <summary>Находит клетку под курсором и передаёт её GridSystem для подсветки.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class CellSelector : MonoBehaviour
    {
        // Grid переводит мировую точку в координаты клетки, InputReader и Camera строят луч от курсора.
        private GridSystem _grid;
        private Transform _player;
        private InputReader _input;
        private Camera _camera;

        // Публичные значения читает InteractionSystem при нажатии действия.
        public bool HasSelection { get; private set; }
        public Vector2Int SelectedPosition { get; private set; }

        public void Configure(GridSystem grid, Transform player, InputReader input = null, Camera camera = null)
        {
            // Зависимости назначаются bootstrap-скриптом один раз.
            _grid = grid;
            _player = player;
            _input = input;
            _camera = camera;
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

            // Луч камеры пересекает горизонтальную плоскость сетки в точке под курсором.
            var hasPointerTarget = _input != null && _input.HasPointer && _camera != null;
            var target = Vector3.zero;
            if (hasPointerTarget)
            {
                var ray = _camera.ScreenPointToRay(_input.PointerPosition);
                hasPointerTarget = new Plane(Vector3.up, Vector3.zero).Raycast(ray, out var distance);
                if (hasPointerTarget)
                {
                    target = ray.GetPoint(distance);
                }
            }

            if (hasPointerTarget)
            {
                SelectWorldPoint(target);
            }
            else
            {
                HasSelection = false;
                _grid.Select(null);
            }
        }

        /// <summary>
        /// Выбирает клетку по мировой точке. Публичный метод позволяет тестам проверять выбор
        /// детерминированно, не подменяя положение аппаратного курсора.
        /// </summary>
        public bool SelectWorldPoint(Vector3 worldPoint)
        {
            HasSelection = _grid != null && _grid.TryWorldToCell(worldPoint, out var position);
            if (HasSelection)
            {
                SelectedPosition = position;
                _grid.Select(position);
            }
            else
            {
                _grid?.Select(null);
            }

            return HasSelection;
        }
    }
}
