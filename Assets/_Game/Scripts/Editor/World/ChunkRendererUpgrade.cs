using MyLittleFarm.Core.Grid;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MyLittleFarm.Editor
{
    /// <summary>Добавляет меш-представление в старые выгруженные сцены без пересоздания их объектов.</summary>
    public static class ChunkRendererUpgrade
    {
        /// <summary>Добавляет представления ко всем сценовым чанкам без удаления существующих объектов.</summary>
        [MenuItem("Tools/My Little Farm/Upgrade Chunk Renderers")]
        private static void Upgrade()
        {
            // Undo сохраняет все ручные настройки сцены и делает добавление компонентов обратимым.
            var chunks = Object.FindObjectsByType<TilemapChunk>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var chunk in chunks)
            {
                if (chunk.GetComponent<ChunkRenderer>() != null) continue;
                Undo.AddComponent<ChunkRenderer>(chunk.gameObject);
                EditorSceneManager.MarkSceneDirty(chunk.gameObject.scene);
            }
        }
    }
}
