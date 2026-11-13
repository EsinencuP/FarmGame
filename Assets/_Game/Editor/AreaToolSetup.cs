#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MyLittleFarm.Editor
{
    public static class AreaToolSetup
    {
        [MenuItem("Tools/My Little Farm/University/Prepare U1-6 Tools")]
        public static void Prepare()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Use Edit Mode.");
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                for(var i=0;i<3;i++)
                {
                    var source=AssetDatabase.LoadAssetAtPath<GameObject>(UniversityArtPreparation.Root+"/tool_hoe_"+i+".prefab");
                    if(source==null)throw new MissingReferenceException("Prepare U1-0 tool art first.");
                    var root=new GameObject("Hoe level "+(i+1));SceneManager.MoveGameObjectToScene(root,scene);
                    var model=UnityEngine.Object.Instantiate(source,root.transform,false);
                    var width=i==0?1f:i==1?1.4f:1.9f;
                    model.transform.localRotation=Quaternion.Euler(0,0,180);
                    model.transform.localScale=new Vector3(1,1,width);
                    // Re-grip the prepared shaft .5m higher and point its blade towards the ground.
                    model.transform.localPosition=new Vector3(0,.5f,.09f*width);
                    foreach(var collider in root.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(collider);
                    foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=2;
                    PrefabUtility.SaveAsPrefabAsset(root,"Assets/_Game/Resources/FarmContent/Hoe"+i+".prefab");
                    UnityEngine.Object.DestroyImmediate(root);
                }
                AssetDatabase.SaveAssets();Debug.Log("U1-6: three metre-scale held hoes prepared; open scene unchanged.");
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
    }
}
#endif
