#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MyLittleFarm.Tests.PlayMode
{
    /// <summary>Exercises real multi-material ghost prefabs, input routing and the independent construction card.</summary>
    public sealed class BuildPreviewUiTests : FarmPlayModeFixture
    {
        private InputReader _input;
        private BuildSystem _build;
        private Camera _camera;
        private GridSystem _grid;
        private void Setup()
        {
            _input=Farm.GetComponent<InputReader>();_input.enabled=false;
            _build=Farm.GetComponentInChildren<BuildSystem>();_grid=Farm.GetComponentInChildren<GridSystem>();
            _camera=Farm.GetComponentInChildren<Camera>();
            var serialized=new SerializedObject(_build);var array=serialized.FindProperty("visualPrefabs");array.arraySize=BuildingDefinition.Catalog.Count;
            for(var i=0;i<array.arraySize;i++)
            {
                var entry=array.GetArrayElementAtIndex(i);var id=BuildingDefinition.Catalog[i].Id;
                entry.FindPropertyRelative("definitionId").stringValue=id;
                entry.FindPropertyRelative("prefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Content/Art/Buildings/"+id+".prefab");
            }
            serialized.ApplyModifiedProperties();
            Farm.GetComponentInChildren<WalletSystem>().SetCoins(100);
            _build.SetActive(true);Point(new Vector2Int(0,3));
        }
        private void Set(string name,object value)=>typeof(InputReader).GetProperty(name).SetValue(_input,value);
        private void Point(Vector2Int cell){Set("HasPointer",true);Set("PointerPosition",(Vector2)_camera.WorldToScreenPoint(_grid.CellToWorld(cell)));}
        private Transform Ghost=>_build.transform.Find("Building model preview");

        [UnityTest]
        public IEnumerator AllFiveGhostsMatchArtAndTintEverySubmeshWithoutBlockingPlacement()
        {
            Setup();
            for(var i=0;i<BuildingDefinition.Catalog.Count;i++)
            {
                Set("BuildSelection",i);yield return null;Set("BuildSelection",-1);yield return new WaitForSeconds(.12f);
                var ghost=Ghost;Assert.That(ghost.gameObject.activeSelf,Is.True);
                Assert.That(_build.CanPlace(BuildingDefinition.Catalog[i].Id,new Vector2Int(0,3),0,null,out var reason),Is.True,reason);
                var renderers=ghost.GetComponentsInChildren<Renderer>();Assert.That(renderers,Is.Not.Empty);
                foreach(var renderer in renderers)
                {
                    Assert.That(renderer.shadowCastingMode,Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
                    foreach(var material in renderer.sharedMaterials)
                    {
                        Assert.That(material.shader.name,Is.EqualTo("MyLittleFarm/BuildingGhost"));
                        Assert.That(material.color.a,Is.InRange(.1f,.8f));
                        Assert.That(material.color.g,Is.GreaterThan(material.color.r));
                    }
                }
                Assert.That(ghost.GetComponentsInChildren<Collider>(true).All(c=>!c.enabled),Is.True);
                var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Content/Art/Buildings/"+BuildingDefinition.Catalog[i].Id+".prefab");
                var reference=Object.Instantiate(source);VisualPrefab.TryGetBounds(reference,out var expected);Object.Destroy(reference);
                VisualPrefab.TryGetBounds(ghost.gameObject,out var actual);
                Assert.That(Vector3.Distance(expected.size,actual.size),Is.LessThan(.002f));
                Point(new Vector2Int(100,100));yield return null;
                Assert.That(ghost.gameObject.activeSelf,Is.False);
                Point(new Vector2Int(0,3));
            }
            _build.SetActive(false);Assert.That(Ghost.gameObject.activeSelf,Is.False);
        }

        [UnityTest]
        public IEnumerator BlockedTintRotationAndCancelKeepUiCameraAndEconomyConsistent()
        {
            Setup();Set("BuildSelection",3);yield return null;Set("BuildSelection",-1);yield return new WaitForSeconds(.12f);
            var card=Farm.GetComponentInChildren<BuildPromptUI>(true);
            var normal=Farm.GetComponentsInChildren<Text>(true).Single(t=>t.name=="Interaction Prompt");
            Assert.That(card.gameObject.activeSelf,Is.True);Assert.That(normal.enabled,Is.False);
            Assert.That(card.PromptText.text,Does.Contain("Q / R").And.Contain("Esc").And.Contain("E / ЛКМ"));
            var cameraRotation=_camera.transform.rotation;
            Set("RotateBuildingPressed",true);Set("RotateLeftPressed",true);yield return null;
            Set("RotateBuildingPressed",false);Set("RotateLeftPressed",false);yield return null;
            Assert.That(Mathf.DeltaAngle(0,Ghost.eulerAngles.y),Is.EqualTo(90f).Within(.1f));
            Assert.That(Quaternion.Angle(cameraRotation,_camera.transform.rotation),Is.LessThan(.1f));
            _grid.SetCellType(new Vector2Int(0,3),CellType.Tilled);yield return new WaitForSeconds(.15f);
            Assert.That(Ghost.GetComponentInChildren<Renderer>().sharedMaterial.color.r,Is.GreaterThan(.8f));
            Set("InteractPressed",true);yield return null;Set("InteractPressed",false);
            Assert.That(_build.Count,Is.Zero);Assert.That(Farm.GetComponentInChildren<WalletSystem>().Coins,Is.EqualTo(100));
            Set("CancelPressed",true);yield return null;Set("CancelPressed",false);yield return null;
            Assert.That(_build.IsActive,Is.False);Assert.That(Ghost.gameObject.activeSelf,Is.False);
            Assert.That(card.gameObject.activeSelf,Is.False);Assert.That(normal.enabled,Is.True);
        }

        [UnityTest]
        public IEnumerator EPlacesAndConfirmsDeletionWhileCancelledMovePreservesBuilding()
        {
            Setup();Set("BuildSelection",3);yield return null;Set("BuildSelection",-1);yield return null;
            Set("InteractPressed",true);yield return null;Set("InteractPressed",false);yield return null;
            Assert.That(_build.Count,Is.EqualTo(1));
            Assert.That(Farm.GetComponentInChildren<WalletSystem>().Coins,Is.EqualTo(97));
            Set("MoveBuildingPressed",true);yield return null;Set("MoveBuildingPressed",false);yield return new WaitForSeconds(.12f);
            Assert.That(_build.CurrentPrompt,Does.Contain("бесплатный перенос"));
            Set("CancelPressed",true);yield return null;Set("CancelPressed",false);yield return null;
            Assert.That(_build.IsActive,Is.True);Assert.That(_build.Count,Is.EqualTo(1));
            Assert.That(_grid.IsOccupied(new Vector2Int(0,3)),Is.True);
            Set("DeleteBuildingPressed",true);yield return null;Set("DeleteBuildingPressed",false);yield return null;
            var card=Farm.GetComponentInChildren<BuildPromptUI>(true);
            Assert.That(card.PromptText.text,Does.Contain("E / Enter — подтвердить").And.Contain("Esc / ПКМ"));
            Assert.That(Ghost.gameObject.activeSelf,Is.False);
            Set("PointerInteractPressed",true);Set("InteractPressed",true);yield return null;
            Assert.That(_build.Count,Is.EqualTo(1),"A pointer click must not accidentally confirm deletion.");
            Set("PointerInteractPressed",false);yield return null;Set("InteractPressed",false);yield return null;
            Assert.That(_build.Count,Is.Zero);
            Assert.That(Farm.GetComponentInChildren<WalletSystem>().Coins,Is.EqualTo(98));
        }
    }
}
#endif
