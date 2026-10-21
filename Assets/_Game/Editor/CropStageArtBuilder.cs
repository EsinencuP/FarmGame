using System;
using System.Collections.Generic;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Farming;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MyLittleFarm.Editor
{
    /// <summary>Authors durable low-poly plant meshes and three distinct prefabs per crop, exclusively in Edit Mode.</summary>
    public static class CropStageArtBuilder
    {
        public const string Root = "Assets/_Game/Content/Art/CropStages";
        private static readonly string[] StageNames = { "Sprout", "Growing", "Mature" };
        private static readonly Color Stem = new Color(0.27f, 0.46f, 0.12f);
        private static readonly Color LeafGreen = new Color(0.27f, 0.55f, 0.16f);
        private static readonly Color YoungGreen = new Color(0.44f, 0.68f, 0.22f);
        private static readonly Color Gold = new Color(0.94f, 0.70f, 0.20f);

        [MenuItem("Tools/My Little Farm/Rebuild Crop Stage Art")]
        public static void RebuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Build crop art in Edit Mode.");
            foreach (var id in FarmCatalog.CropIds)
            {
                BuildStages(id, true);
                var definition = AssetDatabase.LoadAssetAtPath<CropDefinition>("Assets/_Game/Content/Crops/" + id + ".asset");
                if (definition == null) continue;
                var properties = new SerializedObject(definition);
                AssignStages(properties, id);
                properties.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(definition);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Crop art: rebuilt 18 stage prefabs and updated the six existing crop definitions.");
        }

        [MenuItem("Tools/My Little Farm/Rebuild Crop Stage Art", true)]
        private static bool CanRebuild() => !EditorApplication.isPlayingOrWillChangePlaymode;

        public static string PrefabPath(string id, int stage) => Root + "/" + id + "/" + id + "_" + StageNames[stage] + ".prefab";

        /// <summary>Stage envelopes preserve the existing mature tomato height and keep seedlings proportionally small.</summary>
        public static Vector3 StageSize(string id, int stage)
        {
            var height = id == "corn" ? 1.45f : id == "wheat" ? 0.95f : id == "tomato" ? 0.85f
                : id == "carrot" ? 0.35f : id == "potato" ? 0.5f : 0.45f;
            return stage == 0 ? new Vector3(0.24f, 0.14f, 0.24f)
                : stage == 1 ? new Vector3(0.46f, height * 0.54f, 0.46f)
                : new Vector3(0.7f, height, 0.7f);
        }

        /// <summary>Fills each slot with its own prefab; existing authored prefab edits survive ordinary art imports.</summary>
        public static void AssignStages(SerializedObject definition, string id)
        {
            var prefabs = BuildStages(id, false);
            definition.FindProperty("stageCount").intValue = 3;
            var stages = definition.FindProperty("stagePrefabs");
            var sizes = definition.FindProperty("stageSizes");
            stages.arraySize = sizes.arraySize = 3;
            for (var stage = 0; stage < 3; stage++)
            {
                stages.GetArrayElementAtIndex(stage).objectReferenceValue = prefabs[stage];
                sizes.GetArrayElementAtIndex(stage).vector3Value = StageSize(id, stage);
            }
        }

        private static GameObject[] BuildStages(string id, bool overwrite)
        {
            if (Array.IndexOf(FarmCatalog.CropIds, id) < 0) throw new ArgumentException("Unknown crop: " + id);
            EnsureFolder(Root + "/" + id);
            var result = new GameObject[3];
            for (var stage = 0; stage < 3; stage++)
            {
                var path = PrefabPath(id, stage);
                result[stage] = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (result[stage] != null && !overwrite) continue;
                var root = new GameObject(id + "_" + StageNames[stage]);
                try
                {
                    var source = stage == 2 ? MatureSource(id) : null;
                    if (source != null)
                    {
                        var model = UnityEngine.Object.Instantiate(source, root.transform, false);
                        model.name = "Imported mature plant";
                        foreach (var collider in model.GetComponentsInChildren<Collider>(true))
                            UnityEngine.Object.DestroyImmediate(collider);
                        VisualPrefab.FitExisting(model, StageSize(id, stage));
                    }
                    else
                    {
                        var draft = new MeshDraft();
                        if (stage == 0) Sprout(draft, id);
                        else if (id == "carrot") Carrot(draft, stage == 2);
                        else if (id == "potato") Potato(draft, stage == 2);
                        else if (id == "corn") Corn(draft, stage == 2);
                        else if (id == "cucumber") Cucumber(draft, stage == 2);
                        else if (id == "tomato") Tomato(draft);
                        else Wheat(draft);
                        var mesh = draft.ToMesh(root.name);
                        var meshPath = Root + "/" + id + "/" + root.name + ".asset";
                        var saved = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                        if (saved == null) { AssetDatabase.CreateAsset(mesh, meshPath); saved = mesh; }
                        else
                        {
                            EditorUtility.CopySerialized(mesh, saved);
                            EditorUtility.SetDirty(saved);
                            UnityEngine.Object.DestroyImmediate(mesh);
                        }
                        var model = new GameObject("Plant");
                        model.transform.SetParent(root.transform, false);
                        model.AddComponent<MeshFilter>().sharedMesh = saved;
                        var renderer = model.AddComponent<MeshRenderer>();
                        renderer.sharedMaterial = GetMaterial();
                        renderer.shadowCastingMode = ShadowCastingMode.On;
                        renderer.receiveShadows = true;
                        VisualPrefab.FitExisting(model, StageSize(id, stage));
                    }
                    result[stage] = PrefabUtility.SaveAsPrefabAsset(root, path);
                    if (result[stage] == null) throw new InvalidOperationException("Could not save crop art: " + path);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            return result;
        }

        private static GameObject MatureSource(string id)
        {
            var path = id == "tomato" ? "Assets/LowPolyFarmLite/Prefabs/TomatoPlant_01.prefab"
                : id == "wheat" ? "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Env_Wheat.prefab" : null;
            if (path == null) return null;
            return AssetDatabase.LoadAssetAtPath<GameObject>(path)
                ?? throw new InvalidOperationException("Missing mature plant reference: " + path);
        }

        private static Material GetMaterial()
        {
            var path = Root + "/Crop Foliage.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "/CropVertexColor.shader");
            if (shader == null) throw new InvalidOperationException("Import CropVertexColor.shader before building crop art.");
            material = new Material(shader) { name = "Crop Foliage", enableInstancing = true };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var split = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, split));
            AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
        }

        private static Vector3 Radial(float angle, float radius, float y) =>
            new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * radius, y, Mathf.Sin(angle * Mathf.Deg2Rad) * radius);

        private static void Sprout(MeshDraft mesh, string id)
        {
            mesh.Rod(Vector3.zero, Vector3.up * 0.07f, 0.007f, 0.004f, Stem);
            var grass = id == "corn" || id == "wheat";
            var count = grass ? 3 : 2;
            for (var i = 0; i < count; i++)
                mesh.Leaf(Vector3.up * 0.045f, Radial(i * 360f / count, grass ? 0.06f : 0.105f, grass ? 0.18f : 0.115f),
                    grass ? 0.025f : 0.065f, YoungGreen, 0.012f);
            if (!grass) mesh.Leaf(Vector3.up * 0.06f, new Vector3(0.015f, 0.145f, 0.025f), 0.035f, LeafGreen);
        }

        private static void Carrot(MeshDraft mesh, bool mature)
        {
            var count = mature ? 7 : 4;
            for (var i = 0; i < count; i++)
            {
                var start = Vector3.up * 0.025f;
                var tip = Radial(i * 360f / count, 0.22f, 0.26f + i % 2 * 0.07f);
                mesh.Rod(start, tip, 0.006f, 0.002f, Stem);
                var side = Vector3.Cross(tip.normalized, Vector3.up).normalized;
                for (var j = 1; j <= 4; j++)
                {
                    var p = Vector3.Lerp(start, tip, j * 0.17f);
                    var length = 0.06f * (1f - j * 0.12f);
                    mesh.Leaf(p, p + side * length + (tip - start).normalized * 0.04f, 0.025f, LeafGreen);
                    mesh.Leaf(p, p - side * length + (tip - start).normalized * 0.04f, 0.025f, YoungGreen);
                }
            }
            // Only the shoulder shows above soil, not an uprooted carrot floating over the bed.
            if (mature) mesh.Fruit(new Vector3(0f, 0.025f, 0f), new Vector3(0.055f, 0.025f, 0.055f), new Color(0.88f, 0.38f, 0.08f));
            else mesh.Rod(Vector3.zero, Vector3.up * 0.04f, 0.014f, 0.01f, Stem);
        }

        private static void Potato(MeshDraft mesh, bool mature)
        {
            var count = mature ? 6 : 3;
            mesh.Rod(Vector3.zero, Vector3.up * 0.26f, 0.013f, 0.006f, Stem);
            for (var i = 0; i < count; i++)
            {
                var tip = Radial(i * 360f / count, 0.23f, 0.22f + i % 3 * 0.07f);
                var start = Vector3.up * (0.07f + i % 2 * 0.07f);
                mesh.Rod(start, tip, 0.007f, 0.003f, Stem);
                mesh.Leaf(Vector3.Lerp(start, tip, 0.55f), tip + Radial(i * 360f / count, 0.07f, 0.04f), 0.135f, LeafGreen);
                var side = Radial(i * 360f / count + 90f, 0.11f, 0.045f);
                mesh.Leaf(start + (tip - start) * 0.45f, tip * 0.7f + side, 0.10f, YoungGreen);
            }
            if (mature)
                for (var i = 0; i < 3; i++) Flower(mesh, Radial(i * 120f, 0.075f, 0.38f), new Color(0.95f, 0.91f, 0.78f));
        }

        private static void Corn(MeshDraft mesh, bool mature)
        {
            var height = mature ? 1.25f : 0.65f;
            mesh.Rod(Vector3.zero, Vector3.up * height, 0.022f, 0.009f, Stem);
            var count = mature ? 8 : 5;
            for (var i = 0; i < count; i++)
            {
                var y = 0.12f + i * (height - 0.22f) / count;
                var angle = i * 145f;
                var length = mature ? 0.31f : 0.22f;
                mesh.Leaf(Vector3.up * y, Radial(angle, length, y + 0.1f), mature ? 0.09f : 0.065f,
                    i % 2 == 0 ? LeafGreen : YoungGreen, length * 0.35f);
            }
            if (!mature) return;
            for (var i = 0; i < 2; i++)
            {
                var side = i == 0 ? 1f : -1f;
                var center = new Vector3(side * 0.085f, 0.56f + i * 0.22f, 0f);
                mesh.Rod(center - Vector3.up * 0.1f, center + Vector3.up * 0.12f, 0.052f, 0.025f, Gold);
                mesh.Leaf(center - Vector3.up * 0.12f, center + new Vector3(side * 0.1f, 0.06f, 0.025f), 0.08f, LeafGreen);
                mesh.Leaf(center - Vector3.up * 0.11f, center + new Vector3(0, 0.05f, -0.065f), 0.08f, YoungGreen);
                for (var row = 0; row < 4; row++)
                    mesh.Fruit(center + new Vector3(side * 0.025f, -0.06f + row * 0.045f, 0.04f),
                        new Vector3(0.023f, 0.021f, 0.018f), Gold);
            }
            mesh.Rod(Vector3.up * height, Vector3.up * (height + 0.19f), 0.009f, 0.003f, Gold);
            for (var i = 0; i < 5; i++)
                mesh.Rod(Vector3.up * (height + 0.015f), Radial(i * 72f, 0.09f, height + 0.13f), 0.007f, 0.002f, Gold);
        }

        private static void Cucumber(MeshDraft mesh, bool mature)
        {
            var count = mature ? 8 : 4;
            for (var i = 0; i < count; i++)
            {
                var angle = i * 137f;
                var end = Radial(angle, 0.18f + i % 2 * 0.08f, 0.09f + i % 3 * 0.05f);
                mesh.Rod(Vector3.zero, end, 0.009f, 0.004f, Stem);
                mesh.Leaf(end * 0.62f, end + Radial(angle, 0.095f, 0.045f), 0.18f, i % 2 == 0 ? LeafGreen : YoungGreen, 0.015f);
            }
            if (!mature) return;
            for (var i = 0; i < 3; i++)
            {
                var center = Radial(i * 120f + 40f, 0.16f, 0.09f);
                mesh.Fruit(center, new Vector3(0.04f, 0.085f, 0.04f), new Color(0.12f, 0.35f, 0.10f));
                mesh.Rod(center + Vector3.up * 0.07f, center + Vector3.up * 0.13f, 0.005f, 0.004f, Stem);
            }
            Flower(mesh, new Vector3(0.08f, 0.23f, 0.03f), Gold);
        }

        private static void Tomato(MeshDraft mesh)
        {
            mesh.Rod(Vector3.zero, Vector3.up * 0.48f, 0.014f, 0.004f, Stem);
            for (var i = 0; i < 6; i++)
            {
                var start = Vector3.up * (0.12f + i * 0.05f);
                var end = Radial(i * 137f, 0.19f, start.y + 0.075f);
                mesh.Rod(start, end, 0.006f, 0.002f, Stem);
                mesh.Leaf(end * 0.8f + start * 0.2f, end + Radial(i * 137f, 0.06f, 0.03f), 0.08f, LeafGreen);
                for (var side = -1; side <= 1; side += 2)
                    mesh.Leaf(Vector3.Lerp(start, end, 0.5f), Vector3.Lerp(start, end, 0.8f) + Radial(i * 137f + side * 65f, 0.08f, 0.025f), 0.06f, YoungGreen);
            }
        }

        private static void Wheat(MeshDraft mesh)
        {
            for (var i = 0; i < 2; i++)
            {
                var p = new Vector3(i * 0.045f, 0f, 0f);
                mesh.Rod(p, p + Vector3.up * 0.45f, 0.005f, 0.003f, Stem);
                mesh.Leaf(p + Vector3.up * 0.12f, p + new Vector3(-0.11f, 0.32f, 0.025f), 0.024f, YoungGreen);
                mesh.Leaf(p + Vector3.up * 0.23f, p + new Vector3(0.11f, 0.39f, -0.02f), 0.024f, LeafGreen);
                mesh.Fruit(p + Vector3.up * 0.49f, new Vector3(0.025f, 0.06f, 0.02f), YoungGreen);
            }
        }

        private static void Flower(MeshDraft mesh, Vector3 center, Color color)
        {
            mesh.Rod(center - Vector3.up * 0.09f, center, 0.004f, 0.002f, Stem);
            for (var i = 0; i < 5; i++)
                mesh.Leaf(center, center + Radial(i * 72f, 0.035f, 0.007f), 0.03f, color);
            mesh.Fruit(center + Vector3.up * 0.008f, Vector3.one * 0.012f, Gold);
        }

        /// <summary>Builds closed, flat-shaded colored geometry; no primitive GameObjects or runtime generation.</summary>
        private sealed class MeshDraft
        {
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<int> _triangles = new List<int>();
            private readonly List<Color> _colors = new List<Color>();

            private void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color, Vector3 outward)
            {
                var normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude < 0.0000000001f) return;
                if (Vector3.Dot(normal, outward) < 0f) { var swap = b; b = c; c = swap; }
                var index = _vertices.Count;
                _vertices.Add(a); _vertices.Add(b); _vertices.Add(c);
                for (var i = 0; i < 3; i++) { _triangles.Add(index + i); _colors.Add(color); }
            }

            public void Rod(Vector3 start, Vector3 end, float bottomRadius, float topRadius, Color color)
            {
                var direction = (end - start).normalized;
                var right = Vector3.Cross(direction, Vector3.forward).normalized;
                if (right.sqrMagnitude < 0.01f) right = Vector3.right;
                var forward = Vector3.Cross(direction, right);
                for (var i = 0; i < 6; i++)
                {
                    var angle = i * Mathf.PI / 3f;
                    var next = (i + 1) * Mathf.PI / 3f;
                    var a = right * Mathf.Cos(angle) + forward * Mathf.Sin(angle);
                    var b = right * Mathf.Cos(next) + forward * Mathf.Sin(next);
                    Triangle(start + a * bottomRadius, start + b * bottomRadius, end + a * topRadius, color, a + b);
                    Triangle(start + b * bottomRadius, end + b * topRadius, end + a * topRadius, color, a + b);
                    Triangle(start, start + b * bottomRadius, start + a * bottomRadius, color, -direction);
                    Triangle(end, end + a * topRadius, end + b * topRadius, color, direction);
                }
            }

            public void Leaf(Vector3 start, Vector3 end, float width, Color color, float arch = 0.008f)
            {
                var direction = (end - start).normalized;
                var side = Vector3.Cross(direction, Vector3.up).normalized;
                if (side.sqrMagnitude < 0.01f) side = Vector3.right;
                var middle = Vector3.Lerp(start, end, 0.48f) + Vector3.up * arch;
                var edge = new[] { start, middle + side * width * 0.5f, end, middle - side * width * 0.5f };
                for (var i = 0; i < 4; i++)
                {
                    Triangle(edge[i], edge[(i + 1) % 4], middle + Vector3.up * 0.008f, color, Vector3.up);
                    Triangle(edge[i], middle - Vector3.up * 0.003f, edge[(i + 1) % 4], color * new Color(0.85f, 0.85f, 0.85f, 1f), Vector3.down);
                }
            }

            public void Fruit(Vector3 center, Vector3 radii, Color color)
            {
                for (var row = 0; row < 4; row++)
                for (var side = 0; side < 6; side++)
                {
                    var a = FruitPoint(row, side, radii);
                    var b = FruitPoint(row + 1, side, radii);
                    var c = FruitPoint(row + 1, side + 1, radii);
                    var d = FruitPoint(row, side + 1, radii);
                    Triangle(center + a, center + b, center + c, color, a + b + c);
                    Triangle(center + a, center + c, center + d, color, a + c + d);
                }
            }

            private static Vector3 FruitPoint(int row, int side, Vector3 radii)
            {
                var latitude = row * Mathf.PI / 4f;
                var longitude = side * Mathf.PI / 3f;
                return Vector3.Scale(radii, new Vector3(Mathf.Sin(latitude) * Mathf.Cos(longitude), Mathf.Cos(latitude), Mathf.Sin(latitude) * Mathf.Sin(longitude)));
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(_vertices); mesh.SetTriangles(_triangles, 0); mesh.SetColors(_colors);
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
