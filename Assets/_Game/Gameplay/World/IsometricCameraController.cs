using MyLittleFarm.Core;
using UnityEngine;

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
        [SerializeField, Min(0.001f)] private float zoomSensitivity = 0.015f;
        [Tooltip("Минимальная дистанция приближения камеры.")]
        [SerializeField, Min(0.1f)] private float minDistance = 7f;
        [Tooltip("Максимальная дистанция удаления камеры.")]
        [SerializeField, Min(0.1f)] private float maxDistance = 20f;

        // Input задаёт команды, target — игрок, yaw — текущий угол, distance — текущий масштаб обзора.
        private InputReader _input;
        private Transform _target;
        private float _yaw;
        private float _distance;

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
            _distance = Mathf.Clamp(initialDistance, minDistance, Mathf.Max(minDistance, maxDistance));
            SnapToTarget();
        }

        private void LateUpdate()
        {
            // LateUpdate выполняется после движения игрока, поэтому камера следует за актуальной позицией.
            if (_input == null || _target == null)
            {
                return;
            }

            if (_input.RotateLeftPressed)
            {
                _yaw -= keyboardRotationStep;
            }

            if (_input.RotateRightPressed)
            {
                _yaw += keyboardRotationStep;
            }

            _yaw += _input.OrbitDelta * orbitSensitivity;
            _distance = Mathf.Clamp(_distance - _input.ZoomDelta * zoomSensitivity,
                minDistance, Mathf.Max(minDistance, maxDistance));
            SnapToTarget();
        }

        private void SnapToTarget()
        {
            // Камера смотрит немного выше основания игрока под постоянным углом 38 градусов.
            var lookPoint = _target.position + Vector3.up * lookHeight;
            var rotation = Quaternion.Euler(pitch, _yaw, 0f);
            transform.position = lookPoint + rotation * (Vector3.back * _distance);
            transform.rotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
        }
    }
}
