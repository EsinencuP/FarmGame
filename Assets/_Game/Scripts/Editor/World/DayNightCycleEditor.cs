using MyLittleFarm.Gameplay.World;
using UnityEditor;
using UnityEngine;

namespace MyLittleFarm.Editor
{
    [CustomEditor(typeof(DayNightCycle))]
    public sealed class DayNightCycleEditor : UnityEditor.Editor
    {
        private bool _previewing;
        private float _previewHour=8f;
        private void OnEnable()=>EditorApplication.playModeStateChanged+=OnPlayModeChanged;
        private void OnDisable()
        {
            EditorApplication.playModeStateChanged-=OnPlayModeChanged;
            Restore();
        }
        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.ExitingEditMode)Restore();
        }
        private void Restore()
        {
            if(!_previewing||Application.isPlaying||target==null)return;
            var cycle=(DayNightCycle)target;cycle.SetHour(cycle.StartingHour);_previewing=false;
        }
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var cycle=(DayNightCycle)target;
            EditorGUILayout.Space();EditorGUILayout.LabelField("Current hour",cycle.Hour.ToString("00.00"));
            if(Application.isPlaying)return;
            EditorGUILayout.HelpBox("Lighting preview only; does not start gameplay or advance crops. Closing this Inspector restores starting-hour lighting.",MessageType.Info);
            EditorGUI.BeginChangeCheck();_previewHour=EditorGUILayout.Slider("Preview hour",_previewHour,0f,23.99f);
            if(EditorGUI.EndChangeCheck())Preview(cycle,_previewHour);
            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button("Sunrise"))Preview(cycle,6.5f);
            if(GUILayout.Button("Noon"))Preview(cycle,12f);
            if(GUILayout.Button("Sunset"))Preview(cycle,17.5f);
            if(GUILayout.Button("Night"))Preview(cycle,0f);
            EditorGUILayout.EndHorizontal();
            if(GUILayout.Button("Restore starting hour"))Restore();
        }
        private void Preview(DayNightCycle cycle,float hour)
        {
            _previewHour=hour;_previewing=true;cycle.SetHour(hour);SceneView.RepaintAll();
        }
    }
}
