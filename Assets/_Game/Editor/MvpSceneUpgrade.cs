using System;
using System.Linq;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.Animals;
using MyLittleFarm.Gameplay.World;
using MyLittleFarm.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MyLittleFarm.Editor
{
    /// <summary>Replaces the remaining baked prototype visuals while preserving gameplay components and IDs.</summary>
    public static class MvpSceneUpgrade
    {
        private const string PrototypePath = "Assets/_Game/Scenes/Prototype.unity";
        private const string Farm = "Assets/LowPolyFarmLite/Prefabs/";
        private const string Ranch = "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/";
        private const string Npc = "Assets/Stylized NPC - Peasant Nolant/";

        [MenuItem("Tools/My Little Farm/Upgrade Prototype To MVP")]
        public static void Upgrade()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != PrototypePath)
                throw new InvalidOperationException("Open Prototype.unity in Edit Mode before upgrading the scene.");

            ImportedArtSetup.ImportIntoPrototype();
            var root = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>(FindObjectsInactive.Include);
            if (root == null) throw new MissingReferenceException("The baked GameBootstrap is missing.");
            var grid = root.GetComponentInChildren<GridSystem>(true);
            if (grid == null) throw new MissingReferenceException("The baked GridSystem is missing.");

            PlayerRigSetup.Install(root);
            ExpandWorld(root, grid);
            ReplacePen(root, grid);
            ReplaceSaleCrate(root);
            FixHud(root);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save the upgraded Prototype scene.");
            Debug.Log("My Little Farm: MVP scene upgrade completed (player, animation, playable map, pen, crate, HUD).");
        }

        private static AnimatorController CreateLocomotionController()
        {
            EnsureFolder("Assets/_Game/Content/Animation");
            const string path = "Assets/_Game/Content/Animation/PlayerLocomotion.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller != null) { RepairLocomotion(controller); return controller; }

            var modelPath = Npc + "Models/Peasant Nolant(Free Version).fbx";
            var clips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().ToArray();
            var idle = Array.Find(clips, clip => clip.name == "metarig|Idle");
            var walk = Array.Find(clips, clip => clip.name == "metarig|Walk");
            if (idle == null || walk == null)
                throw new MissingReferenceException("Stylized NPC must contain the Idle and Walk animation clips.");

            controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            var blend = new BlendTree
            {
                name = "Idle Walk",
                blendParameter = "Speed",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(blend, controller);
            blend.AddChild(idle, 0f);
            blend.AddChild(walk, 0.6f);
            var stateMachine = controller.layers[0].stateMachine;
            var state = stateMachine.AddState("Locomotion");
            state.motion = blend;
            stateMachine.defaultState = state;
            RepairLocomotion(controller);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        public static void RepairLocomotion(AnimatorController controller)
        {
            foreach (var name in new[] { "Grounded", "VerticalSpeed", "Sprinting", "GaitPlayback" })
                if (!controller.parameters.Any(p => p.name == name))
                    controller.AddParameter(name, name == "VerticalSpeed" || name == "GaitPlayback"
                        ? AnimatorControllerParameterType.Float : AnimatorControllerParameterType.Bool);
            var machine = controller.layers[0].stateMachine;
            var locomotion = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Locomotion");
            if (locomotion == null || !(locomotion.motion is BlendTree blend)) return;
            locomotion.speedParameter = "GaitPlayback";
            locomotion.speedParameterActive = true;
            if (blend.children.Length == 3 && blend.children[1].motion == blend.children[2].motion)
            {
                var children = blend.children.Take(2).ToArray();
                children[1].threshold = 0.6f;
                blend.children = children;
                blend.name = "Idle Walk";
            }
            // With no Jump clip installed, hold a neutral pose instead of walking in mid-air.
            var airborne = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Airborne pose");
            if (airborne == null)
            {
                airborne = machine.AddState("Airborne pose");
                airborne.motion = blend.children[0].motion;
                airborne.speed = 0f;
                var takeoff = locomotion.AddTransition(airborne);
                takeoff.hasExitTime = false;
                takeoff.duration = 0.12f;
                takeoff.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
                var landing = airborne.AddTransition(locomotion);
                landing.hasExitTime = false;
                landing.duration = 0.12f;
                landing.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
            }
            EditorUtility.SetDirty(controller);
        }

        private static void ReplacePlayer(GameBootstrap root, AnimatorController controller)
        {
            var player = root.GetComponentInChildren<PlayerController>(true);
            if (player == null) throw new MissingReferenceException("PlayerController is missing.");
            var visual = player.transform.Find("Nolant Visual");
            if (visual == null)
            {
                var prefab = Load(Npc + "Prefabs/Peasant Nolant Blue(Free Version).prefab");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, player.transform);
                instance.name = "Nolant Visual";
                VisualPrefab.FitExisting(instance, new Vector3(0.85f, 1.8f, 0.85f));
                var capsule = player.GetComponent<CharacterController>();
                instance.transform.localPosition += Vector3.down *
                    (capsule.height * 0.5f - capsule.center.y);
                visual = instance.transform;
            }
            var animator = visual.GetComponentInChildren<Animator>(true);
            if (animator == null) throw new MissingComponentException("Stylized NPC prefab has no Animator.");
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            EditorUtility.SetDirty(animator);
            RemovePrototypeMesh(player.gameObject);
            GameplayReviewRepair.AlignPlayerVisual(player);
        }

        private static void ExpandWorld(GameBootstrap root, GridSystem grid)
        {
            EnsureFolder("Assets/_Game/Content/World");
            var terrain = Load(Farm + "Terrain_01.prefab");
            var cell = grid.CellSize;
            var width = grid.ChunkSizeX * cell;
            var depth = grid.ChunkSizeZ * cell;
            for (var x = -1; x <= 1; x++)
            for (var z = -1; z <= 1; z++)
            {
                var name = x == 0 && z == 0 ? "TileBlock_Start"
                    : x == 1 && z == 0 ? "TileBlock_Extra" : $"TileBlock_{x}_{z}";
                var child = root.transform.Find(name);
                if (child == null)
                {
                    var block = new GameObject(name);
                    block.transform.SetParent(root.transform, false);
                    block.transform.localPosition = new Vector3(x * width, 0f, z * depth);
                    block.AddComponent<TilemapChunk>();
                    block.AddComponent<ChunkRenderer>();
                    var surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    surface.name = "Terrain Surface";
                    surface.transform.SetParent(block.transform, false);
                    surface.transform.localPosition = new Vector3(width * 0.5f, -0.25f, depth * 0.5f);
                    surface.transform.localScale = new Vector3(width, 0.5f, depth);
                    surface.GetComponent<Renderer>().enabled = false;
                    child = block.transform;
                }

                var chunk = child.GetComponent<TilemapChunk>();
                if (chunk == null) throw new MissingComponentException(name + " has no TilemapChunk.");
                var path = $"Assets/_Game/Content/World/{name}.asset";
                var map = AssetDatabase.LoadAssetAtPath<WorldMapAsset>(path);
                if (map == null)
                {
                    map = ScriptableObject.CreateInstance<WorldMapAsset>();
                    AssetDatabase.CreateAsset(map, path);
                }
                chunk.AssignWorldMap(map);
                EditorUtility.SetDirty(chunk);
                if (child.Find("Ground Visual") == null)
                {
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(terrain, child);
                    model.name = "Ground Visual";
                    VisualPrefab.FitExisting(model, new Vector3(width, 0.4f, depth));
                    model.transform.localPosition += new Vector3(width * 0.5f, 0f, depth * 0.5f);
                }
                // Existing imported visuals need the same alignment when the upgrade is repeated.
                AlignGround(chunk, child.Find("Ground Visual").gameObject);
            }

            MoveImportedEnvironment(root, grid);
            AddBorderEnvironment(root, grid);
            RepairTreeTerrain(root, grid);
            grid.RegisterSceneChunks(root.GetComponentsInChildren<TilemapChunk>(true));
            foreach (var chunk in root.GetComponentsInChildren<TilemapChunk>(true)) chunk.ApplyTo(grid);
            foreach (var renderer in root.GetComponentsInChildren<ChunkRenderer>(true))
            {
                var serialized = new SerializedObject(renderer);
                serialized.FindProperty("grassColor").colorValue = new Color(0.24f, 0.45f, 0.18f, 0.76f);
                serialized.FindProperty("rockColor").colorValue = new Color(0.24f, 0.45f, 0.18f, 0.76f);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                renderer.Rebuild();
            }
        }

        public static void AlignGround(TilemapChunk chunk, GameObject model)
        {
            if (chunk == null) throw new ArgumentNullException(nameof(chunk));
            GroundAlignmentValidator.AlignGroundVisual(model);
        }

        private static void MoveImportedEnvironment(GameBootstrap root, GridSystem grid)
        {
            var imported = root.transform.Find("Imported Art");
            if (imported == null) return;
            for (var index = imported.childCount - 1; index >= 0; index--)
            {
                var item = imported.GetChild(index);
                if (item.name == "Farm Backdrop" || item.name.StartsWith("South Fence Art"))
                {
                    Undo.DestroyObjectImmediate(item.gameObject);
                    continue;
                }
                var cell = grid.WorldToGrid(item.position);
                var chunkCoord = grid.GridToChunk(cell);
                var name = chunkCoord == Vector2Int.zero ? "TileBlock_Start"
                    : chunkCoord == new Vector2Int(1, 0) ? "TileBlock_Extra"
                    : $"TileBlock_{chunkCoord.x}_{chunkCoord.y}";
                var chunk = root.transform.Find(name);
                if (chunk == null) throw new MissingReferenceException("No block for " + item.name);
                Undo.SetTransformParent(item, chunk, "Integrate imported environment");
                if (item.name.Contains("Tree") || item.name.Contains("Rock"))
                    PaintObstacle(chunk.GetComponent<TilemapChunk>(), cell, CellType.Rock);
            }
            Undo.DestroyObjectImmediate(imported.gameObject);
        }

        private static void AddBorderEnvironment(GameBootstrap root, GridSystem grid)
        {
            var tree = Load(Farm + "Tree_04.prefab");
            var rock = Load(Farm + "Rock_04.prefab");
            var positions = new[]
            {
                new Vector3(-7f, 0f, -7f), new Vector3(-7f, 0f, 15f),
                new Vector3(5f, 0f, -7f), new Vector3(16f, 0f, -7f),
                new Vector3(16f, 0f, 16f)
            };
            foreach (var position in positions)
                PlaceScenery(root, grid, tree, "Boundary Tree", position,
                    new Vector3(2.2f, 3f, 2.2f), CellType.Rock);
            PlaceScenery(root, grid, rock, "Boundary Rock", new Vector3(-6f, 0f, 8f),
                Vector3.one * 1.2f, CellType.Rock);
        }

        private static void PlaceScenery(GameBootstrap root, GridSystem grid, GameObject prefab,
            string name, Vector3 position, Vector3 size, CellType terrain)
        {
            var coordinate = grid.WorldToGrid(position);
            var chunkCoord = grid.GridToChunk(coordinate);
            var chunkName = chunkCoord == Vector2Int.zero ? "TileBlock_Start"
                : chunkCoord == new Vector2Int(1, 0) ? "TileBlock_Extra"
                : $"TileBlock_{chunkCoord.x}_{chunkCoord.y}";
            var parent = root.transform.Find(chunkName);
            if (parent == null || parent.Find(name + " " + coordinate.x + " " + coordinate.y) != null) return;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name + " " + coordinate.x + " " + coordinate.y;
            VisualPrefab.FitExisting(instance, size);
            instance.transform.position += position - parent.position;
            PaintObstacle(parent.GetComponent<TilemapChunk>(), coordinate, terrain);
        }

        private static void PaintObstacle(TilemapChunk chunk, Vector2Int cell, CellType terrain)
        {
            if (chunk?.WorldMap == null) return;
            chunk.WorldMap.SetTerrain(cell, new TerrainCell(terrain));
            EditorUtility.SetDirty(chunk.WorldMap);
        }

        private static void RepairTreeTerrain(GameBootstrap root, GridSystem grid)
        {
            // An interrupted upgrade may have saved Tree as terrain; permanent obstacles use Rock terrain.
            foreach (var chunk in root.GetComponentsInChildren<TilemapChunk>(true))
            {
                if (chunk.WorldMap != null)
                {
                    var serialized = new SerializedObject(chunk.WorldMap);
                    var cells = serialized.FindProperty("cells");
                    for (var index = 0; index < cells.arraySize; index++)
                    {
                        var terrain = cells.GetArrayElementAtIndex(index).FindPropertyRelative("terrain");
                        var type = terrain.FindPropertyRelative("type");
                        if (type.intValue == (int)CellType.Tree) type.intValue = (int)CellType.Rock;
                    }
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                for (var i = 0; i < chunk.transform.childCount; i++)
                {
                    var child = chunk.transform.GetChild(i);
                    if (child.name.Contains("Tree"))
                        PaintObstacle(chunk, grid.WorldToGrid(child.position), CellType.Rock);
                }
            }
        }

        private static void ReplacePen(GameBootstrap root, GridSystem grid)
        {
            var pen = root.transform.Find("Chicken Pen Mockup");
            var animals = root.GetComponentInChildren<AnimalSystem>(true);
            if (pen == null || animals == null || pen.Find("Farm Fence Models") != null) return;
            for (var i = pen.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(pen.GetChild(i).gameObject);
            pen.name = "Chicken Pen";
            var fences = new GameObject("Farm Fence Models").transform;
            fences.SetParent(pen, false);
            var prefab = Load(Ranch + "Env_WoodFence_01.prefab");
            var min = animals.PenMinCell;
            var max = animals.PenMaxCell;
            var size = grid.CellSize;
            var gate = min.x + (max.x - min.x + 1) / 2;
            for (var x = min.x; x <= max.x; x++)
            {
                var center = (x + 0.5f) * size;
                if (x != gate) PlaceFence(fences, prefab, $"South {x}",
                    new Vector3(center, 0f, min.y * size), size, 0f);
                PlaceFence(fences, prefab, $"North {x}",
                    new Vector3(center, 0f, (max.y + 1) * size), size, 0f);
            }
            for (var z = min.y; z <= max.y; z++)
            {
                var center = (z + 0.5f) * size;
                PlaceFence(fences, prefab, $"West {z}",
                    new Vector3(min.x * size, 0f, center), size, 90f);
                PlaceFence(fences, prefab, $"East {z}",
                    new Vector3((max.x + 1) * size, 0f, center), size, 90f);
            }
        }

        private static void PlaceFence(Transform parent, GameObject prefab, string name,
            Vector3 position, float cellSize, float yaw)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            VisualPrefab.FitExisting(instance, new Vector3(cellSize, 0.75f, 0.2f));
            instance.transform.localPosition += position;
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        private static void ReplaceSaleCrate(GameBootstrap root)
        {
            var crate = root.transform.Find("Sale Crate");
            if (crate == null) throw new MissingReferenceException("Sale Crate is missing.");
            var previous = crate.Find("Imported Sale Box");
            if (previous != null) Undo.DestroyObjectImmediate(previous.gameObject);
            crate.localScale = Vector3.one;
            var collider = crate.GetComponent<BoxCollider>();
            if (collider != null) collider.size = new Vector3(1.7f, 1.1f, 1.4f);
            RemovePrototypeMesh(crate.gameObject);
            if (crate.Find("Farm Sale Box") != null) return;
            var prefab = Load(Ranch + "Prop_WoodenBox_01.prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, crate);
            instance.name = "Farm Sale Box";
            VisualPrefab.FitExisting(instance, new Vector3(1.6f, 1f, 1.3f));
            instance.transform.localPosition += new Vector3(0f, -crate.position.y, 0f);
        }

        private static void FixHud(GameBootstrap root)
        {
            var canvas = root.GetComponentInChildren<Canvas>(true);
            if (canvas == null) throw new MissingReferenceException("Prototype HUD Canvas is missing.");
            var panel = Find(canvas.transform, "Stats Panel").GetComponent<RectTransform>();
            var stats = Find(canvas.transform, "Stats").GetComponent<UnityEngine.UI.Text>();
            var help = Find(canvas.transform, "Help").GetComponent<UnityEngine.UI.Text>();
            var status = Find(canvas.transform, "Status").GetComponent<UnityEngine.UI.Text>();
            var onboarding = Find(canvas.transform, "Onboarding Hint").GetComponent<UnityEngine.UI.Text>();
            var prompt = Find(canvas.transform, "Interaction Prompt").GetComponent<UnityEngine.UI.Text>();
            panel.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;

            var slots = canvas.transform.Find("Quick Slots");
            if (slots == null)
            {
                var bar = new GameObject("Quick Slots", typeof(RectTransform), typeof(UnityEngine.UI.Image),
                    typeof(UnityEngine.UI.HorizontalLayoutGroup));
                slots = bar.transform;
                slots.SetParent(canvas.transform, false);
                var background = bar.GetComponent<UnityEngine.UI.Image>();
                background.color = new Color(0.12f, 0.16f, 0.12f, 0.76f);
                background.raycastTarget = false;
                var layout = bar.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.spacing = 8f;
                layout.childControlWidth = false;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                layout.padding = new RectOffset(8, 8, 7, 7);
            }
            for (var i = 0; i < 6; i++)
            {
                var icon = Find(canvas.transform, $"Seed Icon {i + 1}");
                icon.SetParent(slots, false);
                var rect = icon.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = rect.anchorMin;
                rect.pivot = rect.anchorMin;
                rect.sizeDelta = new Vector2(48f, 48f);
                var label = icon.Find("Key Label");
                if (label == null)
                {
                    var labelObject = new GameObject("Key Label", typeof(RectTransform), typeof(UnityEngine.UI.Text));
                    label = labelObject.transform;
                    label.SetParent(icon, false);
                    var text = labelObject.GetComponent<UnityEngine.UI.Text>();
                    text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    text.text = (i + 1).ToString();
                    text.fontSize = 19;
                    text.alignment = TextAnchor.LowerRight;
                    text.color = Color.white;
                    text.raycastTarget = false;
                    var labelRect = labelObject.GetComponent<RectTransform>();
                    labelRect.anchorMin = Vector2.zero;
                    labelRect.anchorMax = Vector2.one;
                    labelRect.offsetMin = new Vector2(2f, 2f);
                    labelRect.offsetMax = new Vector2(-3f, -2f);
                }
                Find(canvas.transform, $"Harvest Icon {i + 1}").gameObject.SetActive(false);
            }
            foreach (var name in new[] { "Seed Icons Label", "Harvest Icons Label", "Coin Icon", "Tool Icon" })
                Find(canvas.transform, name).gameObject.SetActive(false);

            var responsive = canvas.GetComponent<ResponsiveHudLayout>();
            if (responsive == null) responsive = canvas.gameObject.AddComponent<ResponsiveHudLayout>();
            responsive.Configure(panel, stats, help, status, onboarding, prompt,
                slots.GetComponent<RectTransform>());
            EditorUtility.SetDirty(responsive);
            HudConsistencySetup.Configure(root);
        }

        private static void RemovePrototypeMesh(GameObject owner)
        {
            var renderer = owner.GetComponent<MeshRenderer>();
            if (renderer != null) Undo.DestroyObjectImmediate(renderer);
            var filter = owner.GetComponent<MeshFilter>();
            if (filter != null) Undo.DestroyObjectImmediate(filter);
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            throw new MissingReferenceException(name + " is missing from Prototype HUD.");
        }

        private static GameObject Load(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new MissingReferenceException("Imported prefab is missing: " + path);
            return prefab;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
