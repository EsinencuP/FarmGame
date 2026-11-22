using System;
using MyLittleFarm.Core;
using MyLittleFarm.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MyLittleFarm.Editor
{
    /// <summary>Imports the user's four transparent PNGs and replaces only front-end button visuals.</summary>
    public static class MainMenuButtonSetup
    {
        private const string Folder = "Assets/_Game/Content/UI/MainMenuButtons/";
        [MenuItem("Tools/My Little Farm/UI/Apply Illustrated Main Menu Buttons")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != FarmSceneFlow.MenuScene) FarmSceneSetup.OpenMenu();
            var menu = UnityEngine.Object.FindAnyObjectByType<MainMenuUI>();
            Undo.RegisterFullObjectHierarchyUndo(menu.gameObject, "Replace main menu buttons");
            ApplyTo(menu); menu.Preview(); EditorUtility.SetDirty(menu);
            EditorSceneManager.MarkSceneDirty(menu.gameObject.scene); EditorSceneManager.SaveScene(menu.gameObject.scene); AssetDatabase.SaveAssets();
        }
        public static void ApplyTo(MainMenuUI menu)
        {
            menu.SetActionArtwork(Import("Play"), Import("Saves"), Import("Settings"), Import("Exit"));
        }
        private static Sprite Import(string name)
        {
            var path = Folder + name + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new MissingReferenceException("Missing supplied button: " + path);
            if (importer.textureType != TextureImporterType.Sprite || importer.maxTextureSize != 1024 || importer.mipmapEnabled || importer.isReadable)
            {
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = Vector4.zero; importer.spritePixelsPerUnit = 100; importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false; importer.isReadable = false; importer.maxTextureSize = 1024;
                importer.npotScale = TextureImporterNPOTScale.None; importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
