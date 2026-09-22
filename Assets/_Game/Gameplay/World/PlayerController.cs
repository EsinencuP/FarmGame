using MyLittleFarm.Core;
using UnityEngine;

namespace MyLittleFarm.Gameplay.World
{
    /// <summary>Перемещает и поворачивает игрока относительно направления изометрической камеры.</summary>
    [DefaultExecutionOrder(-150)]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        // Постоянное ускорение вниз имитирует гравитацию CharacterController.
        private const float Gravity = -20f;

        // Зависимости предоставляют команды, физическое движение, ориентацию камеры и границы мира.
        private InputReader _input;
        private CharacterController _controller;
        private Transform _cameraTransform;
        // Вертикальная скорость накапливает действие гравитации между кадрами.
        private float _verticalVelocity;
        private GridSystem _grid;

        // Параметры сериализуются, поэтому после выгрузки сцены настраиваются через Inspector.
        [SerializeField, Min(0f)] private float walkSpeed = 4.5f;
        [SerializeField, Min(0f)] private float runSpeed = 7.5f;
        [SerializeField, Min(0f)] private float jumpHeight = 1.35f;

        /// <summary>Скорость обычного движения в мировых единицах за секунду.</summary>
        public float WalkSpeed { get => walkSpeed; set => walkSpeed = Mathf.Max(0f, value); }
        /// <summary>Скорость при удержании Shift.</summary>
        public float RunSpeed { get => runSpeed; set => runSpeed = Mathf.Max(0f, value); }
        /// <summary>Высота прыжка, из которой рассчитывается начальная вертикальная скорость.</summary>
        public float JumpHeight { get => jumpHeight; set => jumpHeight = Mathf.Max(0f, value); }

        /// <summary>Получает ввод и локальные компоненты после создания игрока.</summary>
        public void Configure(InputReader input)
        {
            _input = input;
            _controller = GetComponent<CharacterController>();
            _grid = GetComponentInParent<GameBootstrap>()?.GetComponentInChildren<GridSystem>();
        }

        public void SetCamera(Transform cameraTransform)
        {
            // Transform камеры нужен для движения в направлениях, понятных на экране.
            _cameraTransform = cameraTransform;
        }

        private void Update()
        {
            // Направления камеры проецируются на горизонтальную плоскость XZ.
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

            // Двумерный ввод преобразуется в трёхмерный вектор мирового движения.
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

            // На земле небольшая отрицательная скорость удерживает CharacterController в контакте с поверхностью.
            var isGrounded = _controller.isGrounded;
            if (isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f;
            }

            // Прыжок выключен в режиме строительства вместе с горизонтальным управлением.
            if (isGrounded && _input.JumpPressed && !_input.BuildModeActive && !_input.SuppressGameplayThisFrame)
            {
                _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
            }

            _verticalVelocity += Gravity * Time.deltaTime;
            var horizontalSpeed = _input.SprintHeld ? RunSpeed : WalkSpeed;
            var velocity = move * horizontalSpeed + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);
            if (_grid != null)
            {
                var safePosition = _grid.ClampToGround(transform.position);
                if (safePosition != transform.position) Teleport(safePosition);
            }
        }

        public void Teleport(Vector3 position)
        {
            // CharacterController временно выключается, иначе прямое изменение Transform игнорируется физикой.
            _verticalVelocity = 0;
            _controller.enabled = false;
            transform.position = position;
            _controller.enabled = true;
        }
    }
}
