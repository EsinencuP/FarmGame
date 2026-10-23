using System;
using MyLittleFarm.Core;
using MyLittleFarm.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MyLittleFarm.Editor
{
    /// <summary>UI-only migration; preserves world, inventory, tutorial progress and player files.</summary>
    public static class CompactHudSetup
    {
        [MenuItem("Tools/My Little Farm/UI/Apply Compact Gameplay HUD")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
            if (SceneManager.GetActiveScene().path != FarmSceneFlow.GameScene) FarmSceneSetup.OpenGame();
            var hud = UnityEngine.Object.FindAnyObjectByType<HUDController>();
            Undo.RegisterFullObjectHierarchyUndo(hud.gameObject, "Compact gameplay HUD");
            ApplyTo(hud);
            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene); EditorSceneManager.SaveScene(hud.gameObject.scene);
            // Keep the same help page in front-end settings, without changing the title buttons.
            var title = EditorSceneManager.OpenScene(FarmSceneFlow.MenuScene, OpenSceneMode.Additive);
            foreach (var root in title.GetRootGameObjects()) foreach (var settings in root.GetComponentsInChildren<FarmSettingsUI>(true))
            { settings.EnsureControlsGuide(); EditorUtility.SetDirty(settings); }
            EditorSceneManager.MarkSceneDirty(title); EditorSceneManager.SaveScene(title); EditorSceneManager.CloseScene(title, true);
            AssetDatabase.SaveAssets();
        }
        public static void ApplyTo(HUDController hud)
        {
            var layout = hud.GetComponent<ResponsiveHudLayout>(); var serialized = new SerializedObject(layout);
            var panel = (RectTransform)serialized.FindProperty("statsPanel").objectReferenceValue;
            var coin = Find<Image>(hud.transform, "Coin Icon");
            coin.transform.SetParent(panel, false); coin.gameObject.SetActive(true); coin.enabled = true; coin.sprite = FarmUITheme.Sprite("Icons/coin");
            coin.color = Color.white; coin.preserveAspect = true; coin.raycastTarget = false;
            var journal = hud.GetComponentInChildren<FarmJournalUI>(true);
            var clockPanel = hud.transform.Find("World Clock Panel") as RectTransform;
            if (clockPanel == null)
            {
                clockPanel = FarmUITheme.Image(hud.transform, "World Clock Panel", "Skin/panel").rectTransform;
                clockPanel.GetComponent<Image>().raycastTarget = false;
                var icon = FarmUITheme.Icon(clockPanel, "clock");
                FarmUITheme.Place(icon.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(10, 1), new Vector2(36, 40));
            }
            journal.ClockText.transform.SetParent(clockPanel, false);
            var speed = hud.GetComponentInChildren<ClockSpeedUI>(true);
            if (speed != null)
            {
                speed.transform.SetParent(clockPanel, false);
                var label = speed.GetComponentInChildren<Text>(true); label.text = "×1"; label.fontSize = 20; label.resizeTextForBestFit = true;
            }
            serialized.FindProperty("coinIcon").objectReferenceValue = coin.rectTransform;
            serialized.FindProperty("clockPanel").objectReferenceValue = clockPanel;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            hud.UseCompactPresentation();
            hud.GetComponentInChildren<OnboardingOverlay>(true)?.HidePresentation();
            hud.GetComponentInChildren<FarmQuickSlotUI>(true)?.RepairIconLayout();
            foreach (var settings in hud.GetComponentsInChildren<FarmSettingsUI>(true)) settings.EnsureControlsGuide();
            layout.SetEditorPreview(ResponsiveHudLayout.PreviewMode.Exploration);
            // Clock stays below modal windows; keep the menu controller as the last Canvas sibling.
            hud.GetComponentInChildren<FarmMenuUI>(true)?.transform.SetAsLastSibling();
            EditorUtility.SetDirty(hud); EditorUtility.SetDirty(layout);
        }
        private static T Find<T>(Transform root, string name) where T : Component
        {
            foreach (var component in root.GetComponentsInChildren<T>(true)) if (component.name == name) return component;
            throw new MissingReferenceException(name);
        }
    }
}
