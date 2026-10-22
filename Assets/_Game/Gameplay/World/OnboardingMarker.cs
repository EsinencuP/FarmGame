using UnityEngine;

namespace MyLittleFarm.Gameplay.World
{
    /// <summary>Collider-free world geometry with normal depth testing.</summary>
    public sealed class OnboardingMarker : MonoBehaviour
    {
        [SerializeField] private Transform diamond;
        private Vector3 _anchor;
        public void Show(Vector3 anchor)
        {
            _anchor = anchor;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }
        public void Hide() { if (gameObject.activeSelf) gameObject.SetActive(false); }
        private void LateUpdate()
        {
            transform.position = _anchor + Vector3.up * (.12f * Mathf.Sin(Time.unscaledTime * 2.6f));
            if (diamond != null) diamond.localRotation = Quaternion.Euler(0f, Time.unscaledTime * 45f, 0f);
        }
    }
}
