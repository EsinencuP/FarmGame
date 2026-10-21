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
        public const float RunAnimationThreshold = 3.2f;
        [Header("Movement")]
        [Tooltip("Скорость обычной ходьбы в мировых единицах за секунду.")]
        [SerializeField, Min(0.1f)] private float walkSpeed = 2.2f;
        [Tooltip("Скорость бега при удержании Shift.")]
        [SerializeField, Min(0.1f)] private float runSpeed = 4.5f;
        [SerializeField, Min(0.1f)] private float sprintSpeed = 6.5f;
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
        private Animator _animator;
        private bool _animatorHasSpeed;
        private bool _animatorHasGrounded;
        private bool _animatorHasVerticalSpeed;
        private bool _animatorHasSprinting;
        private bool _animatorHasGaitPlayback;
        private bool _directionalAnimator;
        private float _idleSeconds;
        private float _gripUntil;
        private static readonly int SpeedParameter = Animator.StringToHash("Speed");
        private static readonly int GroundedParameter = Animator.StringToHash("Grounded");
        private static readonly int VerticalSpeedParameter = Animator.StringToHash("VerticalSpeed");
        private static readonly int SprintingParameter = Animator.StringToHash("Sprinting");
        private static readonly int GaitPlaybackParameter = Animator.StringToHash("GaitPlayback");
        // Вертикальная скорость накапливает действие гравитации между кадрами.
        private float _verticalVelocity;

        /// <summary>Обычная горизонтальная скорость в мировых единицах за секунду.</summary>
        public float MoveSpeed { get => walkSpeed; set => walkSpeed = Mathf.Max(0.1f, value); }
        /// <summary>Скорость бега, доступная проверкам и другим игровым системам только для чтения.</summary>
        public float RunSpeed => runSpeed;
        /// <summary>Высота прыжка, установленная в Inspector.</summary>
        public float JumpHeight => jumpHeight;
        public float SprintSpeed => sprintSpeed;
        public Transform VisualRoot => transform.Find("Character Visual") ?? transform.Find("Nolant Visual");

        private void OnValidate()
        {
            // Бег по смыслу не должен становиться медленнее обычной ходьбы после ручной настройки.
            runSpeed = Mathf.Max(walkSpeed, runSpeed);
            sprintSpeed = Mathf.Max(runSpeed, sprintSpeed);
        }

        /// <summary>Получает ввод и локальные компоненты после создания игрока.</summary>
        public void Configure(InputReader input, GridSystem grid = null)
        {
            _input = input;
            _controller = GetComponent<CharacterController>();
            _grid = grid;
            _animator = GetComponentInChildren<Animator>(true);
            _animatorHasSpeed = false;
            _animatorHasGrounded = _animatorHasVerticalSpeed = _animatorHasSprinting = false;
            _animatorHasGaitPlayback = false;
            _directionalAnimator = false;
            if (_animator != null)
            {
                _animator.applyRootMotion = false;
                foreach (var parameter in _animator.parameters)
                {
                    if (parameter.name == "MoveX") _directionalAnimator = true;
                    if (parameter.nameHash == SpeedParameter && parameter.type == AnimatorControllerParameterType.Float)
                        _animatorHasSpeed = true;
                    if (parameter.nameHash == GroundedParameter && parameter.type == AnimatorControllerParameterType.Bool)
                        _animatorHasGrounded = true;
                    if (parameter.nameHash == VerticalSpeedParameter && parameter.type == AnimatorControllerParameterType.Float)
                        _animatorHasVerticalSpeed = true;
                    if (parameter.nameHash == SprintingParameter && parameter.type == AnimatorControllerParameterType.Bool)
                        _animatorHasSprinting = true;
                    if (parameter.nameHash == GaitPlaybackParameter && parameter.type == AnimatorControllerParameterType.Float)
                        _animatorHasGaitPlayback = true;
                }
            }
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

            var facing = _input.StrafeHeld ? forward : move;
            var turn = facing.sqrMagnitude > 0.001f ? Vector3.SignedAngle(transform.forward, facing, Vector3.up) : 0f;
            if (facing.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(facing, Vector3.up),
                    1f - Mathf.Exp(-rotationSpeed * Time.deltaTime));
            }

            // Небольшая отрицательная скорость удерживает контроллер на земле; прыжок задаёт скорость из высоты.
            if (_controller.isGrounded)
            {
                _verticalVelocity = groundedVelocity;
                if (_input.JumpPressed && !_input.BuildModeActive && !_input.SuppressGameplayThisFrame)
                {
                    _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                    if (_directionalAnimator) _animator.SetTrigger("Jump");
                }
            }
            else
            {
                _verticalVelocity += gravity * Time.deltaTime;
            }

            var fastSprint = _input.FastSprintHeld && Vector3.Dot(move, transform.forward) >= -0.1f;
            var horizontalSpeed = fastSprint ? sprintSpeed : _input.SprintHeld ? runSpeed : walkSpeed;
            // Проверяются промежуточные клетки, чтобы бег при редком кадре не перескочил закрытый сектор.
            if (_grid != null && move.sqrMagnitude > 0f
                && !CanTraverse(move * horizontalSpeed * Time.deltaTime)) move = Vector3.zero;
            var velocity = move * horizontalSpeed + Vector3.up * _verticalVelocity;
            var collision = _controller.Move(velocity * Time.deltaTime);
            if ((collision & CollisionFlags.Above) != 0 && _verticalVelocity > 0f)
                _verticalVelocity = 0f;
            if (_animatorHasGrounded) _animator.SetBool(GroundedParameter, _controller.isGrounded);
            if (_animatorHasVerticalSpeed) _animator.SetFloat(VerticalSpeedParameter, _verticalVelocity);
            if (_animatorHasSprinting) _animator.SetBool(SprintingParameter, _input.SprintHeld && move.sqrMagnitude > 0.01f);
            if (_directionalAnimator)
            {
                var actual = _controller.velocity;
                actual.y = 0f;
                var speed = actual.magnitude;
                var local = transform.InverseTransformDirection(speed > 0.08f ? actual / speed : Vector3.zero);
                _idleSeconds = speed < 0.08f && _controller.isGrounded ? _idleSeconds + Time.deltaTime : 0f;
                _animator.SetFloat("Speed", speed, 0.08f, Time.deltaTime);
                _animator.SetFloat("MoveX", local.x, 0.08f, Time.deltaTime);
                _animator.SetFloat("MoveZ", local.z, 0.08f, Time.deltaTime);
                _animator.SetFloat("Gait", speed < 0.08f ? 0f
                    : fastSprint && speed > (runSpeed + sprintSpeed) * 0.5f ? 3f
                    : speed > RunAnimationThreshold ? 2f : 1f,
                    0.1f, Time.deltaTime);
                _animator.SetFloat("Playback", speed > 0.08f ? Mathf.Clamp(speed / horizontalSpeed, 0.6f, 1.5f) : 1f);
                _animator.SetFloat("Turn", speed < 0.08f ? turn / 90f : 0f);
                _animator.SetBool("Relaxed", _idleSeconds % 20f >= 6f && _idleSeconds % 20f < 11f);
                if (_input.GesturePressed && speed < 0.08f && !_input.BuildModeActive && !_input.SuppressGameplayThisFrame)
                    _animator.SetTrigger("Gesture");
                if (speed >= 0.08f || _input.BuildModeActive) _animator.ResetTrigger("Gesture");
                if (_input.InteractPressed && !_input.BuildModeActive) _gripUntil = Time.time + 0.35f;
                var pose = fastSprint && speed > 0.1f ? 2 : Time.time < _gripUntil ? 1 : 0;
                _animator.SetInteger("HandPose", pose);
                _animator.SetLayerWeight(1, Mathf.MoveTowards(_animator.GetLayerWeight(1), pose == 0 ? 0f : 1f, Time.deltaTime * 8f));
                return;
            }
            if (_animatorHasSpeed)
            {
                var actual = _controller.velocity;
                actual.y = 0f;
                if (_animatorHasGaitPlayback)
                    _animator.SetFloat(GaitPlaybackParameter, actual.magnitude > 0.1f
                        ? Mathf.Clamp(actual.magnitude / walkSpeed, 0.25f, 2f) : 1f);
                _animator.SetFloat(SpeedParameter, Mathf.Clamp01(actual.magnitude / runSpeed),
                    0.12f, Time.deltaTime);
            }
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
