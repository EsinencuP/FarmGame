#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using MyLittleFarm.Gameplay.Farming;

namespace MyLittleFarm.Editor
{
    public static class ExtraFarmContentSetup
    {
        [MenuItem("Tools/My Little Farm/University/Prepare U1-7 Content")]
        public static void Prepare()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Use Edit Mode.");
            foreach(var id in new[]{"strawberry","pumpkin"})
            {
                var path="Assets/_Game/Resources/FarmContent/"+id+".asset";
                var definition=AssetDatabase.LoadAssetAtPath<CropDefinition>(path);
                if(definition==null){definition=ScriptableObject.CreateInstance<CropDefinition>();definition.ConfigurePlaceholder(id,id=="strawberry"?"Клубника":"Тыква",id=="strawberry"?30:35,id=="strawberry"?3:2,Color.white);AssetDatabase.CreateAsset(definition,path);}
                var so=new SerializedObject(definition);var prefabs=so.FindProperty("stagePrefabs");var sizes=so.FindProperty("stageSizes");prefabs.arraySize=sizes.arraySize=3;
                var stages=new[]{"Sprout","Growing","Mature"};
                for(var i=0;i<3;i++)
                {
                    prefabs.GetArrayElementAtIndex(i).objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(UniversityArtPreparation.Root+"/"+id+"_"+stages[i]+".prefab");
                    sizes.GetArrayElementAtIndex(i).vector3Value=id=="strawberry"?new Vector3(.25f+i*.15f,.12f+i*.12f,.25f+i*.15f):new Vector3(.24f+i*.24f,.12f+i*.18f,.24f+i*.24f);
                }
                so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(definition);
            }
            var tomato=AssetDatabase.LoadAssetAtPath<CropDefinition>("Assets/_Game/Content/Crops/tomato.asset");
            if(tomato!=null){var so=new SerializedObject(tomato);so.FindProperty("regrowSeconds").floatValue=45;so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(tomato);}
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                var root=new GameObject("Feeder");SceneManager.MoveGameObjectToScene(root,scene);
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(UniversityArtPreparation.Root+"/feeder.prefab");
                if(source==null)throw new MissingReferenceException("Prepare U1-0 feeder art.");
                UnityEngine.Object.Instantiate(source,root.transform,false);
                PrefabUtility.SaveAsPrefabAsset(root,"Assets/_Game/Resources/FarmContent/Feeder.prefab");
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
            AssetDatabase.SaveAssets();Debug.Log("U1-7 crops and feeder authored; open scene unchanged.");
        }
    }
}
#endif
