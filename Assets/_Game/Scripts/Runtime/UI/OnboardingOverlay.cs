using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.World;
using UnityEngine;
using UnityEngine.UI;

namespace MyLittleFarm.UI
{
    /// <summary>Event-driven optional goal card; its host remains enabled when the card disappears.</summary>
    public sealed class OnboardingOverlay : MonoBehaviour
    {
        [SerializeField] private RectTransform card;
        [SerializeField] private Text hint;
        [SerializeField] private Button skip;
        [SerializeField] private bool showOnHud = true;
        private OnboardingSystem _onboarding;
        private InputReader _input;
        private int _bindingRevision = -1;
        public RectTransform Card => card;
        public bool IsVisible => card != null && card.gameObject.activeSelf;
        public void HidePresentation() { showOnHud = false; if (card != null) card.gameObject.SetActive(false); }
        public void Configure(OnboardingSystem onboarding)
        {
            _onboarding = onboarding;
            _input = GetComponentInParent<GameBootstrap>()?.GetComponent<InputReader>();
            if (skip != null) { skip.onClick.RemoveListener(Skip); skip.onClick.AddListener(Skip); }
            Refresh(0);
        }
        private void OnEnable() => GameEvents.OnboardingChanged += Refresh;
        private void OnDisable() => GameEvents.OnboardingChanged -= Refresh;
        private void Skip() => _onboarding?.Skip();
        private void Update()
        {
            if (card == null || _onboarding == null) return;
            if (card.gameObject.activeSelf != ShouldShow || _bindingRevision != InputReader.BindingRevision)
                Refresh(0);
        }
        private bool ShouldShow => showOnHud && _onboarding.IsActive && _input?.ModalInputActive != true;
        private void Refresh(int ignored)
        {
            if (_onboarding == null || card == null) return;
            _bindingRevision = InputReader.BindingRevision;
            card.gameObject.SetActive(ShouldShow);
            if (hint != null) hint.text = _onboarding.CurrentHint;
            if (skip != null) skip.gameObject.SetActive(_onboarding.IsActive && _onboarding.CanSkip);
        }
        public void Preview(int step, bool returningPlayer)
        {
            if (Application.isPlaying) return;
            card.gameObject.SetActive(showOnHud && step < OnboardingSystem.StepCount);
            hint.text = OnboardingSystem.HintFor(step);
            skip.gameObject.SetActive(returningPlayer && step < OnboardingSystem.StepCount);
        }
    }
}
