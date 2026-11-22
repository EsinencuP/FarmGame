using System;
using System.Collections.Generic;
using MyLittleFarm.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MyLittleFarm.UI
{
    /// <summary>Routes menu keyboard input to the currently visible panel.</summary>
    [DefaultExecutionOrder(-190)]
    public sealed class FarmUIFocus : MonoBehaviour
    {
        private readonly List<Selectable> _controls = new List<Selectable>();
        private readonly List<ScrollRect> _scrolls = new List<ScrollRect>();
        private EventSystem _events;
        private Transform _panel;
        private Action _back;
        private GameObject _last;
        private Dropdown _dropdown;
        private int _changedFrame;

        public static GameObject Selected => EventSystem.current?.currentSelectedGameObject;
        public static bool TabPressed => Keyboard.current?.tabKey.wasPressedThisFrame == true;
        public static bool EditingText => Selected != null
            && Selected.TryGetComponent<InputField>(out var field) && field.isFocused;

        public static void Show(Transform panel, Selectable initial, Action back, Dropdown dropdown = null)
        {
            var events = EventSystem.current;
            if (!Application.isPlaying || events == null) return;
            var focus = events.GetComponent<FarmUIFocus>() ?? events.gameObject.AddComponent<FarmUIFocus>();
            focus._panel = panel;
            focus._back = back;
            focus._dropdown = dropdown;
            focus._changedFrame = Time.frameCount;
            focus._last = null;
            events.sendNavigationEvents = false;
            focus.Collect(panel);
            focus._scrolls.Clear();
            if (panel != null) panel.GetComponentsInChildren(true, focus._scrolls);
            Restore(initial == null ? null : initial.gameObject,
                focus._controls.Count == 0 ? null : focus._controls[0]);
        }

        public static void Restore(GameObject previous, Selectable fallback = null)
        {
            var selectable = previous == null ? null : previous.GetComponent<Selectable>();
            FarmUITheme.Focus(Available(selectable) ? selectable : fallback);
        }

        private void Awake()
        {
            _events = GetComponent<EventSystem>();
            if (TryGetComponent<InputSystemUIInputModule>(out var module))
                module.deselectOnBackgroundClick = false;
        }

        private void Update()
        {
            var active = _panel != null && _panel.gameObject.activeInHierarchy;
            var usable = active && !InputReader_AI.CapturingBinding && _changedFrame != Time.frameCount;
            _events.sendNavigationEvents = usable;
            var keyboard = Keyboard.current;
            if (!usable || keyboard == null) return;

            // uGUI creates the list as a child of the resolution control and removes it on close.
            var list = _dropdown != null ? _dropdown.transform.Find("Dropdown List") : null;
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                _events.sendNavigationEvents = false;
                if (list != null) _dropdown.Hide();
                else _back?.Invoke();
                return;
            }

            if (TabPressed)
            {
                Collect(list != null ? list : _panel);
                if (_controls.Count == 0) return;
                var current = _controls.FindIndex(control => control.gameObject == Selected);
                var next = keyboard.shiftKey.isPressed
                    ? (current <= 0 ? _controls.Count - 1 : current - 1)
                    : (current + 1) % _controls.Count;
                FarmUITheme.Focus(_controls[next]);
                _events.sendNavigationEvents = false;
            }
            var page = keyboard.pageUpKey.wasPressedThisFrame ? 1 : keyboard.pageDownKey.wasPressedThisFrame ? -1 : 0;
            if (page != 0) ScrollPage(page);
        }

        private void LateUpdate()
        {
            if (_panel == null || !_panel.gameObject.activeInHierarchy || InputReader_AI.CapturingBinding) return;
            if (FarmSettingsUI.ReduceMotion)
                foreach (var scrollRect in _scrolls)
                    if (scrollRect != null && scrollRect.isActiveAndEnabled) scrollRect.StopMovement();
            var selected = Selected;
            var control = selected == null ? null : selected.GetComponent<Selectable>();
            if (!Available(control) || !selected.transform.IsChildOf(_panel))
            {
                Collect(_panel);
                if (_controls.Count == 0) return;
                control = _controls[0];
                FarmUITheme.Focus(control);
                selected = control.gameObject;
            }
            if (selected == _last) return;
            _last = selected;
            var scroll = selected.GetComponentInParent<ScrollRect>();
            if (scroll == null || !scroll.vertical || scroll.content == null || scroll.viewport == null
                || !selected.transform.IsChildOf(scroll.content)) return;
            Canvas.ForceUpdateCanvases();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, selected.transform);
            var view = scroll.viewport.rect;
            var offset = bounds.min.y < view.yMin ? view.yMin - bounds.min.y
                : bounds.max.y > view.yMax ? view.yMax - bounds.max.y : 0;
            if (offset == 0) return;
            scroll.StopMovement();
            var height = scroll.content.rect.height - view.height;
            if (height > 0) scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition - offset / height);
        }

        private void ScrollPage(int direction)
        {
            if (EditingText) return;
            var scroll = Selected != null ? Selected.GetComponentInParent<ScrollRect>() : null;
            if (scroll == null) scroll = _panel.GetComponentInChildren<ScrollRect>();
            if (scroll == null || !scroll.vertical || scroll.content == null || scroll.viewport == null) return;
            var height = scroll.content.rect.height - scroll.viewport.rect.height;
            if (height <= 0) return;
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = Mathf.Clamp01(
                scroll.verticalNormalizedPosition + direction * scroll.viewport.rect.height * .85f / height);
        }

        private void Collect(Transform panel)
        {
            _controls.Clear();
            if (panel == null) return;
            panel.GetComponentsInChildren(false, _controls);
            _controls.RemoveAll(control => !Available(control) || control.navigation.mode == Navigation.Mode.None);
        }

        private static bool Available(Selectable control) => control != null && control.isActiveAndEnabled && control.IsInteractable();
    }
}
