using UnityEngine;
using UnityEngine.InputSystem;

namespace MyLittleFarm.Core
{
    /// <summary>
    /// Единственная точка чтения клавиатуры и мыши. Остальные системы получают уже готовые
    /// игровые команды и не зависят напрямую от Input System.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed partial class InputReader_AI : MonoBehaviour
    {
        [SerializeField] private RectTransform onboardingPointerBlock;
        [SerializeField] private RectTransform clockSpeedPointerBlock;
        [SerializeField] private RectTransform navigationPointerBlock;
        [SerializeField] private RectTransform quickSlotsPointerBlock;
        internal RectTransform ConstructionPointerBlock { get; set; }
        public bool WorldPointerBlocked { get; private set; }
        [Header("Mouse Controls")]
        [SerializeField] private bool allowMouseInteraction = true;
        [SerializeField] private bool allowMouseCancel = true;

        // Непрерывные значения управления, которые действуют всё время удержания клавиши или движения мыши.
        public Vector2 Movement { get; private set; }
        public float ZoomDelta { get; private set; }
        public float OrbitDelta { get; private set; }
        // Однокадровые команды: true только в кадре, когда соответствующая кнопка была нажата.
        public bool InteractPressed { get; private set; }
        public bool PointerInteractPressed { get; private set; }
        public bool ToggleToolPressed { get; private set; }
        public bool SavePressed { get; private set; }
        public bool LoadPressed { get; private set; }
        public bool PlantTreePressed { get; private set; }
        public bool BuildTogglePressed { get; private set; }
        public bool RotateFieldPressed {get;private set;}
        public bool RotateBuildingPressed { get; private set; }
        public bool MoveBuildingPressed { get; private set; }
        public bool DeleteBuildingPressed { get; private set; }
        public bool CancelPressed { get; private set; }
        public bool ConfirmPressed { get; private set; }
        // SprintHeld действует при удержании Shift, JumpPressed — только в кадре нажатия пробела.
        public bool SprintHeld { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool StrafeHeld { get; private set; }
        public bool FastSprintHeld { get; private set; }
        public bool GesturePressed { get; private set; }
        // Indirect selection is retained for existing Editor preview sessions.
        public int BuildSelection { get; private set; } = -1;
        // Индекс семени: 0–5 для клавиш 1–6, -1 если в кадре слот не выбирался.
        public int SeedSelection { get; private set; } = -1;
        // Текущая экранная позиция указателя нужна строительной системе для луча из камеры.
        public Vector2 PointerPosition { get; private set; }
        public bool HasPointer { get; private set; }
        // Состояния совместного доступа не дают движению и обычному взаимодействию сработать в режиме строительства.
        public bool BuildModeActive { get; internal set; }
        public bool SuppressGameplayThisFrame { get; internal set; }
        public bool ModalInputActive { get; internal set; }
        internal bool ConstructionCatalogueActive { get; set; }
        internal bool FishingActive { get; set; }
        internal float FishingAxis { get; private set; }
        internal bool FishingCancelPressed { get; private set; }
        public bool CustomizeBuildingPressed { get; private set; }
        public bool ModalSubmitPressed { get; private set; }
        public bool ModalCancelPressed { get; private set; }
        public bool JournalPressed { get; private set; }
        public bool PausePressed { get; private set; }
        public bool InventoryPressed { get; private set; }
        public bool MarketPressed { get; private set; }
        public bool WaterPressed { get; private set; }
        internal bool DemoPanelPressed { get; private set; }

        private void Update()
        {
            if (_appliedRevision != BindingRevision) ApplyBindings();
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            var capturing = CapturingBinding;
            var modal = capturing || ModalInputActive;
            var farm = !modal && !BuildModeActive;
            var build = !modal && BuildModeActive;
            var run = !modal && (Held(Command.Run) || Held(Command.RunAlternate));
            var escape = !capturing && keyboard?.escapeKey.wasPressedThisFrame == true;
            var enter = !capturing && keyboard?.enterKey.wasPressedThisFrame == true;

            DemoPanelPressed = MyLittleFarm.UI.FarmDemoPanel.IsAvailable && !capturing
                && keyboard?.f1Key.wasPressedThisFrame == true;
            SuppressGameplayThisFrame = modal;
            HasPointer = mouse != null;
            PointerPosition = mouse?.position.ReadValue() ?? Vector2.zero;
            WorldPointerBlocked = HasPointer && (BlocksPointer(onboardingPointerBlock) || BlocksPointer(clockSpeedPointerBlock)
                || BlocksPointer(navigationPointerBlock) || BlocksPointer(quickSlotsPointerBlock)
                || BlocksPointer(ConstructionPointerBlock));
            FishingAxis = farm && FishingActive && keyboard != null
                ? ReadAxis(keyboard.downArrowKey.isPressed, keyboard.upArrowKey.isPressed) : 0f;
            FishingCancelPressed = farm && FishingActive && (escape
                || (allowMouseCancel && mouse?.rightButton.wasPressedThisFrame == true));
            PausePressed = farm && !FishingActive && escape;
            InventoryPressed = !capturing && !BuildModeActive && Pressed(Command.Inventory);
            MarketPressed = farm && Pressed(Command.Market);
            // H работает в обычной игре, когда курсор не перекрыт интерфейсом.
            WaterPressed = farm && !WorldPointerBlocked && Pressed(Command.Water);
            JournalPressed = farm && Pressed(Command.Guide);
            BuildTogglePressed = !capturing && (!modal || ConstructionCatalogueActive) && Pressed(Command.Build);
            RotateFieldPressed = farm && CanRotateField && Pressed(Command.Rotate);
            RotateBuildingPressed = build && (Pressed(Command.Rotate) || Pressed(Command.RotateAlternate));
            MoveBuildingPressed = build && Pressed(Command.MoveBuilding);
            DeleteBuildingPressed = build && Pressed(Command.DeleteBuilding);
            CustomizeBuildingPressed = build && Pressed(Command.Customize);
            ConfirmPressed = build && enter;
            CancelPressed = build && (escape || (allowMouseCancel && mouse?.rightButton.wasPressedThisFrame == true));
            ModalSubmitPressed = enter;
            ModalCancelPressed = modal && escape;
            BuildSelection = -1;
            SeedSelection = farm ? SlotPressed(Command.Seed1, 6) : -1;
            ToggleToolPressed = farm && !FishingActive && !PausePressed && Pressed(Command.ToggleTool);

            Movement = farm ? new Vector2(ReadAxis(Held(Command.Left), Held(Command.Right)),
                ReadAxis(Held(Command.Back), Held(Command.Forward))) : Vector2.zero;
            ZoomDelta = modal || WorldPointerBlocked ? 0f : mouse?.scroll.ReadValue().y ?? 0f;
            if (ZoomDelta != 0f && InputSystem.settings.scrollDeltaBehavior == InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange
                && (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor))
                ZoomDelta /= 120f;
            OrbitDelta = !modal && mouse?.middleButton.isPressed == true ? mouse.delta.ReadValue().x : 0f;
            SprintHeld = farm && run;
            StrafeHeld = farm && Held(Command.Strafe);
            FastSprintHeld = SprintHeld && Held(Command.Sprint);
            GesturePressed = farm && Pressed(Command.Gesture);
            JumpPressed = farm && Pressed(Command.Jump);
            PointerInteractPressed = !modal && !WorldPointerBlocked && allowMouseInteraction && mouse?.leftButton.wasPressedThisFrame == true;
            InteractPressed = (!modal && !run && Pressed(Command.Interact)) || PointerInteractPressed;
            SavePressed = !modal && Pressed(Command.Save);
            LoadPressed = !modal && Pressed(Command.Load);
            PlantTreePressed = farm && Pressed(Command.Apple);
        }

        private bool BlocksPointer(RectTransform rect)
        {
            return rect != null && rect.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(rect, PointerPosition);
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
