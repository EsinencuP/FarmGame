using System;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.Farming;
using MyLittleFarm.Gameplay.World;
using MyLittleFarm.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MyLittleFarm.Editor
{
    /// <summary>Explicit static content authoring; no gameplay setup, profile IO or runtime simulation.</summary>
    public static class FarmDepthSetup
    {
        public const string Folder="Assets/_Game/Content/World/Depth";
        [MenuItem("Tools/My Little Farm/Apply P2-7 Farm Depth")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Use Edit Mode.");
            var root=UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
            if(root==null||root.gameObject.scene.path!="Assets/_Game/Scenes/Prototype.unity")throw new InvalidOperationException("Open Prototype first.");
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/_Game/Content/World","Depth");
            ConfigureCrop("carrot",5,9,1,false);ConfigureCrop("tomato",11,15,2,false);
            ConfigureCrop("cucumber",18,22,1,false);ConfigureCrop("corn",0,0,0,true);
            var progress=root.GetComponentInChildren<FarmProgression>(true);
            if(progress==null){var go=new GameObject("Farm Progression");go.transform.SetParent(root.transform,false);progress=go.AddComponent<FarmProgression>();}
            var caches=new RareSeedCache[3];var locations=new[]{new Vector2Int(-14,19),new Vector2Int(-2,31),new Vector2Int(28,20)};
            var names=new[]{"Западный пруд","Северный лес","Восточный холм"};
            var prefab=CachePrefab();
            for(var i=0;i<3;i++)
            {
                foreach(var cache in root.GetComponentsInChildren<RareSeedCache>(true))if(cache.Id==FarmProgression.CacheIds[i]){caches[i]=cache;break;}
                if(caches[i]==null)
                {
                    var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,root.transform);instance.name="Rare Seeds — "+names[i];
                    instance.transform.position=SurfaceAt(root,new Vector3(locations[i].x+.5f,0,locations[i].y+.5f));
                    caches[i]=instance.GetComponent<RareSeedCache>();
                    var settings=new SerializedObject(caches[i]);settings.FindProperty("cacheId").stringValue=FarmProgression.CacheIds[i];settings.FindProperty("locationName").stringValue=names[i];settings.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            var helper=root.transform.Find("Hired Shepherd");
            if(helper==null)
            {
                helper=new GameObject("Hired Shepherd").transform;helper.SetParent(root.transform,false);
                helper.position=SurfaceAt(root,new Vector3(6.85f,0,6.5f));helper.rotation=Quaternion.Euler(0,230,0);
                var visual=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Content/Character/HumanPlayerVisual.prefab");
                if(visual==null)throw new MissingReferenceException("Green character visual is missing.");
                VisualPrefab.Create(visual,helper,new Vector3(10,1.44f,10));
                helper.gameObject.AddComponent<InteractionTarget>().Configure(InteractionTargetKind.Helper,"shepherd",true);
            }
            helper.gameObject.SetActive(false);
            var hud=root.GetComponentInChildren<HUDController>(true);
            var journal=hud.GetComponentInChildren<FarmJournalUI>(true);
            if(journal==null)journal=FarmJournalUI.Create(hud.transform,progress,root.GetComponent<InputReader>(),Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            if(journal.ClockText==null)
            {
                var clock=SaveSlotsUI.Label(journal.transform,"",Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"),21);
                clock.gameObject.name="World Clock";clock.transform.SetAsFirstSibling();
                var settings=new SerializedObject(journal);settings.FindProperty("clock").objectReferenceValue=clock;settings.ApplyModifiedPropertiesWithoutUndo();
            }
            journal.ClearPreview();journal.Resize();
            var layout=hud.GetComponent<ResponsiveHudLayout>();
            if(layout!=null){var uiSettings=new SerializedObject(layout);uiSettings.FindProperty("worldClock").objectReferenceValue=journal.ClockText;uiSettings.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(layout);}
            // Save-slot modal stays above this journal/toasts; modal input prevents opening both at once.
            var slots=hud.GetComponentInChildren<SaveSlotsUI>(true);if(slots!=null)slots.transform.SetAsLastSibling();
            var serialized=new SerializedObject(progress);serialized.FindProperty("journal").objectReferenceValue=journal;
            serialized.FindProperty("shepherdVisual").objectReferenceValue=helper.gameObject;
            var array=serialized.FindProperty("seedCaches");array.arraySize=3;for(var i=0;i<3;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=caches[i];serialized.ApplyModifiedPropertiesWithoutUndo();
            hud.GetComponent<ResponsiveHudLayout>()?.SetEditorPreview(ResponsiveHudLayout.PreviewMode.Exploration);
            EditorUtility.SetDirty(progress);EditorUtility.SetDirty(journal);AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);EditorSceneManager.SaveScene(root.gameObject.scene);
            Debug.Log("P2-7 authored: three time windows, three seed caches, hired helper, journal and 20 local achievements. No gameplay initialized.");
        }
        private static void ConfigureCrop(string id,float start,float end,int amount,bool rare)
        {
            var crop=AssetDatabase.LoadAssetAtPath<CropDefinition>("Assets/_Game/Content/Crops/"+id+".asset");
            if(crop==null)throw new MissingReferenceException(id+" crop is missing.");
            var serialized=new SerializedObject(crop);serialized.FindProperty("rareSeeds").boolValue=rare;
            serialized.FindProperty("bonusStartHour").floatValue=start;serialized.FindProperty("bonusEndHour").floatValue=end;
            serialized.FindProperty("bonusHarvestYield").intValue=amount;serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(crop);
        }
        private static GameObject CachePrefab()
        {
            var path=Folder+"/RareSeedCache.prefab";var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(existing!=null)return existing;
            var cloth=Material("Seed Bag",new Color(.48f,.34f,.17f));var gold=Material("Rare Gold",new Color(1,.73f,.12f));var leaf=Material("Leaf Green",new Color(.25f,.48f,.10f));
            var root=new GameObject("Rare seed cache");
            try
            {
                Part(root.transform,"Seed sack",PrimitiveType.Sphere,new Vector3(0,.22f,0),new Vector3(.65f,.44f,.55f),cloth);
                Part(root.transform,"Neck tie",PrimitiveType.Cylinder,new Vector3(0,.43f,0),new Vector3(.23f,.045f,.23f),cloth);
                var seeds=new GameObject("Golden seeds");seeds.transform.SetParent(root.transform,false);
                for(var i=0;i<3;i++)
                {
                    var stem=Part(seeds.transform,"Golden seed",PrimitiveType.Capsule,new Vector3((i-1)*.16f,.66f,0),new Vector3(.13f,.19f,.13f),gold);
                    stem.localRotation=Quaternion.Euler(0,0,(i-1)*-18f);
                    Part(seeds.transform,"Husk",PrimitiveType.Cube,new Vector3((i-1)*.16f,.47f,-.07f),new Vector3(.11f,.20f,.035f),leaf);
                }
                var component=root.AddComponent<RareSeedCache>();var serialized=new SerializedObject(component);serialized.FindProperty("seedsVisual").objectReferenceValue=seeds;serialized.ApplyModifiedPropertiesWithoutUndo();
                root.AddComponent<InteractionTarget>().Configure(InteractionTargetKind.SeedCache,null,true);
                return PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        private static Material Material(string name,Color color)
        {
            var path=Folder+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material!=null)return material;
            material=new Material(Shader.Find("Standard")){name=name,color=color};material.SetFloat("_Glossiness",.15f);AssetDatabase.CreateAsset(material,path);return material;
        }
        private static Transform Part(Transform parent,string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=material;return go.transform;
        }
        private static Vector3 SurfaceAt(GameBootstrap root,Vector3 point)
        {
            var ray=new Ray(new Vector3(point.x,100,point.z),Vector3.down);
            foreach(var chunk in root.GetComponentsInChildren<TilemapChunk>(true))
            {RaycastHit hit;if(chunk.TerrainCollider!=null&&chunk.TerrainCollider.Raycast(ray,out hit,200))return hit.point;}
            return point;
        }
    }
}
