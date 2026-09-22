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

        // Зависимости предоставляют команды, физическое движение и ориентацию камеры.
        private InputReader _input;
        private CharacterController _controller;
        private Transform _cameraTransform;
        // Вертикальная скорость накапливает действие гравитации между кадрами.
        private float _verticalVelocity;
        [SerializeField, Min(0.1f)] private float walkSpeed = 4.5f;
        [SerializeField, Min(0.1f)] private float runSpeed = 7.5f;
        [SerializeField, Min(0.1f)] private float jumpHeight = 1.35f;

        /// <summary>Обычная горизонтальная скорость в мировых единицах за секунду.</summary>
        public float MoveSpeed { get => walkSpeed; set => walkSpeed = value; }

        /// <summary>Получает ввод и локальные компоненты после создания игрока.</summary>
        public void Configure(InputReader input)
        {
            _input = input;
            _controller = GetComponent<CharacterController>();
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

            // Небольшая отрицательная скорость удерживает контроллер на земле; прыжок задаёт скорость из высоты.
            if (_controller.isGrounded)
            {
                _verticalVelocity = -1f;
                if (_input.JumpPressed && !_input.BuildModeActive && !_input.SuppressGameplayThisFrame)
                    _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * Gravity);
            }
            else
            {
                _verticalVelocity += Gravity * Time.deltaTime;
            }

            var horizontalSpeed = _input.SprintHeld ? runSpeed : walkSpeed;
            var velocity = move * horizontalSpeed + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);
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
