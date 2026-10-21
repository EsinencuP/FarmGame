#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.Animals;
using MyLittleFarm.Gameplay.Building;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.Gameplay.Farming;
using MyLittleFarm.Gameplay.World;
using UnityEngine;

namespace MyLittleFarm.Core
{
    /// <summary>Explicit Editor-only 30-minute active playback with real gameplay/input and minute screenshots; never starts automatically.</summary>
    [DefaultExecutionOrder(-201)]
    public sealed class PhaseZeroPlaySession : MonoBehaviour
    {
        private const string Output="Verification/Phase0";
        private readonly Stopwatch _clock=new Stopwatch();
        private readonly Dictionary<string,PropertyInfo> _properties=new Dictionary<string,PropertyInfo>();
        private GameBootstrap _farm;
        private InputReader _input;
        private PlayerController _player;
        private BuildSystem _build;
        private GridSystem _grid;
        private CropSystem _crops;
        private FarmCatalog _catalog;
        private SaveSystem _save;
        private Camera _camera;
        private AnimalSystem _animals;
        private OrchardSystem _orchard;
        private int _job=-1, _minute=-1, _pulse=-1;
        private Vector2Int _buildCell;
        private float _activeSeconds;
        private int _frames, _errors, _placements, _harvests, _roundTrips;
        public bool Complete {get;private set;}
        public string Progress=>$"active={_activeSeconds:F1}/1800s wall={_clock.Elapsed.TotalSeconds:F1}s frames={_frames} errors={_errors} placeAttempts={_placements} harvestAttempts={_harvests} saves={_roundTrips}";

        public void Begin(GameBootstrap farm)
        {
            if(!Application.isPlaying || farm.gameObject.scene.name!="Phase0-Verification")
                throw new InvalidOperationException("Use a disposable Phase0-Verification scene in Play Mode.");
            _farm=farm;_input=farm.GetComponent<InputReader>();_input.enabled=false;
            _player=farm.GetComponentInChildren<PlayerController>();_build=farm.GetComponentInChildren<BuildSystem>();
            _grid=farm.GetComponentInChildren<GridSystem>();_crops=farm.GetComponentInChildren<CropSystem>();
            _catalog=farm.GetComponentInChildren<FarmCatalog>();_save=farm.GetComponentInChildren<SaveSystem>();_save.enabled=false;
            _camera=farm.GetComponentInChildren<Camera>();_camera.GetComponent<IsometricCameraController>().enabled=false;
            _animals=farm.GetComponentInChildren<AnimalSystem>();_orchard=farm.GetComponentInChildren<OrchardSystem>();
            foreach(var property in typeof(InputReader).GetProperties())if(property.CanWrite)_properties[property.Name]=property;
            farm.GetComponentInChildren<WalletSystem>().SetCoins(10000);
            farm.GetComponentInChildren<InventorySystem>().Add("wheat",100);
            _animals.AddChicken(new Vector2Int(2,3),DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            _orchard.PlantApple(new Vector2Int(6,4),DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()-120000);
            for(var i=0;i<_catalog.Crops.Count;i++)for(var stage=0;stage<3;stage++)
            {
                var cell=new Vector2Int(i,stage-6);_grid.SetCellType(cell,CellType.Tilled);
                var definition=_catalog.Crops[i];
                _crops.Plant(cell,definition.CropId,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()-(long)(definition.GrowDurationSeconds*stage*.55f*1000));
            }
            foreach(var chunk in farm.GetComponentsInChildren<ChunkRenderer>())chunk.Rebuild();
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output+"/session-journal.txt","Started UTC "+DateTime.UtcNow.ToString("O")+"\nAutomated active playback; screenshots inspected at checkpoints. timeScale=1.\n");
            Application.runInBackground=true;Time.timeScale=1f;
            Application.logMessageReceived+=Log;
            _clock.Start();
        }

        private void Set(string name,object value)=>_properties[name].SetValue(_input,value);
        private void Point(Vector3 position)=>Set("PointerPosition",(Vector2)_camera.WorldToScreenPoint(position));
        private void Frame(Vector3 position)
        {
            _camera.transform.position=position+new Vector3(-8,7,-10);
            _camera.transform.LookAt(position+Vector3.up*.7f);
        }
        private void Stand(Vector3 position)=>_player.Teleport(new Vector3(position.x,.93f,position.z));

        private void Update()
        {
            if(_farm==null || Complete)return;
            _activeSeconds+=Mathf.Min(Time.unscaledDeltaTime,.5f);_frames++;
            Set("SuppressGameplayThisFrame",false);Set("Movement",Vector2.zero);Set("HasPointer",true);
            Set("SprintHeld",false);Set("FastSprintHeld",false);Set("StrafeHeld",false);
            Set("InteractPressed",false);Set("PointerInteractPressed",false);Set("JumpPressed",false);Set("GesturePressed",false);
            Set("RotateBuildingPressed",false);Set("BuildSelection",-1);
            var job=(int)(_activeSeconds/15f);var local=_activeSeconds%15f;var mode=job%6;
            if(job!=_job)
            {
                _job=job;_pulse=-1;
                _build.SetActive(false);
                foreach(var building in _build.Capture())_build.TryRemove(building.id,out _);
                if(mode==0){Stand(new Vector3(-5.5f,0,-5.5f));Frame(new Vector3(-3,0,-3));}
                if(mode==1){Stand(new Vector3(3.5f,0,3.5f));Frame(new Vector3(3,0,3));}
                if(mode==2){Stand(new Vector3(5.2f,0,4.5f));Frame(new Vector3(6,0,4));}
                if(mode==3)
                {
                    Stand(new Vector3(3.5f,0,.5f));
                    var selected=(job/6)%BuildingDefinition.Catalog.Count;Set("BuildSelection",selected);
                    _buildCell=new Vector2Int(-8,-8);
                    var definition=BuildingDefinition.Catalog[selected];
                    // Search only within the disposable map; no authored terrain or user data is edited for construction.
                    var found=false;
                    for(var z=-8;z<9&&!found;z++)for(var x=-8;x<9&&!found;x++)
                        if(_build.CanPlace(definition.Id,new Vector2Int(x,z),0,null,out _)){_buildCell=new Vector2Int(x,z);found=true;}
                    Frame(BuildingView.Center(_buildCell.x,_buildCell.y,0,definition,_grid));_build.SetActive(true);
                }
                if(mode==4){Stand(new Vector3((job/6)%_catalog.Crops.Count+.5f,0,-2.5f));Frame(new Vector3(2,0,-4));}
                if(mode==5)
                {
                    Stand(new Vector3(3.5f,0,.5f));Frame(new Vector3(3,0,2));
                    if(_save.SaveToPath(Output+"/session-save.json") && _save.LoadFromPath(Output+"/session-save.json"))_roundTrips++;
                }
                File.AppendAllText(Output+"/session-journal.txt",$"{_activeSeconds:F1}s mode={mode} {Progress}\n");
            }
            var pulse=(int)(local/4f);var pressed=pulse!=_pulse;_pulse=pulse;
            if(mode==0)
            {
                var side=(int)(local/.4f)%4;
                Set("Movement",side==0?Vector2.up:side==1?Vector2.right:side==2?Vector2.down:Vector2.left);
                Set("SprintHeld",pulse>=1);Set("FastSprintHeld",pulse==2);Set("StrafeHeld",pulse%2==1);
                Set("JumpPressed",pressed&&pulse>0);Point(new Vector3(-4,0,-4));
            }
            if(mode==1)
            {
                var target=_animals.GetComponentInChildren<InteractionTarget>();
                if(target!=null&&target.TryGetBounds(out var bounds))
                {
                    var screen=(Vector2)_camera.WorldToScreenPoint(bounds.center);
                    Set("PointerPosition",screen+Vector2.one*Mathf.Sin(local*5f)*.75f);
                }
                Set("InteractPressed",pressed&&pulse>0);
            }
            if(mode==2)
            {
                var target=_orchard.GetComponentInChildren<InteractionTarget>();
                if(target!=null&&target.TryGetBounds(out var bounds))Point(bounds.center);
                Set("InteractPressed",pressed&&pulse>0);
            }
            if(mode==3)
            {
                Point(_grid.CellToWorld(_buildCell));
                Set("RotateBuildingPressed",pressed&&pulse>0);
                if(pressed&&pulse==3){Set("InteractPressed",true);_placements++;}
            }
            if(mode==4)
            {
                var index=(job/6)%_catalog.Crops.Count;var cell=new Vector2Int(index,-4);
                Point(_grid.CellToWorld(cell));
                if(pressed&&pulse>0)
                {
                    Set("InteractPressed",true);_harvests++;
                    if(!_crops.Contains(cell))
                    {
                        _grid.SetCellType(cell,CellType.Tilled);
                        _crops.Plant(cell,_catalog.Crops[index].CropId,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                    }
                }
            }
            if(mode==5){Point(new Vector3(8.5f,.55f,1.5f));Set("GesturePressed",pressed&&pulse==1);}
            var minute=(int)(_activeSeconds/60f);
            if(minute!=_minute)
            {
                _minute=minute;
                ScreenCapture.CaptureScreenshot(Output+"/minute-"+minute.ToString("D2")+".png");
                File.WriteAllText(Output+"/session-progress.txt",Progress+"\nUTC "+DateTime.UtcNow.ToString("O"));
            }
            if(_activeSeconds>=1800f && _clock.Elapsed.TotalSeconds>=1800)
            {
                Set("Movement",Vector2.zero);Set("InteractPressed",false);Set("JumpPressed",false);_build.SetActive(false);
                Complete=true;_clock.Stop();
                File.WriteAllText(Output+"/session-complete.txt",Progress+"\nFinished UTC "+DateTime.UtcNow.ToString("O"));
            }
        }

        private void Log(string message,string stack,LogType type)
        {
            if(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;
            _errors++;File.AppendAllText(Output+"/session-errors.txt",message+"\n"+stack+"\n");
        }
        private void OnDestroy(){Application.logMessageReceived-=Log;_clock.Stop();}
    }
}
#endif
