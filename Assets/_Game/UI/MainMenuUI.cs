using MyLittleFarm.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MyLittleFarm.UI
{
    /// <summary>Front end with no references to a farm, inventory, save system or world resources.</summary>
    public sealed class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private RectTransform safeArea, actions, quitPanel;
        [SerializeField] private FarmSettingsUI settings;
        [SerializeField] private GameObject dimmer;
        [SerializeField] private Button start, load, options, exit, cancelQuit, confirmQuit;
        [SerializeField] private CanvasGroup interaction;
        [SerializeField] private Text status;
        [SerializeField] private bool illustratedButtons;
        private enum Page { Main, Settings, Quit }
        private Page _page;
        private Rect _safe;
        private Vector2 _size;
        private void Awake()
        { if (Application.isPlaying) { Time.timeScale = 1; settings.Initialize(); } }
        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            start.onClick.AddListener(Launch); load.onClick.AddListener(Launch);
            options.onClick.AddListener(OpenSettings); exit.onClick.AddListener(AskQuit);
            cancelQuit.onClick.AddListener(Back); confirmQuit.onClick.AddListener(Quit); settings.BackButton.onClick.AddListener(Back);
        }
        private void Start() { if (Application.isPlaying) Show(Page.Main); }
        private void OnDisable()
        {
            if (!Application.isPlaying) return;
            foreach (var button in new[] { start, load, options, exit, cancelQuit, confirmQuit, settings.BackButton }) button.onClick.RemoveAllListeners();
        }
        private void Update()
        {
            Resize();
            if (FarmSceneFlow.IsLoading) return;
            if (!interaction.interactable) { interaction.interactable = true; status.text = "Не удалось загрузить сцену фермы."; }
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            { if (_page == Page.Main) AskQuit(); else Back(); }
        }
        private void Launch()
        {
            if (FarmSceneFlow.LoadGame()) interaction.interactable = false;
            else if (!FarmSceneFlow.IsLoading) status.text = "Не удалось загрузить сцену фермы.";
        }
        private void Back() { MyLittleFarm.Audio.FarmAudio.Play(MyLittleFarm.Audio.SoundId.UiBack); Show(Page.Main); }
        private void OpenSettings() => Show(Page.Settings);
        private void AskQuit() => Show(Page.Quit);
        private void Show(Page page)
        {
            if (page != Page.Main) MyLittleFarm.Audio.FarmAudio.Play(MyLittleFarm.Audio.SoundId.UiOpen);
            _page = page; actions.gameObject.SetActive(page == Page.Main); dimmer.SetActive(page != Page.Main);
            quitPanel.gameObject.SetActive(page == Page.Quit); settings.Hide();
            if (page == Page.Settings) settings.Open();
            else FarmUITheme.Focus(page == Page.Main ? start : cancelQuit);
            Resize(true);
        }
        private static void Quit()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
        public void Preview(bool settingsVisible = false, int tab = 0)
        {
            if (Application.isPlaying) return;
            actions.gameObject.SetActive(!settingsVisible); settings.gameObject.SetActive(settingsVisible); quitPanel.gameObject.SetActive(false); dimmer.SetActive(settingsVisible);
            if (settingsVisible) settings.PreviewPage(tab); Resize(true);
        }
        private void Resize(bool force = false)
        {
            var canvasSize = ((RectTransform)transform).rect.size;
            if (!force && canvasSize == _size && _safe == Screen.safeArea) return;
            _size = canvasSize; _safe = Screen.safeArea;
            var pixels = new Vector2(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
            safeArea.anchorMin = _safe.min / pixels; safeArea.anchorMax = _safe.max / pixels; safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
            Canvas.ForceUpdateCanvases(); var size = safeArea.rect.size;
            var actionSize = illustratedButtons ? new Vector2(560, 572) : new Vector2(440, 446);
            var scale = Mathf.Min(1, size.y * (illustratedButtons ? .57f : .54f) / actionSize.y, (size.x - 32) / actionSize.x);
            actions.localScale = Vector3.one * Mathf.Max(.3f, scale);
            FarmUITheme.Place(actions, new Vector2(.56f, 0), new Vector2(.5f, 0), new Vector2(0, size.y * (illustratedButtons ? .025f : .045f)), actionSize);
            Fit(settings.Panel, size); Fit(quitPanel, size);
        }
        /// <summary>Use supplied art as-is, with baked lettering and a separate, non-overlapping hit area.</summary>
        public void SetActionArtwork(Sprite play, Sprite saves, Sprite settingsArtwork, Sprite exitArtwork)
        {
            ApplyArtwork(start, play, "Играть", 0); ApplyArtwork(load, saves, "Сохранения", 1);
            ApplyArtwork(options, settingsArtwork, "Настройки", 2); ApplyArtwork(exit, exitArtwork, "Выход", 3);
            illustratedButtons = true;
            FarmUITheme.Place(status.rectTransform, Vector2.zero, Vector2.zero, new Vector2(0, -22), new Vector2(560, 40));
            Resize(true);
        }
        private static void ApplyArtwork(Button button, Sprite sprite, string title, int index)
        {
            if (sprite == null) throw new System.ArgumentNullException(nameof(sprite));
            button.name = title;
            var hit = button.GetComponent<Image>(); hit.sprite = null; hit.color = Color.clear; hit.raycastTarget = true;
            foreach (Transform child in button.transform) if (child.name != "Artwork") child.gameObject.SetActive(false);
            var existing = button.transform.Find("Artwork");
            var art = existing == null ? FarmUITheme.Image(button.transform, "Artwork") : existing.GetComponent<Image>();
            art.sprite = sprite; art.type = Image.Type.Simple; art.preserveAspect = true; art.raycastTarget = false; art.color = Color.white;
            // Keep the source's transparent margins without distorting its 3:1 composition.
            FarmUITheme.Place(art.rectTransform, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(560, 560f * sprite.rect.height / sprite.rect.width));
            FarmUITheme.Place((RectTransform)button.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -136 * index), new Vector2(560, 128));
            button.targetGraphic = art;
            var motion = button.GetComponent<MainMenuButtonMotion>() ?? button.gameObject.AddComponent<MainMenuButtonMotion>();
            motion.Configure(art.rectTransform);
            var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1, .96f, .86f);
            colors.selectedColor = new Color(1, .94f, .72f); colors.pressedColor = new Color(.76f, .73f, .67f); colors.disabledColor = new Color(.5f, .5f, .5f, .65f);
            button.colors = colors;
        }
        private static void Fit(RectTransform panel, Vector2 size)
        { panel.localScale = Vector3.one * Mathf.Clamp01(Mathf.Min((size.x - 48) / panel.sizeDelta.x, (size.y - 80) / panel.sizeDelta.y)); }
        public static MainMenuUI Create(Transform parent, Font font, Sprite artwork)
        {
            var root = FarmUITheme.Rect(parent, "Main Menu UI"); FarmUITheme.Stretch(root);
            var ui = root.gameObject.AddComponent<MainMenuUI>(); ui.interaction = root.gameObject.AddComponent<CanvasGroup>();
            var matte = FarmUITheme.Image(root, "Background matte"); FarmUITheme.Stretch(matte.rectTransform); matte.color = new Color32(49, 79, 43, 255);
            var image = FarmUITheme.Image(root, "Main menu artwork"); FarmUITheme.Stretch(image.rectTransform); image.sprite = artwork; image.preserveAspect = true; image.raycastTarget = false;
            var dimmer = FarmUITheme.Image(root, "Dimmer"); FarmUITheme.Stretch(dimmer.rectTransform); dimmer.color = new Color(.09f, .13f, .05f, .58f); ui.dimmer = dimmer.gameObject;
            ui.safeArea = FarmUITheme.Rect(root, "Safe area"); FarmUITheme.Stretch(ui.safeArea);
            ui.actions = FarmUITheme.Rect(ui.safeArea, "Main menu actions");
            ui.start = Action(ui.actions, "СТАРТ", "play", font, 0); ui.load = Action(ui.actions, "Сохранения", "load", font, 1, "secondary");
            ui.options = Action(ui.actions, "Настройки", "settings", font, 2, "ghost"); ui.exit = Action(ui.actions, "Выход", "exit", font, 3, "ghost");
            ui.status = FarmUITheme.Label(ui.actions, "", font, 20); ui.status.color = Color.white;
            FarmUITheme.Place(ui.status.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(440, 64));
            ui.settings = FarmSettingsUI.Create(ui.safeArea, font);
            ui.quitPanel = FarmUITheme.Panel(ui.safeArea, "ВЫЙТИ ИЗ ИГРЫ?", font, new Vector2(740, 300));
            ui.cancelQuit = FarmUITheme.Button(ui.quitPanel, "Отмена", font, "back", "ghost"); ui.confirmQuit = FarmUITheme.Button(ui.quitPanel, "Выйти", font, "exit", "danger");
            FarmUITheme.Place((RectTransform)ui.cancelQuit.transform, Vector2.zero, Vector2.zero, new Vector2(36, 60), new Vector2(310, 76));
            FarmUITheme.Place((RectTransform)ui.confirmQuit.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-36, 60), new Vector2(310, 76));
            ui.Preview(); return ui;
        }
        private static Button Action(Transform parent, string title, string icon, Font font, int index, string kind = "primary")
        { var button = FarmUITheme.Button(parent, title, font, icon, kind); FarmUITheme.Place((RectTransform)button.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -94 * index), new Vector2(440, 82)); return button; }
    }
}
