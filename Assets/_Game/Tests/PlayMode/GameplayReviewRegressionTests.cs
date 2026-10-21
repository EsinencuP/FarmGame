using System;
using System.Collections;
using System.Reflection;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.Animals;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.Farming;
using MyLittleFarm.Gameplay.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MyLittleFarm.Tests.PlayMode
{
    /// <summary>Verifies pointer intent, reach and independent map permissions from the gameplay review.</summary>
    public sealed class GameplayReviewRegressionTests : FarmPlayModeFixture
    {
        [UnityTest]
        public IEnumerator PointerClickOutsideWorldDoesNotFeedNearbyChicken()
        {
            var input = Farm.GetComponent<InputReader>();
            input.enabled = false;
            var grid = Farm.GetComponentInChildren<GridSystem>();
            var player = Farm.GetComponentInChildren<PlayerController>();
            player.enabled = false;
            var animals = Farm.GetComponentInChildren<AnimalSystem>();
            Assert.That(animals.AddChicken(new Vector2Int(1,3), 1), Is.True);
            player.Teleport(grid.CellToWorld(new Vector2Int(1,3)) + Vector3.up * 1.1f);
            var inventory = Farm.GetComponentInChildren<InventorySystem>();
            inventory.Add("wheat", 1);
            var selector = Farm.GetComponentInChildren<CellSelector>();
            selector.enabled = false;
            selector.SelectWorldPoint(Vector3.one * 10000f);
            typeof(InputReader).GetProperty("PointerInteractPressed", BindingFlags.Public | BindingFlags.Instance)
                .SetValue(input, true);
            Assert.That(Farm.GetComponentInChildren<InteractionSystem>().Interact(1000), Is.False);
            Assert.That(inventory.GetAmount("wheat"), Is.EqualTo(1));
            Assert.That(animals.Capture()[0].nextProductAtUnixMs, Is.EqualTo(0));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PointerFeedsHoveredChickenInsteadOfNearestAndNeverFallsThrough()
        {
            var grid = Farm.GetComponentInChildren<GridSystem>();
            var animals = Farm.GetComponentInChildren<AnimalSystem>();
            var selector = Farm.GetComponentInChildren<CellSelector>();
            selector.enabled = false;
            var player = Farm.GetComponentInChildren<PlayerController>();
            player.enabled = false;
            var inventory = Farm.GetComponentInChildren<InventorySystem>();
            var interaction = Farm.GetComponentInChildren<InteractionSystem>();
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Assert.That(animals.AddChicken(new Vector2Int(1,3), now), Is.True);
            Assert.That(animals.AddChicken(new Vector2Int(2,3), now), Is.True);
            inventory.Add("wheat", 2);
            var targets = animals.GetComponentsInChildren<InteractionTarget>();
            player.Teleport(grid.CellToWorld(new Vector2Int(1,3)) + Vector3.up * 1.1f);
            Physics.SyncTransforms();
            var point = targets[1].transform.position;
            selector.SelectRay(new Ray(point + Vector3.up * 8f, Vector3.down));
            Assert.That(selector.HoveredTarget, Is.EqualTo(targets[1]));
            Assert.That(grid.SelectedCell.HasValue, Is.False, "Object selection must hide the ground marker.");
            Assert.That(interaction.Interact(now), Is.True);
            var states = animals.Capture();
            Assert.That(states.Find(a => a.animalId == targets[1].TargetId).nextProductAtUnixMs, Is.GreaterThan(0));
            Assert.That(states.Find(a => a.animalId == targets[0].TargetId).nextProductAtUnixMs, Is.EqualTo(0));
            Assert.That(interaction.Interact(now), Is.False);
            Assert.That(grid.GetCellType(new Vector2Int(2,3)), Is.EqualTo(CellType.Grass));
            player.Teleport(grid.CellToWorld(new Vector2Int(8,7)) + Vector3.up * 1.1f);
            Assert.That(interaction.Interact(now), Is.False, "Picking a target must not bypass reach.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator SelectedGroundWorksBesideChickenAndMarketDoesNotTillWhenFarAway()
        {
            var grid = Farm.GetComponentInChildren<GridSystem>();
            var player = Farm.GetComponentInChildren<PlayerController>();
            var selector = Farm.GetComponentInChildren<CellSelector>();
            selector.enabled = false;
            player.enabled = false;
            var animals = Farm.GetComponentInChildren<AnimalSystem>();
            Assert.That(animals.AddChicken(new Vector2Int(1,3), 1), Is.True);
            player.Teleport(grid.CellToWorld(new Vector2Int(1,3)) + Vector3.up * 1.1f);
            var soil = new Vector2Int(1,2);
            selector.SelectWorldPoint(grid.CellToWorld(soil));
            var interaction = Farm.GetComponentInChildren<InteractionSystem>();
            Assert.That(interaction.Interact(1000), Is.True);
            Assert.That(grid.GetCellType(soil), Is.EqualTo(CellType.Tilled));
            var market = Farm.transform.Find("Sale Crate").GetComponent<InteractionTarget>();
            selector.SelectTarget(market);
            var before = grid.GetCellType(selector.SelectedPosition);
            Assert.That(interaction.Interact(1000), Is.False);
            Assert.That(grid.GetCellType(selector.SelectedPosition), Is.EqualTo(before));
            yield return null;
        }

        [UnityTest]
        public IEnumerator IndependentZonesSurviveDeltaRestoreWithoutLosingPermissionsOnTypeChange()
        {
            var grid = Farm.GetComponentInChildren<GridSystem>();
            var position = new Vector2Int(2,2);
            var baselineHash = grid.ComputeBaseMapHash();
            grid.SetTerrainCell(position, new TerrainCell(CellType.Grass)
                { permissions = CellPermissions.Custom | CellPermissions.Walk | CellPermissions.Build });
            Assert.That(grid.CanTill(position), Is.False);
            Assert.That(grid.CanBuild(position), Is.True);
            Assert.That(grid.IsWalkable(position), Is.True);
            Assert.That(grid.CanPlantTree(position), Is.False);
            var snapshot = JsonUtility.FromJson<GridSaveData>(JsonUtility.ToJson(grid.Serialize()));
            grid.Deserialize(snapshot);
            Assert.That(grid.CanTill(position), Is.False);
            Assert.That(grid.CanBuild(position), Is.True);
            Assert.That(grid.ComputeBaseMapHash(), Is.EqualTo(baselineHash));
            grid.SetCellType(position, CellType.Dirt);
            Assert.That(grid.CanTill(position), Is.False);
            Assert.That(grid.GetTerrainCell(position).permissions, Is.EqualTo(CellPermissions.Custom | CellPermissions.Walk | CellPermissions.Build));
            grid.SetTerrainCell(position, new TerrainCell(CellType.Grass) { permissions = CellPermissions.Custom | CellPermissions.Farm });
            Assert.That(grid.CanTill(position), Is.True);
            Assert.That(grid.CanBuild(position), Is.False);
            Assert.That(grid.IsWalkable(position), Is.False);
            yield return null;
        }
    }
}
