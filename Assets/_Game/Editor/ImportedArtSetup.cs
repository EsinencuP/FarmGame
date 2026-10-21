using System;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.Gameplay.Farming;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MyLittleFarm.Editor
{
    /// <summary>Links imported farm models to the game's authored content and building definitions.</summary>
    public static class ImportedArtSetup
    {
        private const string PrototypePath = "Assets/_Game/Scenes/Prototype.unity";
        private const string Pandazole = "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/";
        private const string Food = "Assets/LowPolyFoodLite/Prefabs/";
        private const string Farm = "Assets/LowPolyFarmLite/Prefabs/";

        [MenuItem("Tools/My Little Farm/Import Art Into Prototype")]
        public static void ImportIntoPrototype()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode
                || SceneManager.GetActiveScene().path != PrototypePath)
            {
                Debug.LogError("Open Prototype.unity in Edit Mode before importing farm art.");
                return;
            }

            var house = Load(Pandazole + "Bld_FarmerHouse.prefab");
            var storage = Load(Pandazole + "Bld_Barn_01.prefab");
            var market = Load(Pandazole + "Bld_StoreBuilding_01.prefab");
            var coop = Load(Pandazole + "Bld_ChickenCoop.prefab");
            var tree = Load(Farm + "Tree_04.prefab");
            var flowerbed = Load("Assets/Nicrom/Shaders/Wind/Prefabs/LPW_Flower_A1_H70cm_01.prefab");
            var appleFruit = Load(Food + "Apple.prefab");

            var bootstrap = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>(FindObjectsInactive.Include);
            if (bootstrap == null)
            {
                Debug.LogError("Prototype has no baked GameBootstrap. Bake the scene first.");
                return;
            }

            EnsureFolder("Assets/_Game/Content");
            EnsureFolder("Assets/_Game/Content/Crops");
            EnsureFolder("Assets/_Game/Content/Trees");

            var catalog = bootstrap.GetComponentInChildren<FarmCatalog>(true);
            var build = bootstrap.GetComponentInChildren<BuildSystem>(true);
            var orchard = bootstrap.GetComponentInChildren<OrchardSystem>(true);
            if (catalog == null || build == null || orchard == null)
                throw new InvalidOperationException("The baked prototype is missing a gameplay system.");

            var crops = new[]
            {
                Crop("carrot", "Морковь", 8f, 2, new Color(0.95f, 0.42f, 0.08f)),
                Crop("potato", "Картофель", 16f, 3, new Color(0.62f, 0.44f, 0.22f)),
                Crop("wheat", "Пшеница", 12f, 4, new Color(0.95f, 0.78f, 0.26f)),
                Crop("tomato", "Томат", 24f, 5, new Color(0.88f, 0.16f, 0.12f)),
                Crop("corn", "Кукуруза", 18f, 4, new Color(0.96f, 0.80f, 0.20f)),
                Crop("cucumber", "Огурец", 10f, 3, new Color(0.22f, 0.70f, 0.28f))
            };
            var catalogProperties = new SerializedObject(catalog);
            var cropAssets = catalogProperties.FindProperty("cropAssets");
            cropAssets.arraySize = crops.Length;
            for (var index = 0; index < crops.Length; index++)
                cropAssets.GetArrayElementAtIndex(index).objectReferenceValue = crops[index];
            catalogProperties.ApplyModifiedProperties();

            var visuals = new[]
            {
                new BuildingArt("house", MetricScaleSetup.GetOrCreate("house")), new BuildingArt("storage", MetricScaleSetup.GetOrCreate("storage")),
                new BuildingArt("market", MetricScaleSetup.GetOrCreate("market")), new BuildingArt("coop", MetricScaleSetup.GetOrCreate("coop")),
                new BuildingArt("flowerbed", MetricScaleSetup.GetOrCreate("flowerbed"))
            };
            var buildingProperties = new SerializedObject(build);
            var visualPrefabs = buildingProperties.FindProperty("visualPrefabs");
            visualPrefabs.arraySize = visuals.Length;
            for (var index = 0; index < visuals.Length; index++)
            {
                var entry = visualPrefabs.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("definitionId").stringValue = visuals[index].id;
                entry.FindPropertyRelative("prefab").objectReferenceValue = visuals[index].prefab;
            }
            buildingProperties.ApplyModifiedProperties();

            var apple = AppleTree(tree, appleFruit);
            var orchardProperties = new SerializedObject(orchard);
            orchardProperties.FindProperty("appleTreeAsset").objectReferenceValue = apple;
            orchardProperties.ApplyModifiedProperties();

            EditorUtility.SetDirty(catalog);
            EditorUtility.SetDirty(build);
            EditorUtility.SetDirty(orchard);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("My Little Farm: imported art is linked to all six crops, five buildings and the apple tree.");
        }

        private readonly struct BuildingArt
        {
            public readonly string id;
            public readonly GameObject prefab;
            public BuildingArt(string id, GameObject prefab) { this.id = id; this.prefab = prefab; }
        }

        private static GameObject Load(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) throw new InvalidOperationException("Imported prefab is missing: " + path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        private static CropDefinition Crop(string id, string title, float seconds, int yield, Color color)
        {
            var path = "Assets/_Game/Content/Crops/" + id + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<CropDefinition>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<CropDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }
            var properties = new SerializedObject(asset);
            properties.FindProperty("cropId").stringValue = id;
            properties.FindProperty("displayName").stringValue = title;
            properties.FindProperty("seedItemId").stringValue = id + "_seed";
            properties.FindProperty("harvestItemId").stringValue = id;
            properties.FindProperty("growDurationSeconds").floatValue = seconds;
            properties.FindProperty("yieldAmount").intValue = yield;
            properties.FindProperty("matureColor").colorValue = color;
            CropStageArtBuilder.AssignStages(properties, id);
            properties.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static TreeDefinition AppleTree(GameObject visualPrefab, GameObject fruitPrefab)
        {
            const string path = "Assets/_Game/Content/Trees/apple.asset";
            var asset = AssetDatabase.LoadAssetAtPath<TreeDefinition>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<TreeDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }
            var properties = new SerializedObject(asset);
            properties.FindProperty("treeId").stringValue = "apple";
            properties.FindProperty("displayName").stringValue = "Яблоня";
            properties.FindProperty("fruitItemId").stringValue = "apple";
            properties.FindProperty("firstHarvestSeconds").floatValue = 30f;
            properties.FindProperty("regrowSeconds").floatValue = 45f;
            properties.FindProperty("yieldAmount").intValue = 3;
            properties.FindProperty("visualPrefab").objectReferenceValue = visualPrefab;
            properties.FindProperty("fruitPrefab").objectReferenceValue = fruitPrefab;
            properties.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

    }
}
