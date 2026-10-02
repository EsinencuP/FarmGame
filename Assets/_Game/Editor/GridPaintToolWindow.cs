using MyLittleFarm.Core.Grid;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace MyLittleFarm.Editor
{
    /// <summary>Даёт дизайнеру палитру и управляет картой выбранного чанка.</summary>
    public sealed class GridPaintToolWindow : EditorWindow
    {
        // Массив ограничивает кисть допустимыми постоянными типами поверхности.
        private static readonly CellType[] Types =
        {
            CellType.Grass, CellType.Dirt, CellType.Water, CellType.Rock,
            CellType.Sand, CellType.Road, CellType.Locked
        };
        private static readonly string[] Labels =
        {
            "Grass", "Dirt", "Water", "Rock", "Sand", "Road", "Locked"
        };

        [MenuItem("Tools/My Little Farm/Grid Paint Palette")]
        /// <summary>Открывает палитру из меню редактора.</summary>
        private static void Open() => GetWindow<GridPaintToolWindow>("Farm Grid Paint");

        /// <summary>Показывает назначение карты, размер кисти и допустимые типы поверхности.</summary>
        private void OnGUI()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox("Map painting is available in Edit Mode.", MessageType.Info);
                return;
            }
            // Кисть доступна только на явно выбранном блоке, чтобы не менять соседние карты.
            var chunk = Selection.activeGameObject == null
                ? null : Selection.activeGameObject.GetComponent<TilemapChunk>();
            if (chunk == null)
            {
                EditorGUILayout.HelpBox("Select a TilemapChunk in the Hierarchy.", MessageType.Info);
                return;
            }
            var map = (WorldMapAsset)EditorGUILayout.ObjectField("World Map", chunk.WorldMap,
                typeof(WorldMapAsset), false);
            if (map != chunk.WorldMap)
            {
                Undo.RecordObject(chunk, "Assign Farm World Map");
                chunk.AssignWorldMap(map);
                EditorUtility.SetDirty(chunk);
                RefreshChunk(chunk);
            }
            if (chunk.WorldMap == null)
            {
                if (GUILayout.Button("Create World Map Asset")) CreateMap(chunk);
                return;
            }
            var selected = System.Array.IndexOf(Types, GridPaintTool.SelectedTerrain);
            selected = EditorGUILayout.Popup("Terrain", Mathf.Max(0, selected), Labels);
            GridPaintTool.SelectedTerrain = Types[selected];
            GridPaintTool.BrushSize = EditorGUILayout.IntSlider("Brush Radius", GridPaintTool.BrushSize, 1, 10);
            EditorGUILayout.LabelField("Painted cells", chunk.WorldMap.CellCount.ToString());
            if (GUILayout.Button("Activate Paint Tool")) ToolManager.SetActiveTool<GridPaintTool>();
        }

        /// <summary>Создаёт ScriptableObject по выбранному пользователем пути и назначает его чанку.</summary>
        private static void CreateMap(TilemapChunk chunk)
        {
            // Файл создаётся только явной командой пользователя в Editor, не при запуске игры.
            var path = EditorUtility.SaveFilePanelInProject("Create World Map", "FarmWorld", "asset",
                "Choose a location for the authored world map.");
            if (string.IsNullOrEmpty(path)) return;
            var map = CreateInstance<WorldMapAsset>();
            AssetDatabase.CreateAsset(map, path);
            Undo.RecordObject(chunk, "Assign Farm World Map");
            chunk.AssignWorldMap(map);
            EditorUtility.SetDirty(chunk);
            RefreshChunk(chunk);
        }

        /// <summary>Перечитывает базовую карту и обновляет представление после редакторской правки.</summary>
        private static void RefreshChunk(TilemapChunk chunk)
        {
            var root = chunk.GetComponentInParent<MyLittleFarm.Core.GameBootstrap>();
            var grid = root == null ? null : root.GetComponentInChildren<GridSystem>(true);
            if (grid == null) return;
            chunk.ApplyTo(grid);
            chunk.GetComponent<ChunkRenderer>()?.Rebuild();
            SceneView.RepaintAll();
        }
    }
}
