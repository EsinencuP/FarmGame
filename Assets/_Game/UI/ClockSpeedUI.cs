using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MyLittleFarm.UI
{
    /// <summary>Session-only control for previewing day/night lighting.</summary>
    public sealed class ClockSpeedUI : MonoBehaviour
    {
        [SerializeField] private DayNightCycle cycle;
        [SerializeField] private InputReader input;
        [SerializeField] private Button button;
        [SerializeField] private Text label;
        private bool _bound;

        private void OnEnable()
        {
            if(!Application.isPlaying||cycle==null||button==null)return;
            cycle.ClockSpeedChanged+=Refresh;
            button.onClick.AddListener(Advance);
            _bound=true;Refresh();
        }

        private void OnDisable()
        {
            if(!_bound)return;
            if(cycle!=null)cycle.ClockSpeedChanged-=Refresh;
            if(button!=null)button.onClick.RemoveListener(Advance);
            _bound=false;
        }

        private void Advance()
        {
            if(!Application.isPlaying||cycle==null||input==null||input.ModalInputActive)return;
            cycle.CycleClockSpeed();
            if(EventSystem.current!=null&&EventSystem.current.currentSelectedGameObject==button.gameObject)
                EventSystem.current.SetSelectedGameObject(null);
        }

        private void Refresh()
        {
            if(label!=null)label.text="Скорость суток · ×"+cycle.ClockSpeed;
        }

        public static ClockSpeedUI Create(Transform parent,DayNightCycle worldClock,InputReader reader,Font font)
        {
            var control=SaveSlotsUI.Button(parent,"Скорость суток · ×1",font);
            control.name="Day Night Speed";
            control.navigation=new Navigation{mode=Navigation.Mode.None};
            var ui=control.gameObject.AddComponent<ClockSpeedUI>();
            ui.cycle=worldClock;ui.input=reader;ui.button=control;ui.label=control.GetComponentInChildren<Text>();
            return ui;
        }
    }
}
