using System;
using System.Collections;
using System.IO;
using System.Reflection;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.Animals;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.World;
using MyLittleFarm.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MyLittleFarm.Tests.PlayMode
{
    /// <summary>Regression coverage for MVP interaction, persistence, motion and HUD lifecycle.</summary>
    public sealed class MvpReliabilityTests : FarmPlayModeFixture
    {
        [UnityTest]
        public IEnumerator UnfedChickenConsumesInteractionWithoutTillingTheSelectedCell()
        {
            var animals = Farm.GetComponentInChildren<AnimalSystem>();
            var inventory = Farm.GetComponentInChildren<InventorySystem>();
            var grid = Farm.GetComponentInChildren<GridSystem>();
            var player = Farm.GetComponentInChildren<PlayerController>();
            var selector = Farm.GetComponentInChildren<CellSelector>();
            var interaction = Farm.GetComponentInChildren<InteractionSystem>();
            inventory.Restore(null);
            var position = new Vector2Int(1, 3);
            Assert.That(animals.AddChicken(position, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), Is.True);
            player.Teleport(grid.GridToWorld(position) + Vector3.up * 1.1f);
            selector.SelectTarget(animals.GetComponentInChildren<InteractionTarget>());
            var before = grid.GetCellType(position);
            Assert.That(interaction.Interact(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), Is.False);
            Assert.That(grid.GetCellType(position), Is.EqualTo(before),
                "An animal prompt must never fall through to soil interaction.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator BackupRecoveryPreservesGoodBackupWhenSavingAgain()
        {
            var save = Farm.GetComponentInChildren<SaveSystem>();
            var wallet = Farm.GetComponentInChildren<WalletSystem>();
            var path = Path.Combine(Application.temporaryCachePath, $"mvp-recovery-{Guid.NewGuid():N}.json");
            try
            {
                wallet.SetCoins(17);
                Assert.That(save.SaveToPath(path), Is.True);
                wallet.SetCoins(29);
                Assert.That(save.SaveToPath(path), Is.True);
                var goodBackup = File.ReadAllText(path + ".bak");
                File.WriteAllText(path, "{");
                Assert.That(save.LoadFromPath(path), Is.True);
                Assert.That(save.LastLoadUsedBackup, Is.True);
                Assert.That(wallet.Coins, Is.EqualTo(17));
                wallet.SetCoins(41);
                Assert.That(save.SaveToPath(path), Is.True);
                Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo(goodBackup));
                Assert.That(save.LoadFromPath(path), Is.True);
                Assert.That(save.LastLoadUsedBackup, Is.False);
                Assert.That(wallet.Coins, Is.EqualTo(41));
                File.WriteAllText(path, "{");
                File.WriteAllText(path + ".bak", "{");
                Assert.That(save.LoadFromPath(path), Is.False);
                Assert.That(wallet.Coins, Is.EqualTo(41), "Invalid candidates must leave the live world unchanged.");
            }
            finally
            {
                foreach (var file in new[] { path, path + ".tmp", path + ".bak" })
                    if (File.Exists(file)) File.Delete(file);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator CameraTurnAndAnimalWanderConvergeWithoutAnInstantSnap()
        {
            var input = Farm.GetComponent<InputReader>();
            input.enabled = false;
            var camera = Farm.GetComponentInChildren<IsometricCameraController>();
            var startYaw = camera.transform.eulerAngles.y;
            typeof(InputReader).GetProperty("RotateLeftPressed", BindingFlags.Instance | BindingFlags.Public)
                .SetValue(input, true);
            yield return null;
            typeof(InputReader).GetProperty("RotateLeftPressed", BindingFlags.Instance | BindingFlags.Public)
                .SetValue(input, false);
            var firstAngle = Mathf.Abs(Mathf.DeltaAngle(startYaw, camera.transform.eulerAngles.y));
            Assert.That(firstAngle, Is.GreaterThan(0f).And.LessThan(90f));
            var animals = Farm.GetComponentInChildren<AnimalSystem>();
            Assert.That(animals.AddChicken(new Vector2Int(1, 3), 1), Is.True);
            animals.enabled = false;
            var view = animals.GetComponentInChildren<AnimalView>();
            var start = view.transform.localPosition;
            view.SetWanderOffset(0.25f, 0f);
            Assert.That(view.transform.localPosition, Is.EqualTo(start));
            yield return new WaitForSeconds(1.5f);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(startYaw, camera.transform.eulerAngles.y)),
                Is.EqualTo(90f).Within(0.1f));
            Assert.That(Vector3.Distance(view.transform.localPosition, start + Vector3.right * 0.25f),
                Is.LessThan(0.005f));
        }

        [UnityTest]
        public IEnumerator HudUnsubscribesWhileDisabledAndRefreshesWhenReenabled()
        {
            var hud = Farm.GetComponentInChildren<HUDController>();
            var wallet = Farm.GetComponentInChildren<WalletSystem>();
            var stats = Farm.transform.Find("HUD Canvas/Stats")?.GetComponent<UnityEngine.UI.Text>();
            if (stats == null)
                foreach (var text in Farm.GetComponentsInChildren<UnityEngine.UI.Text>())
                    if (text.name == "Stats") stats = text;
            Assert.That(stats, Is.Not.Null);
            yield return null;
            hud.enabled = false;
            var before = stats.text;
            wallet.SetCoins(1234);
            yield return null;
            Assert.That(stats.text, Is.EqualTo(before));
            hud.enabled = true;
            yield return null;
            Assert.That(stats.text, Does.Contain("1234"));
        }
    }
}
