using System;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using UnityEngine;

namespace MyLittleFarm.Gameplay.World
{
    /// <summary>Paints the same evaluated cells that the interaction coordinator commits.</summary>
    public sealed class FieldAreaPreview : MonoBehaviour
    {
        private InteractionSystem_AI _actions;
        private CellSelector _selector;
        private GridSystem _grid;
        private InputReader_AI _input;
        private readonly LineRenderer[] _cells=new LineRenderer[9];
        private LineRenderer _arrow;
        private Material _material;
        private readonly Vector3[] _points=new Vector3[5];
        public InteractionSystem_AI.FieldPlan VisiblePlan {get;private set;}
        public void Configure(InteractionSystem_AI actions,CellSelector selector,GridSystem grid,InputReader_AI input)
        {
            _actions=actions;_selector=selector;_grid=grid;_input=input;
            if(_material!=null)return;
            _material=new Material(Shader.Find("Sprites/Default")){hideFlags=HideFlags.DontSave};
            for(var i=0;i<_cells.Length;i++)_cells[i]=Line("Field cell "+i);
            _arrow=Line("Field direction");Hide();
        }
        private LineRenderer Line(string name)
        {
            var go=new GameObject(name);go.layer=2;go.transform.SetParent(transform,false);
            var line=go.AddComponent<LineRenderer>();line.useWorldSpace=true;line.positionCount=5;
            line.widthMultiplier=.027f;line.sharedMaterial=_material;
            line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;return line;
        }
        private void LateUpdate()
        {
            if(_selector==null || !_selector.HasSelection || _selector.HoveredTarget!=null || _input==null || _input.BuildModeActive || _input.ModalInputActive || _input.WorldPointerBlocked)
            {Hide();return;}
            Paint(_actions.PlanField(_selector.SelectedPosition,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        }
        private void Paint(InteractionSystem_AI.FieldPlan plan)
        {
            VisiblePlan=plan;
            if(plan.Action==InteractionSystem_AI.FieldAction.None){Hide();return;}
            _grid.Select(null);
            for(var i=0;i<_cells.Length;i++)
            {
                var active=i<plan.Cells.Count;_cells[i].enabled=active;if(!active)continue;
                var cell=plan.Cells[i];var p=_grid.CellToWorld(cell.Position);var h=_grid.CellSize*.44f;
                for(var c=0;c<5;c++)
                {
                    var x=p.x+(c==0||c==3||c==4?-h:h);var z=p.z+(c<2||c==4?-h:h);
                    _points[c]=new Vector3(x,_grid.GetSurfaceHeight(x,z)+.045f,z);
                }
                var color=cell.Allowed?new Color(.42f,1f,.3f):new Color(1f,.35f,.23f);
                _cells[i].startColor=_cells[i].endColor=color;_cells[i].SetPositions(_points);
            }
            _arrow.enabled=plan.Cells.Count>1;
            if(!_arrow.enabled)return;
            var center=_grid.CellToWorld(plan.Center);var dir=new Vector3(plan.Direction.x,0,plan.Direction.y)*_grid.CellSize;
            var side=new Vector3(dir.z,0,-dir.x);
            _points[0]=center-dir*.28f;_points[1]=center+dir*.28f;_points[2]=center+dir*.1f+side*.14f;
            _points[3]=_points[1];_points[4]=center+dir*.1f-side*.14f;
            for(var i=0;i<5;i++)_points[i].y=_grid.GetSurfaceHeight(_points[i].x,_points[i].z)+.065f;
            _arrow.startColor=_arrow.endColor=new Color(1f,.87f,.35f);_arrow.SetPositions(_points);
        }
        private void Hide(){VisiblePlan=null;foreach(var cell in _cells)if(cell!=null)cell.enabled=false;if(_arrow!=null)_arrow.enabled=false;}
        private void OnDisable()=>Hide();
        private void OnDestroy(){if(_material!=null)Destroy(_material);}
    }
}
