using System.IO;
using MyLittleFarm.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MyLittleFarm.Editor
{
    /// <summary>
    /// Один раз выгружает программный прототип в Prototype.unity. После сохранения объекты
    /// доступны в Hierarchy и Inspector, а Play Mode только подключает существующие компоненты.
    /// </summary>
    [InitializeOnLoad]
    public static class PrototypeSceneBaker
    {
        // Путь ограничивает автоматическую выгрузку одной конкретной сценой проекта.
        private const string PrototypeScenePath = "Assets/_Game/Scenes/Prototype.unity";
        private const string RootName = "My Little Farm — Stage 2";

        /// <summary>Планирует безопасную проверку после завершения загрузки и компиляции редактора.</summary>
        static PrototypeSceneBaker()
        {
            EditorApplication.delayCall += BakeEmptyPrototypeOnce;
            // Если при загрузке проекта активна другая сцена, проверка повторится при открытии Prototype.
            EditorSceneManager.sceneOpened += HandleSceneOpened;
        }

        /// <summary>Планирует выгрузку после полного открытия Prototype-сцены редактором.</summary>
        private static void HandleSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (scene.path == PrototypeScenePath)
            {
                EditorApplication.delayCall += BakeEmptyPrototypeOnce;
            }
        }

        /// <summary>
        /// Автоматически заполняет только полностью пустую Prototype-сцену. Существующую ручную
        /// иерархию метод никогда не перестраивает и не удаляет.
        /// </summary>
        private static void BakeEmptyPrototypeOnce()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                // Ждём завершения импорта скриптов: иначе сцена может получить Missing Script.
                EditorApplication.delayCall += BakeEmptyPrototypeOnce;
                return;
            }

            var scene = SceneManager.GetActiveScene();
            if (scene.path != PrototypeScenePath || scene.GetRootGameObjects().Length != 0)
            {
                return;
            }

            BakeIntoScene(scene);
        }

        /// <summary>Ручная команда полностью пересоздаёт только корень прототипа в активной сцене.</summary>
        [MenuItem("Tools/My Little Farm/Bake Prototype Scene")]
        private static void RebuildPrototypeScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != PrototypeScenePath)
            {
                Debug.LogError($"Open {PrototypeScenePath} before baking the prototype.");
                return;
            }

            // Undo делает удаление прежней выгрузки обратимым стандартной командой редактора.
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponent<GameBootstrap>() != null)
                {
                    Undo.DestroyObjectImmediate(root);
                }
            }

            BakeIntoScene(scene);
        }

        /// <summary>Разрешает пункт меню только для нужной сцены вне Play Mode.</summary>
        [MenuItem("Tools/My Little Farm/Bake Prototype Scene", true)]
        private static bool CanRebuildPrototypeScene()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode
                && SceneManager.GetActiveScene().path == PrototypeScenePath;
        }

        /// <summary>Создаёт корень, запускает редакторскую сборку и сохраняет результат в сцене.</summary>
        private static void BakeIntoScene(Scene scene)
        {
            var root = new GameObject(RootName);
            try
            {
                Undo.RegisterCreatedObjectUndo(root, "Bake My Little Farm prototype");
                var bootstrap = root.AddComponent<GameBootstrap>();
                bootstrap.BuildPrototype();

                EditorUtility.SetDirty(root);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new IOException("Prototype.unity could not be saved after baking.");
                Selection.activeGameObject = root;
                Debug.Log("My Little Farm prototype was baked into Prototype.unity and is ready for Edit Mode setup.");
            }
            catch (System.Exception exception)
            {
                // Не оставляем половину иерархии, которая скрыла бы повторную автосборку.
                Undo.DestroyObjectImmediate(root);
                Debug.LogException(exception);
            }
        }
    }
}
