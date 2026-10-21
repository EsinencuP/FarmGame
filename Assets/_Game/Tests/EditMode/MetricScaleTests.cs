using System.Linq;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Editor;
using MyLittleFarm.Gameplay.Building;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MyLittleFarm.Tests.EditMode
{
    /// <summary>Checks real calibrated doors, footprint reservations, local collider fitting and the new Humanoid library.</summary>
    public sealed class MetricScaleTests
    {
        [TestCase("house")]
        [TestCase("storage")]
        [TestCase("coop")]
        [TestCase("market")]
        [TestCase("flowerbed")]
        public void CalibratedArtFitsItsFootprintAndColliderAtEveryRotation(string id)
        {
            var parent = new GameObject("Metric art verification");
            var grid = GridSystem.Instance;
            GameObject gridObject = null;
            if (grid == null) { gridObject = new GameObject("Test grid"); grid = gridObject.AddComponent<GridSystem>(); }
            try
            {
                var definition = BuildingDefinition.Catalog.First(d => d.Id == id);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MetricScaleSetup.Folder + "/" + id + ".prefab");
                Assert.That(prefab, Is.Not.Null);
                Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
                var state = new BuildingRuntimeState { id="metric",definitionId=id };
                var view = BuildingView.Create(parent.transform,definition,state,grid,prefab);
                for (var turns=0;turns<4;turns++)
                {
                    state.quarterTurns=turns; view.Apply(state,definition,grid);
                    Assert.That(VisualPrefab.TryGetBounds(view.gameObject,out var bounds),Is.True);
                    Assert.That(bounds.min.y,Is.EqualTo(0f).Within(0.001f));
                    Assert.That(bounds.size.x,Is.LessThan(definition.RotatedWidth(turns)));
                    Assert.That(bounds.size.z,Is.LessThan(definition.RotatedDepth(turns)));
                    Assert.That(bounds.size.y,Is.LessThanOrEqualTo(definition.Height));
                    var collider=view.GetComponent<BoxCollider>(); Physics.SyncTransforms();
                    Assert.That(Vector3.Distance(collider.bounds.center,bounds.center),Is.LessThan(0.001f));
                    Assert.That(Vector3.Distance(collider.bounds.size,bounds.size),Is.LessThan(0.001f));
                    var metrics=view.GetComponentInChildren<BuildingArtMetrics>();
                    if(id=="house"||id=="storage"||id=="coop")
                        Assert.That(metrics.DoorHeight,Is.EqualTo(1.98f).Within(0.001f));
                }
                parent.transform.rotation=Quaternion.Euler(0,25,0); parent.transform.localScale=new Vector3(1.2f,0.8f,1.4f);
                Assert.That(VisualPrefab.TryGetLocalBounds(view.gameObject,view.transform,out var local),Is.True);
                Assert.That(Vector3.Distance(view.GetComponent<BoxCollider>().size,local.size),Is.LessThan(0.001f));
            }
            finally { Object.DestroyImmediate(parent); if(gridObject!=null)Object.DestroyImmediate(gridObject); }
        }

        [Test]
        public void KevinCharacterHasMetricHeightAndAllThirtyFourInPlaceClips()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PlayerRigSetup.PrefabPath);
            Assert.That(prefab,Is.Not.Null);
            var instance=Object.Instantiate(prefab);
            try
            {
                var animator=instance.GetComponentInChildren<Animator>();
                Assert.That(animator.avatar.isHuman && animator.avatar.isValid,Is.True);
                Assert.That(animator.applyRootMotion,Is.False);
                animator.Rebind();animator.Update(0f);
                Assert.That(PlayerRigSetup.BakedBounds(instance).size.y,Is.EqualTo(1.8f).Within(0.002f));
                var controller=(AnimatorController)animator.runtimeAnimatorController;
                var clips=controller.animationClips.Distinct().ToArray();
                Assert.That(clips.Length,Is.EqualTo(34));
                Assert.That(clips.All(c=>!c.name.Contains("[RM]")),Is.True);
                Assert.That(clips.Count(c=>c.name.Contains("Walk01_")),Is.EqualTo(8));
                Assert.That(clips.Count(c=>c.name.Contains("Run01_")),Is.EqualTo(8));
                Assert.That(clips.Count(c=>c.name.Contains("Sprint01_")),Is.EqualTo(5));
                Assert.That(controller.layers.Length,Is.EqualTo(2));
            }
            finally{Object.DestroyImmediate(instance);}
        }
    }
}
