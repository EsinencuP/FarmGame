using System.Collections.Generic;
using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Animals;
using MyLittleFarm.Gameplay.World;
using UnityEngine;
using UnityEngine.UI;

namespace MyLittleFarm.UI
{
    /// <summary>Small non-interactive state badges; avoids both animal silhouettes and other badges.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class AnimalStatusOverlay : MonoBehaviour
    {
        private sealed class Badge
        {
            public AnimalView View;
            public RectTransform Rect;
            public AnimalBadgeRing Ring;
            public Image Icon;
            public Rect Body;
            public bool OnScreen, Occluded;
            public float NextOcclusion;
        }
        private readonly List<Badge> _badges=new List<Badge>(16);
        private readonly List<Rect> _used=new List<Rect>(16);
        private static readonly Vector2[] Offsets={Vector2.zero,new Vector2(0,34),new Vector2(-34,0),new Vector2(34,0),new Vector2(-34,34),new Vector2(34,34),new Vector2(0,68)};
        private RectTransform _canvas;
        private Camera _camera;
        private InputReader_AI _input;
        private int _occlusionMask;
        public void Configure(InputReader_AI input,Camera camera)
        {
            _input=input;_camera=camera;
            if(_canvas!=null)return;
            _canvas=FarmUITheme.Rect(transform,"Animal state badges");var canvas=_canvas.gameObject.AddComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=8;
            var group=_canvas.gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;group.interactable=false;
            _occlusionMask=~0; // Trigger animal pick volumes are excluded by QueryTriggerInteraction.Ignore.
        }
        public void Register(AnimalView view)
        {
            if(_canvas==null)return;
            var rect=FarmUITheme.Rect(_canvas,"State "+view.AnimalId);rect.sizeDelta=new Vector2(30,30);
            rect.anchorMin=rect.anchorMax=Vector2.zero;rect.pivot=Vector2.one*.5f;
            var ring=rect.gameObject.AddComponent<AnimalBadgeRing>();ring.raycastTarget=false;
            var icon=FarmUITheme.Icon(rect,FarmUITheme.ItemIcon(view.ProductId));FarmUITheme.Stretch(icon.rectTransform,5);icon.raycastTarget=false;
            _badges.Add(new Badge{View=view,Rect=rect,Ring=ring,Icon=icon});rect.gameObject.SetActive(false);
        }
        public void Clear()
        {foreach(var badge in _badges)if(badge.Rect!=null){badge.Rect.gameObject.SetActive(false);Destroy(badge.Rect.gameObject);}_badges.Clear();}
        private void OnDisable(){if(_canvas!=null)_canvas.gameObject.SetActive(false);}
        private void OnDestroy()=>Clear();
        private void LateUpdate()
        {
            if(_canvas==null || _camera==null)return;
            var hidden=_input==null || _input.ModalInputActive || _input.BuildModeActive || FarmSceneFlow.IsLoading;
            _canvas.gameObject.SetActive(!hidden);if(hidden)return;
            _used.Clear();
            for(var i=_badges.Count-1;i>=0;i--)
            {
                var badge=_badges[i];
                if(badge.View==null){if(badge.Rect!=null)Destroy(badge.Rect.gameObject);_badges.RemoveAt(i);continue;}
                badge.Rect.gameObject.SetActive(false);badge.OnScreen=badge.View.gameObject.activeInHierarchy && ProjectBounds(badge.View.TargetBounds,out badge.Body);
            }
            // Ready products claim the closest clear position before lower-priority waiting/feeding indicators.
            for(var pass=0;pass<2;pass++)foreach(var badge in _badges)
            {
                var view=badge.View;var ready=view.ProductReady;
                if(!badge.OnScreen || (pass==0)!=ready || (!ready && !view.Producing && !view.IsEating))continue;
                if(Time.unscaledTime>=badge.NextOcclusion)
                {badge.NextOcclusion=Time.unscaledTime+.2f;badge.Occluded=Physics.Linecast(_camera.transform.position,view.BadgeAnchor,_occlusionMask,QueryTriggerInteraction.Ignore);}
                if(badge.Occluded)continue;
                var anchor=_camera.WorldToScreenPoint(view.BadgeAnchor);if(anchor.z<=0)continue;
                var origin=new Vector2(anchor.x,Mathf.Max(anchor.y,badge.Body.yMax+22));
                foreach(var offset in Offsets)
                {
                    var center=origin+offset;var rect=new Rect(center.x-17,center.y-17,34,34);
                    if(rect.xMin<4 || rect.yMin<4 || rect.xMax>Screen.width-4 || rect.yMax>Screen.height-4 || Blocked(rect,badge))continue;
                    badge.Rect.anchoredPosition=center;
                    badge.Icon.sprite=FarmUITheme.Sprite("Icons/"+(view.IsEating?"feed":FarmUITheme.ItemIcon(view.ProductId)));
                    badge.Icon.color=ready||view.IsEating?Color.white:new Color(.72f,.77f,.78f,1);
                    badge.Ring.Set(ready?1:view.IsEating?0:view.ProductionProgress,ready?new Color(.35f,.60f,.16f):new Color(.32f,.57f,.68f));
                    badge.Rect.gameObject.SetActive(true);_used.Add(rect);break;
                }
            }
        }
        private bool ProjectBounds(Bounds b,out Rect rect)
        {
            var min=new Vector2(float.MaxValue,float.MaxValue);var max=new Vector2(float.MinValue,float.MinValue);
            for(var x=-1;x<=1;x+=2)for(var y=-1;y<=1;y+=2)for(var z=-1;z<=1;z+=2)
            {var point=_camera.WorldToScreenPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z)));if(point.z<_camera.nearClipPlane){rect=default;return false;}min=Vector2.Min(min,point);max=Vector2.Max(max,point);}
            rect=Rect.MinMaxRect(min.x-2,min.y-2,max.x+2,max.y+2);return rect.xMax>=0&&rect.xMin<=Screen.width&&rect.yMax>=0&&rect.yMin<=Screen.height;
        }
        private bool Blocked(Rect rect,Badge self)
        {
            foreach(var other in _badges)if(other!=self && other.OnScreen && rect.Overlaps(other.Body))return true;
            foreach(var other in _used)if(rect.Overlaps(other))return true;return false;
        }
    }
    /// <summary>Texture-free cream disc and determinate ring; never participates in pointer picking.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AnimalBadgeRing : MaskableGraphic
    {
        private float _fill;
        private Color _progress;
        public void Set(float fill,Color tint)
        {if(Mathf.Abs(fill-_fill)<.005f && tint==_progress)return;_fill=fill;_progress=tint;SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();const int count=32;var radius=rectTransform.rect.width*.5f;
            for(var i=0;i<count;i++)
            {
                var a=(.25f-(float)i/count)*Mathf.PI*2;var b=(.25f-(float)(i+1)/count)*Mathf.PI*2;
                var p=new Vector2(Mathf.Cos(a),Mathf.Sin(a));var q=new Vector2(Mathf.Cos(b),Mathf.Sin(b));
                var index=vh.currentVertCount;vh.AddVert(Vector3.zero,FarmUITheme.Cream,Vector2.zero);vh.AddVert(p*(radius-2),FarmUITheme.Cream,Vector2.zero);vh.AddVert(q*(radius-2),FarmUITheme.Cream,Vector2.zero);vh.AddTriangle(index,index+1,index+2);
                var tint=(i+.5f)/count<=_fill?_progress:FarmUITheme.Muted;index=vh.currentVertCount;
                vh.AddVert(p*radius,tint,Vector2.zero);vh.AddVert(q*radius,tint,Vector2.zero);vh.AddVert(q*(radius-2),tint,Vector2.zero);vh.AddVert(p*(radius-2),tint,Vector2.zero);
                vh.AddTriangle(index,index+1,index+2);vh.AddTriangle(index,index+2,index+3);
            }
        }
    }
}
