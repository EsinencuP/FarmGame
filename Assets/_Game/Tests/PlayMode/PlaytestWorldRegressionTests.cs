using System;
using System.Collections;
using System.IO;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.Animals;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MyLittleFarm.Tests.PlayMode
{
    public sealed class PlaytestWorldRegressionTests : FarmPlayModeFixture
    {
        private T System<T>() where T : Component => Farm.GetComponentInChildren<T>(true);

        [UnityTest]
        public IEnumerator BarnPaddockReservesLandButHasAPhysicalEntrance()
        {
            yield return null;
            System<SectorSystem>().Restore(true);
            System<WalletSystem>().SetCoins(500);
            var buildings = System<BuildSystem>();
            var grid = System<GridSystem>();
            var origin = new Vector2Int(10, 0);
            Assert.That(buildings.TryPlace("barn", origin, 0, out var reason), Is.True, reason);
            var barn = buildings.FindFirstView("barn");
            var inside = origin + new Vector2Int(6, 1);
            Assert.That(grid.IsWalkable(inside), Is.True);
            Assert.That(grid.CanBuild(inside), Is.False);
            Assert.That(grid.CanTill(inside), Is.False);
            Physics.SyncTransforms();

            var start = barn.transform.TransformPoint(new Vector3(1.15f, .7f, -3.3f));
            var fence = barn.transform.TransformPoint(new Vector3(3.45f, .7f, -3.3f));
            Assert.That(Physics.SphereCast(start, .19f, barn.transform.forward, out _, .9f, ~0, QueryTriggerInteraction.Ignore), Is.False);
            Assert.That(Physics.SphereCast(fence, .19f, barn.transform.forward, out _, .9f, ~0, QueryTriggerInteraction.Ignore), Is.True);
        }

        [UnityTest]
        public IEnumerator ChickenHasSeparatePickAndSolidVolumesAndLegacyOverlapCanRestore()
        {
            yield return null;
            var animals = System<AnimalSystem_AI>();
            var grid = System<GridSystem>();
            var save = System<SaveSystem_AI>();
            var player = System<PlayerController>();
            var cell = new Vector2Int(1, 3);
            var ground = grid.GridToWorld(cell);
            Assert.That(animals.AddChicken(cell, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), Is.True);
            var bird = animals.GetComponentInChildren<AnimalView>();
            var pick = Array.Find(bird.GetComponents<Collider>(), collider => collider.isTrigger);
            var body = Array.Find(bird.GetComponents<Collider>(), collider => !collider.isTrigger);
            Assert.That(pick, Is.Not.Null);
            Assert.That(body, Is.Not.Null);

            var legacy = save.Capture();
            legacy.saveVersion = 18;
            legacy.player = new PlayerPositionData { x = ground.x, y = ground.y + .72f, z = ground.z };
            save.Restore(legacy);
            var restored = player.transform.position - ground;
            Assert.That(new Vector2(restored.x, restored.z).magnitude, Is.GreaterThan(.1f));
            Assert.That(animals.ChickenCount, Is.EqualTo(1));
            Assert.That(save.Capture().nextFishCatchAtUnixMs, Is.Zero);

            var invalid = save.Capture();
            invalid.nextFishCatchAtUnixMs = -1;
            Assert.Throws<InvalidDataException>(() => save.Restore(invalid));
        }
    }
}
