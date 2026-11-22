using System;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MyLittleFarm.Editor
{
    /// <summary>Authors the shared interaction layer and selector settings without initializing gameplay or changing physics masks.</summary>
    public static class HoverInteractionSetup
    {
        [MenuItem("Tools/My Little Farm/Apply P0-5 Hover And Interaction")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/_Game/Scenes/Prototype.unity")
                throw new InvalidOperationException("Open Prototype in Edit Mode.");
            var layer = EnsureLayer();
            var farm = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
            var selector = new SerializedObject(farm.GetComponentInChildren<CellSelector>(true));
            selector.FindProperty("interactionLayer").intValue = 1 << layer;
            selector.FindProperty("hoverSettleTime").floatValue = 0.12f;
            selector.ApplyModifiedProperties();
            foreach (var target in farm.GetComponentsInChildren<InteractionTarget>(true))
            {
                target.ApplyPickLayer();
                EditorUtility.SetDirty(target.gameObject);
                foreach (var collider in target.GetComponentsInChildren<Collider>(true)) EditorUtility.SetDirty(collider.gameObject);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save hover setup.");
        }

        private static int EnsureLayer()
        {
            var existing = LayerMask.NameToLayer(InteractionTarget.PickLayerName);
            if (existing >= 0) return existing;
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = settings.FindProperty("layers");
            for (var i = 8; i < layers.arraySize; i++)
            {
                var entry = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(entry.stringValue)) continue;
                entry.stringValue = InteractionTarget.PickLayerName;
                settings.ApplyModifiedProperties();
                return i;
            }
            throw new InvalidOperationException("No free user layer for FarmInteraction.");
        }
    }
}
