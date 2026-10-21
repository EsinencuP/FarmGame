using UnityEngine;
using UnityEngine.UI;
using MyLittleFarm.Gameplay.Building;

namespace MyLittleFarm.UI
{
    /// <summary>Keeps the existing HUD readable when the Game view is narrow or resized.</summary>
    [ExecuteAlways]
    public sealed class ResponsiveHudLayout : MonoBehaviour
    {
        public enum PreviewMode { Exploration, BuildAllowed, BuildBlocked }
        [Header("Edit Mode only — illustrative data, no gameplay initialization")]
        [SerializeField] private PreviewMode editorPreview;
        [SerializeField] private BuildPromptUI buildPrompt;
        [SerializeField] private RectTransform statsPanel;
        [SerializeField] private UnityEngine.UI.Text stats;
        [SerializeField] private UnityEngine.UI.Text help;
        [SerializeField] private UnityEngine.UI.Text status;
        [SerializeField] private UnityEngine.UI.Text onboarding;
        [SerializeField] private UnityEngine.UI.Text prompt;
        [SerializeField] private RectTransform quickSlots;

        private int _width;
        private int _height;
        private RectTransform _canvas;
        private Vector2 _canvasSize;
        private Rect _safeArea;
        private bool _styled;
        private bool _buildingVisible;
        private bool _layoutDirty;

        public void Configure(RectTransform panel, UnityEngine.UI.Text statsText,
            UnityEngine.UI.Text helpText, UnityEngine.UI.Text statusText,
            UnityEngine.UI.Text onboardingText, UnityEngine.UI.Text promptText,
            RectTransform slots, BuildPromptUI constructionPrompt = null)
        {
            statsPanel = panel;
            stats = statsText;
            help = helpText;
            status = statusText;
            onboarding = onboardingText;
            prompt = promptText;
            quickSlots = slots;
            if (constructionPrompt != null) buildPrompt = constructionPrompt;
            ApplyEditorPreview();
            ApplyLayout();
        }

        private void OnEnable()
        {
            _styled = false;
            ApplyEditorPreview();
            ApplyLayout();
        }

        private void OnValidate() => _layoutDirty = true;

        public void SetEditorPreview(PreviewMode mode)
        {
            if (Application.IsPlaying(gameObject)) return;
            editorPreview = mode;
            ApplyEditorPreview();
            ApplyLayout();
        }

        public void ApplyEditorPreview()
        {
            if (Application.IsPlaying(gameObject) || stats == null) return;
            stats.text = "<b>МОНЕТЫ  —</b>\nСЕМЕНА / УРОЖАЙ\n<color=#F0CF78><b>1  Морковь  — / —</b></color>\n2  Картофель  — / —\n3  Пшеница  — / —\n4  Томат  — / —\n5  Кукуруза  — / —\n6  Огурец  — / —\nСЕКТОР  L\nИНСТРУМЕНТ  U\nЯБЛОКИ —   ЯЙЦА —   КУРЫ —\nP: купить семена";
            if (prompt != null) prompt.text = "Наведите курсор на объект или грядку";
            if (status != null) status.text = string.Empty;
            if (onboarding != null) onboarding.text = "Постройте дом (B → 1 → ЛКМ)";
            var building = editorPreview != PreviewMode.Exploration;
            if (buildPrompt != null) buildPrompt.Show(building, BuildSystem.PlacementPrompt(
                BuildingDefinition.Catalog[0], 0, false, editorPreview == PreviewMode.BuildAllowed, "Место занято"));
            if (prompt != null) prompt.enabled = !building;
            if (status != null) status.enabled = true;
            if (onboarding != null) onboarding.enabled = true;
        }

        private void LateUpdate()
        {
            if (_canvas == null) _canvas = GetComponent<RectTransform>();
            if (_layoutDirty)
            {
                _styled = false;
                ApplyEditorPreview();
                _layoutDirty = false;
            }
            if (!_styled || _width != Screen.width || _height != Screen.height || _safeArea != Screen.safeArea
                || (_canvas != null && _canvasSize != _canvas.rect.size)
                || _buildingVisible != (buildPrompt != null && buildPrompt.gameObject.activeSelf)) ApplyLayout();
        }

        private void ApplyLayout()
        {
            _width = Screen.width;
            _height = Screen.height;
            if (_canvas == null) _canvas = GetComponent<RectTransform>();
            if (_canvas == null || statsPanel == null || stats == null || quickSlots == null) return;
            _canvasSize = _canvas.rect.size;
            _safeArea = Screen.safeArea;
            var canvasWidth = _canvas.rect.width > 0f ? _canvas.rect.width : 1920f;
            var canvasHeight = Mathf.Max(1f, _canvas.rect.height);
            var leftInset = _safeArea.xMin / Mathf.Max(1, Screen.width) * canvasWidth;
            var rightInset = (Screen.width - _safeArea.xMax) / Mathf.Max(1, Screen.width) * canvasWidth;
            var topInset = (Screen.height - _safeArea.yMax) / Mathf.Max(1, Screen.height) * canvasHeight;
            var bottomInset = _safeArea.yMin / Mathf.Max(1, Screen.height) * canvasHeight;
            var width = Mathf.Max(80f, canvasWidth - leftInset - rightInset);
            var centerOffset = (leftInset - rightInset) * 0.5f;
            var narrow = width < 1050f;
            _buildingVisible = buildPrompt != null && buildPrompt.gameObject.activeSelf;
            var promptHeight = _buildingVisible ? BuildPromptUI.Height : 56f;
            var panelWidth = Mathf.Min(390f, width - 32f);
            var panelHeight = Mathf.Min(380f, Mathf.Max(160f, canvasHeight - topInset - bottomInset - 330f));
            if (!_styled)
            {
                var background = statsPanel.GetComponent<Image>();
                if (background != null)
                {
                    background.color = new Color(0.075f, 0.13f, 0.095f, 0.92f);
                    background.raycastTarget = false;
                }
                stats.color = new Color(0.96f, 0.95f, 0.86f);
                foreach (var text in new[] { help, status, onboarding, prompt })
                {
                    if (text == null) continue;
                    text.raycastTarget = false;
                    var shadow = text.GetComponent<Shadow>();
                    if (shadow == null) shadow = text.gameObject.AddComponent<Shadow>();
                    shadow.effectColor = new Color(0.03f, 0.06f, 0.04f, 0.85f);
                    shadow.effectDistance = new Vector2(1f, -1f);
                }
                _styled = true;
            }

            Place(statsPanel, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(leftInset + 16f, -topInset - 16f), new Vector2(panelWidth, panelHeight));
            Place(stats.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(16f, -14f), new Vector2(panelWidth - 32f, panelHeight - 28f));
            stats.fontSize = 24;
            stats.resizeTextForBestFit = true;
            stats.resizeTextMinSize = 16;
            stats.resizeTextMaxSize = 24;
            stats.horizontalOverflow = HorizontalWrapMode.Wrap;
            stats.verticalOverflow = VerticalWrapMode.Truncate;

            Place(quickSlots, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(centerOffset, bottomInset + 12f), new Vector2(Mathf.Min(380f, width - 24f), 62f));
            if (help != null)
            {
                help.enabled = !_buildingVisible;
                if (narrow)
                {
                    help.text = "WASD ходьба  ·  E/ЛКМ действие  ·  B стройка\n1–6 семена  ·  P магазин  ·  F5/F9 сохранение";
                    Place(help.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                        new Vector2(centerOffset, bottomInset + 82f), new Vector2(width - 24f, 48f));
                    help.alignment = TextAnchor.MiddleCenter;
                    help.fontSize = 17;
                }
                else
                {
                    help.text = "WASD — движение  ·  Shift — бег  ·  Space — прыжок\nQ / Shift+E — камера  ·  B — строительство\n1–6 — семена  ·  P — магазин  ·  T — яблоня  ·  C — курица\nL — сектор  ·  U — инструмент  ·  F5 — сохранить  ·  F9 — загрузить";
                    Place(help.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                        new Vector2(-rightInset - 20f, -topInset - 145f), new Vector2(Mathf.Min(800f, width - panelWidth - 64f), 110f));
                    help.alignment = TextAnchor.UpperRight;
                    help.fontSize = 19;
                }
            }

            if (prompt != null)
            {
                Place(prompt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(centerOffset, bottomInset + (narrow ? 139f : 86f)), new Vector2(Mathf.Min(1100f, width - 24f), 56f));
                prompt.fontSize = narrow ? 20 : 24;
                prompt.horizontalOverflow = HorizontalWrapMode.Wrap;
                prompt.resizeTextForBestFit = true;
                prompt.resizeTextMinSize = 14;
                prompt.resizeTextMaxSize = prompt.fontSize;
                prompt.verticalOverflow = VerticalWrapMode.Truncate;
                prompt.alignment = TextAnchor.MiddleCenter;
            }
            if (buildPrompt != null)
                Place(buildPrompt.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(centerOffset, bottomInset + (narrow ? 139f : 86f)),
                    new Vector2(Mathf.Min(1100f, width - 24f), BuildPromptUI.Height));
            if (status != null)
            {
                Place(status.rectTransform, narrow ? new Vector2(0.5f, 0f) : new Vector2(1f, 1f),
                    narrow ? new Vector2(0.5f, 0f) : new Vector2(1f, 1f),
                    narrow ? new Vector2(centerOffset, bottomInset + 139f + promptHeight + 10f) : new Vector2(-rightInset - 20f, -topInset - 16f),
                    new Vector2(narrow ? width - 24f : Mathf.Min(800f, width - panelWidth - 64f), 50f));
                status.fontSize = narrow ? 20 : 23;
            }
            if (onboarding != null)
            {
                Place(onboarding.rectTransform, narrow ? new Vector2(0.5f, 0f) : new Vector2(1f, 1f),
                    narrow ? new Vector2(0.5f, 0f) : new Vector2(1f, 1f),
                    narrow ? new Vector2(centerOffset, bottomInset + 202f + promptHeight + 10f) : new Vector2(-rightInset - 20f, -topInset - 78f),
                    new Vector2(narrow ? width - 24f : Mathf.Min(800f, width - panelWidth - 64f), 56f));
                onboarding.fontSize = narrow ? 20 : 23;
            }
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot,
            Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
