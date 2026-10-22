using MyLittleFarm.Gameplay.World;
using UnityEditor;
using UnityEngine;

namespace MyLittleFarm.Editor
{
    [CustomEditor(typeof(WeatherController))]
    public sealed class WeatherControllerEditor : UnityEditor.Editor
    {
        private bool _preview;
        private void OnEnable()=>EditorApplication.playModeStateChanged+=OnPlayModeChanged;
        private void OnDisable(){EditorApplication.playModeStateChanged-=OnPlayModeChanged;Restore();}
        private void OnPlayModeChanged(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingEditMode)Restore();}
        private void Restore()
        {
            if(!_preview||target==null||Application.isPlaying)return;
            ((WeatherController)target).RestorePreview();_preview=false;SceneView.RepaintAll();
        }
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();var weather=(WeatherController)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Rain growth speed",weather.GrowthMultiplier.ToString("0.00")+"x");
            EditorGUILayout.LabelField("Fade remaining",weather.TransitionRemaining.ToString("0.0")+" seconds");
            EditorGUILayout.HelpBox(Application.isPlaying?"Buttons request a smooth weather transition.":"Appearance preview only. Closing the Inspector restores starting weather. No growth or audio playback.",MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            foreach(WeatherState state in System.Enum.GetValues(typeof(WeatherState)))
                if(GUILayout.Button(state.ToString()))
                {
                    if(Application.isPlaying)weather.RequestState(state);
                    else {_preview=true;weather.Preview(state);SceneView.RepaintAll();}
                }
            EditorGUILayout.EndHorizontal();
            if(!Application.isPlaying&&GUILayout.Button("Restore starting weather"))Restore();
        }
    }
}
