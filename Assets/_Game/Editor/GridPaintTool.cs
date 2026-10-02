using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace MyLittleFarm.Editor
{
    /// <summary>Рисует базовые типы поверхности прямо в Scene View на выбранном TilemapChunk.</summary>
    [EditorTool("Paint Farm Grid", typeof(TilemapChunk))]
    public sealed class GridPaintTool : EditorTool
    {
        // Общие настройки доступны окну палитры и сохраняются при переключении инструмента.
        public static CellType SelectedTerrain { get; set; } = CellType.Grass;
        public static int BrushSize { get; set; } = 1;
        private Vector2Int? _lastPainted;

        /// <summary>Показывает кисть и записывает клетки карты при движении нажатой мыши.</summary>
        public override void OnToolGUI(EditorWindow window)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!(window is SceneView) || !(target is TilemapChunk chunk)) return;
            var root = chunk.GetComponentInParent<GameBootstrap>();
            var grid = root == null ? null : root.GetComponentInChildren<GridSystem>(true);
            if (grid == null || chunk.WorldMap == null) return;
            if (!grid.IsChunkLoaded(grid.GridToChunk(grid.WorldToGrid(chunk.transform.position))))
                chunk.ApplyTo(grid);
            var currentEvent = Event.current;
            if (currentEvent.alt) return;
            var ray = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition);
            var ground = new Plane(Vector3.up, chunk.transform.position);
            if (!ground.Raycast(ray, out var distance)) return;
            var center = grid.WorldToGrid(ray.GetPoint(distance));
            var chunkCoordinate = grid.GridToChunk(grid.WorldToGrid(chunk.transform.position));
            if (grid.GridToChunk(center) != chunkCoordinate) return;
            DrawBrush(grid, center, chunkCoordinate);
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            if (currentEvent.type == EventType.MouseUp) _lastPainted = null;
            if (currentEvent.button != 0 || (currentEvent.type != EventType.MouseDown
                && currentEvent.type != EventType.MouseDrag)) return;
            if (!IsPaintable(SelectedTerrain)) return;
            if (_lastPainted != center)
            {
                Undo.RecordObject(chunk.WorldMap, "Paint Farm Grid");
                PaintBrush(grid, chunk, center, chunkCoordinate);
                EditorUtility.SetDirty(chunk.WorldMap);
                _lastPainted = center;
            }
            currentEvent.Use();
        }

        /// <summary>Рисует контур кисти поверх клеток без изменения карты.</summary>
        private static void DrawBrush(GridSystem grid, Vector2Int center, Vector2Int chunkCoordinate)
        {
            // Контур рисуется только на клетках выбранного блока и не создаёт объекты сцены.
            var radius = Mathf.Max(0, BrushSize - 1);
            for (var x = center.x - radius; x <= center.x + radius; x++)
            for (var z = center.y - radius; z <= center.y + radius; z++)
            {
                var coordinate = new Vector2Int(x, z);
                if (grid.GridToChunk(coordinate) != chunkCoordinate) continue;
                var world = grid.GridToWorld(coordinate) + Vector3.up * 0.05f;
                var half = grid.CellSize * 0.5f;
                var corners = new[]
                {
                    world + new Vector3(-half, 0f, -half),
                    world + new Vector3(-half, 0f, half),
                    world + new Vector3(half, 0f, half),
                    world + new Vector3(half, 0f, -half)
                };
                Handles.DrawSolidRectangleWithOutline(corners,
                    new Color(1f, 0.75f, 0.15f, 0.16f), new Color(1f, 0.75f, 0.15f, 0.95f));
            }
        }

        /// <summary>Записывает выбранный тип в базовую карту и обновляет один чанк сцены.</summary>
        private static void PaintBrush(GridSystem grid, TilemapChunk chunk, Vector2Int center,
            Vector2Int chunkCoordinate)
        {
            // Запись идёт в ScriptableObject, затем сцена перечитывает базу и обновляет один меш.
            var radius = Mathf.Max(0, BrushSize - 1);
            for (var x = center.x - radius; x <= center.x + radius; x++)
            for (var z = center.y - radius; z <= center.y + radius; z++)
            {
                var coordinate = new Vector2Int(x, z);
                if (grid.GridToChunk(coordinate) != chunkCoordinate) continue;
                chunk.WorldMap.SetTerrain(coordinate, new TerrainCell(SelectedTerrain));
            }
            chunk.ApplyTo(grid);
            chunk.GetComponent<ChunkRenderer>()?.Rebuild();
            SceneView.RepaintAll();
        }

        /// <summary>Запрещает кисти записывать временные игровые состояния как базовый terrain.</summary>
        private static bool IsPaintable(CellType type) =>
            type == CellType.Grass || type == CellType.Dirt || type == CellType.Water
            || type == CellType.Rock || type == CellType.Sand || type == CellType.Road
            || type == CellType.Locked;
    }
}
