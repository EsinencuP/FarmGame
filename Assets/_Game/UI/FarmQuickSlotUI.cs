using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.Farming;
using UnityEngine;
using UnityEngine.UI;

namespace MyLittleFarm.UI
{
    /// <summary>Pointer selection and event-driven quantities on the existing six seed images.</summary>
    public sealed class FarmQuickSlotUI : MonoBehaviour
    {
        [SerializeField] private InputReader input;
        [SerializeField] private InventorySystem inventory;
        [SerializeField] private FarmCatalog catalog;
        [SerializeField] private QuickSlotSystem slots;
        [SerializeField] private Button[] buttons;
        [SerializeField] private Text[] amounts;
        [SerializeField] private Image[] seedImages;
        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            RepairIconLayout();
            GameEvents.InventoryChanged += Refresh;
            for (var i = 0; i < buttons.Length; i++) { var index = i; buttons[i].onClick.AddListener(() => Select(index)); }
            Refresh();
        }
        private void OnDisable()
        {
            if (!Application.isPlaying) return;
            GameEvents.InventoryChanged -= Refresh; foreach (var button in buttons) button.onClick.RemoveAllListeners();
        }
        private void Select(int index)
        {
            if (input.ModalInputActive || input.BuildModeActive) return;
            input.SuppressGameplayThisFrame = true; slots.Select(index);
        }
        private void Refresh()
        {
            if (catalog == null || inventory == null || catalog.Crops.Count == 0) return;
            for (var i = 0; i < amounts.Length && i < catalog.Crops.Count; i++)
            {
                amounts[i].text = inventory.GetAmount(catalog.Crops[i].SeedItemId).ToString();
                buttons[i].GetComponent<Image>().color = slots.SelectedIndex == i ? new Color(1, .87f, .43f) : Color.white;
                if (seedImages != null && i < seedImages.Length && seedImages[i] != null)
                {
                    var id = catalog.Crops[i].SeedItemId;
                    seedImages[i].sprite = catalog.TryGetItem(id, out var item) && item.Icon != null ? item.Icon : FarmUITheme.Sprite("Icons/" + FarmUITheme.ItemIcon(id));
                    seedImages[i].color = Color.white;
                }
            }
        }
        public void RepairIconLayout()
        {
            if (buttons == null) return;
            seedImages = new Image[buttons.Length];
            // Release the old layout group's driven transforms before giving icons local geometry.
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
            for (var i = 0; i < buttons.Length; i++)
            {
                var child = buttons[i].transform.Find("Seed Icon " + (i + 1));
                if (child == null) continue;
                var image = child.GetComponent<Image>(); seedImages[i] = image;
                FarmUITheme.Place(image.rectTransform, Vector2.one * .5f, Vector2.one * .5f, new Vector2(0, 3), new Vector2(52, 50));
                image.enabled = true; image.gameObject.SetActive(true); image.raycastTarget = false; image.preserveAspect = true; image.color = Color.white;
                image.sprite = FarmUITheme.Sprite("Icons/" + FarmUITheme.ItemIcon(FarmCatalog.CropIds[i] + "_seed"));
            }
        }
        public static void Install(RectTransform bar, InputReader input, InventorySystem inventory, FarmCatalog catalog, QuickSlotSystem slots, Font font)
        {
            var existing = bar.GetComponent<FarmQuickSlotUI>();
            if (existing != null) { existing.RepairIconLayout(); return; }
            var ui = bar.gameObject.AddComponent<FarmQuickSlotUI>(); ui.input = input; ui.inventory = inventory; ui.catalog = catalog; ui.slots = slots;
            ui.buttons = new Button[6]; ui.amounts = new Text[6];
            FarmUITheme.Skin(bar.GetComponent<Image>(), "panel");
            var layout = bar.GetComponent<HorizontalLayoutGroup>(); layout.padding = new RectOffset(14, 14, 10, 10); layout.spacing = 8;
            // Reparent rather than replace: HUDController and bootstrap keep their authored image references.
            var images = new Image[6]; for (var i = 0; i < 6; i++) images[i] = bar.Find("Seed Icon " + (i + 1)).GetComponent<Image>();
            for (var i = 0; i < 6; i++)
            {
                var image = FarmUITheme.Image(bar, "Seed slot " + (i + 1), "Skin/slot"); image.rectTransform.sizeDelta = new Vector2(76, 78);
                ui.buttons[i] = image.gameObject.AddComponent<Button>(); ui.buttons[i].targetGraphic = image;
                images[i].transform.SetParent(image.transform, false); FarmUITheme.Stretch(images[i].rectTransform, 10);
                images[i].rectTransform.offsetMin = new Vector2(12, 20); images[i].raycastTarget = false;
                images[i].sprite = FarmUITheme.Sprite("Icons/" + FarmUITheme.ItemIcon(FarmCatalog.CropIds[i] + "_seed")); images[i].color = Color.white; images[i].preserveAspect = true;
                var key = images[i].transform.Find("Key Label"); if (key != null) key.gameObject.SetActive(false);
                var number = FarmUITheme.Label(image.transform, (i + 1).ToString(), font, 17); number.alignment = TextAnchor.UpperLeft; FarmUITheme.Stretch(number.rectTransform, 7);
                ui.amounts[i] = FarmUITheme.Label(image.transform, "—", font, 19); ui.amounts[i].alignment = TextAnchor.LowerRight; FarmUITheme.Stretch(ui.amounts[i].rectTransform, 7);
            }
            ui.RepairIconLayout();
        }
    }
}
