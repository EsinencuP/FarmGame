using System;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MyLittleFarm.Editor
{
    /// <summary>Upgrades the saved HUD and ghost shader references without initializing or rebuilding gameplay.</summary>
    public static class HudConsistencySetup
    {
        [MenuItem("Tools/My Little Farm/Apply P0-4 HUD And Build Preview")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/_Game/Scenes/Prototype.unity")
                throw new InvalidOperationException("Open Prototype in Edit Mode.");
            var farm = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
            Configure(farm);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save P0-4 HUD.");
        }

        public static void Configure(GameBootstrap farm)
        {
            var canvas = farm.GetComponentInChildren<Canvas>(true);
            Text Text(string name) => canvas.transform.Find(name).GetComponent<Text>();
            var card = canvas.GetComponentInChildren<BuildPromptUI>(true);
            if (card == null)
            {
                card = BuildPromptUI.Create(canvas.transform, Text("Interaction Prompt").font);
                Undo.RegisterCreatedObjectUndo(card.gameObject, "Create construction card");
            }
            var router = new SerializedObject(canvas.GetComponent<InteractionPromptUI>());
            router.FindProperty("buildPrompt").objectReferenceValue = card;
            router.ApplyModifiedProperties();
            var responsive = canvas.GetComponent<ResponsiveHudLayout>();
            if (responsive == null) responsive = Undo.AddComponent<ResponsiveHudLayout>(canvas.gameObject);
            responsive.Configure(canvas.transform.Find("Stats Panel").GetComponent<RectTransform>(),
                canvas.transform.Find("Stats Panel/Stats").GetComponent<Text>(), Text("Help"), Text("Status"),
                Text("Onboarding Hint"), Text("Interaction Prompt"), canvas.transform.Find("Quick Slots").GetComponent<RectTransform>(), card);
            responsive.SetEditorPreview(ResponsiveHudLayout.PreviewMode.Exploration);
            EditorUtility.SetDirty(responsive);
            var build = new SerializedObject(farm.GetComponentInChildren<BuildSystem>(true));
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Game/Content/Art/BuildingGhost.shader");
            if (shader == null) throw new MissingReferenceException("The build ghost shader is missing.");
            build.FindProperty("ghostShader").objectReferenceValue = shader;
            build.ApplyModifiedProperties();
        }
    }
}
