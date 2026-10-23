using System;
using MyLittleFarm.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace MyLittleFarm.UI
{
    /// <summary>Draft settings are committed only by Apply; Back leaves the applied profile intact.</summary>
    public sealed class FarmSettingsUI : MonoBehaviour
    {
        [Serializable] private sealed class Profile : AudioPreferences
        {
            public bool fullscreen = true, vsync = true, shadows = true;
            public int quality = -1, width, height;
        }
        private const string Key = AudioPreferences.Key;
        [SerializeField] private RectTransform panel;
        [SerializeField] private GameObject[] pages;
        [SerializeField] private Button[] tabs;
        [SerializeField] private Slider master;
        [SerializeField] private Text percentage, qualityLabel, resolutionLabel, muteLabel, backgroundLabel, fullscreenLabel, vsyncLabel, shadowLabel;
        [SerializeField] private Slider[] audioSliders;
        [SerializeField] private Text[] audioValues;
        [SerializeField] private Font font;
        [SerializeField] private Text controlsGuide;
        [SerializeField] private Dropdown resolutionDropdown;
        private Vector2Int[] _resolutionChoices;
        private const string Guide = "УПРАВЛЕНИЕ\n\nДвижение — W A S D\nБег — Shift · Спринт — Shift + Ctrl\nПрыжок — Space · Шаги боком — Alt\nДействие — E / ЛКМ · Отмена — Esc / ПКМ\nБыстрые слоты семян — 1–6\nИнвентарь — I · Справочник — J\nСтроительство — B · Каталог — Tab в стройке\nПоворот постройки — Q / R\nПеремещение постройки — M · Удаление — Delete\nПодтверждение удаления — E / Enter\nПоворот камеры — Q / Shift + E / средняя кнопка мыши\nПриближение — колесо мыши\nРынок — P / E рядом с торговой точкой\nТовары — в корзину, покупка — кнопкой «Оплатить»\nПосадить яблоню — T\nКуры, улучшения и найм пастуха — в окне рынка\nНастроить постройку — F\nСохранить — F5 · Слоты сохранений — F9\nПауза — Esc вне строительства и других окон\n\nНАЧАЛО ФЕРМЫ\n\n1. Поставьте дом на свободной земле.\n2. Постройте склад-сундук.\n3. Обработайте землю, посадите семена и соберите урожай.\n4. Купите семена рядом с рынком.\n5. Продайте урожай во вкладке «Продать» рынка.\n\nЗапасы — в инвентаре. Цены — в справочнике и на рынке.\nСтатистика, бонусы времени суток и достижения — в журнале.";
        private Profile _applied, _draft;
        // Serialized controls survive the baked scene, unlike auto-properties.
        [SerializeField] private Button back;
        public RectTransform Panel => panel;
        public Button BackButton => back;
        public void ApplyTheme() { FarmUITheme.Style(apply); FarmUITheme.Style(reset, "ghost"); FarmUITheme.Style(back, "ghost"); }
        public void EnsureAudioControls()
        {
            var page = pages[0].transform;
            var root = page.Find("Audio controls") as RectTransform;
            if (root == null) { root = FarmUITheme.Rect(page, "Audio controls"); FarmUITheme.Stretch(root); }
            mute.transform.SetParent(root, false); background.transform.SetParent(root, false);
            foreach (Transform child in page) if (child != root) child.gameObject.SetActive(false);
            if (audioSliders == null || audioSliders.Length != 5)
            {
                audioSliders = new Slider[5]; audioValues = new Text[5];
                var names = new[] { "Общая громкость", "Музыка", "Игровые звуки", "Окружение", "Интерфейс" };
                for (var i = 0; i < names.Length; i++)
                {
                    var label = FarmUITheme.Label(root, names[i], font, 28); label.alignment = TextAnchor.MiddleLeft;
                    FarmUITheme.Place(label.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -78 * i), new Vector2(290, 58));
                    var track = FarmUITheme.Image(root, names[i] + " slider", "Skin/slot");
                    FarmUITheme.Place(track.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(306, -17 - 78 * i), new Vector2(500, 28));
                    var fill = FarmUITheme.Image(track.transform, "Fill", "Skin/primary"); FarmUITheme.Stretch(fill.rectTransform); fill.raycastTarget = false;
                    var area = FarmUITheme.Rect(track.transform, "Handle area"); FarmUITheme.Stretch(area);
                    area.offsetMin = new Vector2(20, 0); area.offsetMax = new Vector2(-20, 0);
                    var handle = FarmUITheme.Icon(area, "coin"); handle.rectTransform.sizeDelta = new Vector2(42, 42); handle.raycastTarget = true;
                    var slider = track.gameObject.AddComponent<Slider>(); slider.fillRect = fill.rectTransform;
                    slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
                    slider.minValue = 0; slider.maxValue = 1; audioSliders[i] = slider;
                    audioValues[i] = FarmUITheme.Label(root, "100%", font, 27);
                    FarmUITheme.Place(audioValues[i].rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -78 * i), new Vector2(128, 58));
                }
            }
            master = audioSliders[0]; percentage = audioValues[0];
            FarmUITheme.Place((RectTransform)mute.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(-244, -422), new Vector2(460, 64));
            FarmUITheme.Place((RectTransform)background.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(244, -422), new Vector2(460, 64));
        }
        private float DraftAudio(int index) => index == 0 ? _draft.volume : index == 1 ? _draft.musicVolume : index == 2 ? _draft.sfxVolume : index == 3 ? _draft.ambienceVolume : _draft.uiVolume;
        private void SetDraftAudio(int index, float value)
        {
            if (_draft == null) return;
            switch (index) { case 0: _draft.volume = value; break; case 1: _draft.musicVolume = value; break; case 2: _draft.sfxVolume = value; break; case 3: _draft.ambienceVolume = value; break; default: _draft.uiVolume = value; break; }
            audioValues[index].text = Mathf.RoundToInt(value * 100) + "%";
        }
        public void EnsureControlsGuide()
        {
            var page = pages[2].transform;
            if (controlsGuide == null) controlsGuide = page.GetComponentInChildren<Text>(true);
            var existing = page.Find("Controls viewport");
            var viewport = existing as RectTransform;
            if (viewport == null)
            {
                viewport = FarmUITheme.Rect(page, "Controls viewport"); FarmUITheme.Stretch(viewport);
                viewport.offsetMin = new Vector2(8, 0); viewport.offsetMax = new Vector2(-8, 0);
                viewport.gameObject.AddComponent<Image>().color = Color.clear;
                viewport.gameObject.AddComponent<RectMask2D>(); viewport.gameObject.AddComponent<ScrollRect>();
            }
            controlsGuide.transform.SetParent(viewport, false); controlsGuide.name = "Controls guide";
            controlsGuide.text = Guide; controlsGuide.font = font; controlsGuide.fontSize = 24; controlsGuide.color = FarmUITheme.Ink;
            controlsGuide.resizeTextForBestFit = false; controlsGuide.alignment = TextAnchor.UpperLeft;
            controlsGuide.horizontalOverflow = HorizontalWrapMode.Wrap; controlsGuide.verticalOverflow = VerticalWrapMode.Overflow;
            var content = controlsGuide.rectTransform; content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1); content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
            var fit = controlsGuide.GetComponent<ContentSizeFitter>() ?? controlsGuide.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.GetComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 32; scroll.movementType = ScrollRect.MovementType.Clamped;
            viewport.offsetMax = new Vector2(-36, 0);
            var track = page.Find("Controls scrollbar") as RectTransform;
            if (track == null)
            {
                var background = FarmUITheme.Image(page, "Controls scrollbar", "Skin/slot"); track = background.rectTransform;
                var handle = FarmUITheme.Image(track, "Handle", "Skin/secondary"); FarmUITheme.Stretch(handle.rectTransform);
                var scrollbar = track.gameObject.AddComponent<Scrollbar>(); scrollbar.handleRect = handle.rectTransform;
                scrollbar.targetGraphic = handle; scrollbar.direction = Scrollbar.Direction.BottomToTop;
            }
            track.anchorMin = new Vector2(1, 0); track.anchorMax = Vector2.one; track.pivot = new Vector2(1, .5f);
            track.offsetMin = new Vector2(-24, 0); track.offsetMax = Vector2.zero;
            scroll.verticalScrollbar = track.GetComponent<Scrollbar>(); scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        }
        private void Awake() => Initialize();
        public void Initialize()
        {
            if (!Application.isPlaying || _applied != null) return;
            _applied = Read(); ApplyProfile(_applied, false);
        }
        private Profile Defaults() => new Profile { quality = QualitySettings.GetQualityLevel(), width = 1920, height = 1080, fullscreen = Screen.fullScreen };
        private Profile Read()
        {
            Profile result = null;
            try { if (PlayerPrefs.HasKey(Key)) result = JsonUtility.FromJson<Profile>(PlayerPrefs.GetString(Key)); }
            catch (ArgumentException) { }
            result = result ?? Defaults(); result.Sanitize();
            result.quality = Mathf.Clamp(result.quality, 0, Mathf.Max(0, QualitySettings.names.Length - 1));
            return result;
        }
        public void Open()
        {
            _applied = _applied ?? Defaults(); _draft = JsonUtility.FromJson<Profile>(JsonUtility.ToJson(_applied));
            gameObject.SetActive(true); ShowPage(0); Refresh();
        }
        public void Hide() => gameObject.SetActive(false);
        public void PreviewPage(int page)
        {
            if (Application.isPlaying) return;
            gameObject.SetActive(true); _draft = Defaults(); ShowPage(page); Refresh();
        }
        private void ApplyAudio() => FarmAudio.ApplySettings(_applied);
        private void ApplyProfile(Profile profile, bool save)
        {
            _applied = profile; _applied.Sanitize(); ApplyAudio();
            var quality = Mathf.Clamp(profile.quality, 0, QualitySettings.names.Length - 1);
            if (QualitySettings.GetQualityLevel() != quality) QualitySettings.SetQualityLevel(quality, true);
            QualitySettings.vSyncCount = profile.vsync ? 1 : 0;
            QualitySettings.shadows = profile.shadows ? ShadowQuality.All : ShadowQuality.Disable;
            var mode = profile.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            if (profile.width >= 640 && profile.height >= 480
                && (Screen.width != profile.width || Screen.height != profile.height || Screen.fullScreenMode != mode))
                Screen.SetResolution(profile.width, profile.height, mode);
            if (save) { PlayerPrefs.SetString(Key, JsonUtility.ToJson(profile)); PlayerPrefs.Save(); }
        }
        private void Commit() { ApplyProfile(_draft, true); _draft = JsonUtility.FromJson<Profile>(JsonUtility.ToJson(_applied)); Refresh(); }
        private void ResetDraft() { _draft = Defaults(); _draft.volume = .8f; Refresh(); }
        private void CycleQuality() { _draft.quality = (_draft.quality + 1) % Mathf.Max(1, QualitySettings.names.Length); Refresh(); }
        public void EnsureResolutionDropdown()
        {
            resolution.gameObject.SetActive(false);
            if (resolutionDropdown != null) { RepairResolutionTemplate(); return; }
            var image = FarmUITheme.Image(pages[1].transform, "Resolution dropdown", "Skin/ghost");
            FarmUITheme.Place(image.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -72), new Vector2(680, 60));
            resolutionDropdown = image.gameObject.AddComponent<Dropdown>(); resolutionDropdown.targetGraphic = image;
            var caption = FarmUITheme.Label(image.transform, "1920 × 1080", font, 28); FarmUITheme.Stretch(caption.rectTransform, 12); caption.rectTransform.offsetMax = new Vector2(-64, -8);
            var arrow = FarmUITheme.Icon(image.transform, "arrow_right"); arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, -90);
            FarmUITheme.Place(arrow.rectTransform, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-18, 0), new Vector2(30, 30));
            var template = FarmUITheme.Image(image.transform, "Template", "Skin/panel").rectTransform;
            template.anchorMin = Vector2.zero; template.anchorMax = new Vector2(1, 0); template.pivot = new Vector2(.5f, 1); template.anchoredPosition = new Vector2(0, -4); template.sizeDelta = new Vector2(0, 292);
            var viewport = FarmUITheme.Image(template, "Viewport"); FarmUITheme.Stretch(viewport.rectTransform, 10); viewport.color = Color.white;
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var content = FarmUITheme.Rect(viewport.transform, "Content"); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1); content.sizeDelta = new Vector2(0, 54);
            var item = FarmUITheme.Image(content, "Item", "Skin/slot"); item.rectTransform.anchorMin = Vector2.zero; item.rectTransform.anchorMax = new Vector2(1, 0); item.rectTransform.pivot = new Vector2(.5f, .5f); item.rectTransform.anchoredPosition = new Vector2(0, 27); item.rectTransform.sizeDelta = new Vector2(0, 54);
            var selected = FarmUITheme.Icon(item.transform, "confirm"); FarmUITheme.Place(selected.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(12, 0), new Vector2(30, 30));
            var label = FarmUITheme.Label(item.transform, "", font, 26); FarmUITheme.Stretch(label.rectTransform); label.rectTransform.offsetMin = new Vector2(54, 0); label.rectTransform.offsetMax = new Vector2(-12, 0);
            var toggle = item.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = item; toggle.graphic = selected;
            var scroll = template.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport.rectTransform; scroll.content = content; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            resolutionDropdown.template = template; resolutionDropdown.captionText = caption; resolutionDropdown.itemText = label; resolutionDropdown.alphaFadeSpeed = .12f;
            template.gameObject.SetActive(false);
            RepairResolutionTemplate();
        }
        private void RepairResolutionTemplate()
        {
            var viewport = (RectTransform)resolutionDropdown.template.Find("Viewport");
            // Dropdown sizes its popup to content height. Vertical viewport padding would clip
            // the fifth option and require scrolling despite the five-choice limit.
            viewport.offsetMin = new Vector2(10, 0); viewport.offsetMax = new Vector2(-10, 0);
        }
        private void RefreshResolutions()
        {
            if (resolutionDropdown == null) return;
            _resolutionChoices = new[] { new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(3840, 2160) };
            var current = new Vector2Int(_draft.width, _draft.height);
            var index = Array.IndexOf(_resolutionChoices, current);
            // A legacy custom size is migrated in the draft, committed only by Apply.
            if (index < 0) { index = 2; _draft.width = 1920; _draft.height = 1080; }
            resolutionDropdown.ClearOptions();
            var options = new System.Collections.Generic.List<string>();
            foreach (var size in _resolutionChoices) options.Add(size.x + " × " + size.y);
            resolutionDropdown.AddOptions(options); resolutionDropdown.SetValueWithoutNotify(index);
            resolutionDropdown.captionText.text = "Разрешение: " + options[index];
        }
        private void SelectResolution(int index)
        {
            if (_draft == null || _resolutionChoices == null || index < 0 || index >= _resolutionChoices.Length) return;
            _draft.width = _resolutionChoices[index].x; _draft.height = _resolutionChoices[index].y;
            resolutionDropdown.captionText.text = "Разрешение: " + _draft.width + " × " + _draft.height;
        }
        private void Refresh()
        {
            master.SetValueWithoutNotify(_draft.volume); percentage.text = Mathf.RoundToInt(_draft.volume * 100) + "%";
            if (audioSliders != null) for (var i = 0; i < audioSliders.Length; i++)
            { var value = DraftAudio(i); audioSliders[i].SetValueWithoutNotify(value); audioValues[i].text = Mathf.RoundToInt(value * 100) + "%"; }
            qualityLabel.text = "Качество: " + QualitySettings.names[Mathf.Clamp(_draft.quality, 0, QualitySettings.names.Length - 1)];
            resolutionLabel.text = "Разрешение: " + _draft.width + " × " + _draft.height;
            RefreshResolutions();
            muteLabel.text = "Без звука: " + OnOff(_draft.mute); backgroundLabel.text = "Звук в фоне: " + OnOff(_draft.backgroundAudio);
            fullscreenLabel.text = "Полный экран: " + OnOff(_draft.fullscreen); vsyncLabel.text = "VSync: " + OnOff(_draft.vsync); shadowLabel.text = "Тени: " + OnOff(_draft.shadows);
        }
        private static string OnOff(bool value) => value ? "Вкл." : "Выкл.";
        private void ShowPage(int index)
        {
            for (var i = 0; i < pages.Length; i++) { pages[i].SetActive(i == index); FarmUITheme.Style(tabs[i], i == index ? "secondary" : "ghost"); }
            if (index == 2)
            {
                var scroll = pages[2].GetComponentInChildren<ScrollRect>(true);
                if (scroll != null) { Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = 1; }
            }
            FarmUITheme.Focus(tabs[index]);
        }
        private Button Row(Transform parent, string title, string icon, int index)
        {
            var button = FarmUITheme.Button(parent, title, font, icon, "ghost");
            FarmUITheme.Place((RectTransform)button.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -index * 72), new Vector2(680, 60));
            // Runtime listeners are installed by Bind, not serialized delegates.
            return button;
        }
        [SerializeField] private Button quality, resolution, fullscreen, vsync, shadows, mute, background, apply, reset;
        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            for (var i = 0; i < tabs.Length; i++) { var tab = i; tabs[i].onClick.AddListener(() => ShowPage(tab)); }
            if (audioSliders != null) for (var i = 0; i < audioSliders.Length; i++)
            { var index = i; audioSliders[i].onValueChanged.AddListener(value => SetDraftAudio(index, value)); }
            quality.onClick.AddListener(CycleQuality);
            if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(SelectResolution);
            fullscreen.onClick.AddListener(() => { _draft.fullscreen = !_draft.fullscreen; Refresh(); });
            vsync.onClick.AddListener(() => { _draft.vsync = !_draft.vsync; Refresh(); });
            shadows.onClick.AddListener(() => { _draft.shadows = !_draft.shadows; Refresh(); });
            mute.onClick.AddListener(() => { _draft.mute = !_draft.mute; Refresh(); });
            background.onClick.AddListener(() => { _draft.backgroundAudio = !_draft.backgroundAudio; Refresh(); });
            apply.onClick.AddListener(Commit); reset.onClick.AddListener(ResetDraft);
        }
        private void OnDisable()
        {
            if (!Application.isPlaying) return;
            if (resolutionDropdown != null) { resolutionDropdown.Hide(); resolutionDropdown.onValueChanged.RemoveListener(SelectResolution); }
            foreach (var button in new[] { quality, resolution, fullscreen, vsync, shadows, mute, background, apply, reset }) button.onClick.RemoveAllListeners();
            foreach (var tab in tabs) tab.onClick.RemoveAllListeners();
            if (audioSliders != null) foreach (var slider in audioSliders) slider.onValueChanged.RemoveAllListeners();
        }
        public static FarmSettingsUI Create(Transform parent, Font font)
        {
            var root = FarmUITheme.Rect(parent, "Settings"); FarmUITheme.Stretch(root);
            var ui = root.gameObject.AddComponent<FarmSettingsUI>(); ui.font = font;
            ui.panel = FarmUITheme.Panel(root, "НАСТРОЙКИ", font, new Vector2(1060, 800));
            ui.pages = new GameObject[3]; ui.tabs = new Button[3];
            var titles = new[] { "Звук", "Графика", "Управление" }; var icons = new[] { "volume", "graphics", "controls" };
            for (var i = 0; i < 3; i++)
            {
                ui.tabs[i] = FarmUITheme.Button(ui.panel, titles[i], font, icons[i], "ghost");
                FarmUITheme.Place((RectTransform)ui.tabs[i].transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2((i - 1) * 316, -64), new Vector2(300, 64));
                var page = FarmUITheme.Rect(ui.panel, titles[i]); FarmUITheme.Stretch(page); page.offsetMin = new Vector2(42, 124); page.offsetMax = new Vector2(-42, -162); ui.pages[i] = page.gameObject;
            }
            var audio = ui.pages[0].transform;
            var caption = FarmUITheme.Label(audio, "Общая громкость", font, 28);
            FarmUITheme.Place(caption.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(-120, 0), new Vector2(380, 48));
            var sliderImage = FarmUITheme.Image(audio, "Master volume", "Skin/slot");
            FarmUITheme.Place(sliderImage.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(-50, -76), new Vector2(620, 28));
            ui.master = sliderImage.gameObject.AddComponent<Slider>();
            var fill = FarmUITheme.Image(sliderImage.transform, "Fill"); FarmUITheme.Stretch(fill.rectTransform); fill.color = new Color32(108, 194, 74, 255); fill.raycastTarget = false;
            var handleArea = FarmUITheme.Rect(sliderImage.transform, "Handle area"); FarmUITheme.Stretch(handleArea);
            var handle = FarmUITheme.Icon(handleArea, "coin"); handle.rectTransform.sizeDelta = new Vector2(50, 50);
            ui.master.fillRect = fill.rectTransform; ui.master.handleRect = handle.rectTransform; ui.master.targetGraphic = handle; ui.master.minValue = 0; ui.master.maxValue = 1;
            ui.percentage = FarmUITheme.Label(audio, "80%", font, 28); FarmUITheme.Place(ui.percentage.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(338, -64), new Vector2(100, 52));
            ui.mute = ui.Row(audio, "Без звука", "volume_mute", 3); ui.muteLabel = ui.mute.GetComponentInChildren<Text>();
            ui.background = ui.Row(audio, "Звук в фоне", "music", 4); ui.backgroundLabel = ui.background.GetComponentInChildren<Text>();
            var graphics = ui.pages[1].transform;
            ui.quality = ui.Row(graphics, "Качество", "graphics", 0); ui.qualityLabel = ui.quality.GetComponentInChildren<Text>();
            ui.resolution = ui.Row(graphics, "Разрешение", "display", 1); ui.resolutionLabel = ui.resolution.GetComponentInChildren<Text>();
            ui.fullscreen = ui.Row(graphics, "Полный экран", "fullscreen", 2); ui.fullscreenLabel = ui.fullscreen.GetComponentInChildren<Text>();
            ui.vsync = ui.Row(graphics, "VSync", "display", 3); ui.vsyncLabel = ui.vsync.GetComponentInChildren<Text>();
            ui.shadows = ui.Row(graphics, "Тени", "day", 4); ui.shadowLabel = ui.shadows.GetComponentInChildren<Text>();
            var controls = FarmUITheme.Label(ui.pages[2].transform,
                "Движение                       W A S D\nБег / спринт                     Shift / Shift + Ctrl\nДействие                         E / ЛКМ\nПрыжок                           Space\nСемена                            1–6\nИнвентарь / рынок                I / P у рынка\nСтроительство / каталог         B / Tab\nПоворот постройки                Q / R\nПоворот камеры                  Q / Shift + E\nЖурнал / пауза                   J / Esc\nСохранить / загрузить           F5 / F9", font, 25);
            controls.alignment = TextAnchor.MiddleLeft;
            ui.back = FarmUITheme.Button(ui.panel, "Назад", font, "back", "ghost");
            ui.reset = FarmUITheme.Button(ui.panel, "Сбросить", font, null, "ghost");
            ui.apply = FarmUITheme.Button(ui.panel, "Применить", font, "confirm");
            FarmUITheme.Place((RectTransform)ui.back.transform, Vector2.zero, Vector2.zero, new Vector2(36, 30), new Vector2(260, 70));
            FarmUITheme.Place((RectTransform)ui.reset.transform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(30, 30), new Vector2(230, 70));
            FarmUITheme.Place((RectTransform)ui.apply.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-36, 30), new Vector2(300, 70));
            ui.EnsureAudioControls(); ui.EnsureControlsGuide(); ui.EnsureResolutionDropdown(); ui.ShowPage(0); root.gameObject.SetActive(false); return ui;
        }
    }
}
