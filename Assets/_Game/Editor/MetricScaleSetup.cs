using System;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.Building;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MyLittleFarm.Editor
{
    /// <summary>Authors metric building wrappers and installs the 1.8-m Humanoid without rebuilding the world map.</summary>
    public static class MetricScaleSetup
    {
        public const string Folder = "Assets/_Game/Content/Art/Buildings";
        private const string Ranch = "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/";

        [MenuItem("Tools/My Little Farm/Apply P0-3 Metric Scale And Kevin Character")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/_Game/Scenes/Prototype.unity")
                throw new InvalidOperationException("Open Prototype in Edit Mode.");
            var farm = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
            if (Mathf.Abs(farm.GetComponentInChildren<GridSystem>().CellSize - 1f) > 0.0001f)
                throw new InvalidOperationException("The authored map must use one unit per cell.");
            PlayerRigSetup.Install(farm);
            var properties = new SerializedObject(farm.GetComponentInChildren<BuildSystem>());
            var array = properties.FindProperty("visualPrefabs");
            array.arraySize = BuildingDefinition.Catalog.Count;
            for (var i = 0; i < array.arraySize; i++)
            {
                var definition = BuildingDefinition.Catalog[i];
                var entry = array.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("definitionId").stringValue = definition.Id;
                entry.FindPropertyRelative("prefab").objectReferenceValue = GetOrCreate(definition.Id);
            }
            properties.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save metric setup.");
            Debug.Log("P0-3: metric building art and Kevin Humanoid installed.");
        }

        public static GameObject GetOrCreate(string id, bool rebuild = false)
        {
            EnsureFolder(Folder);
            var path = Folder + "/" + id + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && !rebuild) return existing;
            var root = new GameObject(id + " metric art");
            try
            {
                var metrics = root.AddComponent<BuildingArtMetrics>();
                if (id == "flowerbed") Flowerbed(root.transform);
                else
                {
                    var name = id == "house" ? "Bld_FarmerHouse" : id == "storage" ? "Bld_Barn_01"
                        : id == "coop" ? "Bld_ChickenCoop" : "Bld_StoreBuilding_01";
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(Ranch + name + ".prefab");
                    if (source == null) throw new MissingReferenceException(name);
                    var model = UnityEngine.Object.Instantiate(source, root.transform, false);
                    model.name = "Model";
                    foreach (var collider in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                    model.transform.localRotation = Quaternion.Euler(0, 180, 0) * model.transform.localRotation;
                    VisualPrefab.TryGetBounds(model, out var bounds);
                    // Source door leaves: house and barn. The coop source has only a chicken hatch;
                    // give its compact body a separate human service door rather than treating the raised hatch panel as one.
                    var bottom = id == "house" ? 0.1758f : 0f;
                    var top = id == "house" ? 2.6835f : 3.6f;
                    var scale = id == "market" ? 3f / bounds.size.y : id == "coop" ? 3.9213f / bounds.size.y
                        : BuildingArtMetrics.DoorClearance / (top - bottom);
                    model.transform.localScale *= scale;
                    VisualPrefab.TryGetBounds(model, out bounds);
                    model.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                    VisualPrefab.TryGetBounds(model, out bounds);
                    if (id == "coop") ServiceDoor(root.transform, metrics, bounds);
                    else if (id != "market") metrics.ConfigureDoor(new Vector3(0, bottom * scale, bounds.min.z),
                        new Vector3(0, top * scale, bounds.min.z));
                }
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void ServiceDoor(Transform root, BuildingArtMetrics metrics, Bounds body)
        {
            var wood = Material("Bed timber", new Color(0.38f, 0.22f, 0.11f));
            var frame = Material("Coop door frame", new Color(0.87f, 0.85f, 0.72f));
            var iron = Material("Coop door hardware", new Color(0.12f, 0.15f, 0.17f));
            const float bottom = 0.46f;
            var z = body.max.z + 0.025f;
            var height = BuildingArtMetrics.DoorClearance;
            Part(root, "Service door", new Vector3(0, bottom + height * 0.5f, z), new Vector3(0.86f, height, 0.055f), wood);
            foreach (var side in new[] { -1f, 1f })
                Part(root, "Door jamb", new Vector3(side * 0.47f, bottom + height * 0.5f, z + 0.015f), new Vector3(0.08f, height, 0.08f), frame);
            Part(root, "Door lintel", new Vector3(0, bottom + height + 0.04f, z + 0.015f), new Vector3(1.02f, 0.08f, 0.08f), frame);
            foreach (var y in new[] { bottom + 0.3f, bottom + height - 0.3f })
                Part(root, "Door brace", new Vector3(0, y, z + 0.04f), new Vector3(0.81f, 0.07f, 0.04f), iron);
            Part(root, "Door handle", new Vector3(0.29f, bottom + 0.92f, z + 0.07f), new Vector3(0.045f, 0.15f, 0.055f), iron);
            Part(root, "Lower service step", new Vector3(0, 0.115f, body.max.z + 0.53f), new Vector3(1.04f, 0.23f, 0.34f), wood);
            Part(root, "Upper service step", new Vector3(0, 0.23f, body.max.z + 0.2f), new Vector3(1.04f, 0.46f, 0.34f), wood);
            VisualPrefab.TryGetBounds(root.gameObject, out var full);
            var offset = new Vector3(full.center.x, 0, full.center.z);
            foreach (Transform child in root) child.localPosition -= offset;
            metrics.ConfigureDoor(new Vector3(0, bottom, z + 0.03f) - offset,
                new Vector3(0, bottom + height, z + 0.03f) - offset);
        }

        private static void Flowerbed(Transform root)
        {
            var wood = Material("Bed timber", new Color(0.38f, 0.22f, 0.11f));
            var soil = Material("Bed soil", new Color(0.22f, 0.14f, 0.07f));
            Part(root, "Soil", new Vector3(0,0.06f,0), new Vector3(0.78f,0.12f,0.78f), soil);
            foreach (var side in new[] { -1f, 1f })
            {
                Part(root, "Side rail", new Vector3(side * 0.415f,0.075f,0), new Vector3(0.07f,0.15f,0.9f), wood);
                Part(root, "End rail", new Vector3(0,0.075f,side * 0.415f), new Vector3(0.76f,0.15f,0.07f), wood);
            }
            var flower = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Nicrom/Shaders/Wind/Prefabs/LPW_Flower_A1_H70cm_01.prefab");
            if (flower == null) throw new MissingReferenceException("The flower model is missing.");
            for (var i = 0; i < 5; i++)
            {
                var model = UnityEngine.Object.Instantiate(flower, root, false);
                foreach (var collider in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                VisualPrefab.FitExisting(model, new Vector3(0.22f, 0.28f, 0.22f));
                model.transform.localPosition += new Vector3(i == 4 ? 0 : i % 2 == 0 ? -0.23f : 0.23f,
                    0.12f, i == 4 ? 0 : i < 2 ? -0.23f : 0.23f);
            }
        }

        private static void Part(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube); part.name = name;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false); part.transform.localPosition = position; part.transform.localScale = size;
            part.GetComponent<Renderer>().sharedMaterial = material;
        }
        private static Material Material(string name, Color color)
        {
            var path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard")) { name = name, color = color, enableInstancing = true };
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var split = path.LastIndexOf('/'); EnsureFolder(path.Substring(0, split));
            AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
        }
    }
}
