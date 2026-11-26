using UnityEngine;
using UnityEngine.UI;

namespace MyLittleFarm.UI
{
    public sealed class FarmScreenArtwork : ScriptableObject
    {
        [SerializeField] private Sprite loading;

        internal static GameObject CreateLoading(Transform parent, string message, Font font)
        {
            var backdrop = FarmUITheme.Image(parent, "Loading screen");
            FarmUITheme.Stretch(backdrop.rectTransform);
            backdrop.color = new Color32(29, 45, 34, 255);
            var artwork = Resources.Load<FarmScreenArtwork>("FarmUI/ScreenArtwork");
            if (artwork != null && artwork.loading != null)
            {
                var image = FarmUITheme.Image(backdrop.transform, "Loading illustration");
                FarmUITheme.Stretch(image.rectTransform);
                image.sprite = artwork.loading;
                image.preserveAspect = true;
                image.raycastTarget = false;
            }

            var caption = FarmUITheme.Image(backdrop.transform, "Loading caption");
            caption.color = new Color(.07f, .11f, .08f, .9f);
            FarmUITheme.Place(caption.rectTransform, new Vector2(.5f, .1f), new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(570, 84));
            var label = FarmUITheme.Label(caption.transform, message, font, 34);
            label.color = FarmUITheme.Cream;
            return backdrop.gameObject;
        }
    }
}
