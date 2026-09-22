using UnityEngine;

namespace MyLittleFarm.Gameplay.World
{
    /// <summary>
    /// Сохраняет координаты и исходное состояние визуальной клетки прямо в сцене.
    /// Благодаря этому GridSystem восстанавливает логический словарь без повторной генерации объектов.
    /// </summary>
    public sealed class GridCellView : MonoBehaviour
    {
        // Координаты сериализуются Unity и остаются доступны вне Play Mode.
        [SerializeField] private int x;
        [SerializeField] private int z;
        // State задаёт исходное состояние клетки при старте сцены.
        [SerializeField] private GridCellState state = GridCellState.Soil;

        /// <summary>Клеточная позиция, где Vector2Int.y соответствует мировой оси Z.</summary>
        public Vector2Int Position => new Vector2Int(x, z);
        /// <summary>Состояние поверхности, сохранённое в сцене.</summary>
        public GridCellState State => state;

        /// <summary>Записывает данные при однократной выгрузке клетки в сцену.</summary>
        public void Configure(Vector2Int position, GridCellState initialState)
        {
            x = position.x;
            z = position.y;
            state = initialState;
        }
    }
}
