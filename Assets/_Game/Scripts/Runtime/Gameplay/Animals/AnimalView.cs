using System;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.World;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Animals
{
    /// <summary>Bounded, forward-facing locomotion. Saved art and animation follow actual displacement.</summary>
    public sealed class AnimalView : MonoBehaviour
    {
        private static readonly int SpeedHash=Animator.StringToHash("Speed"), EatingHash=Animator.StringToHash("Eating"), StrideHash=Animator.StringToHash("StrideRate");
        private AnimalVisualRig _rig;
        private Animator _animator;
        private GridSystem _grid;
        private BoxCollider _pick;
        private InteractionTarget _pointer;
        private BoxCollider _body;
        private CharacterController _playerBody;
        private Vector3 _home, _offset, _target, _normal=Vector3.up;
        private Quaternion _homeYaw=Quaternion.identity;
        private Vector2 _halfArea=new Vector2(.48f,.48f);
        private float _yaw, _nextMotionAt, _eatingUntil;
        private float[] _footOffsets;
        private uint _random;
        private bool _manualTarget;
        private int _ready;
        private long _fedAt, _readyAt;
        public string AnimalId { get; private set; }
        public string ProductId { get; private set; }
        public float ActualSpeed { get; private set; }
        public bool IsEating => Time.time < _eatingUntil;
        public bool ProductReady => _ready>0 || (_readyAt>0 && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()>=_readyAt);
        public bool Producing => _readyAt>0 && !ProductReady;
        public float ProductionProgress
        { get {var duration=_readyAt-_fedAt;return duration<=0?0:Mathf.Clamp01((float)((double)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()-_fedAt)/duration));} }
        public Vector3 BadgeAnchor => transform.position+Vector3.up*_rig.BadgeHeight;
        public Bounds TargetBounds
        {
            get
            {
                _pointer.TryGetBounds(out var bounds);
                return bounds;
            }
        }
        public Vector2 HomeHalfArea => _halfArea;
        public Vector3 HomePosition => _home;
        public static AnimalView Create(Vector3 position,AnimalDefinition definition,Transform parent,string animalId=null,
            IWorldGridReader grid=null, CharacterController playerBody=null)
        {
            var prefab=definition.VisualPrefab;
            if(prefab==null && definition.AnimalId=="chicken") prefab=Resources.Load<GameObject>("FarmContent/ChickenVisual");
            if(prefab==null)throw new MissingReferenceException("Final animal prefab is missing: "+definition.AnimalId);
            var root=new GameObject("Animal "+definition.AnimalId);root.transform.SetParent(parent,false);root.transform.position=position;
            var model=Instantiate(prefab,root.transform,false);var view=root.AddComponent<AnimalView>();
            view.AnimalId=animalId;view.ProductId=definition.ProductItemId;view._grid=grid as GridSystem;
            view._rig=model.GetComponent<AnimalVisualRig>();
            if(view._rig==null)throw new MissingComponentException("Animal visual rig is missing: "+definition.AnimalId);
            view._animator=view._rig.Animator;view._animator.applyRootMotion=false;view._animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            view._footOffsets=new float[view._rig.FootBones.Length];
            view._random=2166136261u;foreach(var ch in animalId??definition.AnimalId)view._random=(view._random^ch)*16777619u;
            var target=root.AddComponent<InteractionTarget>();target.Configure(InteractionTargetKind.Animal,animalId);
            target.SetPickBounds(view._rig.PickBounds);view._pick=root.GetComponent<BoxCollider>();
            view._pointer = target;
            view._playerBody = playerBody;
            view._body = root.AddComponent<BoxCollider>();
            var pickBounds = view._rig.PickBounds;
            var cow = definition.AnimalId == "cow";
            // Shoulders and torso carry collision. Heads, tails and idle animation stay in the pick volume.
            view._body.center = new Vector3(0f, cow ? 0.65f : 0.24f, 0f);
            view._body.size = Vector3.Scale(pickBounds.size, new Vector3(0.9f, cow ? 0.9f : 0.72f, cow ? 0.59f : 0.75f));
            view.SetHabitat(position,0,definition.AnimalId=="cow" ? new Vector2(1.78f,1.40f)
                : new Vector2(.48f,.48f)*(view._grid==null?1:view._grid.CellSize));
            return view;
        }
        private float Random01(){_random^=_random<<13;_random^=_random>>17;_random^=_random<<5;return (_random&0xffffff)/16777216f;}
        public void SetHabitat(Vector3 center,float yaw,Vector2 halfArea)
        {
            UndoFootOffsets();_home=center;_homeYaw=Quaternion.Euler(0,yaw,0);_halfArea=halfArea;
            _offset=_target=Vector3.zero;_yaw=yaw+Random01()*360;_manualTarget=false;
            _nextMotionAt=Time.time+1+Random01()*2;ActualSpeed=0;
            PlaceOnGround(center,Quaternion.Euler(0,_yaw,0),out var rotation);
            if(!FitsHome(center,rotation))
            {
                var found=false;
                for(var turn=0;turn<4;turn++)
                {
                    _yaw=yaw+turn*90;PlaceOnGround(center,Quaternion.Euler(0,_yaw,0),out rotation);
                    if(FitsHome(center,rotation)){found=true;break;}
                }
                // In a tight enclosure keep the body upright; per-foot residuals still follow the surface.
                if(!found){_yaw=yaw;rotation=_homeYaw;_normal=Vector3.up;}
            }
            transform.rotation=rotation;
        }
        public void Rebase(Vector3 position,float yaw)=>SetHabitat(position,yaw,_halfArea);
        public void SetWanderOffset(float x,float z)
        { _manualTarget=true;_target=SafeTarget(new Vector3(x,0,z)); }
        public void PlayEating()
        { _eatingUntil=Time.time+2.4f;_target=_offset;_nextMotionAt=_eatingUntil+1;ActualSpeed=0; }
        public void SyncProduction(AnimalRuntimeState state,long now)
        {
            if(state.lastFedAtUnixMs!=_fedAt && state.nextProductAtUnixMs>0)
            {var remaining=2.4f-(now-state.lastFedAtUnixMs)/1000f;if(remaining>0)_eatingUntil=Time.time+remaining;}
            if (_readyAt > 0 && _ready == 0 && state.readyProductCount > 0 && Time.timeScale > 0)
                GameEvents.RaiseActionFeedback("animal-ready", transform.position);
            _ready=state.readyProductCount;_fedAt=state.lastFedAtUnixMs;_readyAt=state.nextProductAtUnixMs;
        }
        public float HorizontalDistanceSquared(Vector3 player)
        {
            var local = transform.InverseTransformPoint(player) - _pick.center;
            var half = _pick.size * 0.5f;
            local = new Vector3(Mathf.Clamp(local.x, -half.x, half.x), Mathf.Clamp(local.y, -half.y, half.y),
                Mathf.Clamp(local.z, -half.z, half.z));
            var delta = transform.TransformPoint(local + _pick.center) - player;
            delta.y = 0f;
            return delta.sqrMagnitude;
        }
        private Vector3 SafeTarget(Vector3 value)
        {
            var bounds=_rig.PickBounds;
            var radius=Mathf.Sqrt(Mathf.Pow(Mathf.Max(Mathf.Abs(bounds.min.x),Mathf.Abs(bounds.max.x)),2)+Mathf.Pow(Mathf.Max(Mathf.Abs(bounds.min.z),Mathf.Abs(bounds.max.z)),2));
            var margin=.025f;
            var x=Mathf.Max(0,_halfArea.x-radius-margin);var z=Mathf.Max(0,_halfArea.y-radius-margin);
            return new Vector3(Mathf.Clamp(value.x,-x,x),0,Mathf.Clamp(value.z,-z,z));
        }
        private void Update()
        {
            if(_rig==null)return;UndoFootOffsets();var dt=Time.deltaTime;
            if(dt<=0){ActualSpeed=0;return;}
            if(!IsEating && !_manualTarget && (_target-_offset).sqrMagnitude<.00001f && Time.time>=_nextMotionAt)
            {
                _target=SafeTarget(new Vector3((Random01()*2-1)*.6f,0,(Random01()*2-1)*.3f));
                _nextMotionAt=Time.time+2+Random01()*3;
            }
            var before=transform.position;var delta=_homeYaw*(_target-_offset);
            if(!IsEating && delta.sqrMagnitude>.00001f)
            {
                var desired=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;
                _yaw=Mathf.MoveTowardsAngle(_yaw,desired,180*dt);
                if(Mathf.Abs(Mathf.DeltaAngle(_yaw,desired))<3)
                    _offset=Vector3.MoveTowards(_offset,_target,_rig.MoveSpeed*dt);
            }
            else if(IsEating)_target=_offset;
            var position=_home+_homeYaw*_offset;
            PlaceOnGround(position,Quaternion.Euler(0,_yaw,0),out var rotation);
            // Exact projected envelope containment also covers the turn and terrain lean.
            if(!FitsHome(position,rotation) || OverlapsPlayer(transform.position, rotation))
            {transform.position=before;_normal=transform.up;_offset=Quaternion.Inverse(_homeYaw)*(before-_home);_offset.y=0;_target=_offset;ActualSpeed=0;}
            else
            {transform.rotation=rotation;var moved=transform.position-before;moved.y=0;ActualSpeed=moved.magnitude/dt;}
            if(IsEating)ActualSpeed=0;
            _animator.SetBool(EatingHash,IsEating);_animator.SetFloat(SpeedHash,ActualSpeed);
            _animator.SetFloat(StrideHash,Mathf.Max(.05f,ActualSpeed/_rig.GaitSpeed));
        }
        private bool OverlapsPlayer(Vector3 position, Quaternion rotation)
        {
            return _playerBody != null && _playerBody.enabled
                && Physics.ComputePenetration(_body, position, rotation,
                    _playerBody, _playerBody.transform.position, _playerBody.transform.rotation, out _, out _);
        }
        private float Height(float x,float z)=>_grid==null?_home.y:_grid.GetSurfaceHeight(x,z);
        private void PlaceOnGround(Vector3 position,Quaternion yaw,out Quaternion rotation)
        {
            var right=yaw*Vector3.right;var forward=yaw*Vector3.forward;
            var w=ProductId=="milk"?.23f:.09f;var d=ProductId=="milk"?.74f:.06f;
            var l=Height(position.x-right.x*w,position.z-right.z*w);var r=Height(position.x+right.x*w,position.z+right.z*w);
            var b=Height(position.x-forward.x*d,position.z-forward.z*d);var f=Height(position.x+forward.x*d,position.z+forward.z*d);
            _normal=(Vector3.up-right*((r-l)/(w*2))-forward*((f-b)/(d*2))).normalized;
            rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(forward,_normal),_normal);
            position.y=Height(position.x,position.z);transform.position=position;
        }
        private bool FitsHome(Vector3 world,Quaternion rotation)
        {
            var local=Quaternion.Inverse(_homeYaw)*(world-_home);var q=Quaternion.Inverse(_homeYaw)*rotation;var b=_rig.PickBounds;
            for(var x=-1;x<=1;x+=2)for(var y=-1;y<=1;y+=2)for(var z=-1;z<=1;z+=2)
            {var p=local+q*(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z)));if(Mathf.Abs(p.x)>_halfArea.x || Mathf.Abs(p.z)>_halfArea.y)return false;}
            return true;
        }
        private void UndoFootOffsets()
        {
            if(_rig==null || _footOffsets==null)return;
            for(var i=0;i<_footOffsets.Length;i++){_rig.FootBones[i].position-=Vector3.up*_footOffsets[i];_footOffsets[i]=0;}
        }
        private void LateUpdate()
        {
            if(_rig==null || _normal.y<.1f)return;
            // Small per-foot residuals follow the two actual terrain triangles, preserving authored swing lift.
            for(var i=0;i<_footOffsets.Length;i++)
            {
                var contact=_rig.FootContacts[i].position;var offset=contact-transform.position;
                var plane=transform.position.y-(_normal.x*offset.x+_normal.z*offset.z)/_normal.y;
                var correction=Mathf.Clamp(Height(contact.x,contact.z)-plane,-.10f,.10f);
                _rig.FootBones[i].position+=Vector3.up*correction;_footOffsets[i]=correction;
            }
        }
    }
}
