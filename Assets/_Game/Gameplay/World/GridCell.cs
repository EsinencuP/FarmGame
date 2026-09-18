using System;
using UnityEngine;

namespace MyLittleFarm.Gameplay.World
{
    /// <summary>Допустимые логические состояния поверхности клетки.</summary>
    public enum GridCellState
    {
        Soil = 0,   // Исходная необработанная земля.
        Tilled = 1, // Земля готова к посадке.
        Blocked = 2 // Клетка запрещена для земледелия и строительства.
    }

    /// <summary>Соединяет логические координаты и состояние клетки с её объектом в сцене.</summary>
    public sealed class GridCell
    {
        // Position использует x/y Vector2Int, где y соответствует мировой оси Z.
        public Vector2Int Position { get; }
        public GridCellState State { get; private set; }
        // View и Renderer позволяют GridSystem обновлять внешний вид без повторного поиска компонента.
        public GameObject View { get; }
        public Renderer Renderer { get; }

        public GridCell(Vector2Int position, GridCellState state, GameObject view)
        {
            // Все постоянные ссылки клетки устанавливаются один раз при генерации сетки.
            Position = position;
            State = state;
            View = view;
            Renderer = view.GetComponent<Renderer>();
        }

        public void SetState(GridCellState state)
        {
            // Визуальный цвет после смены обновляет владеющий GridSystem.
            State = state;
        }
    }

    /// <summary>Компактное представление изменённой клетки для JSON-сохранения.</summary>
    [Serializable]
    public sealed class GridCellSaveData
    {
        // Сохраняются только координаты и состояние; визуальный объект создаётся заново.
        public int x;
        public int z;
        public GridCellState state;
    }
}
