using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MyLittleFarm.Editor
{
    /// <summary>Explicit asset authoring only. Does not instantiate a playable farm or modify player saves.</summary>
    public static class PersonalizationSetup
    {
        public const string Folder="Assets/_Game/Content/World/Personalization";
        private const string Farm="Assets/LowPolyFarmLite/Prefabs/";
        private const string Ranch="Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/";
        private static readonly Color Timber=new Color(.39f,.24f,.12f),Cream=new Color(.87f,.79f,.59f),Iron=new Color(.19f,.22f,.23f);

        [MenuItem("Tools/My Little Farm/Apply P2-4 Personalization")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Use Edit Mode.");
            var scene=EditorSceneManager.GetActiveScene();
            if(scene.path!="Assets/_Game/Scenes/Prototype.unity")throw new InvalidOperationException("Open Prototype first.");
            EnsureFolder(Folder);EnsureFolder(Folder+"/Materials");EnsureFolder(Folder+"/Meshes");
            var root=UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
            var build=root==null?null:root.GetComponentInChildren<BuildSystem>(true);
            if(build==null)throw new MissingReferenceException("Prototype building system is missing.");
            var assets=new List<GameObject>();var manifest=new StringBuilder("# P2-4 decoration manifest\n\n30 new decorations, plus the existing flowerbed. One cell = one Unity unit. Imported source assets remain unchanged; original constructions use saved primitive/mesh geometry and shared Standard materials. Existing imported package licence/provenance files remain authoritative. Final runtime review is deferred to Phase 2 closure.\n\n| ID | Object | Cells | Price | Source / construction |\n|---|---|---|---|---|\n");
            foreach(var definition in FarmDecorationCatalog.Items)
            {
                var path=Folder+"/"+definition.Id+".prefab";
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(prefab==null)
                {
                    var go=new GameObject(definition.Name);
                    try
                    {
                        var content=new GameObject("Metric Art");content.transform.SetParent(go.transform,false);
                        CreateDecor(content.transform,definition.Id);
                        // All art is authored/fitted once; runtime must preserve its metric proportions.
                        VisualPrefab.FitExisting(content,new Vector3(definition.Width-.12f,definition.Height,definition.Depth-.12f));
                        go.AddComponent<DecorationArtMetrics>();
                        prefab=PrefabUtility.SaveAsPrefabAsset(go,path);
                    }
                    finally{UnityEngine.Object.DestroyImmediate(go);}
                }
                assets.Add(prefab);
                manifest.AppendLine("| "+definition.Id+" | "+definition.Name+" | "+definition.Width+"×"+definition.Depth+" | "+definition.Price+" | "+SourceFor(definition.Id)+" |");
            }
            var config=new SerializedObject(build);var visuals=config.FindProperty("visualPrefabs");
            for(var i=0;i<assets.Count;i++)
            {
                var id=FarmDecorationCatalog.Items[i].Id;var slot=-1;
                for(var j=0;j<visuals.arraySize;j++)if(visuals.GetArrayElementAtIndex(j).FindPropertyRelative("definitionId").stringValue==id){slot=j;break;}
                if(slot<0){slot=visuals.arraySize;visuals.arraySize++;}
                var entry=visuals.GetArrayElementAtIndex(slot);entry.FindPropertyRelative("definitionId").stringValue=id;entry.FindPropertyRelative("prefab").objectReferenceValue=assets[i];
            }
            var houses=config.FindProperty("houseLevelPrefabs");houses.arraySize=3;
            for(var level=0;level<3;level++)houses.GetArrayElementAtIndex(level).objectReferenceValue=EnsureHouse(level);
            var canvas=root.GetComponentInChildren<Canvas>(true);var input=root.GetComponent<InputReader>();
            if(canvas==null||input==null)throw new MissingReferenceException("Baked Canvas/InputReader is missing.");
            var ui=canvas.GetComponentInChildren<BuildCustomizationUI>(true);
            if(ui==null)ui=BuildCustomizationUI.Create(canvas.transform,build,input,Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            config.FindProperty("customizationUI").objectReferenceValue=ui;config.ApplyModifiedPropertiesWithoutUndo();
            if(UnityEngine.Object.FindAnyObjectByType<EventSystem>()==null)
            {var events=new GameObject("Farm UI Event System",typeof(EventSystem),typeof(InputSystemUIInputModule));events.transform.SetParent(root.transform,false);events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();}
            ApplyPondWater();
            Directory.CreateDirectory("Docs/Art");File.WriteAllText("Docs/Art/P2DecorationManifest.md",manifest.ToString(),new UTF8Encoding(false));
            EditorUtility.SetDirty(build);EditorUtility.SetDirty(ui);EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            Debug.Log("P2-4 saved: 30 purchasable decorations, 3 house tiers, editable signs, animated pond and baked catalogue UI. No gameplay initialized.");
        }

        private static string SourceFor(string id)
        {
            if(id=="fence_picket")return Ranch+"Env_WoodFence_01.prefab";
            if(id=="fence_rails")return Farm+"Fence_01.prefab";
            if(id=="fence_cross")return "Original timber cross rails";
            if(id=="fence_corner")return "Two LowPolyFarmLite Fence_01 segments";
            if(id.StartsWith("lantern_"))return "World/Lighting/FarmLantern.prefab, composed/scaled variants (real lights)";
            if(id.StartsWith("flower_"))return "Existing Art/Buildings/flowerbed.prefab + original containers";
            if(id.StartsWith("rock_"))return Farm+"Rock_04.prefab, composed/scaled clusters";
            if(id=="well_stone")return "Original stone ring, bucket and crossbar";
            if(id=="well_roof")return Ranch+"Env_Well_01.prefab + original timber frame and gabled roof";
            if(id=="barrel_wood")return Ranch+"Prop_Berrel_03.prefab";
            if(id=="crate_stack")return Farm+"Box_01.prefab × 3";
            if(id=="hay_bale")return Ranch+"Prop_Haystack_01.prefab";
            if(id=="wheelbarrow")return Ranch+"Prop_Wheelbarrow.prefab";
            if(id=="watering_can")return Farm+"WateringCan_01.prefab";
            return "Original authored geometry; shared Standard materials / flag shader / plain TextMesh signs";
        }

        private static void CreateDecor(Transform parent,string id)
        {
            var wood=Mat("Timber",Timber);var pale=Mat("Pale Wood",Cream);var stone=Mat("Stone",new Color(.46f,.48f,.42f));var iron=Mat("Iron",Iron);
            if(id=="fence_picket"){Import(parent,Ranch+"Env_WoodFence_01.prefab",new Vector3(1,1.15f,.3f));return;}
            if(id=="fence_rails"){Import(parent,Farm+"Fence_01.prefab",new Vector3(1,1.1f,.3f));return;}
            if(id=="fence_cross")
            {foreach(var x in new[]{-.43f,.43f})Part(parent,"Fence post",new Vector3(x,.5f,0),new Vector3(.1f,1,.14f),wood);foreach(var angle in new[]{-35f,35f}){var rail=Part(parent,"Cross rail",new Vector3(0,.5f,0),new Vector3(1,.08f,.08f),wood);rail.transform.localRotation=Quaternion.Euler(0,0,angle);}return;}
            if(id=="fence_corner")
            {var a=Import(parent,Farm+"Fence_01.prefab",new Vector3(.85f,1.1f,.2f));a.transform.localPosition=new Vector3(0,0,.35f);var b=Import(parent,Farm+"Fence_01.prefab",new Vector3(.85f,1.1f,.2f));b.transform.localRotation=Quaternion.Euler(0,90,0);b.transform.localPosition=new Vector3(-.35f,0,0);return;}
            if(id=="gate_rustic")
            {foreach(var x in new[]{-.85f,.85f})Part(parent,"Gate post",new Vector3(x,.57f,0),new Vector3(.13f,1.14f,.16f),wood);for(var i=0;i<7;i++)Part(parent,"Gate slat",new Vector3(-.65f+i*.22f,.57f,0),new Vector3(.13f,.8f,.08f),pale);Part(parent,"Latch",new Vector3(.55f,.65f,-.09f),new Vector3(.2f,.06f,.06f),iron);return;}
            if(id.StartsWith("lantern_"))
            {var size=id=="lantern_low"?1.3f:2.2f;var source="Assets/_Game/Content/World/Lighting/FarmLantern.prefab";var first=Import(parent,source,new Vector3(.85f,size,.6f));if(id=="lantern_double"){first.transform.localPosition=new Vector3(-.48f,0,0);var second=Import(parent,source,new Vector3(.85f,size,.6f));second.transform.localPosition=new Vector3(.48f,0,0);second.transform.localRotation=Quaternion.Euler(0,180,0);}return;}
            if(id.StartsWith("flower_"))
            {
                var longBed=id=="flower_long";var barrel=id=="flower_barrel";var raised=id=="flower_raised";
                var width=longBed?1.75f:.8f;var height=barrel?.55f:raised?.4f:.18f;
                Part(parent,"Planter",new Vector3(0,height*.5f,0),new Vector3(width,height,.75f),barrel?wood:pale,barrel||id=="flower_round"?PrimitiveType.Cylinder:PrimitiveType.Cube);
                if(barrel)Part(parent,"Iron hoop",new Vector3(0,height*.65f,0),new Vector3(.82f,.035f,.77f),iron,PrimitiveType.Cylinder);
                for(var i=0;i<(longBed?4:2);i++){var plant=Import(parent,"Assets/_Game/Content/Art/Buildings/flowerbed.prefab",new Vector3(.38f,.38f,.58f));plant.transform.localPosition=new Vector3((i-(longBed?1.5f:.5f))*.36f,height,0);}
                return;
            }
            if(id=="bench_wood"||id=="bench_stone")
            {
                var material=id=="bench_stone"?stone:wood;
                foreach(var x in new[]{-.65f,.65f})Part(parent,"Leg",new Vector3(x,.23f,0),new Vector3(.18f,.46f,.6f),material);
                for(var i=0;i<3;i++)Part(parent,"Seat slat",new Vector3(0,.48f,-.21f+i*.21f),new Vector3(1.75f,.09f,.17f),material);
                if(id=="bench_wood"){foreach(var x in new[]{-.65f,.65f})Part(parent,"Back support",new Vector3(x,.72f,.28f),new Vector3(.08f,.7f,.08f),wood);for(var i=0;i<2;i++)Part(parent,"Back slat",new Vector3(0,.75f+i*.2f,.29f),new Vector3(1.75f,.13f,.08f),pale);}return;
            }
            if(id.StartsWith("rock_"))
            {var count=id=="rock_group"?3:1;for(var i=0;i<count;i++){var rock=Import(parent,Farm+"Rock_04.prefab",id=="rock_large"?new Vector3(1.8f,1.1f,1.8f):new Vector3(.75f,.7f,.75f));rock.transform.localRotation=Quaternion.Euler(0,i*105,0);rock.transform.localPosition=new Vector3((i-(count-1)*.5f)*.55f,0,i%2*.12f);}return;}
            if(id=="well_roof")
            {Import(parent,Ranch+"Env_Well_01.prefab",new Vector3(1.5f,.85f,1.5f));foreach(var x in new[]{-.62f,.62f})Part(parent,"Roof support",new Vector3(x,1.12f,0),new Vector3(.12f,2.24f,.12f),wood);Roof(parent,"Well roof",new Vector3(0,2.05f,0),new Vector3(1.8f,.4f,1.7f),Mat("Rust Roof",new Color(.51f,.25f,.16f)));return;}
            if(id=="well_stone")
            {for(var i=0;i<12;i++){var a=i*Mathf.PI/6;var block=Part(parent,"Well stone",new Vector3(Mathf.Cos(a)*.63f,.38f,Mathf.Sin(a)*.63f),new Vector3(.34f,.76f,.32f),stone);block.transform.localRotation=Quaternion.Euler(0,-i*30,0);}Part(parent,"Well water",new Vector3(0,.15f,0),new Vector3(1.05f,.06f,1.05f),Mat("Still Water",new Color(.12f,.36f,.39f)),PrimitiveType.Cylinder);foreach(var x in new[]{-.72f,.72f})Part(parent,"Well upright",new Vector3(x,.64f,0),new Vector3(.1f,1.28f,.12f),wood);Part(parent,"Crossbar",new Vector3(0,1.18f,0),new Vector3(1.6f,.12f,.12f),wood);Part(parent,"Rope",new Vector3(0,.87f,0),new Vector3(.025f,.55f,.025f),pale);return;}
            if(id=="weather_vane")
            {
                Part(parent,"Mast",new Vector3(0,1.05f,0),new Vector3(.07f,2.1f,.07f),iron);
                var arrow=new GameObject("Turning arrow");arrow.transform.SetParent(parent,false);arrow.transform.localPosition=Vector3.up*2.12f;
                Part(arrow.transform,"Shaft",Vector3.zero,new Vector3(.83f,.04f,.04f),iron);var tip=Part(arrow.transform,"Arrow head",new Vector3(.33f,0,0),new Vector3(.17f,.17f,.04f),Mat("Copper",new Color(.76f,.43f,.2f)));tip.transform.localRotation=Quaternion.Euler(0,0,45);
                Part(arrow.transform,"Tail",new Vector3(-.3f,.04f,0),new Vector3(.16f,.25f,.04f),pale);
                var vane=parent.gameObject.AddComponent<WindVaneMotion>();var so=new SerializedObject(vane);so.FindProperty("arrow").objectReferenceValue=arrow.transform;so.ApplyModifiedPropertiesWithoutUndo();return;
            }
            if(id.StartsWith("flag_"))
            {
                Part(parent,"Flag pole",new Vector3(-.35f,1.1f,0),new Vector3(.06f,2.2f,.06f),wood);
                Part(parent,"Finial",new Vector3(-.35f,2.23f,0),Vector3.one*.12f,Mat("Brass",new Color(.77f,.6f,.22f)),PrimitiveType.Sphere);
                var flag=new GameObject("Wind cloth",typeof(MeshFilter),typeof(MeshRenderer));flag.transform.SetParent(parent,false);flag.transform.localPosition=new Vector3(-.32f,2.13f,0);
                flag.GetComponent<MeshFilter>().sharedMesh=FlagMesh(id=="flag_blue");
                flag.GetComponent<MeshRenderer>().sharedMaterial=Mat(id,id=="flag_blue"?new Color(.15f,.39f,.65f):new Color(.25f,.52f,.22f),"MyLittleFarm/Decor/Flag");return;
            }
            if(FarmDecorationCatalog.SupportsText(id))
            {
                var wide=id=="sign_wide";var width=wide?1.7f:.82f;
                foreach(var x in wide?new[]{-.64f,.64f}:new[]{0f})Part(parent,"Sign post",new Vector3(x,.72f,0),new Vector3(.09f,1.44f,.1f),wood);
                Part(parent,"Sign board",new Vector3(0,1.1f,0),new Vector3(width,.57f,.11f),pale);
                var front=WorldText(parent,new Vector3(0,1.1f,-.062f),Quaternion.identity,width);
                var back=WorldText(parent,new Vector3(0,1.1f,.062f),Quaternion.Euler(0,180,0),width);
                var sign=parent.gameObject.AddComponent<FarmSign>();var so=new SerializedObject(sign);so.FindProperty("front").objectReferenceValue=front;so.FindProperty("back").objectReferenceValue=back;so.FindProperty("boardWidth").floatValue=width;
                so.FindProperty("textMaterial").objectReferenceValue=Mat("Sign Text",Color.white,"MyLittleFarm/Decor/Sign Text");so.ApplyModifiedPropertiesWithoutUndo();sign.SetText("Моя ферма");return;
            }
            if(id=="barrel_wood"){Import(parent,Ranch+"Prop_Berrel_03.prefab",new Vector3(.8f,.85f,.8f));return;}
            if(id=="crate_stack")
            {for(var i=0;i<3;i++){var box=Import(parent,Farm+"Box_01.prefab",Vector3.one*.45f);box.transform.localPosition=i==2?new Vector3(0,.44f,0):new Vector3(i==0?-.23f:.23f,0,0);}return;}
            if(id=="hay_bale"){Import(parent,Ranch+"Prop_Haystack_01.prefab",new Vector3(.85f,.65f,.85f));return;}
            if(id=="wheelbarrow"){Import(parent,Ranch+"Prop_Wheelbarrow.prefab",new Vector3(1.8f,.9f,.85f));return;}
            if(id=="watering_can"){Import(parent,Farm+"WateringCan_01.prefab",new Vector3(.75f,.45f,.7f));return;}
            if(id=="birdhouse")
            {Part(parent,"Post",new Vector3(0,.76f,0),new Vector3(.09f,1.52f,.09f),wood);Part(parent,"Bird box",new Vector3(0,1.6f,0),new Vector3(.48f,.5f,.43f),pale);Roof(parent,"Bird roof",new Vector3(0,1.85f,0),new Vector3(.63f,.24f,.6f),Mat("Rust Roof",new Color(.51f,.25f,.16f)));var hole=Part(parent,"Entry hole",new Vector3(0,1.65f,-.22f),new Vector3(.16f,.012f,.16f),iron,PrimitiveType.Cylinder);hole.transform.localRotation=Quaternion.Euler(90,0,0);Part(parent,"Perch",new Vector3(0,1.46f,-.28f),new Vector3(.04f,.04f,.18f),wood);return;}
            throw new InvalidOperationException("No decoration art recipe: "+id);
        }

        private static GameObject EnsureHouse(int level)
        {
            var path=Folder+"/HouseLevel"+level+".prefab";var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(existing!=null)return existing;
            var go=new GameObject(FarmDecorationCatalog.HouseLevelName(level));
            try
            {
                var wood=Mat("Timber",Timber);var roof=Mat(level==0?"Old Roof":"Tile Roof",level==0?new Color(.32f,.22f,.16f):new Color(.57f,.26f,.18f));
                var wall=Mat(level==0?"Aged Boards":level==1?"Cottage Walls":"Manor Walls",level==0?new Color(.48f,.35f,.21f):level==1?new Color(.85f,.78f,.58f):new Color(.89f,.86f,.72f));
                var w=level==0?3.1f:4.25f;var d=level==0?2.65f:3.7f;var h=level==0?2.05f:2.45f;
                Part(go.transform,"Stone foundation",new Vector3(0,.13f,0),new Vector3(w+.12f,.26f,d+.12f),Mat("Stone",new Color(.46f,.48f,.42f)));
                Part(go.transform,"Main walls",new Vector3(0,h*.5f+.14f,0),new Vector3(w,h,d),wall);
                Roof(go.transform,"Gabled roof",new Vector3(0,h+.14f,0),new Vector3(w+.45f,level==0?.8f:1.05f,d+.5f),roof);
                var front=-d*.5f-.03f;const float door=1.584f;
                Part(go.transform,"Door",new Vector3(0,.14f+door*.5f,front-.025f),new Vector3(.78f,door,.08f),wood);
                Part(go.transform,"Door lintel",new Vector3(0,.14f+door+.05f,front-.07f),new Vector3(.96f,.1f,.13f),Mat("Pale Wood",Cream));
                foreach(var x in new[]{-.45f,.45f})Part(go.transform,"Door frame",new Vector3(x,.14f+door*.5f,front-.07f),new Vector3(.08f,door,.13f),Mat("Pale Wood",Cream));
                Part(go.transform,"Door handle",new Vector3(.27f,.94f,front-.12f),Vector3.one*.07f,Mat("Brass",new Color(.77f,.6f,.22f)),PrimitiveType.Sphere);
                foreach(var x in new[]{-w*.32f,w*.32f})Window(go.transform,new Vector3(x,1.3f,front-.04f));
                if(level==0)
                {
                    for(var i=0;i<8;i++)Part(go.transform,"Weathered facade board",new Vector3(-w*.5f+.19f+i*.39f,1.1f,front+.015f),new Vector3(.02f,1.9f,.045f),wood);
                    var patch=Part(go.transform,"Roof patch",new Vector3(-.48f,h+.48f,-.7f),new Vector3(.7f,.08f,.7f),wood);patch.transform.localRotation=Quaternion.Euler(0,0,27);
                }
                else
                {
                    Part(go.transform,"Chimney",new Vector3(-1.1f,h+.75f,.7f),new Vector3(.38f,1.4f,.42f),Mat("Chimney Brick",new Color(.52f,.36f,.28f)));
                    Part(go.transform,"Front step",new Vector3(0,.1f,front-.37f),new Vector3(1.4f,.2f,.6f),Mat("Stone",new Color(.46f,.48f,.42f)));
                    foreach(var x in new[]{-1.35f,1.35f}){var plant=Import(go.transform,"Assets/_Game/Content/Art/Buildings/flowerbed.prefab",new Vector3(.65f,.5f,.6f));plant.transform.localPosition=new Vector3(x,0,front-.32f);}
                }
                if(level==2)
                {
                    foreach(var x in new[]{-2.6f,2.6f})
                    {Part(go.transform,"Manor wing",new Vector3(x,1.18f,.25f),new Vector3(1.3f,2.36f,3.1f),wall);Roof(go.transform,"Wing roof",new Vector3(x,2.36f,.25f),new Vector3(1.55f,.65f,3.45f),roof);Window(go.transform,new Vector3(x,1.3f,-1.33f));}
                    Part(go.transform,"Upper floor",new Vector3(0,3.05f,.2f),new Vector3(2.5f,1.2f,2.5f),wall);Roof(go.transform,"Upper gable",new Vector3(0,3.65f,.2f),new Vector3(2.8f,.8f,2.85f),roof);Window(go.transform,new Vector3(0,3.05f,-1.075f));
                    Part(go.transform,"Porch deck",new Vector3(0,.12f,-2.25f),new Vector3(2.6f,.24f,1.25f),wood);
                    foreach(var x in new[]{-1.12f,1.12f})Part(go.transform,"Porch column",new Vector3(x,1.17f,-2.57f),new Vector3(.12f,2.1f,.12f),Mat("Pale Wood",Cream));
                    Roof(go.transform,"Porch canopy",new Vector3(0,2.22f,-2.15f),new Vector3(2.85f,.38f,1.35f),roof);
                }
                go.AddComponent<BuildingArtMetrics>().ConfigureDoor(new Vector3(0,.14f,front),new Vector3(0,.14f+door,front));
                return PrefabUtility.SaveAsPrefabAsset(go,path);
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }

        private static void Window(Transform parent,Vector3 position)
        {
            Part(parent,"Window frame",position,new Vector3(.76f,.85f,.12f),Mat("Timber",Timber));
            Part(parent,"Window glass",position+Vector3.back*.07f,new Vector3(.59f,.66f,.025f),Mat("Window Glass",new Color(.28f,.48f,.50f)));
            Part(parent,"Window mullion",position+Vector3.back*.09f,new Vector3(.045f,.69f,.03f),Mat("Pale Wood",Cream));
        }
        private static TextMesh WorldText(Transform parent,Vector3 position,Quaternion rotation,float width)
        {
            var go=new GameObject("Plain sign text",typeof(TextMesh));go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=rotation;
            var text=go.GetComponent<TextMesh>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=48;text.characterSize=.055f;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.richText=false;text.color=new Color(.16f,.12f,.07f);
            go.GetComponent<MeshRenderer>().sharedMaterial=text.font.material;
            // Wide and narrow signs share the same safe text capacity, with an authored text frame.
            return text;
        }
        private static GameObject Import(Transform parent,string path,Vector3 maximumSize)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(source==null)throw new MissingReferenceException(path);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(source,parent);VisualPrefab.FitExisting(go,maximumSize);return go;
        }
        private static GameObject Part(Transform parent,string name,Vector3 position,Vector3 size,Material material,PrimitiveType type=PrimitiveType.Cube)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;
            go.transform.localScale=type==PrimitiveType.Cylinder?new Vector3(size.x,size.y*.5f,size.z):size;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=material;return go;
        }
        private static Material Mat(string name,Color color,string shader="Standard")
        {
            var path=Folder+"/Materials/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material!=null)return material;
            material=new Material(Shader.Find(shader)){name=name,enableInstancing=true};if(material.HasProperty("_Color"))material.color=color;if(material.HasProperty("_Glossiness"))material.SetFloat("_Glossiness",.2f);AssetDatabase.CreateAsset(material,path);return material;
        }
        private static void Roof(Transform parent,string name,Vector3 position,Vector3 size,Material material)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;
            var path=Folder+"/Meshes/UnitGable.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null)
            {
                var p=new[]{new Vector3(-.5f,0,-.5f),new Vector3(.5f,0,-.5f),new Vector3(0,1,-.5f),new Vector3(-.5f,0,.5f),new Vector3(.5f,0,.5f),new Vector3(0,1,.5f)};
                var indices=new[]{0,2,1,3,4,5,0,3,5,0,5,2,1,2,5,1,5,4,0,1,4,0,4,3};var vertices=new Vector3[indices.Length];var triangles=new int[indices.Length];
                for(var i=0;i<indices.Length;i++){vertices[i]=p[indices[i]];triangles[i]=i;}mesh=new Mesh{name="Authored Gable"};mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);
            }
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;
        }
        private static Mesh FlagMesh(bool pennant)
        {
            var path=Folder+"/Meshes/"+(pennant?"Pennant":"Flag")+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh!=null)return mesh;
            const int columns=12,rows=4;var vertices=new Vector3[(columns+1)*(rows+1)];var uv=new Vector2[vertices.Length];var triangles=new List<int>();
            for(var y=0;y<=rows;y++)for(var x=0;x<=columns;x++)
            {var u=(float)x/columns;var v=(float)y/rows;var i=y*(columns+1)+x;vertices[i]=new Vector3(u*.72f*(pennant?1-v*.8f:1),-v*.48f,0);uv[i]=new Vector2(u,v);if(x<columns&&y<rows){triangles.AddRange(new[]{i,i+columns+1,i+1,i+1,i+columns+1,i+columns+2});}}
            mesh=new Mesh{name=pennant?"Authored Pennant":"Authored Flag"};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();var bounds=mesh.bounds;bounds.Expand(.14f);mesh.bounds=bounds;AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        private static void ApplyPondWater()
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Content/World/Landscape/PondWater.mat");
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Game/Content/World/Landscape/PondWater.mesh.asset");
            if(material==null||mesh==null)throw new MissingReferenceException("Authored pond is missing.");
            material.shader=Shader.Find("MyLittleFarm/World/Animated Pond");material.SetFloat("_PhaseOffset",0);material.SetFloat("_RippleStrength",.025f);EditorUtility.SetDirty(material);
            var tangents=new Vector4[mesh.vertexCount];var uv=new Vector2[mesh.vertexCount];var vertices=mesh.vertices;
            for(var i=0;i<tangents.Length;i++){tangents[i]=new Vector4(1,0,0,-1);uv[i]=new Vector2(vertices[i].x,vertices[i].z)*.25f;}
            mesh.tangents=tangents;mesh.uv=uv;EditorUtility.SetDirty(mesh);
        }
        private static void EnsureFolder(string path)
        {if(AssetDatabase.IsValidFolder(path))return;var split=path.LastIndexOf('/');EnsureFolder(path.Substring(0,split));AssetDatabase.CreateFolder(path.Substring(0,split),path.Substring(split+1));}
    }
}
