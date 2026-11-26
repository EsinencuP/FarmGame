using System;
using System.Collections.Generic;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.Gameplay.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MyLittleFarm.Editor
{
    public static class GameSceneSetup
    {
        private const string BundlePath = "Assets/_Game/Content/World/GameLandscape.asset";
        private const string Landscape = "Assets/_Game/Content/World/Landscape/";
        private const string FarmPrefabs = "Assets/ThirdParty/LowPolyFarmLite/Prefabs/";
        private const string Nature = "Assets/ThirdParty/Low_Poly_Nature_Pack_Lite/Prefabs/";
        private const int Min = -70;
        private const int Max = 80;
        private const int ChunkSize = 10;
        private const float WaterHeight = -.22f;
        private static readonly Vector4[] Lakes =
        {
            new Vector4(-5, 49, 10, 6),
            new Vector4(50, -21, 7, 9)
        };
        // x/z center, flat radius, height. The surrounding meadow blends into the hills.
        private static readonly Vector4[] Clearings =
        {
            new Vector4(-50, -46, 9, 2.6f), new Vector4(-57, -14, 10, 2.7f),
            new Vector4(-51, 22, 9, 2.5f), new Vector4(-48, 57, 9, 3.3f),
            new Vector4(-13, 67, 8, 3.1f), new Vector4(28, 62, 9, 3.6f),
            new Vector4(59, 43, 10, 3.4f), new Vector4(62, 9, 9, 3.2f),
            new Vector4(54, -46, 9, 3.0f), new Vector4(23, -52, 9, 2.9f),
            new Vector4(-16, -57, 9, 2.5f)
        };
        private static readonly Vector2[][] Trails =
        {
            new[] { new Vector2(-10, 1), new Vector2(-6, 1), new Vector2(1, 1), new Vector2(10, 1),
                new Vector2(11, -3), new Vector2(11, -7) },
            new[] { new Vector2(-10, 1), new Vector2(-13, 1), new Vector2(-16, 1),
                new Vector2(-20, 0), new Vector2(-28, 1), new Vector2(-35, 0),
                new Vector2(-43, -4), new Vector2(-51, -9), new Vector2(-57, -14) },
            new[] { new Vector2(-15, 1), new Vector2(-13, 8), new Vector2(-13, 15),
                new Vector2(-17, 23), new Vector2(-26, 27), new Vector2(-37, 24),
                new Vector2(-44, 20), new Vector2(-51, 22), new Vector2(-55, 32),
                new Vector2(-57, 44), new Vector2(-48, 57) },
            new[] { new Vector2(-57, -14), new Vector2(-60, -26), new Vector2(-57, -38),
                new Vector2(-50, -46), new Vector2(-42, -51), new Vector2(-33, -56),
                new Vector2(-24, -58), new Vector2(-16, -57), new Vector2(-4, -62),
                new Vector2(9, -60), new Vector2(23, -52), new Vector2(38, -49),
                new Vector2(54, -46), new Vector2(63, -35), new Vector2(65, -24),
                new Vector2(66, -12), new Vector2(67, 0), new Vector2(62, 9),
                new Vector2(68, 18), new Vector2(67, 30), new Vector2(59, 43),
                new Vector2(48, 55), new Vector2(39, 63), new Vector2(28, 62),
                new Vector2(13, 66), new Vector2(-1, 68), new Vector2(-13, 67),
                new Vector2(-24, 63), new Vector2(-36, 64), new Vector2(-48, 57) },
            new[] { new Vector2(11, -7), new Vector2(11, -13), new Vector2(8, -23),
                new Vector2(10, -33), new Vector2(17, -42), new Vector2(23, -52) },
            new[] { new Vector2(11, -13), new Vector2(22, -15), new Vector2(31, -11),
                new Vector2(40, -10), new Vector2(48, -9), new Vector2(55, -2), new Vector2(62, 9) },
            new[] { new Vector2(31, -11), new Vector2(31, 0), new Vector2(27, 12), new Vector2(26, 21),
                new Vector2(21, 28), new Vector2(11, 31), new Vector2(2, 35),
                new Vector2(-6, 40), new Vector2(-15, 44), new Vector2(-19, 54), new Vector2(-13, 67) },
            new[] { new Vector2(21, 28), new Vector2(31, 36), new Vector2(43, 36),
                new Vector2(51, 40), new Vector2(59, 43) },
            new[] { new Vector2(2, 35), new Vector2(12, 42), new Vector2(18, 48), new Vector2(28, 62) }
        };

        [MenuItem("Tools/My Little Farm/Author Game Scene")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
            for (var index = 0; index < SceneManager.sceneCount; index++)
                if (SceneManager.GetSceneAt(index).isDirty)
                    throw new InvalidOperationException("Save the open scenes before authoring GameScene.");
            if (AssetDatabase.LoadMainAssetAtPath(BundlePath) != null
                || AssetDatabase.LoadMainAssetAtPath(FarmSceneFlow.GameScene) != null)
                throw new InvalidOperationException("GameScene already exists. Edit the authored scene directly.");

            var material = Load<Material>(Landscape + "LandscapeSurface.mat");
            var water = Load<Material>(Landscape + "PondWater.mat");
            var trees = new[]
            {
                Load<GameObject>(FarmPrefabs + "Tree_04.prefab"),
                Load<GameObject>(Nature + "Trees/Tree_05_spring.prefab"),
                Load<GameObject>(Nature + "Trees/Tree_41_summer.prefab"),
                Load<GameObject>(Nature + "Trees/Tree_05_autumn.prefab")
            };
            var rock = Load<GameObject>(FarmPrefabs + "Rock_04.prefab");
            var grass = Load<GameObject>(FarmPrefabs + "Grass_02.prefab");
            var flowers = Load<GameObject>(Nature + "Plants/Flower_tulip_violet.prefab");

            if (!AssetDatabase.CopyAsset(FarmSceneFlow.PrototypeScene, FarmSceneFlow.GameScene))
                throw new InvalidOperationException("Could not create the GameScene asset.");
            var scene = EditorSceneManager.OpenScene(FarmSceneFlow.GameScene);
            var root = Object.FindAnyObjectByType<GameBootstrap>();
            var grid = root.GetComponentInChildren<GridSystem>();
            grid.ConfigureFromInspector();
            grid.RegisterSceneChunks(root.GetComponentsInChildren<TilemapChunk>(true));

            // Preserve the east and north edge samples before a new chunk owns those vertices.
            var boundary = ReadOriginalEdge(grid);
            var expansion = Child(root.transform, "Game Landscape");
            var foliage = Child(expansion, "Woodland and meadow");
            var decor = new List<(Vector2Int cell, GameObject prefab, float height, float angle)>();
            var chunks = new List<(TilemapChunk chunk, ChunkRenderer view, Mesh mesh)>();
            var random = new System.Random(27109);
            var detailRandom = new System.Random(25631);
            for (var x = Min / ChunkSize; x < Max / ChunkSize; x++)
            for (var z = Min / ChunkSize; z < Max / ChunkSize; z++)
            {
                if (x >= -3 && x <= 3 && z >= -3 && z <= 3) continue;
                var start = new Vector2Int(x * ChunkSize, z * ChunkSize);
                var name = $"Sector {x}, {z}";
                var map = ScriptableObject.CreateInstance<WorldMapAsset>();
                map.name = name;
                for (var dx = 0; dx <= ChunkSize; dx++)
                for (var dz = 0; dz <= ChunkSize; dz++)
                {
                    var corner = start + new Vector2Int(dx, dz);
                    map.SetVertexHeight(corner, Height(corner, boundary));
                }
                for (var dx = 0; dx < ChunkSize; dx++)
                for (var dz = 0; dz < ChunkSize; dz++)
                {
                    var cell = start + new Vector2Int(dx, dz);
                    var point = (Vector2)cell + new Vector2(.5f, .5f);
                    var submerged = CellTouchesWater(map, cell);
                    var pathDistance = TrailDistance(point);
                    var lakeContour = LakeContour(point);
                    var meadow = MeadowDistance(point);
                    var forest = Mathf.PerlinNoise(point.x * .051f + 8f, point.y * .051f + 15f);
                    var wooded = meadow > 4f && forest > .43f;
                    var roll = random.NextDouble();
                    var prop = !submerged && meadow > 1.6f && pathDistance > 1.7f && lakeContour > 1.05f
                        && roll < (wooded ? Mathf.Lerp(.047f, .15f, Mathf.InverseLerp(.43f, .75f, forest)) : .025f);
                    var terrain = new TerrainCell(submerged ? CellType.Water
                        : pathDistance < .9f ? CellType.Road : prop ? CellType.Rock : CellType.Grass)
                    {
                        biome = (byte)(wooded ? 2 : 0),
                        permissions = submerged || prop ? CellPermissions.Custom : CellPermissions.Custom | CellPermissions.Walk
                    };
                    if (terrain.type == CellType.Grass && meadow <= 0f && IsLevel(map, cell))
                        terrain.permissions |= CellPermissions.Farm | CellPermissions.Build | CellPermissions.Orchard;
                    map.SetTerrain(cell, terrain);
                    if (terrain.type == CellType.Grass && meadow > -.5f && pathDistance > 1.3f
                        && detailRandom.NextDouble() < (lakeContour < 1.42f ? .15f : meadow < 4f ? .085f : .032f))
                    {
                        var plant = lakeContour < 1.24f || detailRandom.NextDouble() < .66f ? grass : flowers;
                        decor.Add((cell, plant, .27f + (float)detailRandom.NextDouble() * .4f,
                            (float)detailRandom.NextDouble() * 360));
                    }
                    if (!prop) continue;
                    var stone = random.NextDouble() < .17;
                    var tree = trees[random.Next(wooded ? trees.Length : trees.Length - 1)];
                    decor.Add((cell, stone ? rock : tree,
                        (float)random.NextDouble() * (stone ? .54f : 2.2f) + (stone ? .34f : 2.6f),
                        (float)random.NextDouble() * 360));
                }

                var block = Child(expansion, name);
                block.position = new Vector3(start.x, 0f, start.y);
                var chunk = block.gameObject.AddComponent<TilemapChunk>();
                chunk.AssignWorldMap(map);
                chunk.AssignTerrainCollider(block.gameObject.AddComponent<MeshCollider>());
                var view = block.gameObject.AddComponent<ChunkRenderer>();
                var sample = root.transform.Find("TileBlock_-3_-3").GetComponent<ChunkRenderer>();
                CopyPalette(sample, view);
                var mesh = new Mesh { name = name + " Surface" };
                if (chunks.Count == 0)
                {
                    AssetDatabase.CreateAsset(map, BundlePath);
                    PrepareOriginalChunks(root, grid, material, chunks);
                }
                else AssetDatabase.AddObjectToAsset(map, BundlePath);
                AssetDatabase.AddObjectToAsset(mesh, BundlePath);
                chunks.Add((chunk, view, mesh));
                grid.RegisterSceneChunk(chunk);
            }

            foreach (var entry in chunks)
            {
                entry.view.AssignBakedGeometry(entry.mesh, material);
                EditorUtility.SetDirty(entry.mesh);
            }
            foreach (var item in decor) Place(grid, foliage, item.cell, item.prefab, item.height, item.angle);
            for (var index = 0; index < Lakes.Length; index++) CreateLake(grid, expansion, water, Lakes[index], index);
            FinishScenery(grid, expansion);

            var oldLandscape = root.transform.Find("Authored Landscape");
            Object.DestroyImmediate(oldLandscape.Find("World limits").gameObject);
            Object.DestroyImmediate(oldLandscape.Find("Landscape Earth Sides").gameObject);
            ClearStarterScenery(root.transform, oldLandscape, grid);
            RelocateWorldObjects(root.transform, grid);
            PlaceStartingHouse(root, grid);
            PositionStart(root);
            var hud = root.GetComponentInChildren<MyLittleFarm.UI.ResponsiveHudLayout>(true).transform;
            hud.name = "Farm HUD";
            hud.GetComponent<CanvasScaler>().screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            hud.GetComponent<Canvas>().pixelPerfect = true;

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            var game = new EditorBuildSettingsScene(FarmSceneFlow.GameScene, true);
            var gameIndex = scenes.FindIndex(item => item.path == FarmSceneFlow.GameScene || item.path == FarmSceneFlow.PrototypeScene);
            if (gameIndex < 0) scenes.Add(game);
            else scenes[gameIndex] = game;
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"GameScene authored: {chunks.Count} chunks, {decor.Count} landscape props.");
        }

        private static void PrepareOriginalChunks(GameBootstrap root, GridSystem grid, Material material,
            List<(TilemapChunk chunk, ChunkRenderer view, Mesh mesh)> geometry)
        {
            foreach (var chunk in root.GetComponentsInChildren<TilemapChunk>(true))
            {
                if (chunk.WorldMap == null) continue;
                var origin = grid.WorldToGrid(chunk.transform.position);
                var coordinate = grid.GridToChunk(origin);
                if (Mathf.Max(Mathf.Abs(coordinate.x), Mathf.Abs(coordinate.y)) > 3) continue;
                var map = Object.Instantiate(chunk.WorldMap);
                map.name = chunk.name + " Game Map";
                AssetDatabase.AddObjectToAsset(map, BundlePath);
                for (var x = origin.x; x < origin.x + ChunkSize; x++)
                for (var z = origin.y; z < origin.y + ChunkSize; z++)
                {
                    var cell = new Vector2Int(x, z);
                    var ground = grid.GetTerrainCell(cell);
                    if (ground.type == CellType.Water) continue;
                    var inFarm = x >= -10 && x < 20 && z >= -10 && z < 20;
                    var trail = TrailDistance((Vector2)cell + Vector2.one * .5f) < .9f;
                    if (!inFarm && ground.type == CellType.Rock) continue;
                    if (trail)
                        ground = new TerrainCell(CellType.Road) { permissions = CellPermissions.Custom | CellPermissions.Walk };
                    else if (inFarm || ground.type is CellType.Road or CellType.Locked or CellType.Sand)
                        ground = new TerrainCell(CellType.Grass)
                        {
                            permissions = CellPermissions.Custom | CellPermissions.Walk
                                | (inFarm ? CellPermissions.Farm | CellPermissions.Build | CellPermissions.Orchard : 0)
                        };
                    map.SetTerrain(cell, ground);
                }
                chunk.AssignWorldMap(map);
                chunk.ConfigureLockedSector(false);
                chunk.ApplyTo(grid);
                var copy = Object.Instantiate(chunk.GetComponent<MeshFilter>().sharedMesh);
                copy.name = chunk.name + " Game Surface";
                AssetDatabase.AddObjectToAsset(copy, BundlePath);
                var view = chunk.GetComponent<ChunkRenderer>();
                view.AssignBakedGeometry(copy, material);
                geometry.Add((chunk, view, copy));
            }
        }

        private static void ClearStarterScenery(Transform farm, Transform oldLandscape, GridSystem grid)
        {
            Object.DestroyImmediate(farm.Find("World Lighting/Farm Lanterns").gameObject);
            Object.DestroyImmediate(oldLandscape.Find("RC-02 Landmarks").gameObject);
            foreach (var groupName in new[] { "LowPolyFarmLite Trees", "LowPolyFarmLite Rocks", "LowPolyFarmLite Crown Shrubs" })
            {
                var group = oldLandscape.Find(groupName);
                for (var i = group.childCount - 1; i >= 0; i--)
                {
                    var item = group.GetChild(i);
                    var cell = grid.WorldToGrid(item.position);
                    if (cell.x >= -10 && cell.x < 20 && cell.y >= -10 && cell.y < 20)
                        Object.DestroyImmediate(item.gameObject);
                }
            }
        }

        private static void RelocateWorldObjects(Transform farm, GridSystem grid)
        {
            var crate = farm.Find("Sale Crate");
            crate.position = new Vector3(8.5f, crate.position.y, 3.5f);
            var locations = new Dictionary<string, (Vector2Int cell, string name)>
            {
                { "pond", (new Vector2Int(10, 51), "Кромка дальнего леса") },
                { "forest", (new Vector2Int(-1, 32), "Северная чаща") },
                { "hill", (new Vector2Int(28, 20), "Восточная возвышенность") }
            };
            foreach (var cache in farm.GetComponentsInChildren<RareSeedCache>(true))
            {
                var point = locations[cache.Id];
                cache.transform.position = grid.GridToWorld(point.cell) + Vector3.up * .04f;
                var settings = new SerializedObject(cache);
                settings.FindProperty("locationName").stringValue = point.name;
                settings.ApplyModifiedPropertiesWithoutUndo();
                cache.gameObject.name = "Редкие семена · " + point.name;
                cache.gameObject.SetActive(false);
            }
        }

        private static void PositionStart(GameBootstrap root)
        {
            var player = root.GetComponentInChildren<PlayerController>().transform;
            var spawn = new Vector3(-4.5f, player.position.y, -1.5f);
            root.GetComponentInChildren<IsometricCameraController>().transform.position += spawn - player.position;
            player.position = spawn;
        }

        private static void PlaceStartingHouse(GameBootstrap root, GridSystem grid)
        {
            var cell = new Vector2Int(-9, 3);
            var prefab = Load<GameObject>("Assets/_Game/Content/World/Personalization/HouseLevel0.prefab");
            var house = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            house.name = "Starter House";
            house.transform.position = BuildingView.Center(cell.x, cell.y, 0, BuildingDefinition.Catalog[0], grid);
            var settings = new SerializedObject(root);
            settings.FindProperty("starterHomeCell").vector2IntValue = cell;
            settings.FindProperty("starterHomePreview").objectReferenceValue = house;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Dictionary<Vector2Int, float> ReadOriginalEdge(GridSystem grid)
        {
            var heights = new Dictionary<Vector2Int, float>();
            for (var step = -30; step <= 40; step++)
            {
                Read(new Vector2Int(step, -30));
                Read(new Vector2Int(step, 40));
                Read(new Vector2Int(-30, step));
                Read(new Vector2Int(40, step));
            }
            return heights;
            void Read(Vector2Int corner) => heights[corner] = grid.GetVertexHeight(corner);
        }

        private static float Height(Vector2Int vertex, Dictionary<Vector2Int, float> boundary)
        {
            var seam = new Vector2Int(Mathf.Clamp(vertex.x, -30, 40), Mathf.Clamp(vertex.y, -30, 40));
            var point = (Vector2)vertex;
            var distance = Vector2.Distance(vertex, seam);
            if (distance == 0) return boundary[seam];
            var hills = 1.2f + 2.5f * Mathf.PerlinNoise(vertex.x * .035f + 14f, vertex.y * .035f + 6f)
                + 3.8f * Hill(point, new Vector2(49, 43), 18f)
                + 4f * Hill(point, new Vector2(-43, -32), 17f)
                + 2.4f * Hill(point, new Vector2(18, -48), 17f);
            var ridge = Mathf.Lerp(boundary[seam], hills, Mathf.SmoothStep(0, 1, distance / 16f));
            var lake = LakeContour(point);
            var basin = Mathf.Lerp(-1.45f, WaterHeight - .05f, Mathf.SmoothStep(0, 1, (lake - .25f) / .72f));
            var land = Mathf.Lerp(basin, ridge, Mathf.SmoothStep(0, 1, (lake - .96f) / .4f));
            foreach (var meadow in Clearings)
            {
                var offset = Vector2.Distance(point, new Vector2(meadow.x, meadow.y)) - meadow.z;
                var blend = 1f - Mathf.SmoothStep(0, 1, offset / 6f);
                land = Mathf.Lerp(land, meadow.w, blend);
            }
            return land;
        }

        private static float MeadowDistance(Vector2 point)
        {
            var best = float.PositiveInfinity;
            foreach (var meadow in Clearings)
                best = Mathf.Min(best, Vector2.Distance(point, new Vector2(meadow.x, meadow.y)) - meadow.z);
            return best;
        }

        private static float Hill(Vector2 point, Vector2 center, float radius) =>
            1f - Mathf.SmoothStep(0f, 1f, Vector2.Distance(point, center) / radius);

        private static float LakeContour(Vector2 point)
        {
            var best = float.PositiveInfinity;
            foreach (var lake in Lakes)
            {
                var offset = new Vector2((point.x - lake.x) / lake.z, (point.y - lake.y) / lake.w);
                var angle = Mathf.Atan2(offset.y, offset.x);
                best = Mathf.Min(best, offset.magnitude / (1f + .09f * Mathf.Sin(angle * 3f) + .04f * Mathf.Cos(angle * 5f)));
            }
            return best;
        }

        private static bool CellTouchesWater(WorldMapAsset map, Vector2Int cell) =>
            Mathf.Min(map.VertexHeight(cell), map.VertexHeight(cell + Vector2Int.up),
                map.VertexHeight(cell + Vector2Int.one), map.VertexHeight(cell + Vector2Int.right)) + .02f < WaterHeight;

        private static bool IsLevel(WorldMapAsset map, Vector2Int cell)
        {
            var height = map.VertexHeight(cell);
            return Mathf.Abs(height - map.VertexHeight(cell + Vector2Int.up)) < .03f
                && Mathf.Abs(height - map.VertexHeight(cell + Vector2Int.right)) < .03f
                && Mathf.Abs(height - map.VertexHeight(cell + Vector2Int.one)) < .03f;
        }

        private static float TrailDistance(Vector2 point)
        {
            var best = float.PositiveInfinity;
            foreach (var trail in Trails)
            for (var index = 1; index < trail.Length; index++)
            {
                var start = trail[index - 1];
                var delta = trail[index] - start;
                var projection = Mathf.Clamp01(Vector2.Dot(point - start, delta) / delta.sqrMagnitude);
                best = Mathf.Min(best, (point - start - delta * projection).sqrMagnitude);
            }
            return Mathf.Sqrt(best);
        }

        private static void CopyPalette(ChunkRenderer from, ChunkRenderer to)
        {
            var source = new SerializedObject(from);
            var target = new SerializedObject(to);
            foreach (var field in new[] { "grassColor", "dirtColor", "soilColor", "waterColor", "rockColor", "sandColor", "roadColor" })
                target.FindProperty(field).colorValue = source.FindProperty(field).colorValue;
            target.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Place(GridSystem grid, Transform parent, Vector2Int cell, GameObject prefab, float height, float angle)
            => Place(parent, grid.GridToWorld(cell), prefab, height, angle);

        private static void Place(Transform parent, Vector3 ground, GameObject prefab, float height, float angle)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(ground, Quaternion.Euler(0, angle, 0));
            foreach (var collider in go.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            var renderers = go.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            go.transform.localScale *= height / bounds.size.y;
            var foot = float.PositiveInfinity;
            foreach (var renderer in renderers) foot = Mathf.Min(foot, renderer.bounds.min.y);
            go.transform.position += Vector3.up * (ground.y - foot - .03f);
            // Grid movement already treats the occupied cell as solid. Leave the canopy free.
            go.isStatic = true;
        }

        private static void CreateLake(GridSystem grid, Transform parent, Material material, Vector4 lake, int index)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (var x = Mathf.FloorToInt(lake.x - lake.z - 3); x < lake.x + lake.z + 3; x++)
            for (var z = Mathf.FloorToInt(lake.y - lake.w - 3); z < lake.y + lake.w + 3; z++)
            {
                var a = Surface(grid, x, z);
                var b = Surface(grid, x, z + 1);
                var c = Surface(grid, x + 1, z + 1);
                var d = Surface(grid, x + 1, z);
                WorldLandscapeSetup.AddClippedWater(a, b, c, vertices, triangles);
                WorldLandscapeSetup.AddClippedWater(a, c, d, vertices, triangles);
            }
            var mesh = AddSurface(parent, material, index == 0 ? "North Lake" : "East Lake", vertices, triangles);
            WorldLandscapeSetup.AssignPondSurfaceData(mesh, grid);
        }

        private static void FinishScenery(GridSystem grid, Transform expansion)
        {
            var edge = expansion.Find("World edge");
            if (edge != null)
            {
                var oldMesh = edge.GetComponentInChildren<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(edge.gameObject);
                if (AssetDatabase.GetAssetPath(oldMesh) == BundlePath)
                    Object.DestroyImmediate(oldMesh, allowDestroyingAssets: true);
            }
            var water = new Material(Load<Material>(Landscape + "PondWater.mat")) { name = "Expansion Lake Water" };
            water.SetColor("_DeepColor", new Color(.08f, .31f, .38f));
            water.SetColor("_ShallowColor", new Color(.23f, .54f, .53f));
            water.SetFloat("_RippleStrength", .032f);
            AssetDatabase.AddObjectToAsset(water, BundlePath);
            expansion.Find("North Lake").GetComponent<MeshRenderer>().sharedMaterial = water;
            expansion.Find("East Lake").GetComponent<MeshRenderer>().sharedMaterial = water;
            CreateHorizon(grid, expansion);
        }

        private static void CreateHorizon(GridSystem grid, Transform expansion)
        {
            var horizon = Child(expansion, "Wooded horizon");
            var tree = Load<GameObject>(FarmPrefabs + "Tree_04.prefab");
            var pine = Load<GameObject>(Nature + "Trees/Tree_41_summer.prefab");
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var colors = new List<Color>();
            const int rings = 4;
            const int length = Max - Min;
            var random = new System.Random(26807);
            for (var side = 0; side < 4; side++)
            {
                var start = vertices.Count;
                for (var ring = 0; ring <= rings; ring++)
                for (var step = 0; step <= length; step++)
                {
                    var distance = ring == 0 ? 0f : 3f + (ring - 1) * 8f;
                    var point = HorizonPoint(side, step, distance);
                    var inner = HorizonPoint(side, step, 0f);
                    var ground = grid.GetVertexHeight(new Vector2Int(Mathf.RoundToInt(inner.x), Mathf.RoundToInt(inner.y)));
                    var noise = Mathf.PerlinNoise(point.x * .038f + 17, point.y * .038f + 22);
                    var hill = 4f + 11f * noise;
                    var height = Mathf.Lerp(ground, hill, Mathf.SmoothStep(0, 1, distance / 26f));
                    var vertex = new Vector3(point.x, height + .02f, point.y);
                    vertices.Add(vertex);
                    var nearColor = new Color(.33f, .47f, .22f);
                    var farColor = Color.Lerp(new Color(.19f, .31f, .20f), new Color(.34f, .49f, .29f), noise);
                    colors.Add(Color.Lerp(nearColor, farColor, Mathf.Clamp01(distance / 22f)));
                    var treeSpacing = ring == 1 ? 7 : 11;
                    if ((ring == 1 || ring == 2) && step > 0 && step < length && (step + side * 3) % treeSpacing == 0)
                        Place(horizon, vertex, random.Next(3) == 0 ? tree : pine,
                            3.6f + (float)random.NextDouble() * 3.2f, (float)random.NextDouble() * 360f);
                }
                for (var ring = 0; ring < rings; ring++)
                for (var step = 0; step < length; step++)
                {
                    var a = start + ring * (length + 1) + step;
                    var b = a + length + 1;
                    triangles.Add(a); triangles.Add(b); triangles.Add(b + 1);
                    triangles.Add(a); triangles.Add(b + 1); triangles.Add(a + 1);
                }
            }
            var mesh = AddSurface(horizon, Load<Material>(Landscape + "LandscapeGround.mat"), "Horizon terrain", vertices, triangles);
            mesh.SetColors(colors);
            EditorUtility.SetDirty(mesh);
        }

        private static Vector2 HorizonPoint(int side, int step, float distance)
        {
            var along = Mathf.Lerp(Min - distance, Max + distance, (float)step / (Max - Min));
            return side switch
            {
                0 => new Vector2(Min - distance, along),
                1 => new Vector2(along, Max + distance),
                2 => new Vector2(Max + distance, Min + Max - along),
                _ => new Vector2(Min + Max - along, Min - distance)
            };
        }

        private static Mesh AddSurface(Transform parent, Material material, string name, List<Vector3> vertices, List<int> triangles)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.AddObjectToAsset(mesh, BundlePath);
            var surface = Child(parent, name);
            surface.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = surface.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            surface.gameObject.isStatic = true;
            return mesh;
        }

        private static Vector3 Surface(GridSystem grid, int x, int z) =>
            new Vector3(x, grid.GetVertexHeight(new Vector2Int(x, z)) + .02f, z);

        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static T Load<T>(string path) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing asset: " + path);
    }
}
