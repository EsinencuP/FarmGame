using MyLittleFarm.Core.Grid;
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
            // GridSystem одновременно проверяет тип клетки и отсутствие владельца.
            if (!_grid.CanTill(position))
            {
                return false;
            }

            _grid.SetCellType(position, CellType.Tilled);
            return true;
        }
    }
}
