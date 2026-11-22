#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.Farming;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace MyLittleFarm.Tests.PlayMode
{
    /// <summary>Exercises real authored crop prefabs through same-frame transitions and save/restore of all growth stages.</summary>
    public sealed class CropStageViewTests : FarmPlayModeFixture
    {
        [UnityTest]
        public IEnumerator StageSwitchHidesPreviousModelImmediatelyAndRepeatedStageDoesNotRecreateIt()
        {
            foreach (var id in FarmCatalog.CropIds)
            {
                var definition = Load(id);
                var view = CropView.Create(Vector3.zero, definition, Farm.transform);
                for (var stage = 0; stage < 3; stage++)
                {
                    var previous = ActiveStage(view);
                    view.SetStage(stage, 3);
                    var current = ActiveStage(view);
                    Assert.That(current, Is.Not.Null);
                    Assert.That(current.name, Does.StartWith("Stage " + stage + " - "));
                    if (stage > 0) Assert.That(previous.activeSelf, Is.False, "Deferred Destroy must not leave two stages visible.");
                    view.SetStage(stage, 3);
                    Assert.That(ActiveStage(view), Is.SameAs(current));
                }
                UnityEngine.Object.Destroy(view.gameObject);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator AllTwentyFourAuthoredStagesSurviveSaveRestoreAndMatureHarvest()
        {
            var catalog = Farm.GetComponentInChildren<FarmCatalog>();
            var definitions = new CropDefinition[8];
            for (var i = 0; i < definitions.Length; i++) definitions[i] = Load(FarmCatalog.CropIds[i]);
            typeof(FarmCatalog).GetField("cropAssets", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(catalog, definitions);
            catalog.Configure();
            var grid = Farm.GetComponentInChildren<GridSystem>();
            var crops = Farm.GetComponentInChildren<CropSystem>();
            // Freeze visual polling: the test verifies fixed saved timestamps rather than wall-clock timing.
            crops.enabled = false;
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            for (var i = 0; i < definitions.Length; i++)
            for (var stage = 0; stage < 3; stage++)
            {
                var position = new Vector2Int(i + 1, stage + 1);
                grid.SetCellType(position, CellType.Tilled);
                var age = stage == 0 ? -60f : definitions[i].GrowDurationSeconds * (stage == 1 ? 0.65f : 1.2f);
                Assert.That(crops.Plant(position, definitions[i].CropId, now - (long)(age * 1000)), Is.True);
            }
            var save = Farm.GetComponentInChildren<SaveSystem>();
            var snapshot = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save.Capture()));
            save.Restore(snapshot);
            yield return null;
            var views = crops.GetComponentsInChildren<CropView>();
            Assert.That(views.Length, Is.EqualTo(24));
            foreach (var view in views)
            {
                var position = grid.WorldToGrid(view.transform.position);
                var stage = position.y - 1;
                Assert.That(ActiveStage(view).name, Does.StartWith("Stage " + stage + " - "));
                var ground = grid.GetSurfaceHeight(view.transform.position.x, view.transform.position.z);
                // Renderer bounds include extra culling margin for shader-based crop sway.
                var activeStage = ActiveStage(view);
                foreach (var renderer in activeStage.GetComponentsInChildren<MeshRenderer>()) renderer.ResetLocalBounds();
                Assert.That(VisualPrefab.TryGetBounds(activeStage, out var meshBounds), Is.True);
                Assert.That(meshBounds.min.y, Is.EqualTo(ground).Within(0.001f));
            }
            for (var i = 0; i < definitions.Length; i++)
            {
                Assert.That(crops.TryHarvest(new Vector2Int(i + 1, 3), now, out var item, out var amount), Is.True);
                Assert.That(item, Is.EqualTo(definitions[i].HarvestItemId));
                Assert.That(amount, Is.EqualTo(definitions[i].YieldAmount));
                Assert.That(crops.TryHarvest(new Vector2Int(i + 1, 1), now, out _, out _), Is.False);
            }
        }

        private static CropDefinition Load(string id)
        {
            var definition = AssetDatabase.LoadAssetAtPath<CropDefinition>((id=="strawberry"||id=="pumpkin"?"Assets/_Game/Resources/FarmContent/":"Assets/_Game/Content/Crops/") + id + ".asset");
            Assert.That(definition, Is.Not.Null);
            return definition;
        }

        private static GameObject ActiveStage(CropView view)
        {
            GameObject active = null;
            foreach (Transform child in view.transform)
            {
                if (!child.gameObject.activeSelf) continue;
                Assert.That(active, Is.Null, "Only one growth stage may be active.");
                active = child.gameObject;
            }
            return active;
        }
    }
}
#endif
