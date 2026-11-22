#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.Animals;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.Farming;
using MyLittleFarm.Gameplay.World;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MyLittleFarm.Tests.PlayMode
{
    public sealed class PlaytestBakedFarmTests : FarmPlayModeFixture
    {
        private Scene _prototype;
        private GameBootstrap _baked;

        [UnitySetUp]
        public IEnumerator LoadBakedFarm()
        {
            // Keep the synthetic scene active so the additive Prototype never opens real player persistence.
            Farm.gameObject.SetActive(false);
            var previousCount = SceneManager.sceneCount;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_Game/Scenes/Prototype.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            _prototype = SceneManager.GetSceneAt(previousCount);
            _baked = _prototype.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameBootstrap>(true)).Single();
            yield return null;
            Assert.That(_baked.gameObject.activeInHierarchy, Is.True, "The Windows farm scene must start with its bootstrap active.");
            Assert.That(SceneManager.GetActiveScene().name, Is.Not.EqualTo("Prototype"));
            Assert.That(_baked.GetComponentInChildren<SaveSystem_AI>().HasActiveFarm, Is.False);
        }

        [UnityTearDown]
        public IEnumerator UnloadBakedFarm()
        {
            if (_prototype.IsValid() && _prototype.isLoaded) yield return SceneManager.UnloadSceneAsync(_prototype);
        }

        [UnityTest]
        public IEnumerator AuthoredMapLoadsThreeRealV12FarmsAndTheDevelopedV18()
        {
            var folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Verification");
            var files = new[]
            {
                Path.Combine(folder, "U1-0", "V12", "empty-farm-v12.json"),
                Path.Combine(folder, "U1-0", "V12", "orchard-and-press-v12.json"),
                Path.Combine(folder, "U1-0", "V12", "first-juice-sale-v12.json"),
                Path.Combine(folder, "U1-7", "all-systems-v18.json")
            };
            var saves = _baked.GetComponentInChildren<SaveSystem_AI>();
            foreach (var path in files)
            {
                var original = File.ReadAllText(path);
                var previous = JsonUtility.FromJson<SaveData>(original);
                // LoadFromPath reads this control snapshot without selecting or overwriting a user slot.
                Assert.That(saves.LoadFromPath(path), Is.True, Path.GetFileName(path));
                var current = saves.Capture();
                Assert.That(current.saveVersion, Is.EqualTo(SaveSystem_AI.CurrentSaveVersion));
                Assert.That(current.coins, Is.EqualTo(previous.coins));
                Assert.That(current.buildings.Select(building => building.id), Is.EquivalentTo(previous.buildings.Select(building => building.id)));
                Assert.That(current.crops.Count, Is.EqualTo(previous.crops.Count));
                Assert.That(current.trees.Count, Is.EqualTo(previous.trees.Count));
                Assert.That(current.animals.Select(animal => animal.animalId), Is.EquivalentTo(previous.animals.Select(animal => animal.animalId)));
                Assert.That(current.nextFishCatchAtUnixMs, Is.Zero);
                Assert.That(File.ReadAllText(path), Is.EqualTo(original));
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator DevelopedFarmSurvivesThirtySaveLoadRoundTrips()
        {
            var project = Path.GetDirectoryName(Application.dataPath);
            var control = Path.Combine(project, "Verification", "AdaptiveAI", "demo-orders-v21.json");
            var path = Path.Combine(Application.temporaryCachePath, "farm-round-trip-" + Guid.NewGuid().ToString("N") + ".json");
            var saves = _baked.GetComponentInChildren<SaveSystem_AI>();
            try
            {
                Assert.That(saves.LoadFromPath(control), Is.True);
                var expected = saves.Capture();
                for (var round = 0; round < 30; round++)
                {
                    Assert.That(saves.SaveToPath(path), Is.True, $"Write {round + 1}");
                    Assert.That(saves.LoadFromPath(path), Is.True, $"Read {round + 1}");
                    Assert.That(saves.LastLoadUsedBackup, Is.False, $"Unexpected recovery at {round + 1}");
                    var actual = saves.Capture();
                    Assert.That(actual.coins, Is.EqualTo(expected.coins));
                    Assert.That(actual.buildings.Select(building => building.id),
                        Is.EquivalentTo(expected.buildings.Select(building => building.id)));
                    Assert.That(actual.animals.Select(animal => animal.animalId),
                        Is.EquivalentTo(expected.animals.Select(animal => animal.animalId)));
                    Assert.That(actual.crops.Select(crop => crop.Position),
                        Is.EquivalentTo(expected.crops.Select(crop => crop.Position)));
                    Assert.That(actual.trees.Count, Is.EqualTo(expected.trees.Count));
                    yield return null;
                }
                Assert.That(File.Exists(path + ".bak"), Is.True);
            }
            finally
            {
                File.Delete(path);
                File.Delete(path + ".bak");
            }
        }

        [UnityTest]
        public IEnumerator MarketOpensLockedSectorWhileKeepingTheAuthoredTrail()
        {
            var sector = _baked.GetComponentInChildren<SectorSystem>();
            var grid = _baked.GetComponentInChildren<GridSystem>();
            var player = _baked.GetComponentInChildren<PlayerController>();
            var wallet = _baked.GetComponentInChildren<WalletSystem>();
            var market = _baked.GetComponentInChildren<FarmMarket_AI>();
            var trail = new Vector2Int(10, 0);
            var dirt = Enumerable.Range(10, 10).SelectMany(x => Enumerable.Range(0, 10).Select(z => new Vector2Int(x, z)))
                .First(cell => grid.GetTerrainCell(cell).type == CellType.Dirt);
            Assert.That(grid.GetTerrainCell(trail).type, Is.EqualTo(CellType.Sand));
            Assert.That(grid.GetCellType(dirt), Is.EqualTo(CellType.Locked));
            Assert.That(grid.CanTill(dirt), Is.False);
            Assert.That(grid.IsWalkable(trail), Is.False);
            player.Teleport(_baked.transform.Find("Sale Crate").position + new Vector3(1.2f, 1f, 0));
            wallet.SetCoins(sector.PurchasePrice + 1);
            var cart = new Dictionary<string, int> { ["sector"] = 1 };
            Assert.That(market.Checkout(FarmMarket_AI.Category.Upgrades, cart, out var reason), Is.True, reason);
            Assert.That(wallet.Coins, Is.EqualTo(1));
            Assert.That(sector.IsUnlocked, Is.True);
            Assert.That(grid.GetTerrainCell(trail).type, Is.EqualTo(CellType.Sand));
            Assert.That(grid.GetCellType(dirt), Is.EqualTo(CellType.Dirt));
            Assert.That(grid.CanTill(dirt), Is.True);
            Assert.That(grid.IsWalkable(trail), Is.True);
            Assert.That(market.Checkout(FarmMarket_AI.Category.Upgrades, cart, out _), Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RainHydratesFieldCropsAndOrchardBeforeHarvest()
        {
            var crops = _baked.GetComponentInChildren<CropSystem_AI>();
            var soil = _baked.GetComponentInChildren<SoilSystem>();
            var orchard = _baked.GetComponentInChildren<OrchardSystem_AI>();
            var weather = _baked.GetComponentInChildren<WeatherController_AI>();
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var field = new Vector2Int(1, 1);
            var tree = new Vector2Int(1, 2);
            Assert.That(soil.Till(field), Is.True);
            Assert.That(crops.Plant(field, "tomato", now), Is.True);
            Assert.That(orchard.PlantApple(tree, now), Is.True);

            var rain = weather.Capture();
            rain.targetState = (int)WeatherState.LightRain;
            rain.cloudCover = rain.rainIntensity = 1f;
            rain.transitionRemaining = 0f;
            rain.holdRemaining = 45f;
            weather.Restore(rain);
            yield return new WaitForSecondsRealtime(1.25f);

            var crop = crops.Capture().Single(plant => plant.Position == field);
            var fruit = orchard.Capture().Single(plant => plant.Position == tree);
            Assert.That(crop.manuallyWatered, Is.False);
            Assert.That(crop.rainySeconds, Is.GreaterThan(.5f));
            Assert.That(crop.weatherGrowthBonusSeconds, Is.GreaterThan(.1f));
            Assert.That(fruit.manuallyWatered, Is.False);
            Assert.That(fruit.rainySeconds, Is.GreaterThan(.5f));
        }

        [UnityTest]
        public IEnumerator FishCatchRestoresOnceAndSellsThroughMarket()
        {
            var grid = _baked.GetComponentInChildren<GridSystem>();
            var player = _baked.GetComponentInChildren<PlayerController>();
            var inventory = _baked.GetComponentInChildren<InventorySystem_AI>();
            var wallet = _baked.GetComponentInChildren<WalletSystem>();
            var fishPond = _baked.GetComponentInChildren<FishPond>();
            var camera = _baked.GetComponentInChildren<IsometricCameraController>();
            var save = _baked.GetComponentInChildren<SaveSystem_AI>();
            Assert.That(fishPond, Is.Not.Null);
            Assert.That(grid.GetCellType(new Vector2Int(-15, 8)), Is.EqualTo(CellType.Water));
            var input = _baked.GetComponent<InputReader_AI>();
            var modalInput = typeof(InputReader_AI).GetProperty(nameof(InputReader_AI.ModalInputActive));
            // This additive test skips the slot screen. Release its input gate as selecting a slot would.
            modalInput.SetValue(input, false);
            yield return new WaitForSecondsRealtime(.25f);

            player.Teleport(fishPond.transform.position + new Vector3(.5f, .76f, 0f));
            camera.RefreshPosition();
            var farmCamera = camera.transform.rotation;
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Assert.That(fishPond.Interact(now), Is.True);
            Assert.That(fishPond.Interact(now), Is.False);
            Assert.That(inventory.GetAmount(FishPond.FishId), Is.Zero);
            modalInput.SetValue(input, true);
            yield return null;
            Assert.That(fishPond.Interact(now), Is.False);
            modalInput.SetValue(input, false);
            yield return null;
            Assert.That(fishPond.Interact(now), Is.True);
            yield return new WaitForSecondsRealtime(.4f);
            var fields = BindingFlags.Instance | BindingFlags.NonPublic;
            var water = (Vector3)typeof(FishPond).GetField("_waterPoint", fields).GetValue(fishPond);
            var towardWater = Vector3.ProjectOnPlane(water - player.transform.position, Vector3.up).normalized;
            var towardCamera = Vector3.ProjectOnPlane(camera.transform.position - player.transform.position, Vector3.up).normalized;
            Assert.That(typeof(InputReader_AI).GetProperty("FishingActive", fields).GetValue(input), Is.True);
            Assert.That(Vector3.Dot(towardWater, towardCamera), Is.LessThan(-.97f));
            Assert.That(Vector3.Dot(towardWater, camera.transform.forward), Is.GreaterThan(.5f));
            yield return new WaitForSecondsRealtime(2.6f);
            now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var interaction = _baked.GetComponentInChildren<InteractionSystem_AI>();
            Assert.That(interaction.Interact(now), Is.True, "The moved camera no longer needs the marker under the cursor.");
            Assert.That(interaction.Interact(now), Is.False, "A second hook cannot award fish.");
            Assert.That(inventory.GetAmount(FishPond.FishId), Is.Zero);

            var challenge = typeof(FishPond).GetField("_challenge", fields).GetValue(fishPond);
            AdvanceFishing(challenge, followFish: true);
            yield return null;
            yield return null;
            Assert.That(inventory.GetAmount(FishPond.FishId), Is.EqualTo(1));
            yield return new WaitForSecondsRealtime(1f);
            Assert.That(Quaternion.Angle(camera.transform.rotation, farmCamera), Is.LessThan(1f));

            var snapshot = save.Capture();
            var restored = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(snapshot));
            save.Restore(restored);
            Assert.That(fishPond.NextCatchAtUnixMs, Is.EqualTo(snapshot.nextFishCatchAtUnixMs));
            Assert.That(fishPond.Interact(now), Is.False);
            var market = _baked.GetComponentInChildren<FarmMarket_AI>(true);
            var balance = wallet.Coins;
            player.Teleport(_baked.transform.Find("Sale Crate").position + Vector3.up);
            var cart = new Dictionary<string, int> { [FishPond.FishId] = 1 };
            Assert.That(market.Checkout(FarmMarket_AI.Category.Sell, cart, out var reason), Is.True, reason);
            Assert.That(wallet.Coins, Is.EqualTo(balance + FishPond.SellPrice));
            Assert.That(inventory.GetAmount(FishPond.FishId), Is.Zero);
        }

        [UnityTest]
        public IEnumerator FishEscapeAndCancelClearTheCameraWithoutAwardingACatch()
        {
            var fishPond = _baked.GetComponentInChildren<FishPond>();
            var camera = _baked.GetComponentInChildren<IsometricCameraController>();
            var player = _baked.GetComponentInChildren<PlayerController>();
            var input = _baked.GetComponent<InputReader_AI>();
            var modalInput = typeof(InputReader_AI).GetProperty(nameof(InputReader_AI.ModalInputActive));
            modalInput.SetValue(input, false);
            player.Teleport(fishPond.transform.position + new Vector3(.5f, .76f, 0f));
            camera.RefreshPosition();
            var farmCamera = camera.transform.rotation;
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Assert.That(fishPond.Interact(now), Is.True);
            yield return new WaitForSecondsRealtime(3f);
            Assert.That(fishPond.Interact(now), Is.True);
            var fields = BindingFlags.Instance | BindingFlags.NonPublic;
            var challenge = typeof(FishPond).GetField("_challenge", fields).GetValue(fishPond);
            AdvanceFishing(challenge, followFish: false);
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(Quaternion.Angle(camera.transform.rotation, farmCamera), Is.LessThan(1f));
            Assert.That(_baked.GetComponentInChildren<InventorySystem_AI>().GetAmount(FishPond.FishId), Is.Zero);
            Assert.That(fishPond.NextCatchAtUnixMs, Is.Zero);
            Assert.That(fishPond.Interact(now), Is.True, "A missed fish allows another cast immediately.");
            fishPond.Restore(0);
            Assert.That(Quaternion.Angle(camera.transform.rotation, farmCamera), Is.LessThan(1f));
            Assert.That(typeof(InputReader_AI).GetProperty("FishingActive", fields).GetValue(input), Is.False);
        }

        [Test]
        public void FishSizeChangesHowFarItCanMoveInTheSameTime()
        {
            var assembly = typeof(FishPond).Assembly;
            var type = assembly.GetType("MyLittleFarm.Gameplay.World.FishingChallenge", true);
            var sizeType = assembly.GetType("MyLittleFarm.Gameplay.World.FishingSize", true);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var distances = new List<float>();
            foreach (var size in Enum.GetValues(sizeType))
            {
                var challenge = Activator.CreateInstance(type, flags, null, new[] { size, new System.Random(132) }, null);
                var advance = type.GetMethod("Advance", flags);
                for (var i = 0; i < 51; i++) advance.Invoke(challenge, new object[] { 1f / 60f, 0f });
                var position = (float)type.GetProperty("Fish", flags).GetValue(challenge);
                distances.Add(Mathf.Abs(.5f - position));
            }
            Assert.That(distances[1], Is.GreaterThan(distances[0] + .04f));
            Assert.That(distances[2], Is.GreaterThan(distances[1] + .04f));
        }

        private static void AdvanceFishing(object challenge, bool followFish)
        {
            Assert.That(challenge, Is.Not.Null);
            var fields = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = challenge.GetType();
            var fish = type.GetProperty("Fish", fields);
            var zone = type.GetProperty("Zone", fields);
            var advance = type.GetMethod("Advance", fields);
            for (var i = 0; i < 500; i++)
            {
                var position = (float)fish.GetValue(challenge);
                var center = (float)zone.GetValue(challenge);
                var axis = followFish ? Mathf.Clamp((position - center) * 5f, -1f, 1f) : position > .5f ? -1f : 1f;
                var result = advance.Invoke(challenge, new object[] { .05f, axis }).ToString();
                if (result == "InProgress") continue;
                Assert.That(result, Is.EqualTo(followFish ? "Caught" : "Escaped"));
                return;
            }
            Assert.Fail("Fishing must resolve before the maximum round time.");
        }
    }
}
#endif
