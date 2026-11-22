using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MyLittleFarm.UI
{
    internal sealed class ComicPrologue : MonoBehaviour
    {
        private const float PageFadeSeconds = .45f;
        private const float ExitFadeSeconds = .28f;

        private Texture2D[] _pages;
        private CanvasGroup _content;
        private RawImage _front, _back;
        private Text _hint;
        private Button _next;
        private Text _nextLabel;
        private bool _advance;

        internal static ComicPrologue Open(Transform parent, Font font)
        {
            var story = Resources.Load<ComicProloguePages>("FarmUI/ComicPrologue");
            var pages = story != null ? story.Pages : null;
            if (pages == null || pages.Length == 0 || Array.Exists(pages, page => page == null))
            {
                Debug.LogWarning("Comic prologue is missing its page textures.");
                return null;
            }

            var root = FarmUITheme.Rect(parent, "Comic prologue");
            FarmUITheme.Stretch(root);
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 30000;
            root.gameObject.AddComponent<GraphicRaycaster>();

            var view = root.gameObject.AddComponent<ComicPrologue>();
            view._pages = pages;
            view.Build(font);
            return view;
        }

        private void Build(Font font)
        {
            var matte = FarmUITheme.Image(transform, "Backdrop");
            FarmUITheme.Stretch(matte.rectTransform);
            matte.color = new Color32(16, 13, 11, 255);

            var story = FarmUITheme.Rect(transform, "Story");
            FarmUITheme.Stretch(story);
            _content = story.gameObject.AddComponent<CanvasGroup>();
            _content.alpha = 0;

            var viewport = FarmUITheme.Rect(story, "Page area");
            FarmUITheme.Stretch(viewport, 16);
            viewport.offsetMin = new Vector2(16, 68);
            _back = CreatePage(viewport);
            _front = CreatePage(viewport);

            _hint = FarmUITheme.Label(story, "", font, 24);
            _hint.color = FarmUITheme.Cream;
            var hint = _hint.rectTransform;
            FarmUITheme.Place(hint, new Vector2(.5f, 0), new Vector2(1, 0), new Vector2(-12, 10), new Vector2(230, 48));
            _hint.alignment = TextAnchor.MiddleRight;
            _next = FarmUITheme.Button(story, FarmText.Get("prologue.next"), font);
            FarmUITheme.Place((RectTransform)_next.transform, new Vector2(.5f, 0), Vector2.zero,
                new Vector2(12, 10), new Vector2(230, 48));
            _nextLabel = _next.GetComponentInChildren<Text>();
            _next.onClick.AddListener(() => _advance = true);
        }

        private static RawImage CreatePage(Transform parent)
        {
            var page = FarmUITheme.Rect(parent, "Comic page").gameObject.AddComponent<RawImage>();
            page.raycastTarget = false;
            page.color = Color.clear;
            page.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            return page;
        }

        internal IEnumerator Play()
        {
            for (var index = 0; index < _pages.Length; index++)
            {
                var texture = _pages[index];
                _front.texture = texture;
                _front.GetComponent<AspectRatioFitter>().aspectRatio = (float)texture.width / texture.height;
                _front.transform.SetAsLastSibling();
                var lastPage = index == _pages.Length - 1;
                _hint.text = FarmText.Get("prologue.page", index + 1, _pages.Length);
                _nextLabel.text = FarmText.Get(lastPage ? "prologue.finish" : "prologue.next");
                _next.interactable = false;

                if (index == 0)
                {
                    _front.color = Color.white;
                    yield return Fade(PageFadeSeconds, alpha => _content.alpha = alpha);
                }
                else
                {
                    yield return Fade(PageFadeSeconds, alpha => _front.color = new Color(1, 1, 1, alpha));
                }

                _back.color = Color.clear;
                _back.texture = null;
                (_front, _back) = (_back, _front);

                // Wait for a fresh press after each transition, including the one from the slot menu.
                _advance = false;
                _next.interactable = true;
                FarmUIFocus.Show(transform, _next, null);
                do { yield return null; }
                while (!_advance && Keyboard.current?.spaceKey.wasPressedThisFrame != true);
            }

            _next.interactable = false;
            FarmUIFocus.Show(null, null, null);
            yield return Fade(ExitFadeSeconds, alpha => _content.alpha = 1 - alpha);
        }

        private static IEnumerator Fade(float seconds, Action<float> setAlpha)
        {
            if (!FarmSettingsUI.ReduceMotion)
            {
                for (var elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
                {
                    setAlpha(Mathf.SmoothStep(0, 1, elapsed / seconds));
                    yield return null;
                }
            }
            setAlpha(1);
        }
    }
}
