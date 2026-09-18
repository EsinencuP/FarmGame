using UnityEngine;

namespace MyLittleFarm.Gameplay.World
{
    [DefaultExecutionOrder(-100)]
    public sealed class CellSelector : MonoBehaviour
    {
        private GridSystem _grid;
        private Transform _player;

        public bool HasSelection { get; private set; }
        public Vector2Int SelectedPosition { get; private set; }

        public void Configure(GridSystem grid, Transform player)
        {
            _grid = grid;
            _player = player;
        }

        private void Update()
        {
            RefreshSelection();
        }

        public void RefreshSelection()
        {
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
