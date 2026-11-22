using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Economy;
using UnityEngine;

namespace MyLittleFarm.Gameplay.World
{
    /// <summary>Metre-scale grip follows the animated hand without inheriting the imported bone scale.</summary>
    [DefaultExecutionOrder(180)]
    public sealed class HeldHoeView : MonoBehaviour
    {
        private PlayerController _player;
        private QuickSlotSystem _slots;
        private ToolUpgradeSystem _upgrades;
        private InputReader _input;
        private Transform _hand;
        private Transform _grip;
        private readonly GameObject[] _models=new GameObject[3];
        private float _workUntil;
        public void Configure(PlayerController player,QuickSlotSystem slots,ToolUpgradeSystem upgrades,InputReader input)
        {
            _player=player;_slots=slots;_upgrades=upgrades;_input=input;
            var animator=player.GetComponentInChildren<Animator>(true);
            if(animator!=null)foreach(var bone in animator.GetComponentsInChildren<Transform>(true))if(bone.name=="hand.R"){_hand=bone;break;}
            if(_hand==null && animator!=null && animator.isHuman)_hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
            if(_grip!=null)return;
            _grip=new GameObject("Held hoe grip").transform;_grip.SetParent(transform,false);
            for(var i=0;i<3;i++)
            {
                var prefab=Resources.Load<GameObject>("FarmContent/Hoe"+i);
                if(prefab==null){Debug.LogError("Prepared hoe prefab is missing: Hoe"+i,this);continue;}
                _models[i]=Instantiate(prefab,_grip,false);_models[i].SetActive(false);
            }
        }
        public void Work()=>_workUntil=Time.time+.32f;
        private void LateUpdate()
        {
            if(_player==null || _grip==null)return;
            var visible=_slots?.Mode==QuickSlotSystem.SelectionMode.Hoe && (_upgrades==null || _upgrades.HasBaseHoe) && _input!=null && !_input.BuildModeActive && !_input.ModalInputActive && !_player.HoldingFishingRod;
            _player.HoldingHoe=visible;
            var level=_upgrades?.Level??0;
            for(var i=0;i<3;i++)if(_models[i]!=null)_models[i].SetActive(visible && i==level);
            if(!visible)return;
            _grip.position=_hand!=null?_hand.position+_player.transform.rotation*new Vector3(.01f,0,.025f):transform.position+transform.rotation*new Vector3(.36f,-.35f,.16f);
            var stroke=_workUntil>Time.time?Mathf.Sin(Mathf.Clamp01(1-(_workUntil-Time.time)/.32f)*Mathf.PI):0;
            _grip.rotation=_player.transform.rotation*Quaternion.Euler(-8-28*stroke,65,-22);
        }
        private void OnDisable(){if(_player!=null)_player.HoldingHoe=false;if(_grip!=null)_grip.gameObject.SetActive(false);}
        private void OnEnable(){if(_grip!=null)_grip.gameObject.SetActive(true);}
    }
}
