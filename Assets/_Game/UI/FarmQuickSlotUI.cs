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
        private Text[] _keyLabels;
        private int _bindingRevision = -1;
        private Button _hand,_hoe;
        private MyLittleFarm.Gameplay.World.ToolUpgradeSystem _tools;
        public bool HasTools=>_hand!=null;
        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            RepairIconLayout();
            if(buttons==null || slots==null)return;
            EnsureTools();
            _keyLabels = new Text[buttons.Length];
            for (var i = 0; i < buttons.Length; i++)
                foreach (var label in buttons[i].GetComponentsInChildren<Text>())
                    if (label.alignment == TextAnchor.UpperLeft) { _keyLabels[i] = label; break; }
            _bindingRevision = -1;
            RefreshKeyLabels();
            GameEvents.InventoryChanged += Refresh;
            for (var i = 0; i < buttons.Length; i++)
            {
                var index = i; buttons[i].onClick.AddListener(() => Select(index));
                FarmItemDrag.Install(buttons[i].gameObject,()=>
                {
                    if(input.ModalInputActive || input.BuildModeActive)return null;
                    input.SuppressGameplayThisFrame=true;
                    var id=slots.ItemAt(index);return id==null?null:FarmItemDrag.Snapshot(FarmItemDrag.Source.QuickSlot,id,0,index,null,inventory,slots,null,Icon(id));
                },p=>{if(!input.ModalInputActive&&!input.BuildModeActive){input.SuppressGameplayThisFrame=true;FarmItemDrag.DropOnSlot(p,index,inventory,slots);}});
            }
            Refresh();
        }
        private void Update() => RefreshKeyLabels();

        private void RefreshKeyLabels()
        {
            if (_keyLabels == null || _bindingRevision == InputReader.BindingRevision) return;
            _bindingRevision = InputReader.BindingRevision;
            for (var i = 0; i < _keyLabels.Length && i < 6; i++)
                if (_keyLabels[i] != null) _keyLabels[i].text = InputReader.KeyLabel(InputReader.Command.Seed1 + i);
        }
        private void OnDisable()
        {
            if (!Application.isPlaying) return;
            FarmItemDrag.Cancel();GameEvents.InventoryChanged -= Refresh; if(buttons!=null)foreach (var button in buttons) button.onClick.RemoveAllListeners();
        }
        private void Select(int index)
        {
            if (input.ModalInputActive || input.BuildModeActive) return;
            input.SuppressGameplayThisFrame = true; slots.Select(index);
        }
        private void Refresh()
        {
            if (catalog == null || inventory == null || slots==null || amounts==null) return;
            for (var i = 0; i < amounts.Length; i++)
            {
                var id=slots.ItemAt(i);
                amounts[i].text = id==null?"—":id=="apple_sapling"?"∞":inventory.GetAmount(id).ToString();
                buttons[i].GetComponent<Image>().color = slots.Mode==QuickSlotSystem.SelectionMode.Item && slots.SelectedIndex == i ? new Color(1, .87f, .43f) : Color.white;
                if (seedImages != null && i < seedImages.Length && seedImages[i] != null)
                {
                    seedImages[i].enabled=id!=null;
                    if(id!=null)seedImages[i].sprite=Icon(id);
                    seedImages[i].color = Color.white;
                }
            }
            if(_hand!=null)_hand.GetComponent<Image>().color=slots.Mode==QuickSlotSystem.SelectionMode.Hand?new Color(1,.87f,.43f):Color.white;
            if(_hoe!=null && _tools!=null)_hoe.GetComponentInChildren<Text>().text="Мотыга\n"+(_tools.Level==0?"1×1":_tools.AreaLabel);
            if(_hoe!=null)_hoe.GetComponent<Image>().color=slots.Mode==QuickSlotSystem.SelectionMode.Hoe?new Color(1,.87f,.43f):Color.white;
        }
        private Sprite Icon(string id)=>catalog.TryGetItem(id,out var item)&&item.Icon!=null?item.Icon:FarmUITheme.Sprite("Icons/"+FarmUITheme.ItemIcon(id))??FarmUITheme.Sprite("Icons/inventory");
        private void EnsureTools()
        {
            if(_hand!=null)return;
            _tools=slots.GetComponentInParent<GameBootstrap>()?.GetComponentInChildren<MyLittleFarm.Gameplay.World.ToolUpgradeSystem>(true);
            var font=amounts[0].font;
            _hand=FarmUITheme.Button(transform,"Руки",font,null,"secondary");
            _hoe=FarmUITheme.Button(transform,"Мотыга",font,null,"secondary");
            _hand.transform.SetSiblingIndex(0);_hoe.transform.SetSiblingIndex(1);
            foreach(var b in new[]{_hand,_hoe}){((RectTransform)b.transform).sizeDelta=new Vector2(76,78);FarmUITheme.FitCompactButton(b,17);var label=b.GetComponentInChildren<Text>();FarmUITheme.Stretch(label.rectTransform,2);label.resizeTextMinSize=12;}
            _hand.onClick.AddListener(()=>Tool(QuickSlotSystem.SelectionMode.Hand));_hoe.onClick.AddListener(()=>Tool(QuickSlotSystem.SelectionMode.Hoe));
            // Preserve the six authored item references and include the tools in the same pointer blocker.

        }
        private void Tool(QuickSlotSystem.SelectionMode mode)
        {if(input.ModalInputActive || input.BuildModeActive)return;input.SuppressGameplayThisFrame=true;slots.SelectTool(mode);}
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
            if(Application.isPlaying)ui.OnEnable();
        }
    }
}
