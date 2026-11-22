using System.Collections.Generic;
using MyLittleFarm.Core;
using MyLittleFarm.UI;
using UnityEngine;
using UnityEngine.Rendering;

namespace MyLittleFarm.Gameplay.World
{
    /// <summary>Удерживает изометрическую камеру вокруг игрока и обрабатывает поворот и масштаб.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class IsometricCameraController : MonoBehaviour
    {
        [Header("Camera Orbit")]
        [Tooltip("Начальный угол камеры вокруг вертикальной оси.")]
        [SerializeField] private float initialYaw = 45f;
        [Tooltip("Начальная дистанция камеры до точки слежения.")]
        [SerializeField, Min(0.1f)] private float initialDistance = 13f;
        [Tooltip("Вертикальный угол изометрической камеры.")]
        [SerializeField, Range(5f, 85f)] private float pitch = 38f;
        [Tooltip("Высота точки, на которую смотрит камера относительно игрока.")]
        [SerializeField] private float lookHeight = 0.8f;
        [Tooltip("Поворот камеры клавишами Q и Shift+E в градусах.")]
        [SerializeField, Min(1f)] private float keyboardRotationStep = 90f;
        [Tooltip("Чувствительность вращения средней кнопкой мыши.")]
        [SerializeField, Min(0.001f)] private float orbitSensitivity = 0.15f;
        [Tooltip("Чувствительность масштабирования колёсиком мыши.")]
        [SerializeField, Min(0.001f)] private float zoomSensitivity = 1.35f;
        [Tooltip("Минимальная дистанция приближения камеры.")]
        [SerializeField, Min(0.1f)] private float minDistance = 5.4f;
        [Tooltip("Максимальная дистанция удаления камеры.")]
        [SerializeField, Min(0.1f)] private float maxDistance = 20f;
        [Tooltip("Время сглаживания поворота камеры в секундах.")]
        [SerializeField, Min(0.01f)] private float orbitSmoothTime = 0.18f;
        [Tooltip("Время сглаживания масштаба в секундах.")]
        [SerializeField, Min(0.01f)] private float zoomSmoothTime = 0.12f;

        // Input задаёт команды, target — игрок, yaw — текущий угол, distance — текущий масштаб обзора.
        private InputReader _input;
        private Transform _target;
        private float _yaw;
        private float _distance;
        private float _targetYaw;
        private float _targetDistance;
        private float _yawVelocity;
        private float _distanceVelocity;
        private bool _fishing;
        private Vector3 _fishingWater;
        private float _fishingBlend;
        private Camera _camera;
        private readonly List<(Renderer Visual, int SubMeshes)> _playerVisuals = new();
        private readonly List<Renderer> _occluders = new();
        private readonly RaycastHit[] _hits = new RaycastHit[32];
        private Material _silhouette;
        private CommandBuffer _silhouettePass;
        private bool _showingSilhouette;
        private const string ZoomPreference = "MyLittleFarm.CameraDistance";
        private bool _storeZoom;
        private bool _zoomChanged;

        private void OnValidate()
        {
            // Сохраняет корректный диапазон зума при редактировании значений в Inspector.
            maxDistance = Mathf.Max(minDistance, maxDistance);
            initialDistance = Mathf.Clamp(initialDistance, minDistance, maxDistance);
        }

        public void Configure(InputReader input, Transform target)
        {
            // После получения цели камера сразу занимает правильное положение без первого скачка.
            _input = input;
            _target = target;
            _yaw = initialYaw;
            _storeZoom = Application.isPlaying && gameObject.scene.name == "Prototype";
            var startingDistance = _storeZoom ? PlayerPrefs.GetFloat(ZoomPreference, initialDistance) : initialDistance;
            if (float.IsNaN(startingDistance) || float.IsInfinity(startingDistance)) startingDistance = initialDistance;
            _distance = Mathf.Clamp(startingDistance, minDistance, Mathf.Max(minDistance, maxDistance));
            _targetYaw = _yaw;
            _targetDistance = _distance;
            _yawVelocity = 0f;
            _distanceVelocity = 0f;
            _fishing = false;
            _fishingBlend = 0f;
            ConfigureVisibility();
            SnapToTarget();
        }

        private void LateUpdate()
        {
            // LateUpdate выполняется после движения игрока, поэтому камера следует за актуальной позицией.
            if (_input == null || _target == null)
            {
                return;
            }

            if (!_input.ModalInputActive && !_fishing)
            {
                if (!_input.BuildModeActive && _input.RotateLeftPressed) _targetYaw -= keyboardRotationStep;
                if (!_input.BuildModeActive && _input.RotateRightPressed) _targetYaw += keyboardRotationStep;

                _targetYaw += _input.OrbitDelta * orbitSensitivity;
                if (_input.ZoomDelta != 0f)
                {
                    _targetDistance = Mathf.Clamp(_targetDistance - _input.ZoomDelta * zoomSensitivity,
                        minDistance, Mathf.Max(minDistance, maxDistance));
                    if (_storeZoom)
                    {
                        PlayerPrefs.SetFloat(ZoomPreference, _targetDistance);
                        _zoomChanged = true;
                    }
                }
            }
            _yaw = Mathf.SmoothDamp(_yaw, _targetYaw, ref _yawVelocity,
                orbitSmoothTime, Mathf.Infinity, Time.deltaTime);
            _distance = Mathf.SmoothDamp(_distance, _targetDistance, ref _distanceVelocity,
                zoomSmoothTime, Mathf.Infinity, Time.deltaTime);
            // Keep both angles in the same range without losing the direction of repeated turns.
            if (Mathf.Abs(_yaw) > 360f)
            {
                var offset = Mathf.Floor(_yaw / 360f) * 360f;
                _yaw -= offset;
                _targetYaw -= offset;
            }
            var fishingTarget = _fishing ? 1f : 0f;
            _fishingBlend = FarmSettingsUI.ReduceMotion ? fishingTarget
                : Mathf.MoveTowards(_fishingBlend, fishingTarget, Time.unscaledDeltaTime / .28f);
            SnapToTarget();
        }

        internal void BeginFishing(Vector3 water)
        {
            _fishingWater = water;
            _fishing = true;
        }

        internal void EndFishing(bool immediate)
        {
            _fishing = false;
            if (immediate) RefreshPosition();
        }

        public void RefreshPosition()
        {
            if (_target == null) return;
            _yaw = _targetYaw;
            _distance = _targetDistance;
            _yawVelocity = _distanceVelocity = 0;
            _fishingBlend = _fishing ? 1f : 0f;
            SnapToTarget();
        }

        private void SnapToTarget()
        {
            // Камера смотрит немного выше основания игрока под постоянным углом 38 градусов.
            var lookPoint = _target.position + Vector3.up * lookHeight;
            var rotation = Quaternion.Euler(pitch, _yaw, 0f);
            var offset = rotation * (Vector3.back * _distance);
            var aim = lookPoint;
            if (_fishingBlend > 0f)
            {
                var shoreDirection = Vector3.ProjectOnPlane(_fishingWater - _target.position, Vector3.up);
                if (shoreDirection.sqrMagnitude > .001f)
                {
                    var blend = Mathf.SmoothStep(0f, 1f, _fishingBlend);
                    var overShoulder = -shoreDirection.normalized * 3.9f + Vector3.up * 2.9f;
                    offset = Vector3.Slerp(offset, overShoulder, blend);
                    aim = Vector3.Lerp(lookPoint, Vector3.Lerp(_target.position, _fishingWater, .61f)
                        + Vector3.up * .35f, blend);
                    offset = KeepFishingCameraClear(lookPoint, offset);
                }
            }
            var position = lookPoint + offset;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(aim - position, Vector3.up));
            UpdateSilhouette(lookPoint);
        }

        private Vector3 KeepFishingCameraClear(Vector3 origin, Vector3 offset)
        {
            var distance = offset.magnitude;
            var direction = offset / distance;
            var count = Physics.SphereCastNonAlloc(origin, .21f, direction, _hits, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (hit.collider != null && !hit.collider.transform.IsChildOf(_target))
                    distance = Mathf.Min(distance, Mathf.Max(.45f, hit.distance - .1f));
            }
            return direction * distance;
        }

        private void ConfigureVisibility()
        {
            ReleaseSilhouette();
            _occluders.Clear();
            _playerVisuals.Clear();
            var player = _target == null ? null : _target.GetComponent<PlayerController>();
            if (!Application.isPlaying || player == null || player.VisualRoot == null) return;
            var shader = Resources.Load<Shader>("PlayerOcclusion");
            if (shader == null || !shader.isSupported) return;

            _camera = GetComponent<Camera>();
            foreach (var visual in player.VisualRoot.GetComponentsInChildren<Renderer>())
            {
                var mesh = visual is SkinnedMeshRenderer skinned ? skinned.sharedMesh : visual.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh != null) _playerVisuals.Add((visual, mesh.subMeshCount));
            }
            var root = player.GetComponentInParent<GameBootstrap>();
            if (root != null)
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                    if (!renderer.transform.IsChildOf(_target) && renderer.bounds.size.y > 1.5f)
                        _occluders.Add(renderer);
            _silhouette = new Material(shader) { hideFlags = HideFlags.DontSave };
            _silhouettePass = new CommandBuffer { name = "Occluded farm player" };
            _camera.AddCommandBuffer(CameraEvent.AfterForwardOpaque, _silhouettePass);
        }

        private void UpdateSilhouette(Vector3 lookPoint)
        {
            if (_silhouettePass == null) return;
            // Bounds catch the visual-only landscape props. Solid geometry also includes new buildings.
            var show = IsObscured(lookPoint + Vector3.up * .35f) || IsObscured(lookPoint - Vector3.up * .25f);
            if (show == _showingSilhouette) return;
            _showingSilhouette = show;
            _silhouettePass.Clear();
            if (!show) return;
            for (var pass = 0; pass < 3; pass++)
            foreach (var part in _playerVisuals)
            {
                var visual = part.Visual;
                if (visual == null || !visual.enabled || !visual.gameObject.activeInHierarchy) continue;
                for (var i = 0; i < part.SubMeshes; i++) _silhouettePass.DrawRenderer(visual, _silhouette, i, pass);
            }
        }

        private bool IsObscured(Vector3 point)
        {
            var delta = point - transform.position;
            var distance = delta.magnitude - .45f;
            if (distance <= 0) return false;
            var ray = new Ray(transform.position, delta.normalized);
            foreach (var renderer in _occluders)
                if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy && !renderer.forceRenderingOff
                    && renderer.bounds.IntersectRay(ray, out var hit) && hit < distance) return true;
            var count = Physics.RaycastNonAlloc(ray, _hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
                if (_hits[i].collider != null && !_hits[i].collider.transform.IsChildOf(_target)) return true;
            return false;
        }

        private void OnEnable()
        {
            if (_target != null) ConfigureVisibility();
        }

        private void OnDisable()
        {
            ReleaseSilhouette();
            if (_zoomChanged)
            {
                PlayerPrefs.Save();
                _zoomChanged = false;
            }
        }

        private void ReleaseSilhouette()
        {
            _showingSilhouette = false;
            if (_silhouettePass != null)
            {
                if (_camera != null) _camera.RemoveCommandBuffer(CameraEvent.AfterForwardOpaque, _silhouettePass);
                _silhouettePass.Release();
                _silhouettePass = null;
            }
            if (_silhouette != null) Destroy(_silhouette);
        }
    }
}
