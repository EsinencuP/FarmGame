using System;
using UnityEngine;

namespace MyLittleFarm.Gameplay.World
{
    public enum GridCellState
    {
        Soil = 0,
        Tilled = 1,
        Blocked = 2
    }

    public sealed class GridCell
    {
        public Vector2Int Position { get; }
        public GridCellState State { get; private set; }
        public GameObject View { get; }
        public Renderer Renderer { get; }

        public GridCell(Vector2Int position, GridCellState state, GameObject view)
        {
            Position = position;
            State = state;
            View = view;
            Renderer = view.GetComponent<Renderer>();
        }

        public void SetState(GridCellState state)
        {
            State = state;
        }
    }

    [Serializable]
    public sealed class GridCellSaveData
    {
        public int x;
        public int z;
        public GridCellState state;
    }
}

