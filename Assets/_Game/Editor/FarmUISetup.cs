using System;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.Farming;
using MyLittleFarm.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace MyLittleFarm.Editor
{
    /// <summary>Explicit, idempotent UI authoring; no farm initialization, save access or profile writes.</summary>
    public static class FarmUISetup
    {
        [MenuItem("Tools/My Little Farm/UI/Apply FarmGame UI Kit")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
            var farm = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
            if (farm == null || farm.gameObject.scene.path != "Assets/_Game/Scenes/Prototype.unity") throw new InvalidOperationException("Open Prototype.");
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Game/Resources/FarmUI", "Assets/_Game/Content/UI" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null || importer.textureType == TextureImporterType.Sprite) continue;
                importer.SaveAndReimport();
            }
            FarmUITheme.ClearCache();
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Game/Content/UI/Nunito.ttf");
            if (font == null) throw new MissingReferenceException("Nunito font is missing.");
            var hud = farm.GetComponentInChildren<HUDController>(true);
            var slots = farm.GetComponentInChildren<SaveSlotsUI>(true);
            var saves = farm.GetComponentInChildren<SaveSystem>(true);
            var input = farm.GetComponent<InputReader>();
            var journal = farm.GetComponentInChildren<FarmJournalUI>(true);
            var catalogue = farm.GetComponentInChildren<BuildCustomizationUI>(true);
            if (hud == null || slots == null || saves == null || input == null || journal == null || catalogue == null) throw new MissingReferenceException("Baked farm systems missing.");
            Undo.RegisterFullObjectHierarchyUndo(hud.gameObject, "Apply Farm UI kit");
            ApplyExisting(hud.transform, font);
            foreach (var card in slots.GetComponentsInChildren<SaveSlotCard>(true)) card.EnsureDeleteButton(font);
            var menu = hud.GetComponentInChildren<FarmMenuUI>(true);
            if (menu == null) menu = FarmMenuUI.Create(hud.transform, font, input, saves, slots, journal, catalogue,
                farm.GetComponentInChildren<BuildSystem>(true), farm.GetComponentInChildren<InventorySystem>(true), farm.GetComponentInChildren<WalletSystem>(true),
                farm.GetComponentInChildren<FarmCatalog>(true), farm.GetComponentInChildren<QuickSlotSystem>(true), farm.GetComponentInChildren<SeedShopSystem>(true), farm.GetComponentInChildren<SellingSystem>(true));
            menu.transform.SetAsLastSibling();
            RemoveTitlePresentation(menu);
            menu.ApplyTheme();
            SetReference(saves, "mainMenu", menu); SetReference(slots, "mainMenu", menu);
            SetReference(input, "navigationPointerBlock", menu.transform.Find("HUD navigation"));
            var quick = hud.transform.Find("Quick Slots"); if (quick == null) quick = hud.transform.Find("QuickSlots");
            if (quick != null) SetReference(input, "quickSlotsPointerBlock", quick);
            if (quick != null) FarmQuickSlotUI.Install((RectTransform)quick, input, farm.GetComponentInChildren<InventorySystem>(true),
                farm.GetComponentInChildren<FarmCatalog>(true), farm.GetComponentInChildren<QuickSlotSystem>(true), font);
            // Gameplay overlays must not retain the title artwork in their scene dependencies.
            var slotBackdrop = slots.transform.Find("Slot menu backdrop");
            if (slotBackdrop != null)
            {
                slotBackdrop.GetComponent<Image>().color = new Color(.10f, .14f, .06f, .78f);
                var artwork = slotBackdrop.Find("Menu artwork"); if (artwork != null) UnityEngine.Object.DestroyImmediate(artwork.gameObject);
            }
            foreach (var guid in AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/_Game" }))
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                var icon = FarmUITheme.Sprite("Icons/" + FarmUITheme.ItemIcon(item.ItemId)); if (icon == null) continue;
                SetReference(item, "icon", icon);
            }
            SetReference(hud, "coinSprite", FarmUITheme.Sprite("Icons/coin")); SetReference(hud, "toolSprite", FarmUITheme.Sprite("Icons/tool_hoe"));
            AddCatalogueIcons(catalogue);
            var clock = journal.ClockText;
            if (clock != null)
            {
                clock.color = Color.white;
                var shadow = clock.GetComponent<Shadow>() ?? clock.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(.08f, .12f, .04f, .9f); shadow.effectDistance = new Vector2(1, -1);
            }
            var onboarding = hud.GetComponentInChildren<OnboardingOverlay>(true);
            if (onboarding != null && onboarding.Card != null)
            {
                FarmUITheme.Skin(onboarding.Card.GetComponent<Image>(), "panel");
                foreach (var text in onboarding.Card.GetComponentsInChildren<Text>(true)) text.color = FarmUITheme.Ink;
            }
            slots.Close(); journal.ClearPreview(); menu.Preview(FarmMenuUI.Page.Game);
            CompactHudSetup.ApplyTo(hud);
            GameplayGuideSetup.ApplyTo(farm);
            GameplayMarketSetup.ApplyTo(farm);
            FarmAudioSetup.ApplyToScene(farm.gameObject.scene);
            EditorUtility.SetDirty(menu); EditorSceneManager.MarkSceneDirty(farm.gameObject.scene); EditorSceneManager.SaveScene(farm.gameObject.scene); AssetDatabase.SaveAssets();
            Debug.Log("Gameplay UI installed without title-screen objects or artwork. No player data accessed.");
        }
        private static void RemoveTitlePresentation(FarmMenuUI menu)
        {
            foreach (var path in new[] { "Screen overlay/Title background", "Screen overlay/Safe area/Main menu actions" })
            { var old = menu.transform.Find(path); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject); }
        }
        private static void SetReference(UnityEngine.Object target, string property, UnityEngine.Object value)
        { var data = new SerializedObject(target); var field = data.FindProperty(property); field.objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target); }
        private static void ApplyExisting(Transform root, Font font)
        {
            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                text.font = font;
                if (text.GetComponentInParent<FarmMenuUI>() != null) continue;
                // World-facing helper labels retain their high-contrast light text.
                if (text.GetComponentInParent<Button>() != null || text.GetComponentInParent<SaveSlotCard>() != null
                    || text.GetComponentInParent<SaveSlotsUI>() != null || text.GetComponentInParent<BuildCustomizationUI>() != null || text.GetComponentInParent<FarmJournalUI>() != null)
                    text.color = FarmUITheme.Ink;
                EditorUtility.SetDirty(text);
            }
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            { if (button.GetComponentInParent<FarmMenuUI>() != null || button.GetComponentInParent<FarmQuickSlotUI>() != null) continue; FarmUITheme.Style(button, "ghost"); EditorUtility.SetDirty(button); }
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                if (image.GetComponent<Button>() != null || image.GetComponent<Mask>() != null || image.GetComponent<ScrollRect>() != null || image.sprite != null) continue;
                var name = image.name.ToLowerInvariant();
                if (name.Contains("backdrop") || name.Contains("shade") || name.Contains("confirm load")) image.color = new Color(.10f, .14f, .06f, .58f);
                else if (image.GetComponent<SaveSlotCard>() != null || name.Contains("panel") || name.Contains("farm slots") || name.Contains("confirmation") || name.Contains("save slot") || name.Contains("catalogue") || name.Contains("house upgrade") || name.Contains("sign text") || name.Contains("achievement") || name == "build prompt")
                { FarmUITheme.Skin(image, name.Contains("achievement") || image.GetComponent<SaveSlotCard>() != null ? "slot" : "panel"); foreach (var text in image.GetComponentsInChildren<Text>(true)) text.color = FarmUITheme.Ink; }
                EditorUtility.SetDirty(image);
            }
        }
        private static void AddCatalogueIcons(BuildCustomizationUI catalogue)
        {
            var serialized = new SerializedObject(catalogue); var buttons = serialized.FindProperty("itemButtons");
            for (var i = 0; i < buttons.arraySize; i++)
            {
                var button = (Button)buttons.GetArrayElementAtIndex(i).objectReferenceValue; if (button.transform.Find("Catalogue icon") != null) continue;
                var definition = BuildingDefinition.Catalog[i]; var key = "b_" + definition.Id;
                if (FarmUITheme.Sprite("Icons/" + key) == null) key = "catalog";
                var icon = FarmUITheme.Icon(button.transform, key); icon.name = "Catalogue icon";
                FarmUITheme.Place(icon.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(10, 0), new Vector2(58, 58));
                var label = button.GetComponentInChildren<Text>(); label.rectTransform.offsetMin = new Vector2(76, 4);
            }
        }
        [MenuItem("Tools/My Little Farm/UI/Preview Main Menu")]
        private static void MainPreview() => FarmSceneSetup.OpenMenu();
        [MenuItem("Tools/My Little Farm/UI/Preview Gameplay")]
        private static void GamePreview() => FarmSceneSetup.OpenGame();
    }
}
