using System.Collections.Generic;
using MyLittleFarm.Core;
using MyLittleFarm.Editor;
using MyLittleFarm.Gameplay.Farming;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MyLittleFarm.Tests.EditMode
{
    /// <summary>Validates the actual saved plant assets, stage proportions, ground contact and import assignments.</summary>
    public sealed class CropStageArtTests
    {
        [TestCase("carrot")]
        [TestCase("potato")]
        [TestCase("wheat")]
        [TestCase("tomato")]
        [TestCase("corn")]
        [TestCase("cucumber")]
        public void EachCropHasThreeDistinctGroundedStagesWithIncreasingHeight(string id)
        {
            var definition = AssetDatabase.LoadAssetAtPath<CropDefinition>("Assets/_Game/Content/Crops/" + id + ".asset");
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.StageCount, Is.EqualTo(3));
            Assert.That(definition.StagePrefabs.Length, Is.EqualTo(3));
            var references = new HashSet<GameObject>();
            var meshes = new HashSet<Mesh>();
            var parent = new GameObject("Crop stage art test");
            var previousHeight = 0f;
            try
            {
                for (var stage = 0; stage < 3; stage++)
                {
                    var prefab = definition.StagePrefabs[stage];
                    Assert.That(prefab, Is.Not.Null);
                    Assert.That(references.Add(prefab), Is.True, "Each stage needs its own prefab.");
                    Assert.That(AssetDatabase.GetAssetPath(prefab), Is.EqualTo(CropStageArtBuilder.PrefabPath(id, stage)));
                    Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
                    Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
                    var instance = VisualPrefab.Create(prefab, parent.transform, definition.StageSize(stage));
                    try
                    {
                        Assert.That(VisualPrefab.TryGetBounds(instance, out var bounds), Is.True);
                        Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.001f));
                        Assert.That(bounds.size.x, Is.LessThanOrEqualTo(0.701f));
                        Assert.That(bounds.size.z, Is.LessThanOrEqualTo(0.701f));
                        Assert.That(bounds.size.y, Is.GreaterThan(previousHeight * 1.25f));
                        Assert.That(bounds.size.y, Is.LessThanOrEqualTo(definition.StageSize(stage).y + 0.001f));
                        previousHeight = bounds.size.y;
                        foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
                        {
                            foreach (var material in renderer.sharedMaterials)
                            {
                                Assert.That(material, Is.Not.Null);
                                Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
                            }
                        }
                        var filter = instance.GetComponentInChildren<MeshFilter>();
                        Assert.That(filter, Is.Not.Null);
                        Assert.That(meshes.Add(filter.sharedMesh), Is.True, "A small mature plant is not a separate growth-stage model.");
                        if (AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith(CropStageArtBuilder.Root))
                            AssertValidAuthoredMesh(filter.sharedMesh);
                    }
                    finally { Object.DestroyImmediate(instance); }
                }
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void ImportAssignmentsReuseSavedStagesWithoutReplacingThemWithMatureArt()
        {
            foreach (var id in FarmCatalog.CropIds)
            {
                var temporary = ScriptableObject.CreateInstance<CropDefinition>();
                try
                {
                    var before = new string[3];
                    for (var stage = 0; stage < 3; stage++)
                        before[stage] = AssetDatabase.AssetPathToGUID(CropStageArtBuilder.PrefabPath(id, stage));
                    var properties = new SerializedObject(temporary);
                    CropStageArtBuilder.AssignStages(properties, id);
                    properties.ApplyModifiedPropertiesWithoutUndo();
                    for (var stage = 0; stage < 3; stage++)
                    {
                        Assert.That(temporary.StagePrefabs[stage], Is.EqualTo(
                            AssetDatabase.LoadAssetAtPath<GameObject>(CropStageArtBuilder.PrefabPath(id, stage))));
                        Assert.That(AssetDatabase.AssetPathToGUID(CropStageArtBuilder.PrefabPath(id, stage)), Is.EqualTo(before[stage]));
                    }
                }
                finally { Object.DestroyImmediate(temporary); }
            }
        }

        [Test]
        public void MatureTomatoRetainsTheReferenceModelAndDimensions()
        {
            var definition = AssetDatabase.LoadAssetAtPath<CropDefinition>("Assets/_Game/Content/Crops/tomato.asset");
            var reference = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LowPolyFarmLite/Prefabs/TomatoPlant_01.prefab");
            Assert.That(definition.StagePrefabs[2].GetComponentInChildren<MeshFilter>().sharedMesh,
                Is.EqualTo(reference.GetComponentInChildren<MeshFilter>().sharedMesh));
            Assert.That(definition.StageSize(2), Is.EqualTo(new Vector3(0.7f, 0.85f, 0.7f)));
        }

        private static void AssertValidAuthoredMesh(Mesh mesh)
        {
            Assert.That(mesh.vertexCount, Is.GreaterThan(0).And.LessThan(8000));
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            Assert.That(mesh.colors.Length, Is.EqualTo(vertices.Length));
            for (var i = 0; i < triangles.Length; i += 3)
            {
                var normal = Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]],
                    vertices[triangles[i + 2]] - vertices[triangles[i]]);
                Assert.That(float.IsNaN(normal.sqrMagnitude) || float.IsInfinity(normal.sqrMagnitude), Is.False);
                Assert.That(normal.sqrMagnitude, Is.GreaterThan(0.00000000009f), "Degenerate triangle in " + mesh.name);
            }
        }
    }
}
