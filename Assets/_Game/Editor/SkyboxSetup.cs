using System;
using MyLittleFarm.Gameplay.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MyLittleFarm.Editor
{
    public static class SkyboxSetup
    {
        private const string MaterialPath="Assets/_Game/Content/World/Lighting/FarmDayNightSky.mat";

        [MenuItem("Tools/My Little Farm/Apply Sky Series Skyboxes")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Use Edit Mode.");
            var scene=EditorSceneManager.GetActiveScene();
            if(scene.path!="Assets/_Game/Scenes/Prototype.unity")throw new InvalidOperationException("Open Prototype first.");
            var cycle=UnityEngine.Object.FindAnyObjectByType<DayNightCycle>();
            if(cycle==null)throw new MissingReferenceException("Apply day/night lighting first.");
            var day=AssetDatabase.LoadAssetAtPath<Material>("Assets/SkySeries Freebie/FluffballDay.mat");
            var night=AssetDatabase.LoadAssetAtPath<Material>("Assets/SkySeries Freebie/CoriolisNight4k.mat");
            if(day==null||night==null)throw new MissingReferenceException("SkySeries Freebie materials are missing.");
            var shader=Shader.Find("MyLittleFarm/Skybox/Day Night Cubemaps");
            if(shader==null)throw new MissingReferenceException("Farm skybox shader is missing.");
            var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if(material==null){material=new Material(shader){name="Farm Day Night Sky"};AssetDatabase.CreateAsset(material,MaterialPath);}
            material.shader=shader;
            material.SetTexture("_DayTex",day.GetTexture("_Tex"));
            material.SetTexture("_NightTex",night.GetTexture("_Tex"));
            material.SetFloat("_DayExposure",.95f);
            material.SetFloat("_NightExposure",.8f);
            material.SetColor("_NightTint",new Color(.5f,.5f,.5f));
            Undo.RecordObject(cycle,"Apply Sky Series skyboxes");
            var settings=new SerializedObject(cycle);
            settings.FindProperty("skyboxMaterial").objectReferenceValue=material;
            settings.ApplyModifiedProperties();
            cycle.SetHour(cycle.StartingHour);
            EditorUtility.SetDirty(material);EditorUtility.SetDirty(cycle);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            SceneView.RepaintAll();
            Debug.Log("Sky Series installed: FluffballDay / CoriolisNight4k, smooth day/night blend. Source assets preserved.");
        }
    }
}
