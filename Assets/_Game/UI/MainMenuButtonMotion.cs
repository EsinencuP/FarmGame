using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MyLittleFarm.UI
{
    /// <summary>Animate only the plaque artwork; its pointer target never moves or grows.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Button))]
    public sealed class MainMenuButtonMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform artwork;
        [SerializeField, Range(1, 1.1f)] private float hoverScale = 1.04f;
        [SerializeField, Min(.01f)] private float duration = .14f;
        private Button _button;
        private Coroutine _motion;
        private bool _hovered, _pressed;

        public void Configure(RectTransform visual)
        {
            ResetVisual(); artwork = visual;
            if (artwork != null) artwork.localScale = Vector3.one;
        }
        private void Awake() => _button = GetComponent<Button>();
        private void OnDisable() => ResetVisual();
        private void ResetVisual()
        {
            if (_motion != null) StopCoroutine(_motion);
            _motion = null; _hovered = _pressed = false;
            if (artwork != null) artwork.localScale = Vector3.one;
        }
        public void OnPointerEnter(PointerEventData data) { _hovered = true; Animate(); }
        public void OnPointerExit(PointerEventData data) { _hovered = _pressed = false; Animate(); }
        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left) return;
            _pressed = true; Animate();
        }
        public void OnPointerUp(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left) return;
            _pressed = false; Animate();
        }
        private void Animate()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || artwork == null) return;
            if (_motion != null) StopCoroutine(_motion);
            var available = _button != null && _button.IsInteractable();
            var scale = !available || !_hovered ? 1 : _pressed ? .99f : hoverScale;
            _motion = StartCoroutine(Tween(scale));
        }
        private IEnumerator Tween(float scale)
        {
            var from = artwork.localScale; var to = Vector3.one * scale;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                // Cubic ease-out, restarted from the current scale on rapid pointer changes.
                artwork.localScale = Vector3.LerpUnclamped(from, to, 1 - Mathf.Pow(1 - t, 3));
                yield return null;
            }
            artwork.localScale = to; _motion = null;
        }
    }
}
