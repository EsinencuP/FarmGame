using System;
using MyLittleFarm.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MyLittleFarm.UI
{
    /// <summary>One slot projection. Textures are transient, bounded and disposed with the card.</summary>
    public sealed class SaveSlotCard : MonoBehaviour
    {
        [SerializeField] private Text title, details, noPreview;
        [SerializeField] private RawImage preview;
        [SerializeField] private Button primary, manual;
        [SerializeField] private Text primaryLabel;
        private Texture2D _texture;
        public Button Primary => primary;
        public Button Manual => manual;

        public void Show(SaveSlotInfo slot, bool active, bool legacyBlocked)
        {
            ClearTexture();
            title.text = "СЛОТ " + (slot.Index + 1) + (active ? " · ТЕКУЩИЙ" : "");
            var latest = slot.Latest;
            primary.interactable = latest != null || (slot.IsEmpty && !legacyBlocked);
            primaryLabel.text = slot.IsEmpty ? "Начать ферму" : "Продолжить";
            manual.gameObject.SetActive(latest != null && latest.IsAutosave && slot.Manual.CanLoad);
            if (latest == null)
                details.text = legacyBlocked ? "Старый файл не удалось перенести.\nИсходные данные сохранены.\nВыберите другой пустой слот."
                    : slot.IsEmpty ? "Новая история фермы\n\nСвободный слот" : "Файл повреждён или несовместим.\nДоступной резервной копии нет.\nФайлы сохранены для восстановления.";
            else
                details.text = FormatDate(latest.SavedAtUnixMs) + "\nВ игре: " + FormatPlayTime(latest.PlayTimeSeconds)
                    + " · " + latest.Coins + " монет\nКультуры " + latest.Crops + " · Деревья " + latest.Trees
                    + "\nЖивотные " + latest.Animals + " · Постройки " + latest.Buildings
                    + "\n" + (latest.IsAutosave ? "Автосохранение" : "Ручная точка")
                    + (latest.FromBackup ? " · резервная копия" : "")
                    + (manual.gameObject.activeSelf ? "\nРучная: " + FormatDate(slot.Manual.SavedAtUnixMs) : "");
            _texture = Decode(latest?.PreviewPng);
            preview.texture = _texture; preview.enabled = _texture != null;
            noPreview.gameObject.SetActive(_texture == null);
            noPreview.text = slot.IsEmpty ? "ПУСТОЙ СЛОТ" : "Превью недоступно";
        }

        public static string FormatPlayTime(double seconds)
        {
            var minutes = (long)Math.Floor(Math.Max(0, seconds) / 60);
            return (minutes / 60) + " ч " + (minutes % 60).ToString("00") + " мин";
        }
        private static string FormatDate(long timestamp)
        {
            try { return DateTimeOffset.FromUnixTimeMilliseconds(timestamp).ToLocalTime().ToString("dd.MM.yyyy  HH:mm"); }
            catch (ArgumentOutOfRangeException) { return "Дата недоступна"; }
        }
        private static Texture2D Decode(string png)
        {
            if (string.IsNullOrEmpty(png) || png.Length > 512 * 1024) return null;
            Texture2D texture = null;
            try
            {
                var bytes = Convert.FromBase64String(png);
                if (bytes.Length < 24 || bytes[0] != 137 || bytes[1] != 80 || bytes[2] != 78 || bytes[3] != 71) return null;
                var width = BigEndian(bytes, 16); var height = BigEndian(bytes, 20);
                if (width < 1 || width > 640 || height < 1 || height > 360) return null;
                texture = new Texture2D(2, 2, TextureFormat.RGB24, false) { name = "Slot preview", hideFlags = HideFlags.DontSave };
                if (texture.LoadImage(bytes, true)) return texture;
            }
            catch (FormatException) { }
            catch (Exception error) { Debug.LogWarning("Cannot display save preview: " + error.Message); }
            if (texture != null) DestroyTexture(texture);
            return null;
        }
        private static int BigEndian(byte[] data, int at) => (data[at] << 24) | (data[at+1] << 16) | (data[at+2] << 8) | data[at+3];
        private static void DestroyTexture(Texture2D texture)
        { if (Application.isPlaying) Destroy(texture); else DestroyImmediate(texture); }
        private void ClearTexture()
        { if (preview != null) preview.texture = null; if (_texture != null) DestroyTexture(_texture); _texture = null; }
        public void ReleasePreview() { ClearTexture(); if (preview != null) preview.enabled = false; }
        private void OnDestroy() => ClearTexture();

        public void Resize(float width, bool columns)
        {
            Top(title.rectTransform, 16, 14, width - 32, 32);
            var imageWidth = columns ? Mathf.Min(width - 32, 150 * 16f / 9f) : Mathf.Min(240, width * .36f);
            var imageHeight = imageWidth * 9f / 16f;
            var imageX = columns ? (width - imageWidth) * .5f : 16;
            Top(preview.rectTransform, imageX, 56, imageWidth, imageHeight);
            Top(noPreview.rectTransform, imageX, 56, imageWidth, imageHeight);
            var x = columns ? 16 : imageWidth + 32;
            Top(details.rectTransform, x, columns ? 218 : 56, width - x - 16, columns ? 144 : 168);
            var buttonWidth = (width - 44) * .5f;
            Bottom((RectTransform)primary.transform, 16, buttonWidth);
            Bottom((RectTransform)manual.transform, width - 16 - buttonWidth, buttonWidth);
        }

        public static SaveSlotCard Create(Transform parent, Font font)
        {
            var go = new GameObject("Save Slot", typeof(RectTransform), typeof(Image), typeof(SaveSlotCard));
            go.transform.SetParent(parent, false); go.GetComponent<Image>().color = new Color(.09f,.17f,.12f);
            var card = go.GetComponent<SaveSlotCard>();
            card.title = SaveSlotsUI.Label(go.transform, "Title", font, 24);
            card.details = SaveSlotsUI.Label(go.transform, "Details", font, 18);
            card.details.alignment = TextAnchor.UpperLeft; card.details.resizeTextForBestFit = true;
            card.details.resizeTextMinSize = 14; card.details.resizeTextMaxSize = 18;
            card.noPreview = SaveSlotsUI.Label(go.transform, "No preview", font, 18);
            var image = new GameObject("Farm Preview", typeof(RectTransform), typeof(RawImage));
            image.transform.SetParent(go.transform, false); card.preview = image.GetComponent<RawImage>();
            card.preview.raycastTarget = false;
            card.primary = SaveSlotsUI.Button(go.transform, "Продолжить", font); card.primaryLabel = card.primary.GetComponentInChildren<Text>();
            card.manual = SaveSlotsUI.Button(go.transform, "Ручная точка", font);
            return card;
        }
        private static void Top(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0,1);
            rect.anchoredPosition = new Vector2(x,-y); rect.sizeDelta = new Vector2(width,height);
        }
        private static void Bottom(RectTransform rect, float x, float width)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x,16); rect.sizeDelta = new Vector2(width,44);
        }
    }
}
