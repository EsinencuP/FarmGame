using System;
using System.IO;
using System.Linq;
using MyLittleFarm.Core;
using MyLittleFarm.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MyLittleFarm.Editor
{
    /// <summary>Explicit migration of UI only. The authored farm keeps its scene GUID and world data.</summary>
    public static class FarmSceneSetup
    {
        [MenuItem("Tools/My Little Farm/UI/Separate Main Menu And Farm")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
            // Preserve any already authored scene edits before loading a different scene.
            EditorSceneManager.SaveOpenScenes();
            var game = SceneManager.GetSceneByPath(FarmSceneFlow.GameScene);
            if (!game.IsValid() || !game.isLoaded) game = EditorSceneManager.OpenScene(FarmSceneFlow.GameScene, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(game); FarmUISetup.Apply();
            var title = SceneManager.GetSceneByPath(FarmSceneFlow.MenuScene);
            if (!title.IsValid() || !title.isLoaded)
                title = File.Exists(FarmSceneFlow.MenuScene) ? EditorSceneManager.OpenScene(FarmSceneFlow.MenuScene, OpenSceneMode.Additive) : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(title);
            if (!title.GetRootGameObjects().Any(root => root.GetComponentInChildren<MainMenuUI>(true) != null)) CreateMenu();
            EditorSceneManager.SaveScene(title, FarmSceneFlow.MenuScene);
            var others = EditorBuildSettings.scenes.Where(scene => scene.path != FarmSceneFlow.MenuScene && scene.path != FarmSceneFlow.GameScene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(FarmSceneFlow.MenuScene, true), new EditorBuildSettingsScene(FarmSceneFlow.GameScene, true) }.Concat(others).ToArray();
            var entry = AssetDatabase.LoadAssetAtPath<SceneAsset>(FarmSceneFlow.MenuScene);
            EditorSceneManager.playModeStartScene = entry;
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(FarmSceneFlow.MenuScene, OpenSceneMode.Single);
            Debug.Log("Scene split complete: MainMenu is build entry 0, Prototype is gameplay 1. Original farm GUID preserved; no player data accessed.");
        }
        private static void CreateMenu()
        {
            var camera = new GameObject("Menu Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>(); camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(49, 79, 43, 255); camera.cullingMask = 0;
            camera.orthographic = true; camera.allowHDR = false; camera.allowMSAA = false;
            var light = new GameObject("Menu Light", typeof(Light)).GetComponent<Light>(); light.type = LightType.Directional; light.enabled = false;
            var canvas = new GameObject("Main Menu Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)); canvas.layer = 5;
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Game/Content/UI/Nunito.ttf");
            var background = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Content/UI/MainMenuBackground.png");
            var menu = MainMenuUI.Create(canvas.transform, font, background);
            if (File.Exists("Assets/_Game/Content/UI/MainMenuButtons/Play.png")) MainMenuButtonSetup.ApplyTo(menu);
            var events = new GameObject("Menu Event System", typeof(EventSystem), typeof(InputSystemUIInputModule)); events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
        public static void OpenMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists(FarmSceneFlow.MenuScene)) { Apply(); return; }
            EditorSceneManager.SaveOpenScenes(); EditorSceneManager.OpenScene(FarmSceneFlow.MenuScene);
        }
        public static void OpenGame()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorSceneManager.SaveOpenScenes(); EditorSceneManager.OpenScene(FarmSceneFlow.GameScene);
            UnityEngine.Object.FindAnyObjectByType<FarmMenuUI>()?.Preview(FarmMenuUI.Page.Game);
        }
    }
}
