#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace MyLittleFarm.Tests.PlayMode
{
    /// <summary>Runs the installed Humanoid through real CharacterController movement, directional gaits, gestures and jump phases.</summary>
    public sealed class KevinLocomotionTests : FarmPlayModeFixture
    {
        private InputReader _input;
        private PlayerController _player;
        private Animator _animator;

        private void Install()
        {
            _input=Farm.GetComponent<InputReader>();_input.enabled=false;
            _player=Farm.GetComponentInChildren<PlayerController>();
            _player.GetComponent<Renderer>().enabled=false;
            var capsule=_player.GetComponent<CharacterController>();capsule.height=1.8f;capsule.radius=0.3f;capsule.skinWidth=0.03f;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Content/Character/HumanPlayerVisual.prefab");
            var visual=Object.Instantiate(prefab,_player.transform,false);visual.name="Character Visual";visual.transform.localPosition=Vector3.down*0.93f;
            _animator=visual.GetComponentInChildren<Animator>();
            _player.Configure(_input,Farm.GetComponentInChildren<GridSystem>());
            var facing=new GameObject("Animation test facing");facing.transform.SetParent(Farm.transform,false);
            _player.SetCamera(facing.transform);_player.transform.rotation=Quaternion.identity;
            _player.Teleport(new Vector3(4.5f,0.93f,4.5f));
        }

        [UnityTest]
        public IEnumerator StrafeRunSprintAndGestureUseTheirOwnClips()
        {
            Install();yield return new WaitForSeconds(0.15f);
            Set("StrafeHeld",true);Set("Movement",Vector2.left);
            yield return WaitForClip("Walk01_Left");
            Assert.That(_animator.GetFloat("MoveX"),Is.LessThan(-0.85f));
            AssertClip("Walk01_Left");
            Assert.That(_animator.cullingMode,Is.EqualTo(AnimatorCullingMode.AlwaysAnimate));
            var arm=_animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Assert.That(Mathf.Abs(Vector3.Dot(( _animator.GetBoneTransform(HumanBodyBones.LeftHand).position-arm.position).normalized,_player.transform.right)),
                Is.LessThan(0.92f),"The skeleton must animate, not remain in a T-pose while clips tick.");
            Set("Movement",Vector2.up);Set("SprintHeld",true);
            yield return WaitForClip("Run01_Forward");AssertClip("Run01_Forward");
            var runDeadline=Time.time+1f;
            while(!_animator.GetCurrentAnimatorStateInfo(0).IsName("Run")&&Time.time<runDeadline)yield return null;
            Assert.That(_animator.GetCurrentAnimatorStateInfo(0).IsName("Run"),Is.True);
            Set("FastSprintHeld",true);
            yield return WaitForClip("Sprint01_Forward");AssertClip("Sprint01_Forward");
            Set("Movement",Vector2.zero);Set("SprintHeld",false);Set("FastSprintHeld",false);
            yield return new WaitForSeconds(0.4f);
            typeof(PlayerController).GetField("_idleSeconds",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(_player,6.2f);
            yield return WaitForClip("HumanM@Idle02");
            Set("GesturePressed",true);yield return null;Set("GesturePressed",false);
            yield return new WaitForSeconds(0.15f);AssertClip("Talk01");
            Assert.That(_animator.applyRootMotion,Is.False);
        }

        [UnityTest]
        public IEnumerator JumpUsesTakeoffRiseFallAndLandingAndReturnsToGround()
        {
            Install();yield return new WaitForSeconds(0.15f);
            Set("JumpPressed",true);yield return null;Set("JumpPressed",false);
            var states=new HashSet<string>();var maxY=_player.transform.position.y;
            for(var i=0;i<55;i++)
            {
                foreach(var state in new[]{"Jump takeoff","Jump rise","Fall","Land"})
                    if(_animator.GetCurrentAnimatorStateInfo(0).IsName(state))states.Add(state);
                maxY=Mathf.Max(maxY,_player.transform.position.y);
                yield return new WaitForSeconds(0.03f);
            }
            Assert.That(states,Is.EquivalentTo(new[]{"Jump takeoff","Jump rise","Fall","Land"}));
            Assert.That(maxY,Is.GreaterThan(1.9f).And.LessThan(2.6f));
            Assert.That(_player.GetComponent<CharacterController>().isGrounded,Is.True);
            Assert.That(_player.transform.position.y,Is.EqualTo(0.93f).Within(0.035f));
        }

        private void Set(string name,object value)=>typeof(InputReader).GetProperty(name,BindingFlags.Public|BindingFlags.Instance).SetValue(_input,value);
        private IEnumerator WaitForClip(string name)
        {
            var deadline=Time.time+1.5f;
            while(Time.time<deadline&&!_animator.GetCurrentAnimatorClipInfo(0).Any(c=>c.clip.name.Contains(name)&&c.weight>0.85f))yield return null;
            AssertClip(name);
        }
        private void AssertClip(string name)=>Assert.That(_animator.GetCurrentAnimatorClipInfo(0).Any(c=>c.clip.name.Contains(name)&&c.weight>0.4f),Is.True,
            name+" gait="+_animator.GetFloat("Gait")+" speed="+_animator.GetFloat("Speed")+" clips="+string.Join(",",_animator.GetCurrentAnimatorClipInfo(0).Select(c=>c.clip.name+":"+c.weight)));
    }
}
#endif
