#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.World;
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
        private InputReader_AI _input;
        private BuildSystem _build;
        private Camera _camera;
        private GridSystem _grid;
        private void Setup()
        {
            _input=Farm.GetComponent<InputReader_AI>();_input.enabled=false;
            _build=Farm.GetComponentInChildren<BuildSystem>();_grid=Farm.GetComponentInChildren<GridSystem>();
            _camera=Farm.GetComponentInChildren<Camera>();
            var serialized=new SerializedObject(_build);var array=serialized.FindProperty("visualPrefabs");array.arraySize=5;
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
        private void Set(string name,object value)=>typeof(InputReader_AI).GetProperty(name).SetValue(_input,value);
        private void Point(Vector2Int cell){Set("HasPointer",true);Set("PointerPosition",(Vector2)_camera.WorldToScreenPoint(_grid.CellToWorld(cell)));}
        private Transform Ghost=>_build.transform.Find("Building model preview");

        private void AimFixedCamera(Vector2Int cell)
        {
            _camera.GetComponent<IsometricCameraController>().enabled = false;
            var target = _grid.CellToWorld(cell);
            _camera.transform.position = target + new Vector3(0, 8f, -5f);
            _camera.transform.LookAt(target);
            Set("BuildSelection", -1);
            Physics.SyncTransforms();
            Point(cell);
        }

        [UnityTest]
        public IEnumerator AllFiveGhostsMatchArtAndTintEverySubmeshWithoutBlockingPlacement()
        {
            Setup();
            for(var i=0;i<5;i++)
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
            Assert.That(card.PromptText.text,
                Does.Contain(InputReader_AI.BuildRotateHint).And.Contain("Esc").And.Contain(InputReader_AI.ActionHint));
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
        public IEnumerator HouseFootprintMarksBothCollidersAndTheTilledCell()
        {
            Setup();
            var origin = new Vector2Int(0, 3);
            _build.BeginPlacement(0);
            AimFixedCamera(origin);
            var blockedGround = origin + new Vector2Int(2, 0);
            var firstObstacle = origin + new Vector2Int(1, 1);
            var secondObstacle = origin + new Vector2Int(4, 3);
            _grid.SetCellType(blockedGround, CellType.Tilled);
            foreach (var cell in new[] { firstObstacle, secondObstacle })
            {
                var obstacle = new GameObject("Placement obstacle");
                obstacle.transform.SetParent(Farm.transform);
                obstacle.transform.position = _grid.GridToWorld(cell) + Vector3.up * .5f;
                obstacle.AddComponent<BoxCollider>().size = Vector3.one * .4f;
            }
            Physics.SyncTransforms();
            yield return null;

            var width = BuildingDefinition.Catalog[0].Width;
            var footprint = _build.transform.Find("Building footprint");
            Assert.That(footprint, Is.Not.Null, _build.CurrentPrompt);
            foreach (var cell in new[] { blockedGround, firstObstacle, secondObstacle, origin })
            {
                var offset = cell - origin;
                var line = footprint.GetChild(offset.y * width + offset.x).GetComponent<LineRenderer>();
                Assert.That(line.positionCount, Is.EqualTo(cell == origin ? 5 : 7), $"Footprint at {cell}");
            }
            Assert.That(_build.TryPlace("house", origin, 0, out _), Is.False);
            Assert.That(Farm.GetComponentInChildren<WalletSystem>().Coins, Is.EqualTo(100));
        }

        [UnityTest]
        public IEnumerator EPlacesAndConfirmsDeletionWhileCancelledMovePreservesBuilding()
        {
            Setup();
            _build.BeginPlacement(3);
            AimFixedCamera(new Vector2Int(0,3));
            yield return null;
            Set("InteractPressed",true);yield return null;Set("InteractPressed",false);yield return null;
            Assert.That(_build.Count,Is.EqualTo(1));
            Assert.That(Farm.GetComponentInChildren<WalletSystem>().Coins,Is.EqualTo(95));
            Assert.That(_build.IsActive,Is.False);
            _build.BeginManagement(remove:false);
            Set("InteractPressed",true);yield return null;Set("InteractPressed",false);yield return new WaitForSeconds(.12f);
            Assert.That(_build.CurrentPrompt,Does.Contain("бесплатный перенос"));
            Set("CancelPressed",true);yield return null;Set("CancelPressed",false);yield return null;
            Assert.That(_build.IsActive,Is.False);Assert.That(_build.Count,Is.EqualTo(1));
            Assert.That(_grid.IsOccupied(new Vector2Int(0,3)),Is.True);
            _build.BeginManagement(remove:true);
            Set("InteractPressed",true);yield return null;Set("InteractPressed",false);yield return null;
            var card=Farm.GetComponentInChildren<BuildPromptUI>(true);
            Assert.That(card.PromptText.text,
                Does.Contain("Подтвердить · " + InputReader_AI.KeyLabel(InputReader_AI.Command.Interact) + " / Enter")
                    .And.Contain("Отмена · Esc / ПКМ"));
            Assert.That(Ghost.gameObject.activeSelf,Is.False);
            Set("PointerInteractPressed",true);Set("InteractPressed",true);yield return null;
            Assert.That(_build.Count,Is.EqualTo(1),"A pointer click must not accidentally confirm deletion.");
            Set("PointerInteractPressed",false);Set("InteractPressed",false);yield return null;
            Set("InteractPressed",true);yield return null;Set("InteractPressed",false);yield return null;
            Assert.That(_build.Count,Is.Zero);
            Assert.That(Farm.GetComponentInChildren<WalletSystem>().Coins,Is.EqualTo(97));
        }
    }
}
#endif
