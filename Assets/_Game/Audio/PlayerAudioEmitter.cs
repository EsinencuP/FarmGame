using UnityEngine;

namespace MyLittleFarm.Audio
{
    /// <summary>Distance-based footsteps follow actual movement, never requested movement into a wall.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(CharacterController))]
    public sealed class PlayerAudioEmitter : MonoBehaviour
    {
        [SerializeField, Min(.1f)] private float stepDistance = .9f;
        private CharacterController _controller;
        private Vector3 _previous;
        private bool _grounded;
        private float _distance, _airTime;
        private void OnEnable()
        { _controller = GetComponent<CharacterController>(); _previous = transform.position; _grounded = _controller.isGrounded; _distance = _airTime = 0; }
        private void LateUpdate()
        {
            var position = transform.position; var delta = position - _previous; _previous = position;
            if (!Application.isPlaying || Time.timeScale <= 0 || !_controller.enabled) return;
            var grounded = _controller.isGrounded;
            if (!grounded) _airTime += Time.deltaTime;
            if (_grounded && !grounded && _controller.velocity.y > 1) FarmAudio.Play(SoundId.Jump, position);
            if (!_grounded && grounded) { if (_airTime > .12f) FarmAudio.Play(SoundId.Land, position); _airTime = 0; }
            _grounded = grounded; delta.y = 0;
            if (!grounded || delta.sqrMagnitude > 9) { _distance = 0; return; }
            if (delta.sqrMagnitude < .000001f) { _distance = 0; return; }
            _distance += delta.magnitude;
            if (_distance >= stepDistance) { _distance %= stepDistance; FarmAudio.Play(SoundId.Footstep, position); }
        }
    }
}
