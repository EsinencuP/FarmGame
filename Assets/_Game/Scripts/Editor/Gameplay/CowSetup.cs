using System;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Animals;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.Gameplay.Economy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MyLittleFarm.Editor
{
    /// <summary>Authors U1-1 resource defaults without touching an open farm or save.</summary>
    public static class CowSetup
    {
        private const string Folder = "Assets/_Game/Resources/FarmContent";

        [MenuItem("Tools/My Little Farm/University/Prepare U1-1 Cows")]
        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Game/Resources", "FarmContent");
            var cowPath = Folder + "/Cow.asset";
            if (AssetDatabase.LoadAssetAtPath<AnimalDefinition>(cowPath) == null)
            {
                var cow = ScriptableObject.CreateInstance<AnimalDefinition>();
                cow.ConfigurePlaceholder("cow", "Корова", "wheat", "milk", 60, 1, Color.white);
                var properties = new SerializedObject(cow);
                properties.FindProperty("purchasePrice").intValue = 40;
                properties.FindProperty("visualPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(UniversityArtPreparation.Root + "/cow.prefab");
                properties.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(cow, cowPath);
            }
            var milkPath = Folder + "/Milk.asset";
            if (AssetDatabase.LoadAssetAtPath<ItemDefinition>(milkPath) == null)
            {
                var milk = ScriptableObject.CreateInstance<ItemDefinition>();
                milk.ConfigurePlaceholder("milk", "Молоко", 0, 28, false);
                var properties = new SerializedObject(milk);
                properties.FindProperty("icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Resources/FarmUI/Icons/milk.png");
                properties.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(milk, milkPath);
            }
            var path = Folder + "/Barn.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) CreateBarn(path);
            AssetDatabase.SaveAssets();
            Debug.Log("U1-1: cow, milk and metric barn defaults prepared. No scene or farm changed.");
        }

        private static void CreateBarn(string path)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("Barn with paddock"); SceneManager.MoveGameObjectToScene(root, scene);
                // The compact, already metric barn fits the authored farm without clearing permanent rocks/lamps.
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(MetricScaleSetup.Folder + "/storage.prefab");
                if (source == null) throw new MissingReferenceException("Metric barn art is missing.");
                var shed = UnityEngine.Object.Instantiate(source, root.transform, false); shed.name = "Barn solid";
                var sourceMetrics = shed.GetComponent<BuildingArtMetrics>();
                shed.transform.localPosition = new Vector3(-2, 0, 0);
                VisualPrefab.TryGetLocalBounds(shed, root.transform, out var bounds);
                if (bounds.min.x < -4.44f || bounds.max.x > .44f || bounds.size.z > 5.9f)
                    throw new InvalidOperationException("Metric barn exceeds its reserved solid area.");
                root.AddComponent<BuildingArtMetrics>().ConfigureDoor(
                    root.transform.InverseTransformPoint(sourceMetrics.DoorBottom), root.transform.InverseTransformPoint(sourceMetrics.DoorTop));
                var materialPath = Folder + "/Barn fence.mat";
                var timber = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (timber == null)
                {
                    timber = new Material(Shader.Find("Standard")) { name = "Barn fence", color = new Color(.42f,.28f,.17f), enableInstancing = true };
                    timber.SetFloat("_Glossiness", .1f); AssetDatabase.CreateAsset(timber, materialPath);
                }
                for (var z = -3; z <= 3; z += 2)
                    foreach (var x in new[] { .55f, 4.4f }) Part(root.transform, "Fence post", new Vector3(x,.55f,z*.97f), new Vector3(.1f,1.1f,.1f), timber);
                foreach (var y in new[] { .4f,.8f })
                {
                    foreach (var x in new[] { .55f,4.4f }) Part(root.transform,"Fence rail",new Vector3(x,y,0),new Vector3(.08f,.1f,5.82f),timber);
                    // Leave a 1.2 m opening on the front edge next to the barn.
                    Part(root.transform,"Fence rail",new Vector3(3.075f,y,-2.91f),new Vector3(2.65f,.1f,.08f),timber);
                    Part(root.transform,"Fence rail",new Vector3(2.475f,y,2.91f),new Vector3(3.85f,.1f,.08f),timber);
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        public static void RebuildBarn()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
            CreateBarn(Folder + "/Barn.prefab");
            AssetDatabase.SaveAssets();
        }

        private static void Part(Transform parent,string name,Vector3 position,Vector3 size,Material material)
        {
            var part=GameObject.CreatePrimitive(PrimitiveType.Cube);part.name=name;part.transform.SetParent(parent,false);
            part.transform.localPosition=position;part.transform.localScale=size;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());part.GetComponent<Renderer>().sharedMaterial=material;
        }
    }
}
