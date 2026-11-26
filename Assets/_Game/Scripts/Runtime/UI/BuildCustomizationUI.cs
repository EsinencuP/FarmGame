using System;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.Gameplay.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace MyLittleFarm.UI
{
    /// <summary>Baked construction catalogue and modal editing; no gameplay initialization in Edit Mode.</summary>
    [DefaultExecutionOrder(-170)]
    public sealed class BuildCustomizationUI : MonoBehaviour
    {
        [SerializeField] private BuildSystem buildings;
        [SerializeField] private InputReader_AI input;
        [SerializeField] private Button openCatalogue;
        [SerializeField] private GameObject shade;
        [SerializeField] private RectTransform cataloguePanel,signPanel,housePanel;
        [SerializeField] private Text houseDescription,feedback;
        [SerializeField] private Button saveText,closeText,upgradeHouse,closeHouse;
        [SerializeField] private Text upgradeLabel;
        [SerializeField] private InputField signInput;
        private string _detailId;
        private bool _bound;
        private BuildCatalogueScreen _catalogue;
        private Text _shortcutLabel;
        private int _bindingRevision = -1;
        private QuickSlotSystem _slots;
        private GameObject _previousFocus;

        private void OnEnable()
        {
            if (!Application.isPlaying || buildings == null || input == null || openCatalogue == null) return;
            _slots = buildings.GetComponentInParent<GameBootstrap>()?.GetComponentInChildren<QuickSlotSystem>(true);
            _shortcutLabel = openCatalogue.GetComponentInChildren<Text>(true);
            var shortcut = (RectTransform)openCatalogue.transform;
            var menu = GetComponentInParent<Canvas>()?.GetComponentInChildren<FarmMenuUI>(true);
            if (menu != null) menu.AttachConstructionShortcut(openCatalogue);
            input.ConstructionPointerBlock = shortcut;
            FarmUITheme.Style(openCatalogue, "secondary");
            FarmUITheme.FitCompactButton(openCatalogue, 20);
            openCatalogue.onClick.AddListener(ToggleCatalogue);
            closeText.onClick.AddListener(Close);
            closeHouse.onClick.AddListener(Close);
            saveText.onClick.AddListener(SaveText);
            upgradeHouse.onClick.AddListener(Upgrade);
            buildings.ActiveChanged += OnBuildMode;
            GameEvents.MoneyChanged += RefreshBalance;
            _bindingRevision = -1;
            _bound = true;
        }

        private void OnDisable()
        {
            if (!_bound) return;
            if (input.ConstructionPointerBlock == openCatalogue.transform) input.ConstructionPointerBlock = null;
            buildings.ActiveChanged -= OnBuildMode;
            GameEvents.MoneyChanged -= RefreshBalance;
            openCatalogue.onClick.RemoveListener(ToggleCatalogue);
            closeText.onClick.RemoveListener(Close);
            closeHouse.onClick.RemoveListener(Close);
            saveText.onClick.RemoveListener(SaveText);
            upgradeHouse.onClick.RemoveListener(Upgrade);
            openCatalogue.gameObject.SetActive(false);
            Close();
            _bound = false;
        }

        private void OnDestroy() => _catalogue?.Dispose();

        private void Update()
        {
            if (!_bound) return;
            var showShortcut = !input.ModalInputActive && !FarmSceneFlow.IsLoading;
            if (openCatalogue.gameObject.activeSelf != showShortcut) openCatalogue.gameObject.SetActive(showShortcut);
            if (_bindingRevision != InputReader_AI.BindingRevision)
            {
                _bindingRevision = InputReader_AI.BindingRevision;
                _shortcutLabel.text = FarmText.Get("construction.openCatalogue", InputReader_AI.KeyLabel(InputReader_AI.Command.Build));
            }

            if (input.ConstructionCatalogueActive) _catalogue.RenderNextPreview();
            if (input.ModalSubmitPressed && signPanel.gameObject.activeSelf && signInput.isFocused) SaveText();
        }

        private void OnBuildMode(bool active)
        {
            if (!active) Close();
        }

        private void RefreshBalance(int coins)
        {
            if (input.ConstructionCatalogueActive) _catalogue.RefreshCoins();
        }

        public bool BlocksWorldPointer(Vector2 position) => isActiveAndEnabled
            && (shade.activeSelf || (openCatalogue.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)openCatalogue.transform, position)));

        internal void ToggleCatalogue()
        {
            if (!_bound || FarmSceneFlow.IsLoading) return;
            if (input.ConstructionCatalogueActive) { Close(); return; }
            if (input.ModalInputActive) return;
            if (buildings.IsActive) buildings.SetActive(false);
            BeginModal();
            EnsureCatalogue();
            input.ConstructionCatalogueActive = true;
            cataloguePanel.gameObject.SetActive(true);
            _catalogue.Show();
            FarmUIFocus.Show(_catalogue.Panel, _catalogue.InitialFocus, Close);
        }

        private void EnsureCatalogue()
        {
            if (_catalogue != null) return;
            // Older baked scenes have a compact catalogue inside the same modal host.
            foreach (Transform child in cataloguePanel) child.gameObject.SetActive(false);
            if (cataloguePanel.TryGetComponent<Image>(out var background)) background.enabled = false;
            Stretch(cataloguePanel);
            Action garden = _slots == null ? null : PrepareGarden;
            _catalogue = new BuildCatalogueScreen(cataloguePanel, _shortcutLabel.font, buildings, Choose, Close,
                () => BeginManagement(false), () => BeginManagement(true), garden);
        }

        private void Choose(int index)
        {
            Close();
            buildings.BeginPlacement(index);
        }

        private void BeginManagement(bool remove)
        {
            Close();
            buildings.BeginManagement(remove);
        }

        private void PrepareGarden()
        {
            if (_slots?.SelectTool(QuickSlotSystem.SelectionMode.Hoe) != true) return;
            Close();
            buildings.SetActive(false);
            GameEvents.RaiseStatusChanged("Наведите на свободную землю и обработайте её · " + InputReader_AI.ActionHint);
        }
        public void ShowDetails(BuildingRuntimeState state)
        {
            if (!_bound || state == null || state.IsUnderConstruction || (input.ModalInputActive && !shade.activeSelf)) return;
            if(state.definitionId!="house"&&!FarmDecorationCatalog.SupportsText(state.definitionId))
            {GameEvents.RaiseStatusChanged(FarmText.Get("construction.moveDelete", InputReader_AI.KeyLabel(InputReader_AI.Command.MoveBuilding), InputReader_AI.KeyLabel(InputReader_AI.Command.DeleteBuilding)));return;}
            BeginModal(); _detailId=state.id;
            if(FarmDecorationCatalog.SupportsText(state.definitionId))
            {
                signPanel.gameObject.SetActive(true);signInput.text=state.customText??string.Empty;
                FarmUIFocus.Show(signPanel, signInput, Close); signInput.ActivateInputField();
            }
            else
            {
                housePanel.gameObject.SetActive(true);var level=state.appearanceLevel;
                houseDescription.text = FarmText.Get("construction.houseLevel", level + 1, FarmDecorationCatalog.HouseLevelName(level))
                    + "\n" + (level < 2 ? FarmText.Get("construction.houseNext", FarmDecorationCatalog.HouseLevelName(level + 1)) : FarmText.Get("construction.houseComplete"));
                upgradeHouse.gameObject.SetActive(level < 2);
                upgradeHouse.interactable = false;
                upgradeLabel.text = FarmText.Get("construction.upgradeHouse");
                feedback.text = level < 2 ? FarmText.Get("construction.marketUpgrade", FarmText.Coins(FarmDecorationCatalog.HouseUpgradePrice(level))) : string.Empty; FarmUIFocus.Show(housePanel, closeHouse, Close);
            }
        }
        private void SaveText()
        {
            if(_detailId==null)return;
            if(buildings.TrySetSignText(_detailId,signInput.text,out var reason))Close();
            GameEvents.RaiseStatusChanged(reason);
        }
        private void Upgrade()
        {
            if(_detailId==null)return;
            var id=_detailId;
            if(buildings.TryUpgradeHouse(id,out var reason))ShowDetails(buildings.GetBuilding(id));
            feedback.text=reason;GameEvents.RaiseStatusChanged(reason);
        }
        private void Close()
        {
            if(shade==null)return;
            var ownedModal=shade.activeSelf;
            shade.SetActive(false);cataloguePanel.gameObject.SetActive(false);signPanel.gameObject.SetActive(false);housePanel.gameObject.SetActive(false);
            _detailId = null;
            if (input != null && ownedModal)
            {
                input.ConstructionCatalogueActive = false;
                input.ModalInputActive = false;
                input.SuppressGameplayThisFrame = true;
            }
            if (ownedModal)
            {
                FarmUIFocus.Show(null, null, null);
                FarmUIFocus.Restore(_previousFocus);
                _previousFocus = null;
            }
        }
        private void BeginModal()
        {
            Close();
            _previousFocus = FarmUIFocus.Selected;
            shade.SetActive(true);
            input.ModalInputActive = true;
            input.SuppressGameplayThisFrame = true;
        }

        private void OnRectTransformDimensionsChange()=>Resize();
        private void Resize()
        {
            _catalogue?.Resize();
            if (signPanel == null || housePanel == null) return;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            var area = ((RectTransform)canvas.transform).rect.size;
            var width = Mathf.Clamp(area.x - 40, 260, 640);
            signPanel.sizeDelta = new Vector2(width, 310);
            housePanel.sizeDelta = new Vector2(width, 340);
            foreach (var button in new[] { saveText, closeText, upgradeHouse, closeHouse })
                if (button != null) ((RectTransform)button.transform)
                    .SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, (width - 72) / 2);
        }

        // Called solely by explicit Editor setup. All references are serialized into Prototype.
        public static BuildCustomizationUI Create(Transform parent,BuildSystem build,InputReader_AI reader,Font font)
        {
            var root=new GameObject("Farm Customization",typeof(RectTransform),typeof(BuildCustomizationUI));root.transform.SetParent(parent,false);
            Stretch(root.GetComponent<RectTransform>());var ui=root.GetComponent<BuildCustomizationUI>();ui.buildings=build;ui.input=reader;
            ui.openCatalogue=Button(root.transform,"Строительство · B",font,new Vector2(220,46));
            var open=ui.openCatalogue.GetComponent<RectTransform>();open.anchorMin=open.anchorMax=open.pivot=Vector2.one;open.anchoredPosition=new Vector2(-174,-20);
            ui.shade=new GameObject("Modal Backdrop",typeof(RectTransform),typeof(Image));ui.shade.transform.SetParent(root.transform,false);Stretch(ui.shade.GetComponent<RectTransform>());
            ui.shade.GetComponent<Image>().color=new Color(.015f,.03f,.02f,.8f);
            ui.cataloguePanel = FarmUITheme.Rect(ui.shade.transform, "Catalogue");
            Stretch(ui.cataloguePanel);
            ui.signPanel=Panel(ui.shade.transform,"Sign Text",new Vector2(640,310));
            var title=Label(ui.signPanel,FarmText.Get("construction.signTitle"),font,24);Top(title.rectTransform,20,20,42);
            var caption=Label(ui.signPanel,FarmText.Get("construction.signHelp"),font,18);Top(caption.rectTransform,20,65,38);
            var field=new GameObject("Sign Input",typeof(RectTransform),typeof(Image),typeof(InputField));field.transform.SetParent(ui.signPanel,false);Top(field.GetComponent<RectTransform>(),26,120,62);
            field.GetComponent<Image>().color=new Color(.96f,.94f,.85f);ui.signInput=field.GetComponent<InputField>();
            var content=Label(field.transform,"",font,24);content.color=new Color(.07f,.12f,.08f);Stretch(content.rectTransform);content.rectTransform.offsetMin=new Vector2(14,8);content.rectTransform.offsetMax=new Vector2(-14,-8);content.alignment=TextAnchor.MiddleLeft;
            ui.signInput.textComponent=content;ui.signInput.characterLimit=FarmDecorationCatalog.MaximumSignLength;ui.signInput.lineType=InputField.LineType.SingleLine;
            var placeholder=Label(field.transform,FarmText.Get("construction.signPlaceholder"),font,24);placeholder.color=new Color(.4f,.44f,.38f);Stretch(placeholder.rectTransform);placeholder.rectTransform.offsetMin=new Vector2(14,8);placeholder.rectTransform.offsetMax=new Vector2(-14,-8);placeholder.alignment=TextAnchor.MiddleLeft;ui.signInput.placeholder=placeholder;
            ui.saveText=Button(ui.signPanel,FarmText.Get("construction.signSave"),font,new Vector2(250,46));Bottom(ui.saveText,24,26,false);
            ui.closeText=Button(ui.signPanel,FarmText.Get("construction.signCancel"),font,new Vector2(210,46));Bottom(ui.closeText,-24,26,true);
            ui.housePanel=Panel(ui.shade.transform,"House Upgrade",new Vector2(640,340));
            ui.houseDescription=Label(ui.housePanel,"",font,23);Top(ui.houseDescription.rectTransform,24,20,140);
            ui.feedback=Label(ui.housePanel,"",font,18);Top(ui.feedback.rectTransform,24,165,62);
            ui.upgradeHouse=Button(ui.housePanel,"Улучшить",font,new Vector2(290,46));ui.upgradeLabel=ui.upgradeHouse.GetComponentInChildren<Text>();Bottom(ui.upgradeHouse,24,28,false);
            ui.closeHouse=Button(ui.housePanel,FarmText.Get("construction.close"),font,new Vector2(210,46));Bottom(ui.closeHouse,-24,28,true);
            ui.Resize();ui.Close();ui.openCatalogue.gameObject.SetActive(false);return ui;
        }
        private static RectTransform Panel(Transform parent,string name,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
            go.GetComponent<Image>().color=new Color(.055f,.105f,.075f,.99f);var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=size;return rect;
        }
        private static Text Label(Transform parent,string content,Font font,int size)
        {
            var go=new GameObject("Label",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var text=go.GetComponent<Text>();text.font=font;text.fontSize=size;text.color=new Color(.98f,.96f,.87f);text.text=content;text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;text.supportRichText=false;return text;
        }
        private static Button Button(Transform parent,string title,Font font,Vector2 size)
        {
            var go=new GameObject(title,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);go.GetComponent<RectTransform>().sizeDelta=size;go.GetComponent<Image>().color=new Color(.18f,.30f,.21f);
            var button=go.GetComponent<Button>();var colors=button.colors;colors.highlightedColor=new Color(.72f,.86f,.59f);colors.selectedColor=colors.highlightedColor;colors.pressedColor=new Color(.48f,.65f,.37f);button.colors=colors;
            var text=Label(go.transform,title,font,19);Stretch(text.rectTransform);text.rectTransform.offsetMin=new Vector2(8,3);text.rectTransform.offsetMax=new Vector2(-8,-3);text.resizeTextForBestFit=true;text.resizeTextMinSize=14;text.resizeTextMaxSize=19;return button;
        }
        private static void Stretch(RectTransform rect){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
        private static void Top(RectTransform rect,float inset,float y,float height)
        {rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,1);rect.offsetMin=new Vector2(inset,-y-height);rect.offsetMax=new Vector2(-inset,-y);}
        private static void Bottom(Button button,float x,float y,bool? right)
        {var rect=button.GetComponent<RectTransform>();var anchor=right==null?.5f:right.Value?1f:0f;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(anchor,0);rect.anchoredPosition=new Vector2(x,y);}
    }
}
