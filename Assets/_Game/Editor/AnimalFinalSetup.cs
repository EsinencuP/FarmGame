using System;
using System.Collections.Generic;
using System.Linq;
using MyLittleFarm.Gameplay.Animals;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MyLittleFarm.Editor
{
    /// <summary>Saved meshes, articulated prefabs and clips. Never initializes or saves an open farm.</summary>
    public static class AnimalFinalSetup
    {
        public const string Root="Assets/_Game/Content/Art/Animals";
        private const string ResourcesRoot="Assets/_Game/Resources/FarmContent";
        private static readonly Color Ivory=new Color(.92f,.88f,.74f), Wing=new Color(.78f,.70f,.51f),
            Red=new Color(.75f,.09f,.055f), Gold=new Color(.93f,.48f,.07f), Ink=new Color(.04f,.035f,.03f);
        [MenuItem("Tools/My Little Farm/University/Prepare U1-4 Animals")]
        public static void Prepare()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
            Folder(Root);Folder(Root+"/Meshes");Folder(Root+"/Animations");
            Chicken();Cow();AssetDatabase.SaveAssets();
            Debug.Log("U1-4: saved chicken, finished cow, Idle/Walk/Eat and foot metrics prepared. Open scene unchanged.");
        }
        private static void Folder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;var split=path.LastIndexOf('/');Folder(path.Substring(0,split));
            AssetDatabase.CreateFolder(path.Substring(0,split),path.Substring(split+1));
        }
        private static Material Feathers()
        {
            var path=Root+"/Animal Feathers.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material!=null)return material;
            material=new Material(AssetDatabase.LoadAssetAtPath<Shader>(CropStageArtBuilder.Root+"/CropVertexColor.shader")){name="Animal Feathers",enableInstancing=true};
            AssetDatabase.CreateAsset(material,path);return material;
        }
        private static Transform Part(Transform parent,string name,Vector3 position,MeshDraft draft)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;
            var path=Root+"/Meshes/Chicken_"+name.Replace(" ","_")+".asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null){mesh=draft.Mesh(name);AssetDatabase.CreateAsset(mesh,path);}
            if(_rebuildChickenMeshes){var authored=draft.Mesh(name);EditorUtility.CopySerialized(authored,mesh);EditorUtility.SetDirty(mesh);Object.DestroyImmediate(authored);}
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=Feathers();return go.transform;
        }
        private static Transform Marker(Transform parent,string name,Vector3 position)
        {var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;return go.transform;}
        private static bool _rebuildChickenMeshes;
        public static void RebuildChickenVisual()
        { _rebuildChickenMeshes=true;try {Chicken(true);AssetDatabase.SaveAssets();}finally{_rebuildChickenMeshes=false;} }
        private static void Chicken(bool rebuild=false)
        {
            var path=ResourcesRoot+"/ChickenVisual.prefab";
            if(!rebuild && AssetDatabase.LoadAssetAtPath<GameObject>(path)!=null)return;
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                var root=new GameObject("Chicken visual");SceneManager.MoveGameObjectToScene(root,scene);
                var model=new GameObject("Model");model.transform.SetParent(root.transform,false);
                var body=new MeshDraft();
                body.Loft(new[]{new Vector4(-.19f,.33f,.025f,.06f),new Vector4(-.13f,.33f,.13f,.13f),
                    new Vector4(-.025f,.34f,.158f,.15f),new Vector4(.075f,.37f,.125f,.13f),new Vector4(.15f,.40f,.045f,.065f)},Ivory);
                for(var i=-1;i<=1;i++)body.Feather(new Vector3(i*.045f,.39f,-.13f),new Vector3(i*.07f,.48f-Mathf.Abs(i)*.025f,-.28f),.095f,Ivory);
                Part(model.transform,"Body",Vector3.down*.07f,body);
                foreach(var side in new[]{-1,1})
                {
                    var wing=new MeshDraft();wing.Loft(new[]{new Vector4(-.13f,-.02f,.012f,.02f),new Vector4(-.08f,0,.038f,.07f),new Vector4(.025f,.015f,.043f,.075f),new Vector4(.09f,.02f,.01f,.025f)},Wing);
                    for(var i=0;i<3;i++)wing.Feather(new Vector3(side*.026f,-.02f+i*.025f,-.02f),new Vector3(side*.028f,-.06f+i*.025f,-.13f),.027f,Ivory);
                    Part(model.transform,side<0?"Wing L":"Wing R",new Vector3(side*.14f,.27f,0),wing);
                }
                var head=new MeshDraft();head.Egg(new Vector3(0,.085f,.025f),new Vector3(.075f,.105f,.08f),Ivory);
                head.Loft(new[]{new Vector4(-.035f,-.065f,.055f,.055f),new Vector4(.01f,-.02f,.055f,.09f),new Vector4(.055f,.065f,.04f,.07f)},Ivory);
                head.Beak(new Vector3(0,.066f,.095f),new Vector3(0,.035f,.18f),.052f,Gold);
                head.Crest(new[]{new Vector2(-.04f,.16f),new Vector2(-.04f,.205f),new Vector2(-.015f,.235f),new Vector2(.008f,.205f),
                    new Vector2(.033f,.245f),new Vector2(.053f,.212f),new Vector2(.075f,.23f),new Vector2(.09f,.185f),new Vector2(.07f,.16f)},.017f,Red);
                foreach(var side in new[]{-1,1})
                {
                    head.Egg(new Vector3(side*.066f,.105f,.066f),new Vector3(.013f,.017f,.016f),Ink);
                    head.Egg(new Vector3(side*.072f,.112f,.071f),new Vector3(.003f,.004f,.004f),Color.white);
                    head.Egg(new Vector3(side*.022f,.007f,.105f),new Vector3(.019f,.04f,.023f),Red);
                }
                Part(model.transform,"Head",new Vector3(0,.32f,.13f),head);
                var feet=new List<Transform>();var contacts=new List<Transform>();
                foreach(var side in new[]{-1,1})
                {
                    var mesh=new MeshDraft();mesh.Rod(new Vector3(0,.018f,0),new Vector3(0,.15f,-.015f),.015f,.025f,Gold);
                    for(var toe=-1;toe<=1;toe++)mesh.Rod(new Vector3(0,.017f,0),new Vector3(toe*.043f,.013f,.095f-Mathf.Abs(toe)*.012f),.014f,.006f,Gold);
                    mesh.Rod(new Vector3(0,.015f,0),new Vector3(side*.015f,.012f,-.046f),.011f,.005f,Gold);
                    var leg=Part(model.transform,side<0?"Leg L":"Leg R",new Vector3(side*.085f,-.006f,0),mesh);
                    feet.Add(leg);contacts.Add(Marker(leg,"Foot contact",new Vector3(0,.006f,.04f)));
                }
                var clips=new[]{ChickenClip("Idle",model),ChickenClip("Walk",model),ChickenClip("Eat",model)};
                var animator=model.AddComponent<Animator>();animator.runtimeAnimatorController=Controller("Chicken",clips);
                animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                var bounds=PoseBounds(root,animator.gameObject,clips);var lift=-bounds.min.y;model.transform.localPosition+=Vector3.up*lift;bounds.center+=Vector3.up*lift;bounds.Expand(.012f);
                root.AddComponent<AnimalVisualRig>().Configure(animator,bounds,feet.ToArray(),contacts.ToArray(),.4f,.18f,.78f);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
        private static AnimationClip ChickenClip(string name,GameObject root)
        {
            var path=Root+"/Animations/Chicken_"+name+".anim";var old=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);if(old!=null && !_rebuildChickenMeshes)return old;
            var clip=new AnimationClip{name="Chicken_"+name,frameRate=30};var length=name=="Idle"?2f:name=="Walk"?1f:1.2f;
            foreach(var part in root.GetComponentsInChildren<MeshFilter>())
            {
                var p=part.transform;var position=p.localPosition;
                Curve(clip,p.name,"m_LocalPosition.x",length,position.x,position.x,position.x);
                Curve(clip,p.name,"m_LocalPosition.y",length,position.y,position.y,position.y);
                Curve(clip,p.name,"m_LocalPosition.z",length,position.z,position.z,position.z);
                foreach(var axis in new[]{"x","y","z"})Curve(clip,p.name,"localEulerAnglesRaw."+axis,length,0,0,0);
            }
            if(name=="Idle")
            {Curve(clip,"Head","localEulerAnglesRaw.y",length,-8,8,-8);Curve(clip,"Wing L","localEulerAnglesRaw.z",length,-2,-5,-2);Curve(clip,"Wing R","localEulerAnglesRaw.z",length,2,5,2);}
            if(name=="Walk")
            {
                Keys(clip,"Leg L","m_LocalPosition.z",new[]{0f,.5f,.75f,1f},new[]{.10f,-.10f,0f,.10f},true);
                Keys(clip,"Leg L","m_LocalPosition.y",new[]{0f,.5f,.75f,1f},new[]{-.006f,-.006f,.049f,-.006f},false);
                Keys(clip,"Leg R","m_LocalPosition.z",new[]{0f,.25f,.5f,1f},new[]{-.10f,0f,.10f,-.10f},true);
                Keys(clip,"Leg R","m_LocalPosition.y",new[]{0f,.25f,.5f,1f},new[]{-.006f,.049f,-.006f,-.006f},false);
                Curve(clip,"Head","localEulerAnglesRaw.x",length,-4,4,-4);
            }
            if(name=="Eat")
            {Curve(clip,"Head","m_LocalPosition.y",length,.19f,.18f,.19f);Curve(clip,"Head","localEulerAnglesRaw.x",length,76,82,76);Curve(clip,"Head","localEulerAnglesRaw.y",length,-6,6,-6);}
            Loop(clip);if(old==null)AssetDatabase.CreateAsset(clip,path);else{EditorUtility.CopySerialized(clip,old);EditorUtility.SetDirty(old);Object.DestroyImmediate(clip);clip=old;}return clip;
        }
        private static void Curve(AnimationClip clip,string path,string property,float length,float a,float b,float c)
            => Keys(clip,path,property,new[]{0f,length*.5f,length},new[]{a,b,c},false);
        private static void Keys(AnimationClip clip,string path,string property,float[] times,float[] values,bool linear)
        {
            var curve=new AnimationCurve(times.Select((t,i)=>new Keyframe(t,values[i])).ToArray());
            if(linear)for(var i=0;i<curve.length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.Linear);}
            AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),property),curve);
        }
        private static void Loop(AnimationClip clip)
        {var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;AnimationUtility.SetAnimationClipSettings(clip,settings);clip.EnsureQuaternionContinuity();}
        private static AnimatorController Controller(string species,AnimationClip[] clips)
        {
            var path=Root+"/Animations/"+species+".controller";var old=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);if(old!=null)return old;
            var controller=AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed",AnimatorControllerParameterType.Float);controller.AddParameter("StrideRate",AnimatorControllerParameterType.Float);controller.AddParameter("Eating",AnimatorControllerParameterType.Bool);
            var machine=controller.layers[0].stateMachine;var states=new List<AnimatorState>();
            foreach(var name in new[]{"Idle","Walk","Eat"})
            {var state=machine.AddState(name);state.motion=clips[states.Count];if(name=="Walk"){state.speedParameter="StrideRate";state.speedParameterActive=true;}states.Add(state);}
            machine.defaultState=states[0];
            var walk=states[0].AddTransition(states[1]);walk.hasExitTime=false;walk.duration=.06f;walk.AddCondition(AnimatorConditionMode.Greater,.015f,"Speed");walk.AddCondition(AnimatorConditionMode.IfNot,0,"Eating");
            var idle=states[1].AddTransition(states[0]);idle.hasExitTime=false;idle.duration=.06f;idle.AddCondition(AnimatorConditionMode.Less,.015f,"Speed");
            foreach(var state in states.Take(2)){var eat=state.AddTransition(states[2]);eat.hasExitTime=false;eat.duration=.10f;eat.AddCondition(AnimatorConditionMode.If,0,"Eating");}
            var end=states[2].AddTransition(states[0]);end.hasExitTime=false;end.duration=.12f;end.AddCondition(AnimatorConditionMode.IfNot,0,"Eating");return controller;
        }
        public static void RebuildCowVisual() { Cow(true); AssetDatabase.SaveAssets(); }
        public static void RefineCowEating() { Cow(true,true); AssetDatabase.SaveAssets(); }
        private static void Cow(bool rebuild=false,bool refreshEat=false)
        {
            var path=ResourcesRoot+"/CowVisual.prefab";
            if(rebuild || AssetDatabase.LoadAssetAtPath<GameObject>(path)==null)
            {
                var scene=EditorSceneManager.NewPreviewScene();
                try
                {
                    var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(UniversityArtPreparation.Root+"/cow.prefab"));SceneManager.MoveGameObjectToScene(go,scene);go.name="Cow visual";
                    var animator=go.GetComponentInChildren<Animator>(true);var clips=new List<AnimationClip>();
                    foreach(var state in new[]{"Idle","Walk","Eat"})
                    {
                        var asset=Root+"/Animations/Cow_"+state+".anim";var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(asset);
                        if(state=="Eat" && (clip==null || refreshEat)) clip=CowEating(go,animator,asset,clip);
                        if(clip==null)
                        {
                            var source=AssetDatabase.LoadAssetAtPath<AnimationClip>(UniversityArtPreparation.Root+"/Animations/Cow_"+state+".anim");
                            clip=new AnimationClip{name="Cow_"+state,frameRate=30};
                            // The imported Walk has one 50-frame gait followed by 50 frozen frames.
                            // Keep one full moving cycle. Resample all bindings, including quaternion continuity.
                            var length=state=="Walk"?50f/30f:source.length;
                            foreach(var binding in AnimationUtility.GetCurveBindings(source))
                            {
                                var curve=AnimationUtility.GetEditorCurve(source,binding);var keys=new List<Keyframe>();
                                var count=Mathf.RoundToInt(length*30);
                                for(var i=0;i<=count;i++)keys.Add(new Keyframe(i/30f,curve.Evaluate(i/30f)));
                                var copy=new AnimationCurve(keys.ToArray());
                                for(var i=0;i<copy.length;i++)copy.SmoothTangents(i,0);
                                AnimationUtility.SetEditorCurve(clip,binding,copy);
                            }
                            Loop(clip);AssetDatabase.CreateAsset(clip,asset);
                        }
                        clips.Add(clip);
                    }
                    animator.runtimeAnimatorController=Controller("Cow",clips.ToArray());animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                    var bones=new List<Transform>();var contacts=new List<Transform>();
                    foreach(var name in new[]{"FrontLowLeg.L","FrontLowLeg.R","BackLowLeg.L","BackLowLeg.R"})
                    {
                        var all=go.GetComponentsInChildren<Transform>(true);
                        var bone=all.First(t=>t.name==name);var foot=all.First(t=>t.name==name.Replace("LowLeg","Foot"));
                        // Both independently skinned hoof controls and lower legs receive the same tiny residual.
                        bones.Add(bone);contacts.Add(foot);bones.Add(foot);contacts.Add(foot);
                    }
                    var bounds=PoseBounds(go,animator.gameObject,clips.ToArray());bounds.Expand(.02f);
                    var center=new Vector3(bounds.center.x,0,bounds.center.z);
                    foreach(Transform child in go.transform)child.localPosition-=center;
                    bounds.center-=center;
                    go.AddComponent<AnimalVisualRig>().Configure(animator,bounds,bones.ToArray(),contacts.ToArray(),.74f,.32f,1.62f);
                    PrefabUtility.SaveAsPrefabAsset(go,path);
                }
                finally{EditorSceneManager.ClosePreviewScene(scene);}
            }
            var cow=AssetDatabase.LoadAssetAtPath<AnimalDefinition>(ResourcesRoot+"/Cow.asset");
            if(cow==null)throw new MissingReferenceException("Prepare U1-1 cow definition first.");
            var so=new SerializedObject(cow);so.FindProperty("visualPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(path);so.ApplyModifiedPropertiesWithoutUndo();
        }
        private static AnimationClip CowEating(GameObject root,Animator animator,string asset,AnimationClip old)
        {
            // FBX bone axes are not animal axes. Author the bow in the animal's world frame,
            // then record local quaternions instead of adding an angle to an arbitrary imported Euler axis.
            var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>(UniversityArtPreparation.Root+"/Animations/Cow_Idle.anim");
            var clip=new AnimationClip{name="Cow_Eat",frameRate=30};
            var neck=root.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Neck");
            var head=root.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Head");
            var neckPath=AnimationUtility.CalculateTransformPath(neck,animator.transform);
            var headPath=AnimationUtility.CalculateTransformPath(head,animator.transform);
            var times=new[]{0f,.4f,.8f,1.2f,1.6f};
            foreach(var binding in AnimationUtility.GetCurveBindings(idle))
            {
                if((binding.path==neckPath && (binding.propertyName.StartsWith("m_LocalRotation") || binding.propertyName.StartsWith("m_LocalPosition")))
                    || (binding.path==headPath && binding.propertyName.StartsWith("m_LocalRotation")))continue;
                var source=AnimationUtility.GetEditorCurve(idle,binding);
                var curve=new AnimationCurve(times.Select((time,i)=>new Keyframe(time,source.Evaluate(i==times.Length-1?0:time))).ToArray());
                AnimationUtility.SetEditorCurve(clip,binding,curve);
            }
            var rotations=new Quaternion[2,times.Length];var positions=new Vector3[times.Length];
            for(var i=0;i<times.Length;i++)
            {
                idle.SampleAnimation(animator.gameObject,0);
                var deep=(i&1)==1;var angle=deep?60f:i==2?56f:52f;
                neck.rotation=Quaternion.AngleAxis(angle,root.transform.right)*neck.rotation;
                neck.position-=root.transform.up*(deep?.16f:i==2?.14f:.10f);
                head.rotation=Quaternion.AngleAxis(deep?26f:22f,root.transform.right)*head.rotation;
                rotations[0,i]=neck.localRotation;rotations[1,i]=head.localRotation;positions[i]=neck.localPosition;
            }
            for(var part=0;part<2;part++)foreach(var axis in new[]{0,1,2,3})
            {
                var curve=new AnimationCurve(times.Select((time,i)=>new Keyframe(time,rotations[part,i][axis])).ToArray());
                AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(part==0?neckPath:headPath,typeof(Transform),"m_LocalRotation."+"xyzw"[axis]),curve);
            }
            for(var axis=0;axis<3;axis++)
            {
                var curve=new AnimationCurve(times.Select((time,i)=>new Keyframe(time,positions[i][axis])).ToArray());
                AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(neckPath,typeof(Transform),"m_LocalPosition."+"xyz"[axis]),curve);
            }
            Loop(clip);
            if(old==null)AssetDatabase.CreateAsset(clip,asset);
            else{EditorUtility.CopySerialized(clip,old);EditorUtility.SetDirty(old);Object.DestroyImmediate(clip);clip=old;}
            idle.SampleAnimation(animator.gameObject,0);return clip;
        }
        private static Bounds PoseBounds(GameObject root,GameObject animated,AnimationClip[] clips)
        {
            var found=false;var bounds=new Bounds();
            foreach(var clip in clips)for(var frame=0;frame<=12;frame++)
            {
                clip.SampleAnimation(animated,clip.length*frame/12);
                foreach(var mesh in root.GetComponentsInChildren<MeshFilter>(true))foreach(var vertex in mesh.sharedMesh.vertices)
                {var p=root.transform.InverseTransformPoint(mesh.transform.TransformPoint(vertex));if(!found){bounds=new Bounds(p,Vector3.zero);found=true;}else bounds.Encapsulate(p);}
                foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {var baked=new Mesh();try{skin.BakeMesh(baked,true);foreach(var vertex in baked.vertices){var p=root.transform.InverseTransformPoint(skin.transform.TransformPoint(vertex));if(!found){bounds=new Bounds(p,Vector3.zero);found=true;}else bounds.Encapsulate(p);}}finally{Object.DestroyImmediate(baked);}}
            }
            clips[0].SampleAnimation(animated,0);return bounds;
        }
        private sealed class MeshDraft
        {
            private readonly List<Vector3> vertices=new List<Vector3>();private readonly List<Color> colors=new List<Color>();private readonly List<int> indices=new List<int>();
            private void Tri(Vector3 a,Vector3 b,Vector3 c,Color color,Vector3 outward)
            {
                var n=Vector3.Cross(b-a,c-a);if(n.sqrMagnitude<1e-12f)return;if(Vector3.Dot(n,outward)<0){var swap=b;b=c;c=swap;}
                var index=vertices.Count;vertices.AddRange(new[]{a,b,c});for(var i=0;i<3;i++){indices.Add(index+i);colors.Add(color);}
            }
            public void Loft(Vector4[] rings,Color color)
            {
                Vector3 Point(Vector4 r,int i){var a=i*Mathf.PI/4;return new Vector3(r.z*Mathf.Cos(a),r.y+r.w*Mathf.Sin(a),r.x);}
                for(var r=0;r<rings.Length-1;r++)for(var i=0;i<8;i++)
                {var a=Point(rings[r],i);var b=Point(rings[r],i+1);var c=Point(rings[r+1],i+1);var d=Point(rings[r+1],i);var outside=new Vector3(a.x,a.y-rings[r].y,0);Tri(a,b,c,color,outside);Tri(a,c,d,color,outside);}
                foreach(var end in new[]{0,rings.Length-1})for(var i=0;i<8;i++)Tri(new Vector3(0,rings[end].y,rings[end].x),Point(rings[end],i),Point(rings[end],i+1),color,end==0?Vector3.back:Vector3.forward);
            }
            public void Egg(Vector3 center,Vector3 radii,Color color)
            {
                Vector3 P(int y,int x){var a=y*Mathf.PI/4;var b=x*Mathf.PI/4;return center+Vector3.Scale(radii,new Vector3(Mathf.Sin(a)*Mathf.Cos(b),Mathf.Cos(a),Mathf.Sin(a)*Mathf.Sin(b)));}
                for(var y=0;y<4;y++)for(var x=0;x<8;x++){var a=P(y,x);var b=P(y+1,x);var c=P(y+1,x+1);var d=P(y,x+1);Tri(a,b,c,color,a+b+c-center*3);Tri(a,c,d,color,a+c+d-center*3);}
            }
            public void Feather(Vector3 start,Vector3 tip,float width,Color color)
            {var side=Vector3.right*width*.5f;var ridge=Vector3.Lerp(start,tip,.5f)+Vector3.up*.012f;var edge=new[]{start-side,tip-side*.35f,tip+side*.35f,start+side};for(var i=0;i<4;i++)Tri(edge[i],edge[(i+1)%4],ridge,color,Vector3.up);Tri(edge[0],edge[2],edge[1],color,Vector3.down);Tri(edge[0],edge[3],edge[2],color,Vector3.down);}
            public void Beak(Vector3 root,Vector3 tip,float width,Color color)
            {var a=root+Vector3.left*width*.5f;var b=root+Vector3.right*width*.5f;var c=root+Vector3.down*.035f;Tri(a,b,tip,color,Vector3.up);Tri(a,tip,c,color,Vector3.left);Tri(b,c,tip,color,Vector3.right);Tri(a,c,b,color,Vector3.back);}
            public void Crest(Vector2[] profile,float halfWidth,Color color)
            {
                Vector3 P(int i,float side)=>new Vector3(side*halfWidth,profile[i].y,profile[i].x);
                for(var i=1;i<profile.Length-1;i++){Tri(P(0,-1),P(i,-1),P(i+1,-1),color,Vector3.left);Tri(P(0,1),P(i+1,1),P(i,1),color,Vector3.right);}
                var center=profile.Aggregate(Vector2.zero,(a,b)=>a+b)/profile.Length;
                for(var i=0;i<profile.Length;i++){var j=(i+1)%profile.Length;var n=new Vector3(0,(profile[i].y+profile[j].y)*.5f-center.y,(profile[i].x+profile[j].x)*.5f-center.x);Tri(P(i,-1),P(j,-1),P(j,1),color,n);Tri(P(i,-1),P(j,1),P(i,1),color,n);}
            }
            public void Rod(Vector3 start,Vector3 end,float a,float b,Color color)
            {
                var direction=(end-start).normalized;var right=Vector3.Cross(direction,Vector3.forward).normalized;if(right.sqrMagnitude<.01f)right=Vector3.right;var forward=Vector3.Cross(direction,right);
                for(var i=0;i<6;i++){var n=right*Mathf.Cos(i*Mathf.PI/3)+forward*Mathf.Sin(i*Mathf.PI/3);var m=right*Mathf.Cos((i+1)*Mathf.PI/3)+forward*Mathf.Sin((i+1)*Mathf.PI/3);Tri(start+n*a,start+m*a,end+n*b,color,n+m);Tri(start+m*a,end+m*b,end+n*b,color,n+m);Tri(start,start+m*a,start+n*a,color,-direction);Tri(end,end+n*b,end+m*b,color,direction);}
            }
            public Mesh Mesh(string name)
            {var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;}
        }
    }
}
