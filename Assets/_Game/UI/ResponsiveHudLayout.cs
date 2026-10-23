using UnityEngine;
using UnityEngine.UI;
using MyLittleFarm.Gameplay.Building;

namespace MyLittleFarm.UI
{
    /// <summary>Compact currency/time row, hotbar and contextual action prompts.</summary>
    [ExecuteAlways]
    public sealed class ResponsiveHudLayout : MonoBehaviour
    {
        public enum PreviewMode { Exploration, BuildAllowed, BuildBlocked }
        [SerializeField] private PreviewMode editorPreview;
        [SerializeField] private BuildPromptUI buildPrompt;
        [SerializeField] private RectTransform statsPanel;
        [SerializeField] private Text stats, help, status, onboarding;
        [SerializeField] private OnboardingOverlay onboardingOverlay;
        [SerializeField] private Text worldClock;
        [SerializeField] private RectTransform clockSpeedButton;
        [SerializeField] private Text prompt;
        [SerializeField] private RectTransform quickSlots;
        [SerializeField] private RectTransform coinIcon, clockPanel;
        private RectTransform _canvas;
        private Vector2 _canvasSize;
        private Rect _safeArea;
        private int _width, _height;
        private bool _styled, _buildingVisible, _layoutDirty;

        public void Configure(RectTransform panel, Text statsText, Text helpText, Text statusText,
            Text onboardingText, Text promptText, RectTransform slots, BuildPromptUI constructionPrompt = null)
        {
            statsPanel = panel; stats = statsText; help = helpText; status = statusText;
            onboarding = onboardingText; prompt = promptText; quickSlots = slots;
            if (constructionPrompt != null) buildPrompt = constructionPrompt;
            ApplyEditorPreview(); ApplyLayout();
        }
        private void OnEnable() { _styled = false; ApplyEditorPreview(); ApplyLayout(); }
        private void OnValidate() => _layoutDirty = true;
        public void SetEditorPreview(PreviewMode mode)
        {
            if (Application.IsPlaying(gameObject)) return;
            editorPreview = mode; ApplyEditorPreview(); ApplyLayout();
        }
        public void ApplyEditorPreview()
        {
            if (Application.IsPlaying(gameObject)) return;
            if (stats != null) stats.text = "—";
            if (worldClock != null) worldClock.text = "08:00 · Утро";
            if (prompt != null) prompt.text = "Наведите курсор на объект или грядку";
            if (status != null) { status.text = ""; status.enabled = true; }
            HideGuides();
            var building = editorPreview != PreviewMode.Exploration;
            if (buildPrompt != null) buildPrompt.Show(building, BuildSystem.PlacementPrompt(
                BuildingDefinition.Catalog[0], 0, false, editorPreview == PreviewMode.BuildAllowed, "Место занято"));
            if (prompt != null) prompt.enabled = !building;
        }
        private void HideGuides()
        {
            if (help != null) { help.text = ""; help.gameObject.SetActive(false); }
            if (onboarding != null) { onboarding.text = ""; onboarding.gameObject.SetActive(false); }
            if (onboardingOverlay != null) onboardingOverlay.HidePresentation();
        }
        private void LateUpdate()
        {
            if (_canvas == null) _canvas = GetComponent<RectTransform>();
            if (_layoutDirty) { _styled = false; ApplyEditorPreview(); _layoutDirty = false; }
            if (!_styled || _width != Screen.width || _height != Screen.height || _safeArea != Screen.safeArea
                || (_canvas != null && _canvasSize != _canvas.rect.size)
                || _buildingVisible != (buildPrompt != null && buildPrompt.gameObject.activeSelf)) ApplyLayout();
        }
        private void ApplyLayout()
        {
            if (_canvas == null) _canvas = GetComponent<RectTransform>();
            if (_canvas == null || statsPanel == null || stats == null || quickSlots == null) return;
            _width = Screen.width; _height = Screen.height; _safeArea = Screen.safeArea; _canvasSize = _canvas.rect.size;
            var left = _safeArea.xMin / Mathf.Max(1, Screen.width) * _canvasSize.x;
            var right = (Screen.width - _safeArea.xMax) / Mathf.Max(1, Screen.width) * _canvasSize.x;
            var top = (Screen.height - _safeArea.yMax) / Mathf.Max(1, Screen.height) * _canvasSize.y;
            var bottom = _safeArea.yMin / Mathf.Max(1, Screen.height) * _canvasSize.y;
            var width = Mathf.Max(80, _canvasSize.x - left - right);
            var center = (left - right) * .5f;
            _buildingVisible = buildPrompt != null && buildPrompt.gameObject.activeSelf;
            if (!_styled)
            {
                var image = statsPanel.GetComponent<Image>();
                if (image != null) { FarmUITheme.Skin(image, "panel"); image.raycastTarget = false; }
                stats.color = FarmUITheme.Ink; stats.supportRichText = false;
                if (worldClock != null)
                {
                    worldClock.color = FarmUITheme.Ink;
                    var shadow = worldClock.GetComponent<Shadow>(); if (shadow != null) shadow.enabled = false;
                }
                foreach (var text in new[] { status, prompt })
                {
                    if (text == null) continue; text.raycastTarget = false;
                    var shadow = text.GetComponent<Shadow>() ?? text.gameObject.AddComponent<Shadow>();
                    shadow.effectColor = new Color(.03f, .06f, .04f, .85f); shadow.effectDistance = new Vector2(1, -1);
                }
                HideGuides(); _styled = true;
            }
            Place(statsPanel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(left + 20, -top - 18), new Vector2(220, 76));
            Place(stats.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(74, 1), new Vector2(132, 56));
            stats.fontSize = 32; stats.resizeTextForBestFit = true; stats.resizeTextMinSize = 17; stats.resizeTextMaxSize = 32;
            stats.alignment = TextAnchor.MiddleLeft; stats.raycastTarget = false;
            if (coinIcon != null) Place(coinIcon, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(12, 1), new Vector2(54, 54));
            if (clockPanel != null)
                Place(clockPanel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(left + (width < 510 ? 20 : 252), -top - (width < 510 ? 104 : 18)), new Vector2(254, 76));
            if (worldClock != null)
            {
                Place(worldClock.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(53, 1), new Vector2(143, 52));
                worldClock.alignment = TextAnchor.MiddleLeft; worldClock.fontSize = 25; worldClock.resizeTextForBestFit = true;
                worldClock.resizeTextMinSize = 18; worldClock.resizeTextMaxSize = 25; worldClock.raycastTarget = false;
            }
            if (clockSpeedButton != null)
                Place(clockSpeedButton, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-8, 2), new Vector2(44, 48));
            Place(quickSlots, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(center, bottom + 12), new Vector2(Mathf.Min(524, width - 24), 98));
            if (prompt != null)
            {
                Place(prompt.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(center, bottom + 122), new Vector2(Mathf.Min(1100, width - 24), 56));
                prompt.fontSize = 24; prompt.resizeTextForBestFit = true; prompt.resizeTextMinSize = 14; prompt.resizeTextMaxSize = 24;
                prompt.horizontalOverflow = HorizontalWrapMode.Wrap; prompt.verticalOverflow = VerticalWrapMode.Truncate;
                prompt.alignment = TextAnchor.MiddleCenter;
            }
            if (buildPrompt != null)
                Place(buildPrompt.GetComponent<RectTransform>(), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(center, bottom + 122), new Vector2(Mathf.Min(1100, width - 24), BuildPromptUI.Height));
            if (status != null)
            {
                Place(status.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(center, bottom + 132 + (_buildingVisible ? BuildPromptUI.Height : 56)), new Vector2(Mathf.Min(850, width - 24), 48));
                status.alignment = TextAnchor.MiddleCenter; status.fontSize = 23;
            }
        }
        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        { FarmUITheme.Place(rect, anchor, pivot, position, size); }
    }
}