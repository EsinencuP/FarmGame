using MyLittleFarm.Core;
using UnityEngine;

namespace MyLittleFarm.Gameplay.World
{
    [RequireComponent(typeof(Camera))]
    public sealed class IsometricCameraController : MonoBehaviour
    {
        private InputReader _input;
        private Transform _target;
        private float _yaw = 45f;
        private float _distance = 13f;

        public void Configure(InputReader input, Transform target)
        {
            _input = input;
            _target = target;
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (_input == null || _target == null)
            {
                return;
            }

            if (_input.RotateLeftPressed)
            {
                _yaw -= 90f;
            }

            if (_input.RotateRightPressed)
            {
                _yaw += 90f;
            }

            _yaw += _input.OrbitDelta * 0.15f;
            _distance = Mathf.Clamp(_distance - _input.ZoomDelta * 0.015f, 7f, 20f);
            SnapToTarget();
        }

        private void SnapToTarget()
        {
            var lookPoint = _target.position + Vector3.up * 0.8f;
            var rotation = Quaternion.Euler(38f, _yaw, 0f);
            transform.position = lookPoint + rotation * (Vector3.back * _distance);
            transform.rotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
        }
    }
}

