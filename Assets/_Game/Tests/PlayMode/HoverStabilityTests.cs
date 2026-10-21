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
    /// <summary>Phase-zero regressions for object-first picking, transient misses and a moving target's identity.</summary>
    public sealed class HoverStabilityTests : FarmPlayModeFixture
    {
        [UnityTest]
        public IEnumerator MovingSameChickenAcrossCellEdgeKeepsDwellAndNeverSelectsGround()
        {
            Farm.GetComponent<InputReader>().enabled=false;
            var selector=Farm.GetComponentInChildren<CellSelector>();selector.enabled=false;
            var animals=Farm.GetComponentInChildren<AnimalSystem>();animals.AddChicken(new Vector2Int(1,3),1);animals.enabled=false;
            var target=animals.GetComponentInChildren<InteractionTarget>();target.GetComponent<AnimalView>().enabled=false;
            selector.SelectRay(new Ray(target.transform.position+Vector3.up*5,Vector3.down));
            yield return new WaitForSeconds(.2f);
            Assert.That(selector.HoveredTarget,Is.EqualTo(target));
            Assert.That(selector.IsSelectionStable,Is.True);
            Assert.That(target.gameObject.layer,Is.EqualTo(LayerMask.NameToLayer(InteractionTarget.PickLayerName)));
            target.transform.position+=Vector3.right;Physics.SyncTransforms();
            selector.SelectRay(new Ray(target.transform.position+Vector3.up*5,Vector3.down));
            Assert.That(selector.IsSelectionStable,Is.True,"A moving target's cell is not its hover identity.");
            Assert.That(Farm.GetComponentInChildren<GridSystem>().SelectedCell.HasValue,Is.False);
            target.enabled=false;selector.SelectRay(new Ray(target.transform.position+Vector3.up*5,Vector3.down));
            Assert.That(selector.HoveredTarget,Is.Null);
        }

        [UnityTest]
        public IEnumerator BriefMissRetainsPublishedPromptUntilNewHoverSettles()
        {
            var input=Farm.GetComponent<InputReader>();input.enabled=false;
            var selector=Farm.GetComponentInChildren<CellSelector>();selector.enabled=false;
            var animals=Farm.GetComponentInChildren<AnimalSystem>();animals.AddChicken(new Vector2Int(1,3),1);
            var target=animals.GetComponentInChildren<InteractionTarget>();selector.SelectTarget(target);
            var interaction=Farm.GetComponentInChildren<InteractionSystem>();
            yield return new WaitForSeconds(.25f);
            var original=interaction.CurrentPrompt;Assert.That(original,Does.Contain("Кури"));
            selector.SelectRay(new Ray(new Vector3(5000,10,5000),Vector3.down));
            interaction.SendMessage("Update");
            Assert.That(interaction.CurrentPrompt,Is.EqualTo(original));
            Assert.That(selector.IsSelectionStable,Is.False);
            selector.SelectTarget(target);
            yield return new WaitForSeconds(.25f);
            Assert.That(interaction.CurrentPrompt,Is.EqualTo(original));
        }
    }
}
