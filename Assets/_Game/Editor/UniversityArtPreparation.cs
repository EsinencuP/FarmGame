using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Building;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MyLittleFarm.Editor
{
    /// <summary>Explicit, asset-only U1-0 preparation. Never configures gameplay or saves an open scene.</summary>
    public static class UniversityArtPreparation
    {
        public const string Root = "Assets/_Game/Content/Art/UniversityDemo";
        private const string Animals = "Assets/moreAssets/Farm Animals by @Quaternius/";
        private const string Buildings = "Assets/moreAssets/Farm Buildings - Sept 2018/FBX/";
        private static readonly List<GameObject> Prepared = new List<GameObject>();
        private static Scene _authoringScene;

        [MenuItem("Tools/My Little Farm/University/Prepare U1-0 Art")]
        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
            Folder(Root); Folder(Root + "/Materials"); Folder(Root + "/Meshes"); Folder(Root + "/Animations");
            Prepared.Clear();
            _authoringScene = EditorSceneManager.NewPreviewScene();
            try
            {
            Cow();
            Wrapped("barn", Buildings + "SmallBarn.fbx", 4.2f);
            Wrapped("mill", Buildings + "TowerWindmill.fbx", 5.8f);
            CheeseWorkshop();
            Wrapped("pear_fruit", "Assets/LowPolyFoodLite/Prefabs/Pear.prefab", .26f);
            Wrapped("pumpkin_fruit", "Assets/LowPolyFoodLite/Prefabs/Pumpkin.prefab", .36f);
            Wrapped("strawberry_fruit", Animals + "Models/FBX format/strawberry.fbx", .15f);
            Wrapped("cheese_product", Animals + "Models/FBX format/cheese.fbx", .22f);
            PearTree();
            foreach (var id in new[] { "strawberry", "pumpkin" })
                for (var stage = 0; stage < 3; stage++) Plant(id, stage);
            for (var level = 0; level < 3; level++) Hoe(level);
            Flour();
            Feeder();
            AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.ClosePreviewScene(_authoringScene); }
            CaptureAll();
            AssetDatabase.Refresh();
            Debug.Log("U1-0: prepared " + Prepared.Count + " visual prefabs. No gameplay catalog or scene changed.");
        }

        private static GameObject CreateRoot(string name)
        {
            var root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, _authoringScene);
            return root;
        }

        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            Folder(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }

        private static Material Material(string name, Color color)
        {
            var path = Root + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Standard")) { name = name, color = color, enableInstancing = true };
            material.SetFloat("_Glossiness", .12f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void Save(GameObject root, bool overwrite = false)
        {
            try
            {
                var path = Root + "/" + root.name + ".prefab";
                // Repeated preparation preserves authored edits and GUIDs.
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || overwrite) prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                Prepared.Add(prefab);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static GameObject Source(Transform parent, string path, string name = "Model")
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) throw new MissingReferenceException(path);
            var model = Object.Instantiate(asset, parent, false); model.name = name;
            foreach (var collider in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            return model;
        }

        // Static mesh bounds use renderer geometry. Skinned meshes use a baked pose, not animation envelopes.
        private static Bounds Geometry(GameObject root)
        {
            var found = false; var result = new Bounds();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                foreach (var v in filter.sharedMesh.vertices)
                { var p = root.transform.InverseTransformPoint(filter.transform.TransformPoint(v)); if (!found) { result = new Bounds(p, Vector3.zero); found = true; } else result.Encapsulate(p); }
            }
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = new Mesh();
                try
                {
                    skin.BakeMesh(mesh, true);
                    foreach (var v in mesh.vertices)
                    { var p = root.transform.InverseTransformPoint(skin.transform.TransformPoint(v)); if (!found) { result = new Bounds(p, Vector3.zero); found = true; } else result.Encapsulate(p); }
                }
                finally { Object.DestroyImmediate(mesh); }
            }
            if (!found) throw new InvalidOperationException("No geometry: " + root.name);
            return result;
        }

        private static void Fit(GameObject root, GameObject model, float height)
        {
            var b = Geometry(root);
            model.transform.localScale *= height / b.size.y;
            b = Geometry(root);
            model.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
        }

        private static void Wrapped(string id, string path, float height, bool overwrite = false)
        {
            var root = CreateRoot(id);
            var model = Source(root.transform, path);
            Fit(root, model, height);
            Save(root, overwrite);
        }

        private static void Cow()
        {
            var root = CreateRoot("cow");
            var path = Animals + "FBX/Cow.fbx";
            var scaleRoot = new GameObject("Visual scale"); scaleRoot.transform.SetParent(root.transform, false);
            var model = Source(scaleRoot.transform, path);
            var imported = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().ToArray();
            var idle = imported.First(c => c.name == "Armature|Idle");
            idle.SampleAnimation(model, 0);
            // Imported root curves must not overwrite the metric normalization.
            Fit(root, scaleRoot, 1.35f);
            var controllerPath = Root + "/Animations/Cow.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
                controller.AddParameter("Eating", AnimatorControllerParameterType.Bool);
                var machine = controller.layers[0].stateMachine;
                var states = new List<AnimatorState>();
                foreach (var name in new[] { "Idle", "Walk", "Eat" })
                {
                    var clip = Object.Instantiate(name == "Walk" ? imported.First(c => c.name == "Armature|Walk") : idle);
                    clip.name = "Cow_" + name;
                    var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = true;
                    AnimationUtility.SetAnimationClipSettings(clip, settings);
                    if (name == "Eat")
                    {
                        var head = model.GetComponentsInChildren<Transform>(true).First(t => t.name == "Head");
                        var headPath = AnimationUtility.CalculateTransformPath(head, model.transform);
                        foreach (var binding in AnimationUtility.GetCurveBindings(clip).Where(b => b.path == headPath && b.propertyName.StartsWith("m_LocalRotation")))
                            AnimationUtility.SetEditorCurve(clip, binding, null);
                        var angles = head.localEulerAngles;
                        foreach (var axis in new[] { "x", "y", "z" })
                        {
                            var baseAngle = axis == "x" ? angles.x : axis == "y" ? angles.y : angles.z;
                            var tilt = axis == "x" ? -28f : 0f;
                            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(headPath, typeof(Transform), "localEulerAnglesRaw." + axis),
                                new AnimationCurve(new Keyframe(0, baseAngle + tilt), new Keyframe(clip.length * .5f, baseAngle + tilt - (axis == "x" ? 6 : 0)), new Keyframe(clip.length, baseAngle + tilt)));
                        }
                    }
                    AssetDatabase.CreateAsset(clip, Root + "/Animations/" + clip.name + ".anim");
                    var state = machine.AddState(name); state.motion = clip; states.Add(state);
                }
                machine.defaultState = states[0];
                Transition(states[0], states[1], AnimatorConditionMode.Greater, .1f, "Speed");
                Transition(states[1], states[0], AnimatorConditionMode.Less, .1f, "Speed");
                foreach (var state in states.Take(2)) Transition(state, states[2], AnimatorConditionMode.If, 0, "Eating");
                Transition(states[2], states[0], AnimatorConditionMode.IfNot, 0, "Eating");
            }
            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
            Save(root);
        }

        private static void Transition(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, float threshold, string parameter)
        { var t = from.AddTransition(to); t.hasExitTime = false; t.duration = .16f; t.AddCondition(mode, threshold, parameter); }

        private static void PearTree()
        {
            var root = CreateRoot("pear_tree");
            var model = Source(root.transform, "Assets/LowPolyFarmLite/Prefabs/Tree_04.prefab");
            Fit(root, model, 2.6f);
            // Fruit is a separate reusable prefab, as required by TreeView's Ready Fruit group.
            Save(root);
        }

        private static void Part(Transform parent, string name, Vector3 position, Vector3 size, Material material, PrimitiveType shape = PrimitiveType.Cube)
        {
            var go = GameObject.CreatePrimitive(shape); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = size;
            Object.DestroyImmediate(go.GetComponent<Collider>()); go.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static Material Timber { get { return Material("Timber", new Color(.35f, .22f, .13f)); } }
        private static Material Iron { get { return Material("Iron", new Color(.27f, .31f, .33f)); } }

        private static void CheeseWorkshop()
        {
            var root = CreateRoot("cheese_dairy");
            var model = Source(root.transform, Buildings + "SmallBarn.fbx");
            Fit(root, model, 3.2f);
            var cream = Material("Dairy plaster", new Color(.82f, .76f, .58f));
            foreach (var r in model.GetComponentsInChildren<Renderer>())
                r.sharedMaterials = r.sharedMaterials.Select(m => m != null && m.name.Contains("Red") ? cream : m).ToArray();
            var b = Geometry(root);
            Part(root.transform, "Work table", new Vector3(0, .76f, b.max.z + .38f), new Vector3(1.6f, .12f, .65f), Timber);
            foreach (var x in new[] { -.65f, .65f }) Part(root.transform, "Table leg", new Vector3(x, .36f, b.max.z + .38f), new Vector3(.09f, .72f, .09f), Timber);
            Part(root.transform, "Milk churn", new Vector3(-.42f, 1.03f, b.max.z + .38f), new Vector3(.31f, .22f, .31f), Iron, PrimitiveType.Cylinder);
            Part(root.transform, "Cheese wheel", new Vector3(.32f, .9f, b.max.z + .38f), new Vector3(.44f, .08f, .44f), Material("Cheese", new Color(.94f, .67f, .18f)), PrimitiveType.Cylinder);
            Save(root);
        }

        private static void Plant(string id, int stage)
        {
            var root = CreateRoot(id + "_" + new[] { "Sprout", "Growing", "Mature" }[stage]);
            var mesh = new CropStageArtBuilder.MeshDraft();
            var green = new Color(.27f, .55f, .16f);
            var spread = stage == 0 ? .13f : stage == 1 ? .27f : .34f;
            var count = stage == 0 ? 3 : id == "pumpkin" ? 6 : 9;
            for (var i = 0; i < count; i++)
            {
                var a = i * Mathf.PI * 2 / count;
                var tip = new Vector3(Mathf.Cos(a) * spread, stage == 0 ? .12f : .17f + i % 3 * .045f, Mathf.Sin(a) * spread);
                mesh.Rod(Vector3.up * .015f, tip * .75f, .012f, .006f, green);
                mesh.Leaf(tip * .5f, tip, stage == 0 ? .08f : id == "pumpkin" ? .22f : .16f, green, .035f);
            }
            if (id == "strawberry" && stage > 0)
                for (var i = 0; i < 4; i++) mesh.Fruit(new Vector3((i % 2 * 2 - 1) * .12f, .14f, (i / 2 * 2 - 1) * .12f), new Vector3(.04f, .012f, .04f), new Color(.95f, .89f, .71f));
            var asset = mesh.ToMesh(root.name);
            var meshPath = Root + "/Meshes/" + root.name + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (saved == null) { AssetDatabase.CreateAsset(asset, meshPath); saved = asset; } else Object.DestroyImmediate(asset);
            var leaves = new GameObject("Leaves"); leaves.transform.SetParent(root.transform, false);
            leaves.AddComponent<MeshFilter>().sharedMesh = saved;
            leaves.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(CropStageArtBuilder.Root + "/Crop Foliage.mat");
            if (stage == 2)
            {
                var source = id == "pumpkin" ? "Assets/LowPolyFoodLite/Prefabs/Pumpkin.prefab" : Animals + "Models/FBX format/strawberry.fbx";
                for (var i = 0; i < (id == "pumpkin" ? 1 : 4); i++)
                {
                    var fruit = Source(root.transform, source, "Fruit " + i);
                    var holder = new GameObject("Fit fruit"); fruit.transform.SetParent(holder.transform, false);
                    Fit(holder, fruit, id == "pumpkin" ? .32f : .12f);
                    fruit.transform.SetParent(root.transform, false); Object.DestroyImmediate(holder);
                    fruit.transform.localPosition += id == "pumpkin" ? Vector3.zero : new Vector3((i % 2 * 2 - 1) * .14f, .015f, (i / 2 * 2 - 1) * .14f);
                }
            }
            Save(root);
        }

        private static void Hoe(int level)
        {
            var root = CreateRoot("tool_hoe_" + level);
            var model = Source(root.transform, "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Prop_BigFarmingTool_06.prefab");
            Fit(root, model, 1.45f);
            // Grip pivot is .35 m above the lower end; the hand socket owns final orientation.
            model.transform.localPosition -= Vector3.up * .35f;
            var metal = level == 2 ? Material("Brass", new Color(.70f, .47f, .19f)) : Iron;
            for (var i = 0; i < level; i++)
                Part(root.transform, "Upgrade band " + i, new Vector3(0, .43f + i * .10f, 0), new Vector3(.065f, .05f, .065f), metal);
            Save(root);
        }

        private static void Flour()
        {
            var root = CreateRoot("flour_product");
            Part(root.transform, "Sack", new Vector3(0, .16f, 0), new Vector3(.25f, .16f, .25f), Material("Flour sack", new Color(.76f, .62f, .40f)), PrimitiveType.Cylinder);
            Part(root.transform, "Flour", new Vector3(0, .325f, 0), new Vector3(.21f, .015f, .21f), Material("Flour", new Color(.94f, .89f, .74f)), PrimitiveType.Cylinder);
            Save(root);
        }

        private static void Feeder()
        {
            var root = CreateRoot("feeder");
            Part(root.transform, "Base", new Vector3(0, .17f, 0), new Vector3(.8f, .12f, .48f), Timber);
            foreach (var x in new[] { -.38f, .38f }) Part(root.transform, "End", new Vector3(x, .28f, 0), new Vector3(.06f, .28f, .48f), Timber);
            foreach (var z in new[] { -.23f, .23f }) Part(root.transform, "Side", new Vector3(0, .28f, z), new Vector3(.8f, .28f, .05f), Timber);
            Part(root.transform, "Feed", new Vector3(0, .27f, 0), new Vector3(.68f, .03f, .36f), Material("Feed", new Color(.76f, .60f, .25f)));
            foreach (Transform child in root.transform) child.localPosition -= Vector3.up * .11f;
            Save(root);
        }

        public static void CaptureAll(string cowState = "Idle")
        {
            var output = Path.GetFullPath("Verification/U1-0/Previews"); Directory.CreateDirectory(output);
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var cameraGo = new GameObject("Art preview camera"); SceneManager.MoveGameObjectToScene(cameraGo, scene);
                var camera = cameraGo.AddComponent<Camera>(); camera.scene = scene; camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.11f, .15f, .13f, 1);
                var lightGo = new GameObject("Art preview sun"); SceneManager.MoveGameObjectToScene(lightGo, scene);
                var light = lightGo.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.3f;
                lightGo.transform.rotation = Quaternion.Euler(45, -35, 0);
                foreach (var prefab in Prepared)
                {
                    var go = Object.Instantiate(prefab); SceneManager.MoveGameObjectToScene(go, scene);
                    try
                    {
                        var animator = go.GetComponentInChildren<Animator>();
                        if (animator != null)
                        {
                            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "/Animations/Cow_" + cowState + ".anim");
                            clip.SampleAnimation(animator.gameObject, cowState == "Idle" ? 0 : clip.length * .5f);
                        }
                        var b = Geometry(go); camera.orthographicSize = Mathf.Max(b.size.y, b.size.x, b.size.z) * .72f;
                        camera.transform.position = b.center + new Vector3(1, .8f, 1.2f).normalized * 20;
                        camera.transform.LookAt(b.center);
                        var rt = RenderTexture.GetTemporary(512, 512, 24, RenderTextureFormat.ARGB32);
                        var previous = RenderTexture.active;
                        var image = new Texture2D(512, 512, TextureFormat.RGBA32, false);
                        try
                        {
                            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                            image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); image.Apply();
                            File.WriteAllBytes(Path.Combine(output, prefab.name + (prefab.name == "cow" && cowState != "Idle" ? "_" + cowState : "") + ".png"), image.EncodeToPNG());
                            var icon = IconName(prefab.name);
                            if (icon != null)
                            {
                                var iconPath = "Assets/_Game/Resources/FarmUI/Icons/" + icon + ".png";
                                if (!File.Exists(iconPath))
                                {
                                    camera.backgroundColor = Color.clear; camera.Render();
                                    image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); image.Apply();
                                    File.WriteAllBytes(iconPath, image.EncodeToPNG());
                                    camera.backgroundColor = new Color(.11f, .15f, .13f, 1);
                                    AssetDatabase.ImportAsset(iconPath);
                                }
                            }
                        }
                        finally { camera.targetTexture = null; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(image); }
                    }
                    finally { Object.DestroyImmediate(go); }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static string IconName(string prefab)
        {
            switch (prefab)
            {
                case "pear_fruit": return "crop_pear";
                case "strawberry_fruit": return "crop_strawberry";
                case "barn": return "b_barn";
                case "mill": return "b_mill";
                case "cheese_dairy": return "b_cheese_dairy";
                case "cheese_product": return "cheese";
                case "flour_product": return "flour";
                case "feeder": return "b_feeder";
                default: return null;
            }
        }
    }
}
