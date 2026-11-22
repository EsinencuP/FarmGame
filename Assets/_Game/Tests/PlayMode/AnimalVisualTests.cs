using System;
using System.Collections;
using System.Collections.Generic;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Animals;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.World;
using MyLittleFarm.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MyLittleFarm.Tests.PlayMode
{
    // Prepared in U1-4; do not execute until the major U1 closure.
    public sealed class AnimalVisualTests : FarmPlayModeFixture
    {
        private T System<T>() where T:Component=>Farm.GetComponentInChildren<T>(true);
        [UnityTest]
        public IEnumerator FinalChickenUsesSavedArtAndItsBadgeCannotCaptureClicks()
        {
            yield return null;var animals=System<AnimalSystem_AI>();
            Assert.That(animals.AddChicken(new Vector2Int(1,3),1),Is.True);
            var view=animals.GetComponentInChildren<AnimalView>();var rig=view.GetComponentInChildren<AnimalVisualRig>();
            Assert.That(rig,Is.Not.Null);Assert.That(rig.Animator.runtimeAnimatorController,Is.Not.Null);
            Assert.That(rig.Animator.HasState(0,Animator.StringToHash("Base Layer.Idle")),Is.True);
            Assert.That(rig.Animator.HasState(0,Animator.StringToHash("Base Layer.Walk")),Is.True);
            Assert.That(rig.Animator.HasState(0,Animator.StringToHash("Base Layer.Eat")),Is.True);
            var state=animals.Capture();state[0].readyProductCount=1;animals.Restore(state);yield return null;
            view=animals.GetComponentInChildren<AnimalView>();Assert.That(view.ProductReady,Is.True);
            var overlay=animals.GetComponent<AnimalStatusOverlay>();
            Assert.That(overlay.GetComponentInChildren<GraphicRaycaster>(true),Is.Null);
            foreach(var graphic in overlay.GetComponentsInChildren<Graphic>(true))Assert.That(graphic.raycastTarget,Is.False);
            var target=view.GetComponent<InteractionTarget>();Assert.That(target.TryGetBounds(out var before),Is.True);
            System<InventorySystem_AI>().Add("wheat",1);Assert.That(animals.TryCollect(state[0].animalId,out _),Is.True);
            yield return null;Assert.That(view.ProductReady,Is.False);
            Assert.That(target.TryGetBounds(out var after),Is.True);Assert.That(after.size,Is.EqualTo(before.size));
        }
        [UnityTest]
        public IEnumerator FeedingStopsTravelAndWaitingStateSurvivesJsonRestore()
        {
            yield return null;var animals=System<AnimalSystem_AI>();var items=System<InventorySystem_AI>();var now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Assert.That(animals.AddChicken(new Vector2Int(1,3),now),Is.True);items.Add("wheat",1);
            var view=animals.GetComponentInChildren<AnimalView>();view.SetWanderOffset(.02f,0);yield return new WaitForSeconds(.1f);
            var id=animals.Capture()[0].animalId;Assert.That(animals.Feed(id,now),Is.True);
            var position=view.transform.position;yield return new WaitForSeconds(.25f);
            Assert.That(view.IsEating,Is.True);Assert.That(view.ActualSpeed,Is.Zero);
            Assert.That(Vector3.Distance(view.transform.position,position),Is.LessThan(.001f));
            var save=System<SaveSystem_AI>();save.Restore(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save.Capture())));
            view=animals.GetComponentInChildren<AnimalView>();Assert.That(view.Producing,Is.True);
            Assert.That(animals.Capture()[0].nextProductAtUnixMs,Is.EqualTo(now+40000));
        }
        [UnityTest]
        public IEnumerator CowEnvelopesRemainInSeparateHalfPaddocksAfterBarnRotation()
        {
            yield return null;System<SectorSystem>().Restore(true);System<WalletSystem>().SetCoins(500);
            var build=System<BuildSystem>();Assert.That(build.TryPlace("barn",new Vector2Int(10,0),0,out var reason),Is.True,reason);
            var barn=build.GetAt(new Vector2Int(10,0));var animals=System<AnimalSystem_AI>();
            Assert.That(animals.TryPurchase(new Dictionary<string,int>{{"cow",2}},DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),out reason),Is.True,reason);
            foreach(var view in animals.GetComponentsInChildren<AnimalView>())view.SetWanderOffset(.3f,0);
            for(var frame=0;frame<60;frame++){yield return null;CheckInside(animals,0);}
            Assert.That(build.TryMove(barn.id,new Vector2Int(10,0),2,out reason),Is.True,reason);
            yield return null;CheckInside(animals,180);
        }
        private static void CheckInside(AnimalSystem_AI animals,float yaw)
        {
            foreach(var view in animals.GetComponentsInChildren<AnimalView>())
            {
                var box=view.GetComponent<BoxCollider>();
                for(var x=-1;x<=1;x+=2)for(var y=-1;y<=1;y+=2)for(var z=-1;z<=1;z+=2)
                {
                    var point=box.transform.TransformPoint(box.center+Vector3.Scale(box.size*.5f,new Vector3(x,y,z)));
                    var local=Quaternion.Euler(0,-yaw,0)*(point-view.HomePosition);
                    Assert.That(Mathf.Abs(local.x),Is.LessThanOrEqualTo(view.HomeHalfArea.x+.001f));
                    Assert.That(Mathf.Abs(local.z),Is.LessThanOrEqualTo(view.HomeHalfArea.y+.001f));
                }
            }
        }
    }
}
