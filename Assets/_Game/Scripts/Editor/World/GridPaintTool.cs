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
        static GridPaintTool() => Undo.undoRedoPerformed += RefreshSelectedWorld;

        internal static void RefreshSelectedWorld()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || Selection.activeGameObject==null)return;
            var selected=Selection.activeGameObject.GetComponent<TilemapChunk>();
            var root=selected==null?null:selected.GetComponentInParent<GameBootstrap>();
            if(root==null)return;
            var grid=root.GetComponentInChildren<GridSystem>(true);
            var chunks=root.GetComponentsInChildren<TilemapChunk>(true);
            grid.RegisterSceneChunks(chunks);
            foreach(var chunk in chunks)chunk.ApplyTo(grid);
            foreach(var chunk in chunks)
            {
                chunk.GetComponent<ChunkRenderer>()?.Rebuild();
                var filter=chunk.GetComponent<MeshFilter>();
                if(filter!=null && filter.sharedMesh!=null && AssetDatabase.Contains(filter.sharedMesh))EditorUtility.SetDirty(filter.sharedMesh);
            }
            SceneView.RepaintAll();
        }
        // Общие настройки доступны окну палитры и сохраняются при переключении инструмента.
        public static CellType SelectedTerrain { get; set; } = CellType.Grass;
        public static PathSurface SelectedPathSurface { get; set; } = PathSurface.Dirt;
        public static int BrushSize { get; set; } = 1;
        public static bool PaintPermissions { get; set; }
        public static CellPermissions SelectedPermissions { get; set; } = CellPermissions.Inherit;
        private Vector2Int? _lastPainted;

        /// <summary>Показывает кисть и записывает клетки карты при движении нажатой мыши.</summary>
        public override void OnToolGUI(EditorWindow window)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!(window is SceneView) || !(target is TilemapChunk chunk)) return;
            var root = chunk.GetComponentInParent<GameBootstrap>();
            var grid = root == null ? null : root.GetComponentInChildren<GridSystem>(true);
            if (grid == null || chunk.WorldMap == null) return;
            grid.RegisterSceneChunk(chunk);
            var currentEvent = Event.current;
            if (currentEvent.alt) return;
            var ray = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition);
            if (!grid.TryRaycastSurface(ray, out var surfaceHit)) return;
            var center = grid.WorldToGrid(surfaceHit.point);
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
                    Corner(grid,world,-half,-half),
                    Corner(grid,world,-half,half),
                    Corner(grid,world,half,half),
                    Corner(grid,world,half,-half)
                };
                Handles.DrawSolidRectangleWithOutline(corners,
                    new Color(1f, 0.75f, 0.15f, 0.16f), new Color(1f, 0.75f, 0.15f, 0.95f));
            }
        }

        private static Vector3 Corner(GridSystem grid, Vector3 center, float x, float z)
        {
            var point=center+new Vector3(x,0,z);
            point.y=grid.GetSurfaceHeight(point.x,point.z)+.05f;
            return point;
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
                var terrain = grid.GetTerrainCell(coordinate);
                if (PaintPermissions) terrain.permissions = SelectedPermissions;
                else
                {
                    terrain.type = SelectedTerrain;
                    terrain.pathSurface = SelectedTerrain == CellType.Road ? SelectedPathSurface : PathSurface.Dirt;
                }
                chunk.WorldMap.SetTerrain(coordinate, terrain);
            }
            chunk.ApplyTo(grid);
            chunk.GetComponent<ChunkRenderer>()?.Rebuild();
            var filter=chunk.GetComponent<MeshFilter>();
            if(filter!=null && filter.sharedMesh!=null && AssetDatabase.Contains(filter.sharedMesh))EditorUtility.SetDirty(filter.sharedMesh);
            SceneView.RepaintAll();
        }

        /// <summary>Запрещает кисти записывать временные игровые состояния как базовый terrain.</summary>
        private static bool IsPaintable(CellType type) =>
            type == CellType.Grass || type == CellType.Dirt || type == CellType.Water
            || type == CellType.Rock || type == CellType.Sand || type == CellType.Road
            || type == CellType.Locked;
    }
}
