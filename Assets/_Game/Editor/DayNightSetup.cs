using System;
using System.Collections.Generic;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MyLittleFarm.Editor
{
    /// <summary>Authors saved scene lights and a reusable lantern; never initializes or runs gameplay.</summary>
    public static class DayNightSetup
    {
        private const string Folder="Assets/_Game/Content/World/Lighting";
        // Farm yard first, then the existing authored trails. Posts sit on cell edges, clear of crop centers.
        private static readonly Vector2[] Positions={
            new Vector2(1,-1),new Vector2(7,-1),new Vector2(10,3),new Vector2(7,7),
            new Vector2(0,8),new Vector2(-5,2),new Vector2(15,9),
            new Vector2(-10,-16),new Vector2(-13,-5),new Vector2(-13,6),new Vector2(-13,17),
            new Vector2(-8,23),new Vector2(3,23),new Vector2(15,23),new Vector2(26,18),new Vector2(26,5),new Vector2(26,-8)
        };

        [MenuItem("Tools/My Little Farm/Apply P2-2 Day Night Lighting")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Use Edit Mode.");
            var scene=EditorSceneManager.GetActiveScene();
            if(scene.path!="Assets/_Game/Scenes/Prototype.unity")throw new InvalidOperationException("Open Prototype first.");
            var root=UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
            var grid=root.GetComponentInChildren<GridSystem>(true);
            grid.RegisterSceneChunks(root.GetComponentsInChildren<TilemapChunk>(true));
            EnsureFolder(Folder);
            var lighting=root.transform.Find("World Lighting");
            if(lighting==null){var go=new GameObject("World Lighting");Undo.RegisterCreatedObjectUndo(go,"Author day/night lighting");go.transform.SetParent(root.transform,false);lighting=go.transform;}
            var cycle=lighting.GetComponent<DayNightCycle>();if(cycle==null)cycle=lighting.gameObject.AddComponent<DayNightCycle>();
            var sunObject=root.transform.Find("Sun");
            if(sunObject==null)throw new MissingReferenceException("Authored Sun is missing.");
            var sun=sunObject.GetComponent<Light>();sun.type=LightType.Directional;sun.lightmapBakeType=LightmapBakeType.Realtime;sun.shadows=LightShadows.Soft;
            var moonObject=lighting.Find("Moon Fill");
            if(moonObject==null){var go=new GameObject("Moon Fill");go.transform.SetParent(lighting,false);moonObject=go.transform;}
            var moon=moonObject.GetComponent<Light>();if(moon==null)moon=moonObject.gameObject.AddComponent<Light>();
            moon.type=LightType.Directional;moon.lightmapBakeType=LightmapBakeType.Realtime;moon.shadows=LightShadows.None;
            var prefab=EnsureLanternPrefab();
            var lamps=lighting.Find("Farm Lanterns");
            if(lamps==null){var go=new GameObject("Farm Lanterns");go.transform.SetParent(lighting,false);lamps=go.transform;}
            for(var i=0;i<Positions.Length;i++)
            {
                var name="Lantern "+(i+1).ToString("00");
                if(lamps.Find(name)!=null)continue;
                var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,lamps);go.name=name;
                var position=Positions[i];
                go.transform.position=new Vector3(position.x,grid.GetSurfaceHeight(position.x,position.y),position.y);
                go.transform.rotation=Quaternion.Euler(0,i<7?i*90f:90f,0);
                Undo.RegisterCreatedObjectUndo(go,"Place farm lantern");
            }
            var lanterns=lamps.GetComponentsInChildren<FarmLantern>(true);
            var settings=new SerializedObject(cycle);
            settings.FindProperty("sun").objectReferenceValue=sun;
            settings.FindProperty("moon").objectReferenceValue=moon;
            settings.FindProperty("worldCamera").objectReferenceValue=root.GetComponentInChildren<Camera>(true);
            var references=settings.FindProperty("authoredLanterns");references.arraySize=lanterns.Length;
            for(var i=0;i<lanterns.Length;i++)references.GetArrayElementAtIndex(i).objectReferenceValue=lanterns[i];
            settings.ApplyModifiedPropertiesWithoutUndo();cycle.SetHour(cycle.StartingHour);
            EditorUtility.SetDirty(cycle);EditorUtility.SetDirty(sun);EditorUtility.SetDirty(moon);
            EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            Debug.Log("P2-2 lighting saved: 14-minute visual clock, realtime sun/moon, "+lanterns.Length+" functional lanterns. Crop growth remains UTC-based.");
        }

        private static GameObject EnsureLanternPrefab()
        {
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/FarmLantern.prefab");if(existing!=null)return existing;
            var wood=Material("Lantern Wood",new Color(.28f,.18f,.10f));
            var iron=Material("Lantern Iron",new Color(.13f,.17f,.18f));
            var glassMaterial=Material("Lantern Emissive Glass",new Color(.94f,.64f,.27f));
            // Keep the Standard emission variant enabled even if its material Inspector is opened.
            // Instances override this tiny fallback with zero by day and HDR emission at night.
            glassMaterial.EnableKeyword("_EMISSION");glassMaterial.SetColor("_EmissionColor",new Color(.01f,.006f,.002f));
            glassMaterial.globalIlluminationFlags=MaterialGlobalIlluminationFlags.None;EditorUtility.SetDirty(glassMaterial);
            var go=new GameObject("Farm Lantern");
            Part(go.transform,"Stone Foot",PrimitiveType.Cube,new Vector3(0,.05f,0),new Vector3(.32f,.10f,.32f),iron);
            Part(go.transform,"Timber Post",PrimitiveType.Cylinder,new Vector3(0,1.08f,0),new Vector3(.13f,1.08f,.13f),wood);
            Part(go.transform,"Cross Arm",PrimitiveType.Cube,new Vector3(.23f,2.13f,0),new Vector3(.72f,.10f,.12f),wood);
            Part(go.transform,"Lantern Base",PrimitiveType.Cube,new Vector3(.48f,1.67f,0),new Vector3(.39f,.09f,.34f),iron);
            var glass=Part(go.transform,"Warm Glass",PrimitiveType.Cube,new Vector3(.48f,1.89f,0),new Vector3(.29f,.36f,.25f),glassMaterial).GetComponent<MeshRenderer>();
            Part(go.transform,"Lantern Cap",PrimitiveType.Cube,new Vector3(.48f,2.10f,0),new Vector3(.42f,.09f,.37f),iron);
            foreach(var side in new[]{-1f,1f})foreach(var depth in new[]{-1f,1f})
                Part(go.transform,"Corner Frame",PrimitiveType.Cube,new Vector3(.48f+side*.16f,1.89f,depth*.14f),new Vector3(.035f,.40f,.035f),iron);
            var bulb=new GameObject("Warm Point Light");bulb.transform.SetParent(go.transform,false);bulb.transform.localPosition=new Vector3(.48f,1.88f,0);
            var light=bulb.AddComponent<Light>();light.type=LightType.Point;light.lightmapBakeType=LightmapBakeType.Realtime;
            light.color=new Color(1f,.68f,.32f);light.range=6.8f;light.intensity=0f;light.enabled=false;light.shadows=LightShadows.None;light.renderMode=LightRenderMode.ForcePixel;
            var collider=go.AddComponent<BoxCollider>();collider.center=new Vector3(0,1.08f,0);collider.size=new Vector3(.22f,2.16f,.22f);
            var lamp=go.AddComponent<FarmLantern>();var settings=new SerializedObject(lamp);
            settings.FindProperty("pointLight").objectReferenceValue=light;settings.FindProperty("glass").objectReferenceValue=glass;settings.ApplyModifiedPropertiesWithoutUndo();
            var result=PrefabUtility.SaveAsPrefabAsset(go,Folder+"/FarmLantern.prefab");UnityEngine.Object.DestroyImmediate(go);return result;
        }

        private static GameObject Part(Transform parent,string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
            var collider=go.GetComponent<Collider>();if(collider!=null)UnityEngine.Object.DestroyImmediate(collider);
            go.GetComponent<MeshRenderer>().sharedMaterial=material;return go;
        }

        private static Material Material(string name,Color color)
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/"+name+".mat");if(material!=null)return material;
            material=new Material(Shader.Find("Standard")){name=name,color=color,enableInstancing=true};material.SetFloat("_Glossiness",.22f);
            AssetDatabase.CreateAsset(material,Folder+"/"+name+".mat");return material;
        }

        private static void EnsureFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;var slash=path.LastIndexOf('/');var parent=path.Substring(0,slash);EnsureFolder(parent);AssetDatabase.CreateFolder(parent,path.Substring(slash+1));
        }
    }
}
