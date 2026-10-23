using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MyLittleFarm.UI
{
    /// <summary>Shared tokens and real uGUI controls derived from the supplied FarmGame_UI kit.</summary>
    public static class FarmUITheme
    {
        public static readonly Color Ink = new Color32(59, 42, 26, 255);
        public static readonly Color Muted = new Color32(138, 117, 96, 255);
        public static readonly Color Cream = new Color32(255, 244, 220, 255);
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        public static void ClearCache() => Sprites.Clear();
        public static Sprite Sprite(string path)
        {
            if (!Sprites.TryGetValue(path, out var sprite) || sprite == null)
                Sprites[path] = sprite = Resources.Load<Sprite>("FarmUI/" + path);
            return sprite;
        }
        public static string ItemIcon(string id)
        {
            if (id == "egg" || id == "apple") return id == "egg" ? "egg" : "crop_apple";
            if (id.EndsWith("_seed"))
            {
                var crop = id.Substring(0, id.Length - 5);
                return crop == "potato" || crop == "cucumber" ? "crop_" + crop : "seeds_" + crop;
            }
            return "crop_" + id;
        }
        public static RectTransform Rect(Transform parent, string name)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); return rect;
        }
        public static void Stretch(RectTransform rect, float margin = 0)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.one * margin; rect.offsetMax = Vector2.one * -margin; }
        public static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        { rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size; }
        public static Image Image(Transform parent, string name, string sprite = null)
        {
            var rect = Rect(parent, name); var image = rect.gameObject.AddComponent<Image>();
            image.color = Color.white;
            if (sprite != null) { image.sprite = Sprite(sprite); image.type = UnityEngine.UI.Image.Type.Sliced; }
            return image;
        }
        public static Image Icon(Transform parent, string key)
        { var icon = Image(parent, "Icon " + key); icon.sprite = Sprite("Icons/" + key); icon.preserveAspect = true; icon.raycastTarget = false; return icon; }
        public static void Skin(Image image, string skin)
        { image.sprite = Sprite("Skin/" + skin); image.type = UnityEngine.UI.Image.Type.Sliced; image.color = Color.white; }
        public static Text Label(Transform parent, string content, Font font, int size = 26)
        {
            var label = Rect(parent, "Label").gameObject.AddComponent<Text>(); label.text = content;
            label.font = font; label.fontSize = size; label.fontStyle = FontStyle.Bold; label.color = Ink; label.raycastTarget = false;
            label.alignment = TextAnchor.MiddleCenter; label.supportRichText = false;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = Mathf.Min(18, size); label.resizeTextMaxSize = size;
            Stretch(label.rectTransform); return label;
        }
        public static Button Button(Transform parent, string title, Font font, string icon = null, string kind = "primary")
        {
            var image = Image(parent, title, "Skin/" + kind); var button = image.gameObject.AddComponent<Button>();
            MyLittleFarm.Audio.UIAudioFeedback.Install(button);
            button.targetGraphic = image; Style(button, kind);
            var label = Label(image.transform, title, font, 28); Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(icon == null ? 14 : 66, 10); label.rectTransform.offsetMax = new Vector2(-14, -10);
            label.color = kind == "ghost" ? Ink : Color.white;
            if (icon != null) Place(Icon(image.transform, icon).rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(16, 3), new Vector2(42, 42));
            return button;
        }
        public static void Style(Button button, string kind = "primary")
        {
            if (button.targetGraphic is Image image) Skin(image, kind);
            var label = button.GetComponentInChildren<Text>(true);
            if (label != null) label.color = kind == "ghost" ? Ink : Color.white;
            var colors = button.colors; colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1, 1, .78f); colors.selectedColor = new Color(1, .92f, .53f);
            colors.pressedColor = new Color(.72f, .78f, .62f); colors.disabledColor = new Color(.65f, .64f, .59f, .65f);
            colors.fadeDuration = .08f; button.colors = colors;
        }
        public static RectTransform Panel(Transform parent, string title, Font font, Vector2 size)
        {
            var panel = Image(parent, title, "Skin/panel").rectTransform;
            Place(panel, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, size);
            var ribbon = Image(panel, "Title ribbon", "Skin/ribbon"); ribbon.raycastTarget = false;
            Place(ribbon.rectTransform, new Vector2(.5f, 1), Vector2.one * .5f, new Vector2(0, -3), new Vector2(Mathf.Min(520, size.x - 96), 74));
            Label(ribbon.transform, title, font, 34).color = Cream; return panel;
        }
        public static void Focus(Selectable control)
        { if (Application.isPlaying && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(control == null ? null : control.gameObject); }
    }
}
