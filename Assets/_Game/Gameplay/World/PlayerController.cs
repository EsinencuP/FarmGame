using MyLittleFarm.Core;
using UnityEngine;

namespace MyLittleFarm.Gameplay.World
{
    [DefaultExecutionOrder(-150)]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        private const float Gravity = -20f;

        private InputReader _input;
        private CharacterController _controller;
        private Transform _cameraTransform;
        private float _verticalVelocity;
        private GridSystem _grid;

        public float MoveSpeed { get; set; } = 4.5f;

        public void Configure(InputReader input)
        {
            _input = input;
            _controller = GetComponent<CharacterController>();
            _grid = GetComponentInParent<GameBootstrap>()?.GetComponentInChildren<GridSystem>();
        }

        public void SetCamera(Transform cameraTransform)
        {
            _cameraTransform = cameraTransform;
        }

        private void Update()
        {
            if (_input == null || _cameraTransform == null)
            {
                return;
            }

            var forward = _cameraTransform.forward;
            forward.y = 0f;
            forward.Normalize();
            var right = _cameraTransform.right;
            right.y = 0f;
            right.Normalize();

            var move = forward * _input.Movement.y + right * _input.Movement.x;
            if (_input.BuildModeActive || _input.SuppressGameplayThisFrame) move = Vector3.zero;
            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }

            if (move.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(move, Vector3.up),
                    14f * Time.deltaTime);
            }

            _verticalVelocity = _controller.isGrounded ? -1f : _verticalVelocity + Gravity * Time.deltaTime;
            var velocity = move * MoveSpeed + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);
            if (_grid != null)
            {
                var safePosition = _grid.ClampToGround(transform.position);
                if (safePosition != transform.position) Teleport(safePosition);
            }
        }

        public void Teleport(Vector3 position)
        {
            _verticalVelocity = 0;
            _controller.enabled = false;
            transform.position = position;
            _controller.enabled = true;
        }
    }
}
