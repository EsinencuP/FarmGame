using System.Collections;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.Animals;
using MyLittleFarm.Gameplay.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MyLittleFarm.Tests.PlayMode
{
    /// <summary>Verifies physical player contact and chicken feet at rest and while wandering.</summary>
    public sealed class GroundContactTests : FarmPlayModeFixture
    {
        [UnityTest]
        public IEnumerator PlayerSettlesOnThePhysicalPlane()
        {
            Farm.GetComponent<InputReader>().enabled = false;
            var player = Farm.GetComponentInChildren<PlayerController>();
            player.Teleport(new Vector3(3.5f, 1.1f, 0.5f));
            for (var i = 0; i < 120 && !player.GetComponent<CharacterController>().isGrounded; i++)
                yield return null;
            var controller = player.GetComponent<CharacterController>();
            Assert.That(controller.isGrounded, Is.True);
            // The capsule contact includes skinWidth; the imported art compensates for it.
            Assert.That(controller.bounds.min.y - controller.skinWidth, Is.EqualTo(0f).Within(0.03f));
        }

        [UnityTest]
        public IEnumerator ChickenFeetStayOnGroundDuringWander()
        {
            var animals = Farm.GetComponentInChildren<AnimalSystem>();
            Assert.That(animals.AddChicken(new Vector2Int(1, 3), 1), Is.True);
            var chicken = animals.GetComponentInChildren<AnimalView>();
            Assert.That(VisualPrefab.TryGetBounds(chicken.gameObject, out var initial), Is.True);
            Assert.That(initial.min.y, Is.EqualTo(0f).Within(0.001f));
            chicken.SetWanderOffset(0.2f, -0.15f);
            for (var i = 0; i < 30; i++)
            {
                yield return null;
                Assert.That(VisualPrefab.TryGetBounds(chicken.gameObject, out var bounds), Is.True);
                Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.001f));
            }
        }
    }
}
