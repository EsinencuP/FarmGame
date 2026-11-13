using System;
using System.Linq;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.Gameplay.Economy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MyLittleFarm.Editor
{
    /// <summary>Explicit asset authoring only; never writes the open scene or initializes a farm.</summary>
    public static class ProductionSetup
    {
        private const string Folder="Assets/_Game/Resources/FarmContent";
        [MenuItem("Tools/My Little Farm/University/Prepare U1-3 Production")]
        public static void Prepare()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
            MakeBuilding("Mill","mill",5,4);
            MakeBuilding("Dairy","cheese_dairy",5,6);
            MakeItem("Flour",ProductionRecipe.Mill);
            MakeItem("Cheese",ProductionRecipe.Dairy);
            AssetDatabase.SaveAssets();
            Debug.Log("U1-3 production art and items prepared. Open scene and saves unchanged.");
        }
        private static void Part(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);
            go.transform.localPosition=position;go.transform.localScale=scale;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial=material;
        }
        private static void MakeItem(string file,ProductionRecipe recipe)
        {
            var path=Folder+"/"+file+".asset"; if(AssetDatabase.LoadAssetAtPath<ItemDefinition>(path)!=null) return;
            var item=ScriptableObject.CreateInstance<ItemDefinition>(); item.ConfigurePlaceholder(recipe.OutputId,recipe.OutputName,0,recipe.SellPrice,false);
            var so=new SerializedObject(item);so.FindProperty("icon").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Resources/FarmUI/Icons/"+recipe.OutputId+".png");
            so.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.CreateAsset(item,path);
        }
        public static void RebuildMill()
        { MakeBuilding("Mill","mill",5,4,true); AssetDatabase.SaveAssets(); }
        private static void MakeBuilding(string file,string source,int width,int depth,bool rebuild=false)
        {
            var path=Folder+"/"+file+".prefab";if(!rebuild && AssetDatabase.LoadAssetAtPath<GameObject>(path)!=null) return;
            var preview=EditorSceneManager.NewPreviewScene();
            try
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(UniversityArtPreparation.Root+"/"+source+".prefab");
                if(prefab==null) throw new MissingReferenceException("Prepare U1-0 art first.");
                var root=new GameObject(file); SceneManager.MoveGameObjectToScene(root,preview);
                var model=UnityEngine.Object.Instantiate(prefab,root.transform,false);
                foreach(var collider in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                // Preserve the prepared metre-scale silhouettes. Adapt only the door leaf to human clearance.
                var door=model.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r=>r.name.IndexOf("door",StringComparison.OrdinalIgnoreCase)>=0);
                if(door!=null)
                {
                    var bottom=door.bounds.min.y;
                    var pivot=new GameObject("Human-height door");pivot.transform.SetParent(root.transform,false);
                    door.transform.SetParent(pivot.transform,true);
                    pivot.transform.localScale=new Vector3(1,BuildingArtMetrics.DoorClearance/door.bounds.size.y,1);
                    pivot.transform.localPosition+=Vector3.up*(bottom-door.bounds.min.y);
                }
                else
                {
                    VisualPrefab.TryGetLocalBounds(model,root.transform,out var body);
                    var leaf=GameObject.CreatePrimitive(PrimitiveType.Cube);leaf.name="Mill service door";leaf.transform.SetParent(model.transform,false);
                    UnityEngine.Object.DestroyImmediate(leaf.GetComponent<Collider>());
                    leaf.transform.localPosition=new Vector3(0,BuildingArtMetrics.DoorClearance*.5f,body.min.z-.035f);
                    leaf.transform.localScale=new Vector3(.9f,BuildingArtMetrics.DoorClearance,.07f);
                    var wood=AssetDatabase.LoadAssetAtPath<Material>(UniversityArtPreparation.Root+"/Materials/Timber.mat");
                    door=leaf.GetComponent<Renderer>();door.sharedMaterial=wood;
                    // A shallow entry housing joins the flat door to the tapering tower wall.
                    var stone=AssetDatabase.LoadAssetAtPath<Material>(UniversityArtPreparation.Root+"/Materials/Iron.mat");
                    var front=leaf.transform.localPosition.z;
                    Part(model.transform,"Entry housing",new Vector3(0,1.04f,front+.28f),new Vector3(1.14f,2.08f,.55f),stone);
                    Part(model.transform,"Door handle",new Vector3(.29f,.95f,front-.05f),new Vector3(.05f,.16f,.05f),stone);
                    Part(model.transform,"Door brace",new Vector3(0,.34f,front-.046f),new Vector3(.84f,.08f,.03f),stone);
                }
                VisualPrefab.TryGetLocalBounds(model,root.transform,out var bounds);
                model.transform.localPosition-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
                VisualPrefab.TryGetLocalBounds(model,root.transform,out bounds);
                if(bounds.size.x>width-.12f || bounds.size.z>depth-.12f)
                    throw new InvalidOperationException("Metric production model exceeds footprint: "+source+" "+bounds.size);
                root.AddComponent<BuildingArtMetrics>().ConfigureDoor(root.transform.InverseTransformPoint(new Vector3(door.bounds.center.x,door.bounds.min.y,door.bounds.center.z)),
                    root.transform.InverseTransformPoint(new Vector3(door.bounds.center.x,door.bounds.max.y,door.bounds.center.z)));
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }
    }
}
