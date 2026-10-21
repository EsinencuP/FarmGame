using UnityEngine;
using UnityEngine.UI;

namespace MyLittleFarm.UI
{
    /// <summary>Owns the dedicated construction card; it never reads or initializes the farm.</summary>
    public sealed class BuildPromptUI : MonoBehaviour
    {
        public const float Height = 180f;
        [SerializeField] private Text promptText;
        public Text PromptText => promptText;

        public void Configure(Text text)
        {
            promptText = text;
            text.raycastTarget = false;
            text.fontSize = 20;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 16;
            text.resizeTextMaxSize = 20;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.alignment = TextAnchor.MiddleCenter;
            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(18f, 12f); rect.offsetMax = new Vector2(-18f, -12f);
            GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Height);
        }

        public void Show(bool visible, string content)
        {
            if (promptText != null && promptText.text != content) promptText.text = content;
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }

        /// <summary>Called only by the explicit scene baker/setup (or isolated test fixtures).</summary>
        public static BuildPromptUI Create(Transform parent, Font font)
        {
            var panel = new GameObject("Build Prompt", typeof(RectTransform), typeof(Image), typeof(BuildPromptUI));
            panel.transform.SetParent(parent, false);
            var background = panel.GetComponent<Image>();
            background.color = new Color(0.055f, 0.105f, 0.075f, 0.95f);
            background.raycastTarget = false;
            var textObject = new GameObject("Build Prompt Text", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(panel.transform, false);
            var text = textObject.GetComponent<Text>();
            text.font = font; text.color = new Color(0.98f, 0.96f, 0.87f);
            var card = panel.GetComponent<BuildPromptUI>();
            card.Configure(text);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 86f); rect.sizeDelta = new Vector2(1100f, Height);
            card.Show(false, string.Empty);
            return card;
        }
    }
}
