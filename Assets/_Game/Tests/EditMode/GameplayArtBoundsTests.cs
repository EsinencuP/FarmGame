using MyLittleFarm.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MyLittleFarm.Tests.EditMode
{
    /// <summary>Checks imported building fit against saved footprints, including rotated narrow models.</summary>
    public sealed class GameplayArtBoundsTests
    {
        [TestCase("Bld_FarmerHouse", 1.88f, 2.8f, 1.88f)]
        [TestCase("Bld_Barn_01", 1.88f, 1.8f, 0.88f)]
        [TestCase("Bld_StoreBuilding_01", 0.88f, 1.6f, 1.88f)]
        [TestCase("Bld_ChickenCoop", 1.88f, 1.7f, 1.88f)]
        public void ImportedBuildingKeepsGroundContactAndFitsReservedCells(string model, float width, float height, float depth)
        {
            var parent = new GameObject("Art bounds verification");
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/" + model + ".prefab");
                Assert.That(prefab, Is.Not.Null);
                var visual = VisualPrefab.CreateBuilding(prefab, parent.transform, new Vector3(width,height,depth));
                Assert.That(VisualPrefab.TryGetBounds(visual, out var bounds), Is.True);
                Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.001f));
                Assert.That(bounds.size.x, Is.LessThanOrEqualTo(width + 0.001f));
                Assert.That(bounds.size.z, Is.LessThanOrEqualTo(depth + 0.001f));
                Assert.That(bounds.size.y, Is.EqualTo(height).Within(0.001f));
                foreach (var collider in visual.GetComponentsInChildren<Collider>()) Assert.That(collider.enabled, Is.False);
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void FlowerbedKeepsGroundContact()
        {
            var parent = new GameObject("Flowerbed ground verification");
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Nicrom/Shaders/Wind/Prefabs/LPW_Flower_A1_H70cm_01.prefab");
                Assert.That(prefab, Is.Not.Null);
                var visual = VisualPrefab.CreateBuilding(prefab, parent.transform, new Vector3(0.88f, 0.45f, 0.88f));
                Assert.That(VisualPrefab.TryGetBounds(visual, out var bounds), Is.True);
                Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.001f));
            }
            finally { Object.DestroyImmediate(parent); }
        }
    }
}
