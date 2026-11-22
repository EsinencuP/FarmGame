using System.Collections.Generic;
using MyLittleFarm.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MyLittleFarm.UI
{
    public sealed partial class FarmSettingsUI
    {
        private readonly List<Text> _bindingLabels = new();
        private bool _capturingControl;

        private void EnsureBindingRows()
        {
            if (_bindingLabels.Count > 0) { RefreshBindingLabels(); return; }
            if (controlsGuide == null) EnsureControlsGuide();
            var scroll = controlsGuide.GetComponentInParent<ScrollRect>();
            var content = FarmUITheme.Rect(scroll.viewport, "Control bindings");
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1); content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6; layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;

            controlsGuide.transform.SetParent(content, false);
            if (controlsGuide.TryGetComponent<ContentSizeFitter>(out var previousFitter)) previousFitter.enabled = false;
            controlsGuide.text = FarmText.Get("settings.bindingHelp");
            controlsGuide.fontSize = 22;
            var resetBindings = AddBindingButton(content, FarmText.Get("settings.resetBindings"));
            resetBindings.onClick.AddListener(() => { InputReader_AI.ResetBindings(); RefreshBindingLabels(); });
            foreach (var command in InputReader_AI.Commands)
            {
                var button = AddBindingButton(content, "");
                button.name = "Bind " + command;
                var label = button.GetComponentInChildren<Text>();
                _bindingLabels.Add(label);
                button.onClick.AddListener(() => CaptureBinding(command, label));
            }
            var help = FarmUITheme.Label(content,
                FarmText.Get("settings.firstSteps"), font, 22);
            help.resizeTextForBestFit = false;
            help.alignment = TextAnchor.UpperLeft;
            help.verticalOverflow = VerticalWrapMode.Overflow;
            RefreshBindingLabels();
        }

        private Button AddBindingButton(Transform parent, string title)
        {
            var button = FarmUITheme.Button(parent, title, font, kind: "ghost");
            button.gameObject.AddComponent<LayoutElement>().preferredHeight = 48;
            var label = button.GetComponentInChildren<Text>();
            label.alignment = TextAnchor.MiddleLeft;
            label.fontSize = 23; label.resizeTextMaxSize = 23;
            return button;
        }

        private void CaptureBinding(InputReader_AI.Command command, Text label)
        {
            CancelControlCapture();
            var previous = FarmUIFocus.Selected;
            FarmUITheme.Focus(null);
            _capturingControl = true;
            label.text = FarmText.Get("settings.waitForKey", InputReader_AI.CommandLabel(command));
            InputReader_AI.BeginBindingCapture(command, message =>
            {
                _capturingControl = false;
                RefreshBindingLabels();
                FarmUIFocus.Restore(previous);
                if (message.StartsWith("Эта") || message.StartsWith("Клавиша"))
                {
                    label.text += "   ·   " + message;
                    label.color = new Color(.64f, .27f, .14f);
                }
            });
        }

        private void RefreshBindingLabels()
        {
            for (var i = 0; i < _bindingLabels.Count; i++)
            {
                var command = InputReader_AI.Commands[i];
                _bindingLabels[i].text = InputReader_AI.CommandLabel(command) + "   ·   " + InputReader_AI.KeyLabel(command);
                _bindingLabels[i].color = FarmUITheme.Ink;
            }
        }

        private void CancelControlCapture()
        {
            if (_capturingControl) InputReader_AI.CancelBindingCapture();
        }
    }
}
