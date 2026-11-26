using System;
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
    public static class FarmFrontEndSetup
    {
        internal const string MenuArtPath = "Assets/_Game/Content/Art/Main Menu.png";
        private const string LoadingArtPath = "Assets/_Game/Content/Art/Louding.png";
        private const string ArtworkPath = "Assets/_Game/Resources/FarmUI/ScreenArtwork.asset";

        [MenuItem("Tools/My Little Farm/UI/Author Save Selection And Artwork")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save edits in the open scenes before authoring the front end.");

            var original = EditorSceneManager.GetActiveScene().path;
            var menuArt = ImportArtwork(MenuArtPath);
            var loadingArt = ImportArtwork(LoadingArtPath);
            var artwork = AssetDatabase.LoadAssetAtPath<FarmScreenArtwork>(ArtworkPath);
            if (artwork == null)
            {
                artwork = ScriptableObject.CreateInstance<FarmScreenArtwork>();
                AssetDatabase.CreateAsset(artwork, ArtworkPath);
            }
            var settings = new SerializedObject(artwork);
            settings.FindProperty("loading").objectReferenceValue = loadingArt;
            settings.ApplyModifiedPropertiesWithoutUndo();

            var menu = EditorSceneManager.OpenScene(FarmSceneFlow.MenuScene);
            var main = menu.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<MainMenuUI>(true)).Single();
            main.transform.Find("Main menu artwork").GetComponent<Image>().sprite = menuArt;
            main.Preview();
            EditorSceneManager.MarkSceneDirty(menu);
            EditorSceneManager.SaveScene(menu);

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(FarmSceneFlow.SlotsScene) == null)
                CreateSelectionScene(menuArt);
            else
            {
                var existing = EditorSceneManager.OpenScene(FarmSceneFlow.SlotsScene);
                var canvas = existing.GetRootGameObjects().Single(go => go.GetComponent<Canvas>() != null);
                canvas.transform.Find("Selection artwork").GetComponent<Image>().sprite = menuArt;
                EditorSceneManager.MarkSceneDirty(existing);
                EditorSceneManager.SaveScene(existing);
            }
            RegisterScenes();
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(string.IsNullOrEmpty(original) ? FarmSceneFlow.MenuScene : original);
        }

        internal static void RegisterScenes()
        {
            var paths = new[] { FarmSceneFlow.MenuScene, FarmSceneFlow.SlotsScene, FarmSceneFlow.GameScene };
            var others = EditorBuildSettings.scenes.Where(scene => !paths.Contains(scene.path)).ToArray();
            EditorBuildSettings.scenes = paths.Where(path => AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                .Select(path => new EditorBuildSettingsScene(path, true)).Concat(others).ToArray();
        }

        private static void CreateSelectionScene(Sprite artwork)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Selection Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(29, 45, 34, 255);
            camera.cullingMask = 0;
            camera.orthographic = true;
            camera.allowHDR = camera.allowMSAA = false;

            var canvas = new GameObject("Save Selection Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.layer = 5;
            var surface = canvas.GetComponent<Canvas>();
            surface.renderMode = RenderMode.ScreenSpaceOverlay;
            surface.pixelPerfect = true;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var image = FarmUITheme.Image(canvas.transform, "Selection artwork");
            FarmUITheme.Stretch(image.rectTransform);
            image.sprite = artwork;
            image.preserveAspect = true;
            image.raycastTarget = false;
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Game/Content/UI/Nunito.ttf");
            var slots = SaveSlotsUI.Create(canvas.transform, null, null, font);
            slots.ConfigureSelectionScene();
            var events = new GameObject("Selection Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            EditorSceneManager.SaveScene(scene, FarmSceneFlow.SlotsScene);
        }

        private static Sprite ImportArtwork(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter
                ?? throw new MissingReferenceException("Missing screen artwork: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
