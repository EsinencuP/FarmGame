using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace MyLittleFarm.Core
{
    public sealed partial class InputReader
    {
        public enum Command
        {
            Forward, Back, Left, Right, Run, RunAlternate, Sprint, Jump, Strafe, Gesture,
            Interact, OrbitLeft, OrbitRight, Save, Load, Market, Apple, Build,
            Rotate, RotateAlternate, MoveBuilding, DeleteBuilding, Catalogue, Customize, Guide, Inventory,
            Seed1, Seed2, Seed3, Seed4, Seed5, Seed6, Building1, Building2, Building3, Building4, Building5
        }

        [Flags]
        private enum Mode { Farm = 1, Build = 2, Both = Farm | Build }

        private readonly struct Binding
        {
            public readonly string Label;
            public readonly Key Default;
            public readonly Mode Context;

            public Binding(string label, Key key, Mode context)
            { Label = label; Default = key; Context = context; }
        }

        private static readonly Binding[] Bindings =
        {
            new("Вперёд", Key.W, Mode.Farm), new("Назад", Key.S, Mode.Farm),
            new("Влево", Key.A, Mode.Farm), new("Вправо", Key.D, Mode.Farm),
            new("Бег", Key.LeftShift, Mode.Both), new("Бег · дополнительная", Key.RightShift, Mode.Both),
            new("Спринт · вместе с бегом", Key.LeftCtrl, Mode.Farm), new("Прыжок", Key.Space, Mode.Farm),
            new("Шаги боком", Key.LeftAlt, Mode.Farm), new("Жест", Key.G, Mode.Farm),
            new("Действие / установка", Key.E, Mode.Both), new("Камера влево", Key.Q, Mode.Farm),
            new("Камера вправо · вместе с бегом", Key.E, Mode.Farm),
            new("Сохранить", Key.F5, Mode.Both), new("Слоты сохранений", Key.F9, Mode.Both),
            new("Рынок", Key.P, Mode.Farm), new("Посадить выбранное дерево", Key.T, Mode.Farm),
            new("Открыть / закрыть стройку", Key.B, Mode.Both), new("Повернуть постройку / полосу", Key.R, Mode.Build),
            new("Повернуть постройку · дополнительная", Key.Q, Mode.Build), new("Перенести постройку", Key.M, Mode.Build),
            new("Удалить постройку", Key.Delete, Mode.Build), new("Каталог строительства", Key.Tab, Mode.Build),
            new("Настроить постройку", Key.F, Mode.Build), new(MyLittleFarm.UI.FarmText.Get("settings.openJournal"), Key.J, Mode.Farm),
            new("Инвентарь", Key.I, Mode.Farm), new("Предмет · слот 1", Key.Digit1, Mode.Farm),
            new("Предмет · слот 2", Key.Digit2, Mode.Farm), new("Предмет · слот 3", Key.Digit3, Mode.Farm),
            new("Предмет · слот 4", Key.Digit4, Mode.Farm), new("Предмет · слот 5", Key.Digit5, Mode.Farm),
            new("Предмет · слот 6", Key.Digit6, Mode.Farm), new("Стройка · слот 1", Key.Digit1, Mode.Build),
            new("Стройка · слот 2", Key.Digit2, Mode.Build), new("Стройка · слот 3", Key.Digit3, Mode.Build),
            new("Стройка · слот 4", Key.Digit4, Mode.Build), new("Стройка · слот 5", Key.Digit5, Mode.Build)
        };

        [Serializable] private struct Override { public Command command; public Key key; }
        [Serializable] private sealed class Profile { public List<Override> bindings = new(); }
        private const string PrefsKey = "MyLittleFarm.InputBindings.v1";
        private static Key[] _keys;
        private static int _revision;
        private static int _blockedFrame = -1;
        private static InputActionRebindingExtensions.RebindingOperation _capture;
        private static InputAction _captureAction;
        private InputActionMap _map;
        private InputAction[] _actions;
        private int _appliedRevision = -1;

        public static IReadOnlyList<Command> Commands { get; } = Array.AsReadOnly((Command[])Enum.GetValues(typeof(Command)));
        public static int BindingRevision => _revision;
        public static bool CapturingBinding => _capture != null || Time.frameCount == _blockedFrame;
        public static string CommandLabel(Command command) => Bindings[(int)command].Label;
        public static string KeyLabel(Command command)
        {
            var key = Application.isPlaying ? Keys[(int)command] : Bindings[(int)command].Default;
            return key switch
            {
                Key.LeftShift => "L Shift", Key.RightShift => "R Shift",
                Key.LeftCtrl => "L Ctrl", Key.RightCtrl => "R Ctrl",
                Key.LeftAlt => "L Alt", Key.RightAlt => "R Alt",
                >= Key.Digit1 and <= Key.Digit0 => ((int)(key - Key.Digit1 + 1) % 10).ToString(),
                _ => Keyboard.current?[key].displayName ?? key.ToString()
            };
        }

        // Preserve older context-specific overrides. A shared key retains its existing farm action.
        public static bool CanRotateField
        {
            get
            {
                var key=Keys[(int)Command.Rotate];
                for(var i=0;i<Bindings.Length;i++)if(i!=(int)Command.Rotate && (Bindings[i].Context&Mode.Farm)!=0 && Keys[i]==key)return false;
                return true;
            }
        }
        public static string ActionHint => KeyLabel(Command.Interact) + " / ЛКМ";
        public static string MarketHint => KeyLabel(Command.Market) + " / " + KeyLabel(Command.Interact);
        public static string BuildRotateHint => KeyLabel(Command.RotateAlternate) + " / " + KeyLabel(Command.Rotate);
        public static string SlotHint(Command first, int count)
        {
            var names = new string[count];
            for (var i = 0; i < count; i++) names[i] = KeyLabel(first + i);
            return string.Join(" / ", names);
        }

        private static Key[] Keys
        {
            get
            {
                if (_keys != null) return _keys;
                var keys = DefaultKeys();
                try
                {
                    var profile = JsonUtility.FromJson<Profile>(PlayerPrefs.GetString(PrefsKey, "{}"));
                    if (profile?.bindings == null) return _keys = keys;
                    var candidate = DefaultKeys();
                    foreach (var item in profile.bindings)
                        if ((int)item.command >= 0 && (int)item.command < candidate.Length && AllowedKey(item.key))
                            candidate[(int)item.command] = item.key;
                    for (var i = 0; i < candidate.Length; i++)
                        if (Conflict((Command)i, candidate[i], candidate) >= 0) return _keys = keys;
                    keys = candidate;
                }
                catch (ArgumentException) { }
                return _keys = keys;
            }
        }

        private static Key[] DefaultKeys() => Array.ConvertAll(Bindings, binding => binding.Default);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetBindingSession()
        {
            _capture?.Dispose(); _capture = null;
            _captureAction?.Dispose(); _captureAction = null;
            _keys = null; _revision = 0; _blockedFrame = -1;
        }

        public static void BeginBindingCapture(Command command, Action<string> finished)
        {
            CancelBindingCapture();
            _captureAction = new InputAction(type: InputActionType.Button, binding: "<Keyboard>/space");
            _capture = _captureAction.PerformInteractiveRebinding(0)
                .WithControlsHavingToMatchPath("<Keyboard>")
                .WithExpectedControlType<KeyControl>()
                .WithMatchingEventsBeingSuppressed()
                .WithCancelingThrough("<Keyboard>/escape")
                .WithTimeout(10)
                .OnApplyBinding((_, _) => { })
                .OnCancel(op => FinishCapture(op, finished, "Назначение отменено"))
                .OnComplete(op =>
                {
                    var key = (op.selectedControl as KeyControl)?.keyCode ?? Key.None;
                    FinishCapture(op, finished, AssignBinding(command, key));
                });
            _capture.Start();
        }

        public static void CancelBindingCapture() => _capture?.Cancel();

        private static void FinishCapture(InputActionRebindingExtensions.RebindingOperation operation, Action<string> finished, string message)
        {
            _capture = null;
            _blockedFrame = Time.frameCount;
            operation.Dispose();
            _captureAction.Dispose(); _captureAction = null;
            finished(message);
        }

        private static string AssignBinding(Command command, Key key)
        {
            if (!AllowedKey(key)) return "Эта клавиша зарезервирована. Выберите другую.";
            var conflict = Conflict(command, key, Keys);
            if (conflict >= 0) return "Клавиша уже занята в этом режиме: " + Bindings[conflict].Label;
            Keys[(int)command] = key;
            Persist();
            return CommandLabel(command) + " — " + KeyLabel(command);
        }

        private static bool AllowedKey(Key key) => key > Key.None && key <= Key.F24 && (key <= Key.OEM5 || key >= Key.F13)
            && key != Key.Escape && key != Key.Enter && key != Key.NumpadEnter
            && key != Key.LeftMeta && key != Key.RightMeta;

        private static int Conflict(Command command, Key key, Key[] keys)
        {
            for (var i = 0; i < keys.Length; i++)
            {
                if (i == (int)command || keys[i] != key || (Bindings[i].Context & Bindings[(int)command].Context) == 0) continue;
                // These commands distinguish the same button by the run modifier.
                if ((command == Command.Interact && i == (int)Command.OrbitRight)
                    || (command == Command.OrbitRight && i == (int)Command.Interact)) continue;
                return i;
            }
            return -1;
        }

        public static void ResetBindings()
        {
            CancelBindingCapture();
            _keys = DefaultKeys();
            Persist();
        }

        private static void Persist()
        {
            var profile = new Profile();
            for (var i = 0; i < Bindings.Length; i++)
                if (Keys[i] != Bindings[i].Default) profile.bindings.Add(new Override { command = (Command)i, key = Keys[i] });
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(profile));
            PlayerPrefs.Save();
            _revision++;
        }

        private void ApplyBindings()
        {
            if (_map == null)
            {
                _map = new InputActionMap("Farm keyboard");
                _actions = new InputAction[Bindings.Length];
                for (var i = 0; i < _actions.Length; i++)
                    _actions[i] = _map.AddAction(((Command)i).ToString(), InputActionType.Button, BindingPath(Bindings[i].Default));
            }
            _map.Disable();
            for (var i = 0; i < _actions.Length; i++)
                _actions[i].ApplyBindingOverride(0, BindingPath(Keys[i]));
            _map.Enable();
            _appliedRevision = _revision;
        }

        private static string BindingPath(Key key)
        {
            var control = Keyboard.current?[key].name;
            if (control == null)
                control = key >= Key.Digit1 && key <= Key.Digit0 ? ((int)(key - Key.Digit1 + 1) % 10).ToString() : key.ToString();
            return "<Keyboard>/" + control;
        }

        private void OnEnable() => ApplyBindings();
        private void OnDisable() => _map?.Disable();
        private void OnDestroy() => _map?.Dispose();

        private bool Pressed(Command command) => _actions[(int)command].WasPressedThisFrame();
        private bool Held(Command command) => _actions[(int)command].IsPressed();

        private int SlotPressed(Command first, int count)
        {
            for (var i = 0; i < count; i++) if (Pressed(first + i)) return i;
            return -1;
        }
    }
}
