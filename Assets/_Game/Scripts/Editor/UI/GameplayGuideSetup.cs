using System;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Animals;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.Farming;
using MyLittleFarm.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MyLittleFarm.Editor
{
    /// <summary>Game-scene-only migration. Does not load or author MainMenu content.</summary>
    public static class GameplayGuideSetup
    {
        [MenuItem("Tools/My Little Farm/UI/Apply Gameplay Handbook And Navigation")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
            if (SceneManager.GetActiveScene().path != FarmSceneFlow.GameScene) FarmSceneSetup.OpenGame();
            var farm = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
            Undo.RegisterFullObjectHierarchyUndo(farm.gameObject, "Gameplay handbook and navigation");
            ApplyTo(farm);
            EditorSceneManager.MarkSceneDirty(farm.gameObject.scene); EditorSceneManager.SaveScene(farm.gameObject.scene); AssetDatabase.SaveAssets();
        }
        public static void ApplyTo(GameBootstrap farm)
        {
            var menu = farm.GetComponentInChildren<FarmMenuUI>(true);
            var nav = menu.transform.Find("HUD navigation");
            var removed = new[] { "Инвентарь · I", "Магазин · O", "Каталог · B / Tab" };
            // Names may contain '/', which Transform.Find treats as a hierarchy path.
            foreach (var button in nav.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                if (button.transform.parent == nav && Array.IndexOf(removed, button.name) >= 0) UnityEngine.Object.DestroyImmediate(button.gameObject);
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Game/Content/UI/Nunito.ttf");
            var animals = farm.GetComponentInChildren<AnimalSystem>(true); var orchard = farm.GetComponentInChildren<OrchardSystem>(true);
            menu.ConfigureGameplayReference(font, farm.GetComponentInChildren<FarmCatalog>(true), animals, orchard, farm.GetComponentInChildren<SeedShopSystem>(true));
            farm.GetComponentInChildren<MyLittleFarm.Gameplay.World.FarmProgression>(true).ConfigureGuideNavigation(menu);
            menu.GetComponentInChildren<FarmInventoryUI>(true).ConfigureReferenceDetails(font, orchard, animals);
            menu.Preview(FarmMenuUI.Page.Game); EditorUtility.SetDirty(menu);
        }
    }
}
