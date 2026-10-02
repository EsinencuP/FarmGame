using MyLittleFarm.Core.Grid;
using UnityEditor;
using UnityEngine;

namespace MyLittleFarm.Editor
{
    /// <summary>Показывает назначение карты и быстрый переход к кисти из Inspector.</summary>
    [CustomEditor(typeof(WorldMapAsset))]
    public sealed class WorldMapAssetEditor : UnityEditor.Editor
    {
        /// <summary>Сохраняет обычный Inspector для данных и добавляет кнопку палитры.</summary>
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var map = (WorldMapAsset)target;
            EditorGUILayout.LabelField("Base map cells", map.CellCount.ToString());
            if (GUILayout.Button("Open Grid Paint Palette"))
                EditorApplication.ExecuteMenuItem("Tools/My Little Farm/Grid Paint Palette");
        }
    }
}
