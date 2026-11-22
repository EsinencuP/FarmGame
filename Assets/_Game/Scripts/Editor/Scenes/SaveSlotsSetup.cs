using System;
using MyLittleFarm.Core;
using MyLittleFarm.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MyLittleFarm.Editor
{
    /// <summary>Authors only the slot UI and serialized settings; never reads/writes a player's files.</summary>
    public static class SaveSlotsSetup
    {
        [MenuItem("Tools/My Little Farm/Apply P2-6 Save Slots")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
            var root = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
            if (root == null || root.gameObject.scene.path != "Assets/_Game/Scenes/Prototype.unity")
                throw new InvalidOperationException("Open Prototype first.");
            var saves = root.GetComponentInChildren<SaveSystem>(true);
            var hud = root.GetComponentInChildren<HUDController>(true);
            var input = root.GetComponent<InputReader>();
            if (saves == null || hud == null || input == null) throw new MissingReferenceException("Baked systems are missing.");
            var ui = hud.GetComponentInChildren<SaveSlotsUI>(true);
            if (ui == null) ui = SaveSlotsUI.Create(hud.transform, saves, input, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            ui.transform.SetAsLastSibling(); ui.EnsureScrollbar(); ui.Close(); ui.Resize();
            var serialized = new SerializedObject(saves);
            serialized.FindProperty("slotMenu").objectReferenceValue = ui;
            serialized.FindProperty("previewCamera").objectReferenceValue = root.GetComponentInChildren<Camera>(true);
            serialized.FindProperty("autosaveIntervalSeconds").floatValue = 300;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            hud.GetComponent<ResponsiveHudLayout>()?.SetEditorPreview(ResponsiveHudLayout.PreviewMode.Exploration);
            EditorUtility.SetDirty(ui); EditorUtility.SetDirty(saves);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene); EditorSceneManager.SaveScene(root.gameObject.scene);
            Debug.Log("P2-6: three-slot menu authored; autosave 300 seconds. No gameplay or player files accessed.");
        }
    }
}
