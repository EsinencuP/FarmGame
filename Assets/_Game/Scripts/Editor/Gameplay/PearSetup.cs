using System;
using System.Collections.Generic;
using System.IO;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.Farming;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MyLittleFarm.Editor
{
    /// <summary>Explicit U1-2 asset authoring; preserves source art and never initializes a farm.</summary>
    public static class PearSetup
    {
        private const string Folder = "Assets/_Game/Resources/FarmContent";
        private const string Art = UniversityArtPreparation.Root;

        [MenuItem("Tools/My Little Farm/University/Prepare U1-2 Pear")]
        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Game/Resources", "FarmContent");
            var tree = MakeTree(); var fruit = MakeFruit();
            MakeSaplingIcon(tree);
            MakeItem("PearFruit", "pear", "Груша", 0, 24, "crop_pear");
            MakeItem("PearSapling", "pear_sapling", "Саженец груши", 30, 0, "sapling_pear");
            if (AssetDatabase.LoadAssetAtPath<TreeDefinition>(Folder + "/Pear.asset") == null)
            {
                var definition = ScriptableObject.CreateInstance<TreeDefinition>();
                definition.ConfigurePlaceholder("pear", "Груша", 45, 60, 3, new Color(.39f,.55f,.13f), new Color(.88f,.71f,.22f));
                var properties = new SerializedObject(definition);
                properties.FindProperty("fruitItemId").stringValue = "pear";
                properties.FindProperty("saplingItemId").stringValue = "pear_sapling";
                properties.FindProperty("visualPrefab").objectReferenceValue = tree;
                properties.FindProperty("fruitPrefab").objectReferenceValue = fruit;
                properties.FindProperty("visualSize").vector3Value = new Vector3(1.6f,3f,1.6f);
                properties.FindProperty("fruitSize").floatValue = .36f;
                properties.FindProperty("preserveFruitMaterials").boolValue = true;
                properties.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(definition, Folder + "/Pear.asset");
            }
            AssetDatabase.SaveAssets();
            Debug.Log("U1-2: pear definition, sapling, fruit and distinctive art prepared. Existing apple and scene unchanged.");
        }

        private static GameObject MakeTree()
        {
            var path = Art + "/pear_orchard.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (existing != null) return existing;
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("Pear orchard visual"); SceneManager.MoveGameObjectToScene(root,scene);
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/pear_tree.prefab");
                if (source == null) throw new MissingReferenceException("Prepare U1-0 pear art first.");
                var model = UnityEngine.Object.Instantiate(source,root.transform,false);
                model.transform.localScale = new Vector3(.7f,1.15f,.7f);
                return PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static Material Material(string name, Color color)
        {
            var path = Art + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path); if (material != null) return material;
            material = new Material(Shader.Find("Standard")) { name = name, color = color, enableInstancing = true };
            material.SetFloat("_Glossiness", .12f); AssetDatabase.CreateAsset(material,path); return material;
        }

        private static GameObject MakeFruit()
        {
            var path = Art + "/pear_ripe.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (existing != null) return existing;
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/pear_fruit.prefab");
                if (source == null) throw new MissingReferenceException("Prepare U1-0 pear fruit first.");
                var root = UnityEngine.Object.Instantiate(source); SceneManager.MoveGameObjectToScene(root,scene);root.name="Ripe pear";
                VisualPrefab.TryGetBounds(root,out var bounds);
                var body = Material("Pear golden skin",new Color(.92f,.70f,.18f));
                var stem = Material("Pear stem",new Color(.30f,.18f,.10f));
                var index = 0;
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    var meshPath = Art + "/Meshes/pear_ripe_" + index++ + ".asset";
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if (mesh == null)
                    {
                        mesh = UnityEngine.Object.Instantiate(filter.sharedMesh); mesh.name = "Ripe pear with separate stem";
                        var vertices = mesh.vertices; var triangles = mesh.triangles;
                        var bodyIndices = new List<int>(); var stemIndices = new List<int>();
                        for (var t=0;t<triangles.Length;t+=3)
                        {
                            var center = (vertices[triangles[t]]+vertices[triangles[t+1]]+vertices[triangles[t+2]])/3;
                            var indices = filter.transform.TransformPoint(center).y > bounds.min.y + bounds.size.y*.86f ? stemIndices : bodyIndices;
                            indices.Add(triangles[t]);indices.Add(triangles[t+1]);indices.Add(triangles[t+2]);
                        }
                        mesh.subMeshCount=2;mesh.SetTriangles(bodyIndices,0);mesh.SetTriangles(stemIndices,1);
                        AssetDatabase.CreateAsset(mesh,meshPath);
                    }
                    filter.sharedMesh=mesh;filter.GetComponent<MeshRenderer>().sharedMaterials=new[]{body,stem};
                }
                return PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static void MakeItem(string file,string id,string title,int buy,int sell,string icon)
        {
            var path=Folder+"/"+file+".asset";
            if(AssetDatabase.LoadAssetAtPath<ItemDefinition>(path)!=null)return;
            var item=ScriptableObject.CreateInstance<ItemDefinition>();item.ConfigurePlaceholder(id,title,buy,sell,buy>0);
            var properties=new SerializedObject(item);
            properties.FindProperty("icon").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Resources/FarmUI/Icons/"+icon+".png");
            properties.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.CreateAsset(item,path);
        }

        private static void MakeSaplingIcon(GameObject prefab)
        {
            var path="Assets/_Game/Resources/FarmUI/Icons/sapling_pear.png";if(File.Exists(path))return;
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                var model=UnityEngine.Object.Instantiate(prefab);SceneManager.MoveGameObjectToScene(model,scene);
                VisualPrefab.TryGetBounds(model,out var bounds);
                var lightObject=new GameObject("Icon light");SceneManager.MoveGameObjectToScene(lightObject,scene);
                var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.transform.rotation=Quaternion.Euler(45,-35,0);
                var cameraObject=new GameObject("Icon camera");SceneManager.MoveGameObjectToScene(cameraObject,scene);
                var camera=cameraObject.AddComponent<Camera>();camera.scene=scene;camera.orthographic=true;camera.orthographicSize=bounds.size.y*.65f;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
                camera.transform.position=bounds.center+new Vector3(4,2,5);camera.transform.LookAt(bounds.center);
                var target=RenderTexture.GetTemporary(256,256,24,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;
                var image=new Texture2D(256,256,TextureFormat.RGBA32,false);
                try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,256,256),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
                finally{camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);UnityEngine.Object.DestroyImmediate(image);}
                AssetDatabase.ImportAsset(path);
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
    }
}
