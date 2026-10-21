using System.Collections.Generic;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MyLittleFarm.Tests.EditMode
{
    /// <summary>Prevents a visible/physical ground gap from returning in existing, moved or inactive chunks.</summary>
    public sealed class GroundAlignmentTests
    {
        private GameObject _root;
        private TilemapChunk _chunk;
        private GameObject _visual;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Ground alignment test");
            _chunk = _root.AddComponent<TilemapChunk>();
            var surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            surface.name = "Terrain Surface";
            surface.transform.SetParent(_root.transform, false);
            surface.transform.localPosition = new Vector3(5f, -0.25f, 5f);
            surface.transform.localScale = new Vector3(10f, 0.5f, 10f);
            _visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _visual.name = "Ground Visual";
            _visual.transform.SetParent(_root.transform, false);
            _visual.transform.localScale = new Vector3(10f, 0.4f, 10f);
            _visual.transform.localPosition = new Vector3(5f, -0.6f, 5f);
            Object.DestroyImmediate(_visual.GetComponent<Collider>());
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void OriginalMinusPointFourGapIsReportedAndExistingVisualIsRepaired()
        {
            var issues = new List<string>();
            GroundAlignmentValidator.ValidateChunk(_chunk, issues);
            Assert.That(issues, Has.Some.Contains("Ground Visual top"));
            Assert.That(VisualPrefab.TryGetBounds(_visual, out var initial), Is.True);
            Assert.That(initial.max.y, Is.EqualTo(-0.4f).Within(0.00001f));
            var colliderPosition = _chunk.TerrainCollider.transform.position;
            MvpSceneUpgrade.AlignGround(_chunk, _visual);
            Assert.That(VisualPrefab.TryGetBounds(_visual, out var bounds), Is.True);
            Assert.That(bounds.max.y, Is.EqualTo(0f).Within(0.00001f));
            Assert.That(_chunk.TerrainCollider.transform.position, Is.EqualTo(colliderPosition));
            issues.Clear();
            GroundAlignmentValidator.ValidateChunk(_chunk, issues);
            Assert.That(issues, Is.Empty);
        }

        [Test]
        public void WorldHeightAlignmentPreservesPlacementAndIsIdempotent()
        {
            _root.transform.position = new Vector3(-10f, 2f, 10f);
            _root.transform.localScale = new Vector3(2f, 1.5f, 3f);
            var previous = _visual.transform.position;
            var scale = _visual.transform.localScale;
            Assert.That(GroundAlignmentValidator.AlignGroundVisual(_visual), Is.True);
            Assert.That(VisualPrefab.TryGetBounds(_visual, out var bounds), Is.True);
            Assert.That(bounds.max.y, Is.EqualTo(0f).Within(0.00001f));
            Assert.That(_visual.transform.position.x, Is.EqualTo(previous.x));
            Assert.That(_visual.transform.position.z, Is.EqualTo(previous.z));
            Assert.That(_visual.transform.localScale, Is.EqualTo(scale));
            Assert.That(GroundAlignmentValidator.AlignGroundVisual(_visual), Is.False);
        }

        [Test]
        public void PhysicalOffsetIsReportedInsteadOfBecomingTheVisualTarget()
        {
            _chunk.TerrainCollider.transform.position += Vector3.up * 0.3f;
            MvpSceneUpgrade.AlignGround(_chunk, _visual);
            var issues = new List<string>();
            GroundAlignmentValidator.ValidateChunk(_chunk, issues);
            Assert.That(issues, Has.Some.Contains("Terrain Surface top corner"));
            Assert.That(VisualPrefab.TryGetBounds(_visual, out var bounds), Is.True);
            Assert.That(bounds.max.y, Is.EqualTo(0f).Within(0.00001f));
        }

        [Test]
        public void InactiveSectorsUseAuthoredColliderGeometry()
        {
            GroundAlignmentValidator.AlignGroundVisual(_visual);
            _root.SetActive(false);
            _chunk.TerrainCollider.transform.localPosition += Vector3.down * 0.4f;
            var issues = new List<string>();
            GroundAlignmentValidator.ValidateChunk(_chunk, issues);
            Assert.That(issues, Has.Some.Contains("Terrain Surface top corner"));
        }

        [Test]
        public void MissingVisualAndTriggerSurfaceCannotPass()
        {
            Object.DestroyImmediate(_visual);
            _chunk.TerrainCollider.isTrigger = true;
            var issues = new List<string>();
            GroundAlignmentValidator.ValidateChunk(_chunk, issues);
            Assert.That(issues, Has.Some.Contains("Ground Visual or its renderer is missing"));
            Assert.That(issues, Has.Some.Contains("non-trigger"));
        }

        [Test]
        public void SavedPrototypeHasNineAlignedSurfacesAndGroundedPlayerArt()
        {
            const string path = "Assets/_Game/Scenes/Prototype.unity";
            var scene = SceneManager.GetSceneByPath(path);
            var alreadyLoaded = scene.IsValid() && scene.isLoaded;
            if (!alreadyLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                // Exclude this test's deliberately misaligned synthetic surface.
                _root.SetActive(false);
                var chunks = new List<TilemapChunk>();
                foreach (var root in scene.GetRootGameObjects())
                    if (root != _root) chunks.AddRange(root.GetComponentsInChildren<TilemapChunk>(true));
                Assert.That(chunks.Count, Is.EqualTo(9));
                foreach (var chunk in chunks)
                {
                    var issues = new List<string>();
                    GroundAlignmentValidator.ValidateChunk(chunk, issues);
                    Assert.That(issues, Is.Empty, string.Join("\n", issues));
                }
                foreach (var root in scene.GetRootGameObjects())
                foreach (var player in root.GetComponentsInChildren<MyLittleFarm.Gameplay.World.PlayerController>(true))
                {
                    var capsule = player.GetComponent<CharacterController>();
                    var visual = player.VisualRoot;
                    Assert.That(visual, Is.Not.Null);
                    Assert.That(GroundAlignmentValidator.TryGetPlayerFeetLocalY(player.transform, visual, out var feet), Is.True);
                    Assert.That(feet, Is.EqualTo(capsule.center.y - capsule.height * 0.5f - capsule.skinWidth).Within(0.001f));
                }
            }
            finally { if (!alreadyLoaded) EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void ImportedPlayerSolesMatchCapsuleContactAndRepeatedRepairDoesNotDrift()
        {
            var player = new GameObject("Imported player feet test");
            try
            {
                var controller = player.AddComponent<MyLittleFarm.Gameplay.World.PlayerController>();
                var capsule = player.GetComponent<CharacterController>();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Stylized NPC - Peasant Nolant/Prefabs/Peasant Nolant Blue(Free Version).prefab");
                Assert.That(prefab, Is.Not.Null);
                var visual = VisualPrefab.Create(prefab, player.transform, new Vector3(0.85f, 1.8f, 0.85f));
                visual.name = "Nolant Visual";
                var animator = visual.GetComponentInChildren<Animator>();
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    "Assets/_Game/Content/Animation/PlayerLocomotion.controller");
                animator.Rebind();
                animator.Update(0f);
                GameplayReviewRepair.AlignPlayerVisual(controller);
                Assert.That(GroundAlignmentValidator.TryGetPlayerFeetLocalY(player.transform, visual.transform, out var feet), Is.True);
                Assert.That(feet, Is.EqualTo(capsule.center.y - capsule.height * 0.5f - capsule.skinWidth).Within(0.001f));
                var position = visual.transform.localPosition;
                GameplayReviewRepair.AlignPlayerVisual(controller);
                Assert.That(Vector3.Distance(visual.transform.localPosition, position), Is.LessThan(0.00001f));
            }
            finally { Object.DestroyImmediate(player); }
        }
    }
}
