using UnityEngine;
using UnityEngine.InputSystem;

namespace MyLittleFarm.Core
{
    /// <summary>
    /// Единственная точка чтения клавиатуры и мыши. Остальные системы получают уже готовые
    /// игровые команды и не зависят напрямую от Input System.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class InputReader : MonoBehaviour
    {
        [Header("Movement Keys")]
        [SerializeField] private Key moveForwardKey = Key.W;
        [SerializeField] private Key moveBackwardKey = Key.S;
        [SerializeField] private Key moveLeftKey = Key.A;
        [SerializeField] private Key moveRightKey = Key.D;
        [SerializeField] private Key sprintKey = Key.LeftShift;
        [SerializeField] private Key alternateSprintKey = Key.RightShift;
        [SerializeField] private Key jumpKey = Key.Space;

        [Header("Gameplay Keys")]
        [SerializeField] private Key interactKey = Key.E;
        [SerializeField] private Key rotateCameraLeftKey = Key.Q;
        [Tooltip("Поворот вправо срабатывает вместе с одной из клавиш бега.")]
        [SerializeField] private Key rotateCameraRightKey = Key.E;
        [SerializeField] private Key saveKey = Key.F5;
        [SerializeField] private Key loadKey = Key.F9;

        [Header("Building Keys")]
        [SerializeField] private Key buildToggleKey = Key.B;
        [SerializeField] private Key rotateBuildingKey = Key.R;
        [SerializeField] private Key moveBuildingKey = Key.M;
        [SerializeField] private Key deleteBuildingKey = Key.Delete;
        [SerializeField] private Key confirmKey = Key.Enter;
        [SerializeField] private Key cancelKey = Key.Escape;
        [SerializeField] private Key buildingSlot1Key = Key.Digit1;
        [SerializeField] private Key buildingSlot2Key = Key.Digit2;
        [SerializeField] private Key buildingSlot3Key = Key.Digit3;
        [SerializeField] private Key buildingSlot4Key = Key.Digit4;

        [Header("Mouse Controls")]
        [SerializeField] private bool allowMouseInteraction = true;
        [SerializeField] private bool allowMouseCancel = true;

        // Непрерывные значения управления, которые действуют всё время удержания клавиши или движения мыши.
        public Vector2 Movement { get; private set; }
        public float ZoomDelta { get; private set; }
        public float OrbitDelta { get; private set; }
        // Однокадровые команды: true только в кадре, когда соответствующая кнопка была нажата.
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
        // SprintHeld действует при удержании Shift, JumpPressed — только в кадре нажатия пробела.
        public bool SprintHeld { get; private set; }
        public bool JumpPressed { get; private set; }
        // Индекс выбранной постройки: 0–3 для клавиш 1–4, -1 если выбор не менялся.
        public int BuildSelection { get; private set; } = -1;
        // Текущая экранная позиция указателя нужна строительной системе для луча из камеры.
        public Vector2 PointerPosition { get; private set; }
        public bool HasPointer { get; private set; }
        // Состояния совместного доступа не дают движению и обычному взаимодействию сработать в режиме строительства.
        public bool BuildModeActive { get; internal set; }
        public bool SuppressGameplayThisFrame { get; internal set; }

        private void Update()
        {
            // Устройства могут отсутствовать, поэтому каждое чтение защищено проверкой на null.
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            SuppressGameplayThisFrame = false;
            HasPointer = mouse != null;
            PointerPosition = mouse == null ? Vector2.zero : mouse.position.ReadValue();
            BuildTogglePressed = WasPressed(keyboard, buildToggleKey);
            RotateBuildingPressed = WasPressed(keyboard, rotateBuildingKey);
            MoveBuildingPressed = WasPressed(keyboard, moveBuildingKey);
            DeleteBuildingPressed = WasPressed(keyboard, deleteBuildingKey);
            ConfirmPressed = WasPressed(keyboard, confirmKey);
            CancelPressed = WasPressed(keyboard, cancelKey)
                || (allowMouseCancel && mouse != null && mouse.rightButton.wasPressedThisFrame);
            BuildSelection = WasPressed(keyboard, buildingSlot1Key) ? 0
                : WasPressed(keyboard, buildingSlot2Key) ? 1 : WasPressed(keyboard, buildingSlot3Key) ? 2
                : WasPressed(keyboard, buildingSlot4Key) ? 3 : -1;

            Movement = keyboard == null
                ? Vector2.zero
                : new Vector2(
                    ReadAxis(IsPressed(keyboard, moveLeftKey), IsPressed(keyboard, moveRightKey)),
                    ReadAxis(IsPressed(keyboard, moveBackwardKey), IsPressed(keyboard, moveForwardKey)));

            ZoomDelta = mouse == null ? 0f : mouse.scroll.ReadValue().y;
            OrbitDelta = mouse != null && mouse.middleButton.isPressed
                ? mouse.delta.ReadValue().x
                : 0f;

            var shift = IsPressed(keyboard, sprintKey) || IsPressed(keyboard, alternateSprintKey);
            SprintHeld = shift;
            JumpPressed = WasPressed(keyboard, jumpKey);
            InteractPressed = (WasPressed(keyboard, interactKey) && !shift)
                || (allowMouseInteraction && mouse != null && mouse.leftButton.wasPressedThisFrame);
            RotateLeftPressed = WasPressed(keyboard, rotateCameraLeftKey);
            RotateRightPressed = WasPressed(keyboard, rotateCameraRightKey) && shift;
            SavePressed = WasPressed(keyboard, saveKey);
            LoadPressed = WasPressed(keyboard, loadKey);
        }

        private static bool IsPressed(Keyboard keyboard, Key key)
        {
            // Key.None отключает конкретную команду без необходимости менять код.
            return keyboard != null && key != Key.None && keyboard[key].isPressed;
        }

        private static bool WasPressed(Keyboard keyboard, Key key)
        {
            // Однокадровая проверка используется всеми переназначаемыми командами.
            return keyboard != null && key != Key.None && keyboard[key].wasPressedThisFrame;
        }

        private static float ReadAxis(bool negative, bool positive)
        {
            // Одновременное нажатие противоположных направлений взаимно их отменяет.
            if (negative == positive)
            {
                return 0f;
            }

            return positive ? 1f : -1f;
        }
    }
}
