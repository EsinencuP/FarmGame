using MyLittleFarm.Core.Grid;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Farming
{
    /// <summary>Содержит правило перехода свободной клетки из обычной земли в обработанную.</summary>
    public sealed class SoilSystem : MonoBehaviour
    {
        // Интерфейс разрешает системе менять грядку, не открывая внутренние массивы чанка.
        private IWorldGridWriter _grid;

        /// <summary>Получает доступ к правилам и записи фермерского слоя.</summary>
        public void Configure(IWorldGridWriter grid)
        {
            // Bootstrap передаёт единственную сетку текущей фермы.
            _grid = grid;
        }

        /// <summary>Создаёт обработанную грядку только на разрешённой свободной поверхности.</summary>
        public bool Till(Vector2Int position)
        {
            // GridSystem одновременно проверяет тип клетки и отсутствие владельца.
            if (!_grid.CanTill(position))
            {
                return false;
            }

            _grid.SetFarmingCell(position, new FarmingCell { soilType = CellType.Tilled });
            return true;
        }
    }
}
