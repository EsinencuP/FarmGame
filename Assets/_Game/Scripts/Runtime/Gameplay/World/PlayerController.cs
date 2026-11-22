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
        private InputReader_AI _input;
        private CharacterController _controller;
        private Transform _cameraTransform;
        private GridSystem _grid;
        private readonly Collider[] _spawnContacts = new Collider[16];
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
        public bool HoldingHoe {get;internal set;}
        internal bool HoldingFishingRod { get; set; }
        public Transform VisualRoot => transform.Find("Character Visual") ?? transform.Find("Nolant Visual");

        private void OnValidate()
        {
            // Бег по смыслу не должен становиться медленнее обычной ходьбы после ручной настройки.
            runSpeed = Mathf.Max(walkSpeed, runSpeed);
            sprintSpeed = Mathf.Max(runSpeed, sprintSpeed);
        }

        /// <summary>Получает ввод и локальные компоненты после создания игрока.</summary>
        public void Configure(InputReader_AI input, GridSystem grid = null)
        {
            _input = input;
            _controller = GetComponent<CharacterController>();
            // Slow slide/grounded steps can be shorter than the old 1 mm filter on fast displays.
            _controller.minMoveDistance = 0f;
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
            var start = transform.position;
            var horizontal = move * (horizontalSpeed * Time.deltaTime);
            if (_grid != null && !CanTraverse(start, horizontal))
            {
                // A diagonal into a terrain boundary can still slide along its free side.
                var x = new Vector3(horizontal.x, 0f, 0f);
                var z = new Vector3(0f, 0f, horizontal.z);
                if (!CanTraverse(start, x)) x = Vector3.zero;
                if (!CanTraverse(start, z)) z = Vector3.zero;
                horizontal = x.sqrMagnitude >= z.sqrMagnitude ? x : z;
            }
            var collision = _controller.Move(horizontal + Vector3.up * (_verticalVelocity * Time.deltaTime));
            if (_grid != null && !CanTraverse(start, transform.position - start))
            {
                // Physics can slide or depenetrate the controller in a different direction than requested.
                SetPosition(new Vector3(start.x, transform.position.y, start.z));
            }
            if ((collision & CollisionFlags.Above) != 0 && _verticalVelocity > 0f)
                _verticalVelocity = 0f;
            if (_animatorHasGrounded) _animator.SetBool(GroundedParameter, _controller.isGrounded);
            if (_animatorHasVerticalSpeed) _animator.SetFloat(VerticalSpeedParameter, _verticalVelocity);
            if (_animatorHasSprinting) _animator.SetBool(SprintingParameter, _input.SprintHeld && horizontal.sqrMagnitude > 0f);
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
                var pose = fastSprint && speed > 0.1f ? 2 : HoldingHoe || HoldingFishingRod || Time.time < _gripUntil ? 1 : 0;
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

        /// <summary>Visits each grid cell crossed by the horizontal segment, including on a long frame.</summary>
        private bool CanTraverse(Vector3 start, Vector3 displacement)
        {
            var cell = _grid.WorldToGrid(start);
            var destination = _grid.WorldToGrid(start + displacement);
            if (cell == destination) return true;

            var size = _grid.CellSize;
            var stepX = destination.x.CompareTo(cell.x);
            var stepZ = destination.y.CompareTo(cell.y);
            var nextX = stepX == 0 ? float.PositiveInfinity
                : ((cell.x + (stepX > 0 ? 1 : 0)) * size - start.x) / displacement.x;
            var nextZ = stepZ == 0 ? float.PositiveInfinity
                : ((cell.y + (stepZ > 0 ? 1 : 0)) * size - start.z) / displacement.z;
            var strideX = stepX == 0 ? float.PositiveInfinity : size / Mathf.Abs(displacement.x);
            var strideZ = stepZ == 0 ? float.PositiveInfinity : size / Mathf.Abs(displacement.z);

            while (cell != destination)
            {
                if (cell.x == destination.x) nextX = float.PositiveInfinity;
                if (cell.y == destination.y) nextZ = float.PositiveInfinity;
                if (nextX == nextZ && !_grid.IsWalkable(cell + new Vector2Int(stepX, 0))
                    && !_grid.IsWalkable(cell + new Vector2Int(0, stepZ))) return false;
                var crossX = nextX <= nextZ;
                var crossZ = nextZ <= nextX;
                if (crossX) { cell.x += stepX; nextX += strideX; }
                if (crossZ) { cell.y += stepZ; nextZ += strideZ; }
                if (!_grid.IsWalkable(cell)) return false;
            }
            return true;
        }

        public void Teleport(Vector3 position)
        {
            _verticalVelocity = 0;
            SetPosition(position);
        }

        internal bool TryFindSafeRestorePosition(Vector3 requested, out Vector3 position)
        {
            position = requested;
            if (_grid == null) return true;
            Physics.SyncTransforms();
            if (HasClearance(requested)) return true;

            var offset = transform.TransformVector(_controller.center);
            var halfHeight = _controller.height * Mathf.Abs(transform.lossyScale.y) * 0.5f;
            var spacing = _grid.CellSize * 0.25f;
            var best = float.PositiveInfinity;
            for (var ring = 0; ring <= 24 && ring * spacing <= best; ring++)
            {
                for (var z = -ring; z <= ring; z++)
                for (var x = -ring; x <= ring; x++)
                {
                    if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) != ring) continue;
                    var distance = new Vector2(x, z).magnitude * spacing;
                    if (distance >= best) continue;
                    var candidate = requested + new Vector3(x * spacing, 0f, z * spacing);
                    candidate.y = _grid.GetSurfaceHeight(candidate.x, candidate.z) + halfHeight - offset.y + 0.03f;
                    if (!CanTraverse(requested, candidate - requested) || !HasClearance(candidate)) continue;
                    position = candidate;
                    best = distance;
                }
            }
            return !float.IsPositiveInfinity(best);
        }

        private bool HasClearance(Vector3 position)
        {
            if (!_grid.IsWalkable(_grid.WorldToGrid(position))) return false;
            var scale = transform.lossyScale;
            var center = position + transform.TransformVector(_controller.center);
            var halfHeight = _controller.height * Mathf.Abs(scale.y) * 0.5f;
            var fullRadius = _controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            var radius = Mathf.Max(.01f, fullRadius - _controller.skinWidth);
            var stem = Vector3.up * Mathf.Max(0f, halfHeight - fullRadius);
            var ground = _grid.GetSurfaceHeight(position.x, position.z);
            var feet = center - Vector3.up * halfHeight;
            if (feet.y < ground - _controller.skinWidth) return false;
            if (feet.y > ground + jumpHeight + 1f
                && !Physics.Raycast(feet + Vector3.up * .08f, Vector3.down, .31f, ~0, QueryTriggerInteraction.Ignore))
                return false;
            var count = Physics.OverlapCapsuleNonAlloc(center - stem, center + stem, radius,
                _spawnContacts, ~0, QueryTriggerInteraction.Ignore);
            if (count == _spawnContacts.Length) return false;
            for (var i = 0; i < count; i++)
                if (_spawnContacts[i] != _controller) return false;
            return true;
        }

        private void SetPosition(Vector3 position)
        {
            // CharacterController временно выключается, иначе прямое изменение Transform игнорируется физикой.
            _controller.enabled = false;
            transform.position = position;
            _controller.enabled = true;
        }
    }
}
