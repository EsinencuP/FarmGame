using UnityEngine;
using UnityEngine.InputSystem;

namespace MyLittleFarm.Core
{
    [DefaultExecutionOrder(-200)]
    public sealed class InputReader : MonoBehaviour
    {
        public Vector2 Movement { get; private set; }
        public float ZoomDelta { get; private set; }
        public float OrbitDelta { get; private set; }
        public bool InteractPressed { get; private set; }
        public bool RotateLeftPressed { get; private set; }
        public bool RotateRightPressed { get; private set; }
        public bool SavePressed { get; private set; }
        public bool LoadPressed { get; private set; }
        public bool BuildTogglePressed { get; private set; }
        public bool RotateBuildingPressed { get; private set; }
        public bool MoveBuildingPressed { get; private set; }
        public bool DeleteBuildingPressed { get; private set; }
        public bool CancelPressed { get; private set; }
        public bool ConfirmPressed { get; private set; }
        public int BuildSelection { get; private set; } = -1;
        public Vector2 PointerPosition { get; private set; }
        public bool HasPointer { get; private set; }
        public bool BuildModeActive { get; internal set; }
        public bool SuppressGameplayThisFrame { get; internal set; }

        private void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            SuppressGameplayThisFrame = false;
            HasPointer = mouse != null;
            PointerPosition = mouse == null ? Vector2.zero : mouse.position.ReadValue();
            BuildTogglePressed = keyboard != null && keyboard.bKey.wasPressedThisFrame;
            RotateBuildingPressed = keyboard != null && keyboard.rKey.wasPressedThisFrame;
            MoveBuildingPressed = keyboard != null && keyboard.mKey.wasPressedThisFrame;
            DeleteBuildingPressed = keyboard != null && keyboard.deleteKey.wasPressedThisFrame;
            ConfirmPressed = keyboard != null && keyboard.enterKey.wasPressedThisFrame;
            CancelPressed = (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                || (mouse != null && mouse.rightButton.wasPressedThisFrame);
            BuildSelection = keyboard == null ? -1 : keyboard.digit1Key.wasPressedThisFrame ? 0
                : keyboard.digit2Key.wasPressedThisFrame ? 1 : keyboard.digit3Key.wasPressedThisFrame ? 2
                : keyboard.digit4Key.wasPressedThisFrame ? 3 : -1;

            Movement = keyboard == null
                ? Vector2.zero
                : new Vector2(
                    ReadAxis(keyboard.aKey.isPressed, keyboard.dKey.isPressed),
                    ReadAxis(keyboard.sKey.isPressed, keyboard.wKey.isPressed));

            ZoomDelta = mouse == null ? 0f : mouse.scroll.ReadValue().y;
            OrbitDelta = mouse != null && mouse.middleButton.isPressed
                ? mouse.delta.ReadValue().x
                : 0f;

            var shift = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
            InteractPressed = (keyboard != null && keyboard.eKey.wasPressedThisFrame && !shift)
                || (mouse != null && mouse.leftButton.wasPressedThisFrame);
            RotateLeftPressed = keyboard != null && keyboard.qKey.wasPressedThisFrame;
            RotateRightPressed = keyboard != null && keyboard.eKey.wasPressedThisFrame && shift;
            SavePressed = keyboard != null && keyboard.f5Key.wasPressedThisFrame;
            LoadPressed = keyboard != null && keyboard.f9Key.wasPressedThisFrame;
        }

        private static float ReadAxis(bool negative, bool positive)
        {
            if (negative == positive)
            {
                return 0f;
            }

            return positive ? 1f : -1f;
        }
    }
}
