using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.World;
using UnityEngine;
using UnityEngine.UI;

namespace MyLittleFarm.UI
{
    internal sealed class FishingHUD
    {
        private static readonly Color Green = new(.53f, .80f, .32f);
        private readonly RectTransform _panel;
        private readonly Text _title;
        private readonly Text _hint;
        private readonly RectTransform _controls;
        private readonly RectTransform _zone;
        private readonly RectTransform _fish;
        private readonly RectTransform _progress;
        private readonly Image _zoneFill;
        private readonly Image _progressFill;

        internal FishingHUD(Transform canvas, Font font)
        {
            _panel = FarmUITheme.Panel(canvas, "", font, new Vector2(220, 440));
            _panel.name = "Fishing HUD";
            FarmUITheme.Place(_panel, new Vector2(.70f, .5f), Vector2.one * .5f,
                new Vector2(0f, -24f), new Vector2(220f, 440f));
            var layer = _panel.gameObject.AddComponent<Canvas>();
            layer.overrideSorting = true;
            layer.sortingOrder = 20;
            _panel.GetComponent<Image>().raycastTarget = false;

            var ribbon = (RectTransform)_panel.Find("Title ribbon");
            ribbon.sizeDelta = new Vector2(204f, 56f);
            _title = ribbon.GetComponentInChildren<Text>();
            _title.resizeTextMaxSize = 26;
            _hint = FarmUITheme.Label(_panel, "", font, 23);
            FarmUITheme.Place(_hint.rectTransform, new Vector2(.5f, 0f), new Vector2(.5f, 0f),
                new Vector2(0f, 13f), new Vector2(198f, 60f));

            _controls = FarmUITheme.Rect(_panel, "Fishing controls");
            FarmUITheme.Stretch(_controls);
            var track = Block(_controls, "Fish track", new Color32(43, 56, 48, 255), new Vector2(-17, 70), new Vector2(90, 300));
            _zoneFill = FarmUITheme.Image(track, "Green capture zone");
            _zoneFill.raycastTarget = false;
            _zone = _zoneFill.rectTransform;
            _zone.offsetMin = new Vector2(4f, 0f);
            _zone.offsetMax = new Vector2(-4f, 0f);

            var fishImage = FarmUITheme.Icon(track, "crop_fish");
            _fish = fishImage.rectTransform;
            _fish.pivot = Vector2.one * .5f;
            var outline = fishImage.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.10f, .14f, .10f, .85f);
            outline.effectDistance = new Vector2(2f, -2f);

            var meter = Block(_controls, "Catch progress track", new Color32(75, 74, 53, 255), new Vector2(54, 70),
                new Vector2(18, 300));
            _progressFill = FarmUITheme.Image(meter, "Catch progress");
            _progressFill.raycastTarget = false;
            _progressFill.color = new Color32(245, 210, 118, 255);
            _progress = _progressFill.rectTransform;
            _progress.offsetMin = new Vector2(3f, 3f);
            _progress.offsetMax = new Vector2(-3f, -3f);
            Close();
        }

        private static RectTransform Block(Transform parent, string name, Color color, Vector2 position, Vector2 size)
        {
            var image = FarmUITheme.Image(parent, name);
            image.color = color;
            image.raycastTarget = false;
            FarmUITheme.Place(image.rectTransform, new Vector2(.5f, 0f), new Vector2(.5f, 0f), position, size);
            return image.rectTransform;
        }

        internal void WaitForBite(bool ready)
        {
            _panel.gameObject.SetActive(true);
            _panel.sizeDelta = new Vector2(250f, 132f);
            _controls.gameObject.SetActive(false);
            _title.text = FarmText.Get(ready ? "fishing.bite" : "fishing.wait");
            _hint.text = ready ? FarmText.Get("fishing.hook", InputReader_AI.ActionHint) : FarmText.Get("fishing.cancel");
        }

        internal void ShowChallenge(FishingChallenge challenge)
        {
            _panel.sizeDelta = new Vector2(220f, 440f);
            _controls.gameObject.SetActive(true);
            _title.text = FarmText.Get(challenge.Size switch
            {
                FishingSize.Small => "fishing.small",
                FishingSize.Medium => "fishing.medium",
                _ => "fishing.large"
            });
            _hint.text = FarmText.Get("fishing.controls");
            _fish.sizeDelta = Vector2.one * (34f + (int)challenge.Size * 7f);
            UpdateChallenge(challenge);
        }

        internal void UpdateChallenge(FishingChallenge challenge)
        {
            var half = FishingChallenge.ZoneHeight * .5f;
            _zone.anchorMin = new Vector2(0f, challenge.Zone - half);
            _zone.anchorMax = new Vector2(1f, challenge.Zone + half);
            _fish.anchorMin = _fish.anchorMax = new Vector2(.5f, challenge.Fish);
            _fish.anchoredPosition = Vector2.zero;
            _progress.anchorMin = Vector2.zero;
            _progress.anchorMax = new Vector2(1f, challenge.Progress);
            _zoneFill.color = challenge.InZone ? Green : Color.Lerp(Green, new Color(.27f, .48f, .28f), .6f);
        }

        internal void Close()
        {
            if (_panel != null) _panel.gameObject.SetActive(false);
        }

        internal void Destroy()
        {
            if (_panel != null) Object.Destroy(_panel.gameObject);
        }
    }
}
