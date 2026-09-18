using MyLittleFarm.Gameplay.World;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Farming
{
    public sealed class SoilSystem : MonoBehaviour
    {
        private GridSystem _grid;

        public void Configure(GridSystem grid)
        {
            _grid = grid;
        }

        public bool Till(Vector2Int position)
        {
            if (_grid.IsOccupied(position) || !_grid.TryGetCell(position, out var cell) || cell.State != GridCellState.Soil)
            {
                return false;
            }

            _grid.SetCellState(position, GridCellState.Tilled);
            return true;
        }
    }
}
