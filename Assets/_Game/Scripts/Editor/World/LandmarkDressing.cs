using System;
using System.Collections.Generic;
using System.Linq;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MyLittleFarm.Editor
{
    /// <summary>Authors renderer-only landmark groups in the existing landscape.</summary>
    public static class LandmarkDressing
    {
        private const int Seed = 29021;
        private const string GroupName = "RC-02 Landmarks";
        private const string Landscape = "Assets/_Game/Content/World/Landscape/";
        private const string Nature = "Assets/ThirdParty/Low_Poly_Nature_Pack_Lite/Prefabs/";

        [MenuItem("Tools/My Little Farm/Landscape/Bake Landmark Reference")]
        public static void BakeReference() => BakeScene(false);

        [MenuItem("Tools/My Little Farm/Landscape/Bake Landmark Map Dressing")]
        public static void BakeMap() => BakeScene(true);

        private static void BakeScene(bool fullMap)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/_Game/Scenes/Prototype.unity")
                throw new InvalidOperationException("Open Prototype in Edit Mode before baking the landmarks.");
            var farm = Object.FindAnyObjectByType<GameBootstrap>();
            var landscape = farm.transform.Find("Authored Landscape");
            var pond = landscape.Find("Pond Water — fixed shoreline").GetComponent<MeshFilter>().sharedMesh;
            var grid = farm.GetComponentInChildren<GridSystem>();
            grid.RegisterSceneChunks(farm.GetComponentsInChildren<TilemapChunk>(true));
            var original = landscape.Find(GroupName);
            var bake = new Bake(grid, farm.transform, original, pond.bounds.min.y);
            var group = Child(landscape, GroupName);
            try
            {
                bake.DressPond(Child(group, "West Pond"));
                bake.DressForest(Child(group, "North Woods"));
                bake.DressHill(Child(group, "East Hill"));
                if (fullMap)
                {
                    bake.DressMapEdges(Child(group, "Trailside groups"));
                    AddBreeze(group);
                }
            }
            catch
            {
                Object.DestroyImmediate(group.gameObject);
                throw;
            }

            Undo.SetCurrentGroupName("Bake Landmark Reference");
            if (original != null) Undo.DestroyObjectImmediate(original.gameObject);
            Undo.RegisterCreatedObjectUndo(group.gameObject, "Bake Landmark Reference");
            Undo.RecordObject(pond, "Bake pond depth");
            WorldLandscapeSetup.AssignPondSurfaceData(pond, grid);
            AssetDatabase.SaveAssetIfDirty(pond);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"Landmark dressing seed {Seed}: {group.GetComponentsInChildren<MeshRenderer>().Length} renderers. Base map {grid.ComputeBaseMapHash()}.");
        }

        private sealed class Bake
        {
            private readonly GridSystem _grid;
            private readonly float _waterY;
            private readonly System.Random _random = new System.Random(Seed);
            private readonly Vector2[] _interactionPoints;
            private readonly Vector2[] _existingProps;
            private readonly List<(Vector2 Position, float Spacing)> _anchors = new List<(Vector2, float)>();
            private readonly Vector2[] _cachePositions;

            public Bake(GridSystem grid, Transform farm, Transform oldGroup, float waterY)
            {
                _grid = grid;
                _waterY = waterY;
                _interactionPoints = farm.GetComponentsInChildren<InteractionTarget>(true)
                    .Select(t => XZ(t.transform.position)).ToArray();
                var caches = farm.GetComponentsInChildren<RareSeedCache>(true);
                _cachePositions = FarmProgression_AI.CacheIds.Select(id => XZ(caches.Single(c => c.Id == id).transform.position)).ToArray();
                _existingProps = farm.GetComponentsInChildren<MeshRenderer>(true)
                    .Where(r => (oldGroup == null || !r.transform.IsChildOf(oldGroup)) &&
                        (r.name.StartsWith("Boundary Tree", StringComparison.Ordinal) ||
                         r.name.StartsWith("Crown Shrub", StringComparison.Ordinal) ||
                         r.name.StartsWith("Landscape Rock", StringComparison.Ordinal)))
                    .Select(r => XZ(r.transform.position)).ToArray();
            }

            public void DressPond(Transform group)
            {
                var reeds = ReedClump();
                var centers = new[] { new Vector2(-25.3f, 8.7f), new Vector2(-24.4f, 12.4f),
                    new Vector2(-20.7f, 14.7f), new Vector2(-16.1f, 13.1f),
                    new Vector2(-14.2f, 10.3f), new Vector2(-16.5f, 6.0f),
                    new Vector2(-20.9f, 4.2f), new Vector2(-23.7f, 6.5f) };
                foreach (var center in centers)
                    Scatter(group, reeds, center, new Vector2(1.2f, 1.1f), 2, .65f, 1.05f, .72f, true);
            }

            public void DressForest(Transform group)
            {
                var pine = Pine();
                var broadleaf = Model.Load("Assets/ThirdParty/LowPolyFarmLite/Prefabs/Tree_04.prefab");
                var shrub = Shrub();
                var fern = Fern();
                var groves = new[] { new Vector2(-7.4f, 30.5f), new Vector2(4.8f, 32.3f), new Vector2(-4.4f, 36.8f) };
                foreach (var center in groves)
                {
                    Scatter(group, pine, center, new Vector2(3.2f, 2.2f), 4, 3.7f, 5.8f, 1.9f);
                    Scatter(group, broadleaf, center, new Vector2(3.4f, 2.3f), 2, 3.1f, 4.2f, 1.7f);
                    Scatter(group, shrub, center, new Vector2(3.9f, 2.6f), 5, .42f, .75f, .55f);
                    Scatter(group, fern, center, new Vector2(3.8f, 2.8f), 3, .24f, .40f, .45f);
                }
            }

            public void DressHill(Transform group)
            {
                var stone = Model.Load(Nature + "Stones/Stone_5_moss.prefab");
                stone = stone.WithMaterial(0, Palette("Landmark Moss Stone", stone.Materials[0], new Color(.88f, .89f, .81f)));
                var shrub = Shrub();
                var center = new Vector2(32.8f, 23.0f);
                if (!Allowed(center, 1.25f, false))
                    throw new InvalidOperationException("The cairn site overlaps protected terrain or an approach.");
                var cairn = Child(group, "Stone cairn");
                var basePoint = Ground(center);
                Put(cairn, stone, basePoint, 1.37f, 33, true);
                Put(cairn, stone, basePoint + new Vector3(-.08f, 1.03f, .08f), .99f, 107, true);
                Put(cairn, stone, basePoint + new Vector3(.01f, 1.77f, .08f), .64f, 61, true);
                _anchors.Add((center, 1.2f));
                Scatter(group, shrub, new Vector2(34.7f, 24.4f), new Vector2(1.7f, 1.7f), 6, .32f, .65f, .55f);
                Scatter(group, stone, new Vector2(34.3f, 25.1f), new Vector2(1.7f, 1.3f), 2, .32f, .58f, .5f);
            }

            public void DressMapEdges(Transform group)
            {
                var shrub = Shrub();
                var fern = Fern();
                var pine = Pine();
                var grass = Model.Load("Assets/ThirdParty/LowPolyFarmLite/Prefabs/Grass_02.prefab");
                var banks = new[] { new Vector2(-19, -20), new Vector2(-3.5f, -20), new Vector2(13, -20),
                    new Vector2(28.5f, -19), new Vector2(-21.5f, 22.0f), new Vector2(-16.9f, 26.3f),
                    new Vector2(30.5f, -7.5f), new Vector2(30.0f, 4.5f), new Vector2(31.3f, 13.8f),
                    new Vector2(12.0f, 30.2f), new Vector2(19.2f, 31.9f), new Vector2(26.0f, 33.5f) };
                for (var i = 0; i < banks.Length; i++)
                {
                    var bank = Child(group, "Trailside pocket " + (i + 1));
                    Scatter(bank, shrub, banks[i], new Vector2(2.4f, 1.5f), 2, .34f, .70f, .58f);
                    Scatter(bank, i % 2 == 0 ? grass : fern, banks[i], new Vector2(2.6f, 1.7f), 2, .24f, .42f, .44f);
                    if (i >= 9)
                        Scatter(bank, pine, banks[i] + Vector2.up, new Vector2(2.4f, 1.35f), 2, 3.3f, 4.6f, 1.8f);
                }
            }

            private void Scatter(Transform parent, Model model, Vector2 center, Vector2 spread,
                int count, float low, float high, float clearance, bool water = false)
            {
                var attemptLimit = count * 30;
                for (var attempt = 0; attempt < attemptLimit && count > 0; attempt++)
                {
                    var p = center + new Vector2(Range(-spread.x, spread.x), Range(-spread.y, spread.y));
                    var height = Range(low, high);
                    var radius = model.Radius * (height / model.Mesh.bounds.size.y);
                    if (!Allowed(p, radius + .09f, water)) continue;
                    var spacing = clearance * .5f;
                    if (_anchors.Any(q => (p - q.Position).sqrMagnitude < Mathf.Pow(spacing + q.Spacing, 2))) continue;
                    _anchors.Add((p, spacing));
                    Put(parent, model, Ground(p), height, Range(0, 360), height >= .8f);
                    count--;
                }
            }

            private bool Allowed(Vector2 p, float radius, bool water)
            {
                var cell = new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));
                var type = _grid.GetTerrainCell(cell).type;
                if (water)
                {
                    var depth = _waterY - Ground(p).y;
                    if (type != CellType.Water || depth < .015f || depth > .29f) return false;
                }
                else if (type != CellType.Grass || !_grid.IsWalkable(cell)) return false;

                for (var z = Mathf.FloorToInt(p.y - radius); z <= Mathf.FloorToInt(p.y + radius); z++)
                for (var x = Mathf.FloorToInt(p.x - radius); x <= Mathf.FloorToInt(p.x + radius); x++)
                {
                    var candidate = new Vector2Int(x, z);
                    var terrain = _grid.GetTerrainCell(candidate);
                    if (terrain.type == CellType.Road || terrain.type == CellType.OutOfBounds || terrain.type == CellType.Locked
                        || terrain.Allows(CellPermissions.Farm, terrain.type == CellType.Grass || terrain.type == CellType.Dirt)
                        || terrain.Allows(CellPermissions.Build, terrain.type == CellType.Grass || terrain.type == CellType.Dirt)
                        || terrain.Allows(CellPermissions.Orchard, terrain.type == CellType.Grass || terrain.type == CellType.Sand)
                        || _grid.GetFarmingCell(candidate).soilType != 0 || _grid.GetBuildingCell(candidate).type != 0) return false;
                }
                if (_interactionPoints.Any(q => Vector2.Distance(p, q) < 2.65f + radius)
                    || _existingProps.Any(q => Vector2.Distance(p, q) < .85f + radius * .45f)) return false;
                return ClearOfApproach(p, new Vector2(-11.5f, 19.5f), _cachePositions[0], radius)
                    && ClearOfApproach(p, new Vector2(-1.5f, 24.5f), _cachePositions[1], radius)
                    && ClearOfApproach(p, new Vector2(24.5f, 20.5f), _cachePositions[2], radius);
            }

            private Vector3 Ground(Vector2 p) => new Vector3(p.x, _grid.GetSurfaceHeight(p.x, p.y) + .02f, p.y);
            private float Range(float low, float high) => Mathf.Lerp(low, high, (float)_random.NextDouble());
        }

        private static bool ClearOfApproach(Vector2 p, Vector2 from, Vector2 to, float radius)
        {
            var segment = to - from;
            var t = Mathf.Clamp01(Vector2.Dot(p - from, segment) / segment.sqrMagnitude);
            return Vector2.Distance(p, from + t * segment) > 1.25f + radius;
        }

        private readonly struct Model
        {
            public readonly Mesh Mesh;
            public readonly Material[] Materials;
            public float Radius => new Vector2(Mesh.bounds.extents.x, Mesh.bounds.extents.z).magnitude;

            public Model(Mesh mesh, Material[] materials) { Mesh = mesh; Materials = materials; }

            public Model WithMaterial(int index, Material material)
            {
                var materials = (Material[])Materials.Clone();
                materials[index] = material;
                return new Model(Mesh, materials);
            }

            public static Model Load(string path)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var filter = asset == null ? null : asset.GetComponentInChildren<MeshFilter>(true);
                if (filter == null || filter.sharedMesh == null) throw new InvalidOperationException("Missing landmark model: " + path);
                return new Model(filter.sharedMesh, filter.GetComponent<MeshRenderer>().sharedMaterials);
            }
        }

        private static Model Shrub()
        {
            var model = Model.Load(Nature + "Bushes/Bush_2_summer.prefab");
            return model.WithMaterial(0, Palette("Landmark Shrub", model.Materials[0], new Color(.76f, .81f, .65f)));
        }

        private static Model Pine()
        {
            var model = Model.Load("Assets/ThirdParty/Imported%20Models/FBX (Unity)/Pine_3.fbx");
            return model.WithMaterial(1, Palette("Landmark Pine", model.Materials[1], new Color(.93f, .95f, .87f), true));
        }

        private static Model Fern()
        {
            var model = Model.Load("Assets/ThirdParty/Imported%20Models/FBX (Unity)/Fern_1.fbx");
            return model.WithMaterial(0, Palette("Landmark Fern", model.Materials[0], new Color(.80f, .90f, .71f), true));
        }

        private static void AddBreeze(Transform group)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(Landscape + "LandmarkBreeze.shader");
            if (shader == null) throw new InvalidOperationException("LandmarkBreeze shader is missing.");
            var reedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Landscape + "PondReeds.mesh.asset");
            var materials = new Dictionary<string, Material>();
            foreach (var renderer in group.GetComponentsInChildren<MeshRenderer>())
            {
                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                var reeds = mesh == reedMesh;
                if (!reeds && mesh.name != "Fern_1") continue;
                if (!materials.TryGetValue(mesh.name, out var material))
                {
                    var name = reeds ? "Landmark Reeds Breeze" : "Landmark Fern Breeze";
                    var path = Landscape + name + ".mat";
                    material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material == null)
                    {
                        material = new Material(shader) { name = name };
                        material.SetFloat("_RootY", mesh.bounds.min.y);
                        material.SetFloat("_HeightInv", 1f / mesh.bounds.size.y);
                        material.SetFloat("_BendMeters", reeds ? .07f : .028f);
                        material.SetFloat("_VertexPalette", reeds ? 1f : 0f);
                        if (!reeds)
                        {
                            material.mainTexture = renderer.sharedMaterial.mainTexture;
                            material.SetColor("_Tint", new Color(.39f, .54f, .26f));
                            material.SetOverrideTag("RenderType", "TransparentCutout");
                            material.renderQueue = (int)RenderQueue.AlphaTest;
                            material.EnableKeyword("_ALPHATEST_ON");
                        }
                        AssetDatabase.CreateAsset(material, path);
                    }
                    materials.Add(mesh.name, material);
                }
                renderer.sharedMaterial = material;
            }
        }

        private static Material Palette(string name, Material source, Color tint, bool cutout = false)
        {
            var path = Landscape + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(source) { name = name, color = tint };
            if (cutout)
            {
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.SetFloat("_Mode", 1);
                material.SetFloat("_Cutoff", .58f);
                material.SetFloat("_SrcBlend", (float)BlendMode.One);
                material.SetFloat("_DstBlend", (float)BlendMode.Zero);
                material.SetFloat("_ZWrite", 1);
                material.EnableKeyword("_ALPHATEST_ON");
                material.renderQueue = (int)RenderQueue.AlphaTest;
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void Put(Transform parent, Model model, Vector3 basePoint, float height, float yaw, bool shadows)
        {
            var bounds = model.Mesh.bounds;
            var scale = height / bounds.size.y;
            var rotation = Quaternion.Euler(0, yaw, 0);
            var instance = Child(parent, model.Mesh.name);
            var pivot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            instance.SetPositionAndRotation(basePoint - rotation * (pivot * scale), rotation);
            instance.localScale = Vector3.one * scale;
            instance.gameObject.AddComponent<MeshFilter>().sharedMesh = model.Mesh;
            var renderer = instance.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = model.Materials;
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }

        private static Model ReedClump()
        {
            var path = Landscape + "PondReeds.mesh.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                var vertices = new List<Vector3>();
                var triangles = new List<int>();
                var colors = new List<Color32>();
                var stem = new Color(.34f, .43f, .19f);
                var leaf = new Color(.43f, .54f, .23f);
                var head = new Color(.31f, .20f, .12f);
                for (var i = 0; i < 6; i++)
                {
                    var angle = i * 2.39996f;
                    var direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                    var stalk = direction * (.12f + .025f * i);
                    var height = .59f + .065f * ((i * 3) % 5);
                    Tube(stalk, stalk + Vector3.up * height, .013f, .009f, stem);
                    if (i % 2 == 0)
                        Tube(stalk + Vector3.up * (height - .12f), stalk + Vector3.up * (height + .04f), .031f, .023f, head);
                    var sideways = Vector3.Cross(Vector3.up, direction);
                    var bend = stalk + direction * .15f + Vector3.up * (height * .58f);
                    var tip = stalk + direction * (.24f + .02f * (i % 3)) + Vector3.up * (height * .8f);
                    var start = vertices.Count;
                    vertices.AddRange(new[] { stalk, bend - sideways * .04f, tip, bend + sideways * .04f });
                    for (var j = 0; j < 4; j++) colors.Add(leaf);
                    triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                    start = vertices.Count;
                    vertices.AddRange(new[] { stalk, bend + sideways * .04f, tip, bend - sideways * .04f });
                    for (var j = 0; j < 4; j++) colors.Add(leaf);
                    triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                }
                mesh = new Mesh { name = "Pond reeds" };
                mesh.SetVertices(vertices);
                mesh.SetColors(colors);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, path);

                void Tube(Vector3 bottom, Vector3 top, float bottomRadius, float topRadius, Color32 color)
                {
                    const int sides = 6;
                    var first = vertices.Count;
                    for (var s = 0; s < sides; s++)
                    {
                        var angle = s * Mathf.PI * 2 / sides;
                        var d = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                        vertices.Add(bottom + d * bottomRadius);
                        vertices.Add(top + d * topRadius);
                        colors.Add(color);
                        colors.Add(color);
                    }
                    for (var s = 0; s < sides; s++)
                    {
                        var a = first + 2 * s;
                        var b = first + 2 * ((s + 1) % sides);
                        triangles.AddRange(new[] { a, a + 1, b + 1, a, b + 1, b });
                        if (s > 0 && s < sides - 1) triangles.AddRange(new[] { first + 1, b + 1, a + 1 });
                    }
                }
            }
            return new Model(mesh, new[] { AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Content/Art/CropStages/Crop Foliage.mat") });
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static Vector2 XZ(Vector3 p) => new Vector2(p.x, p.z);
    }
}
