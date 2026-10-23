using System;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Animals;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.Farming;
using MyLittleFarm.Gameplay.World;
using MyLittleFarm.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MyLittleFarm.Editor
{
    public static class GameplayMarketSetup
    {
        [MenuItem("Tools/My Little Farm/UI/Apply Market And Resolution Dropdown")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
            FarmSceneSetup.OpenGame();
            var farm = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
            Undo.RegisterFullObjectHierarchyUndo(farm.gameObject, "Market, resolutions and lantern shadows");
            ApplyTo(farm);
            var cycle = farm.GetComponentInChildren<DayNightCycle>(true);
            cycle.ConfigureLanternShadowFocus(farm.GetComponentInChildren<PlayerController>(true).transform);
            foreach (var lamp in farm.GetComponentsInChildren<FarmLantern>(true)) { lamp.ConfigureShadowLight(); EditorUtility.SetDirty(lamp); }
            var prefabPath = "Assets/_Game/Content/World/Lighting/FarmLantern.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
            try { prefab.GetComponent<FarmLantern>().ConfigureShadowLight(); PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath); }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            cycle.SetHour(cycle.StartingHour);
            EditorSceneManager.MarkSceneDirty(farm.gameObject.scene); EditorSceneManager.SaveScene(farm.gameObject.scene);
            // Only the newly requested graphics control and help text are migrated in the title scene.
            FarmSceneSetup.OpenMenu();
            var settings = UnityEngine.Object.FindAnyObjectByType<MainMenuUI>().GetComponentInChildren<FarmSettingsUI>(true);
            settings.EnsureResolutionDropdown(); settings.EnsureControlsGuide(); EditorUtility.SetDirty(settings);
            EditorSceneManager.MarkSceneDirty(settings.gameObject.scene); EditorSceneManager.SaveScene(settings.gameObject.scene);
            AssetDatabase.SaveAssets(); FarmSceneSetup.OpenGame();
        }
        public static void ApplyTo(GameBootstrap farm)
        {
            var system = farm.GetComponentInChildren<FarmMarket>(true);
            if (system == null) system = farm.gameObject.AddComponent<FarmMarket>();
            system.Configure(farm.GetComponentInChildren<FarmCatalog>(true), farm.GetComponentInChildren<InventorySystem>(true), farm.GetComponentInChildren<WalletSystem>(true),
                farm.GetComponentInChildren<SeedShopSystem>(true), farm.GetComponentInChildren<ToolUpgradeSystem>(true), farm.GetComponentInChildren<SectorSystem>(true),
                farm.GetComponentInChildren<AnimalSystem>(true), farm.GetComponentInChildren<BuildSystem>(true), farm.GetComponentInChildren<FarmProgression>(true));
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Game/Content/UI/Nunito.ttf");
            var menu = farm.GetComponentInChildren<FarmMenuUI>(true); menu.ConfigureMarket(font, system);
            var prompt = menu.transform.Find("Nearby market prompt").GetComponentInChildren<UnityEngine.UI.Text>(true); prompt.text = "Рынок · P / E";
            foreach (var settings in menu.GetComponentsInChildren<FarmSettingsUI>(true)) { settings.EnsureResolutionDropdown(); settings.EnsureControlsGuide(); }
            menu.Preview(FarmMenuUI.Page.Game); EditorUtility.SetDirty(system); EditorUtility.SetDirty(menu);
        }
    }
}
