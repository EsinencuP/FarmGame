using MyLittleFarm.Gameplay.World;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Farming
{
    /// <summary>Содержит правило перехода свободной клетки из обычной земли в обработанную.</summary>
    public sealed class SoilSystem : MonoBehaviour
    {
        // GridSystem хранит фактическое состояние каждой клетки.
        private GridSystem _grid;

        public void Configure(GridSystem grid)
        {
            // Bootstrap передаёт единственную сетку текущей фермы.
            _grid = grid;
        }

        public bool Till(Vector2Int position)
        {
            // Обработать можно только свободную клетку в исходном состоянии Soil.
            if (_grid.IsOccupied(position) || !_grid.TryGetCell(position, out var cell) || cell.State != GridCellState.Soil)
            {
                return false;
            }

            _grid.SetCellState(position, GridCellState.Tilled);
            return true;
        }
    }
}
