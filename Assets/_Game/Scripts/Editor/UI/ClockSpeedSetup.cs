using System;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.World;
using MyLittleFarm.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MyLittleFarm.Editor
{
    public static class ClockSpeedSetup
    {
        [MenuItem("Tools/My Little Farm/Add Day Night Speed Button")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Use Edit Mode.");
            var scene=EditorSceneManager.GetActiveScene();
            if(scene.path!="Assets/_Game/Scenes/Prototype.unity")throw new InvalidOperationException("Open Prototype first.");
            var root=UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
            var cycle=root.GetComponentInChildren<DayNightCycle>(true);
            var journal=root.GetComponentInChildren<FarmJournalUI>(true);
            var input=root.GetComponent<InputReader>();
            var layout=root.GetComponentInChildren<ResponsiveHudLayout>(true);
            if(cycle==null||journal==null||input==null||layout==null)throw new MissingReferenceException("Clock and HUD must be authored first.");
            var ui=journal.GetComponentInChildren<ClockSpeedUI>(true);
            if(ui==null)ui=ClockSpeedUI.Create(journal.transform,cycle,input,Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            // The journal and save-slot backdrops cover this HUD control when their modal opens.
            ui.transform.SetSiblingIndex(journal.ClockText.transform.GetSiblingIndex()+1);
            var settings=new SerializedObject(layout);
            settings.FindProperty("clockSpeedButton").objectReferenceValue=ui.GetComponent<RectTransform>();
            settings.ApplyModifiedPropertiesWithoutUndo();
            settings=new SerializedObject(input);
            settings.FindProperty("clockSpeedPointerBlock").objectReferenceValue=ui.GetComponent<RectTransform>();
            settings.ApplyModifiedPropertiesWithoutUndo();
            layout.SetEditorPreview(ResponsiveHudLayout.PreviewMode.Exploration);
            EditorUtility.SetDirty(layout);EditorUtility.SetDirty(input);EditorUtility.SetDirty(ui);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log("Day/night speed button authored: x1, x10, x30, x60. Simulation timers retain real-time speed.");
        }
    }
}
