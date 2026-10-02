using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using UnityEngine;

namespace MyLittleFarm.Gameplay.World
{
    /// <summary>Перемещает и поворачивает игрока относительно направления изометрической камеры.</summary>
    [DefaultExecutionOrder(-150)]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Скорость обычной ходьбы в мировых единицах за секунду.")]
        [SerializeField, Min(0.1f)] private float walkSpeed = 4.5f;
        [Tooltip("Скорость бега при удержании Shift.")]
        [SerializeField, Min(0.1f)] private float runSpeed = 7.5f;
        [Tooltip("Максимальная высота прыжка в мировых единицах.")]
        [SerializeField, Min(0.1f)] private float jumpHeight = 1.35f;
        [Tooltip("Ускорение вниз. Значение должно оставаться отрицательным.")]
        [SerializeField, Range(-100f, -1f)] private float gravity = -20f;
        [Tooltip("Небольшая скорость вниз удерживает CharacterController на поверхности.")]
        [SerializeField, Range(-10f, -0.01f)] private float groundedVelocity = -1f;
        [Tooltip("Скорость плавного разворота персонажа к направлению движения.")]
        [SerializeField, Min(0.1f)] private float rotationSpeed = 14f;

        // Зависимости предоставляют команды, физическое движение и ориентацию камеры.
        private InputReader _input;
        private CharacterController _controller;
        private Transform _cameraTransform;
        private GridSystem _grid;
        // Вертикальная скорость накапливает действие гравитации между кадрами.
        private float _verticalVelocity;

        /// <summary>Обычная горизонтальная скорость в мировых единицах за секунду.</summary>
        public float MoveSpeed { get => walkSpeed; set => walkSpeed = Mathf.Max(0.1f, value); }
        /// <summary>Скорость бега, доступная проверкам и другим игровым системам только для чтения.</summary>
        public float RunSpeed => runSpeed;
        /// <summary>Высота прыжка, установленная в Inspector.</summary>
        public float JumpHeight => jumpHeight;

        private void OnValidate()
        {
            // Бег по смыслу не должен становиться медленнее обычной ходьбы после ручной настройки.
            runSpeed = Mathf.Max(walkSpeed, runSpeed);
        }

        /// <summary>Получает ввод и локальные компоненты после создания игрока.</summary>
        public void Configure(InputReader input, GridSystem grid = null)
        {
            _input = input;
            _controller = GetComponent<CharacterController>();
            _grid = grid;
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
                    rotationSpeed * Time.deltaTime);
            }

            // Небольшая отрицательная скорость удерживает контроллер на земле; прыжок задаёт скорость из высоты.
            if (_controller.isGrounded)
            {
                _verticalVelocity = groundedVelocity;
                if (_input.JumpPressed && !_input.BuildModeActive && !_input.SuppressGameplayThisFrame)
                    _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            else
            {
                _verticalVelocity += gravity * Time.deltaTime;
            }

            var horizontalSpeed = _input.SprintHeld ? runSpeed : walkSpeed;
            // Проверяются промежуточные клетки, чтобы бег при редком кадре не перескочил закрытый сектор.
            if (_grid != null && move.sqrMagnitude > 0f
                && !CanTraverse(move * horizontalSpeed * Time.deltaTime)) move = Vector3.zero;
            var velocity = move * horizontalSpeed + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);
        }

        /// <summary>Проверяет весь горизонтальный отрезок движения малыми шагами по GridSystem.</summary>
        private bool CanTraverse(Vector3 displacement)
        {
            var distance = displacement.magnitude;
            var steps = Mathf.Max(1, Mathf.CeilToInt(distance / (_grid.CellSize * 0.45f)));
            // Необычно большой кадр не должен вызывать тысячи проверок и пропускать границу мира.
            if (steps > 128) return false;
            for (var index = 1; index <= steps; index++)
            {
                var point = transform.position + displacement * (index / (float)steps);
                if (!_grid.IsWalkable(_grid.WorldToGrid(point))) return false;
            }
            return true;
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
