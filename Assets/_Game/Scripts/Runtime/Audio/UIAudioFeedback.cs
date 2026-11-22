using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MyLittleFarm.Audio
{
    /// <summary>Separate listener component: never replaces a button's gameplay or navigation callbacks.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Button))]
    public sealed class UIAudioFeedback : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        private Button _button;
        private void Awake() => _button = GetComponent<Button>();
        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (_button == null) _button = GetComponent<Button>();
            _button.onClick.AddListener(Clicked);
        }
        private void OnDisable() { if (_button != null) _button.onClick.RemoveListener(Clicked); }
        private void Clicked() => FarmAudio.Play(SoundId.UiClick);
        public void OnPointerEnter(PointerEventData data) { if (_button != null && _button.IsInteractable()) FarmAudio.Play(SoundId.UiHover); }
        public void OnSelect(BaseEventData data)
        { if (!(data is PointerEventData) && _button != null && _button.IsInteractable()) FarmAudio.Play(SoundId.UiHover); }
        public static void Install(Button button)
        { if (button.GetComponent<UIAudioFeedback>() == null) button.gameObject.AddComponent<UIAudioFeedback>(); }
    }
}
