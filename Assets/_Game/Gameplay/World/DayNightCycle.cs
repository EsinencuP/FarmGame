using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MyLittleFarm.Gameplay.World
{
    /// <summary>Owns world lighting only. Crop age continues to use UTC, independently of this visual clock.</summary>
    [DefaultExecutionOrder(-200)]
    public sealed class DayNightCycle : MonoBehaviour
    {
        [Header("World clock")]
        [SerializeField, Range(12f,16f)] private float dayLengthMinutes=14f;
        [SerializeField, Range(0f,23.99f)] private float startingHour=8f;
        [Header("Authored lights")]
        [SerializeField] private Light sun;
        [SerializeField] private Light moon;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private FarmLantern[] authoredLanterns=System.Array.Empty<FarmLantern>();
        [SerializeField] private float sunAzimuth=-32f;
        [SerializeField, Range(0f,.6f)] private float moonIntensity=.34f;
        [SerializeField] private Color moonColor=new Color(.59f,.69f,1f);
        [Header("Sky Series day / night")]
        [SerializeField] private Material skyboxMaterial;
        [SerializeField, Range(0f,360f)] private float skyboxRotation;
        private Material _runtimeSkybox;
        private Material _previousSkybox;
        private CameraClearFlags _previousClearFlags;
        [Header("Lighting over 24 hours")]
        [SerializeField] private AnimationCurve sunlight=new AnimationCurve(
            new Keyframe(0,0,0,0),new Keyframe(5,0,0,0),new Keyframe(6,.16f,0,0),
            new Keyframe(9,1.05f,0,0),new Keyframe(12,1.25f,0,0),new Keyframe(16,.9f,0,0),
            new Keyframe(18,.16f,0,0),new Keyframe(19,0,0,0),new Keyframe(24,0,0,0));
        [SerializeField] private Gradient sunColors=Palette(new[]{
            new Color(.56f,.65f,1f),new Color(.68f,.58f,.81f),new Color(1f,.55f,.31f),
            new Color(1f,.90f,.72f),new Color(1f,.96f,.86f),new Color(1f,.65f,.37f),
            new Color(.60f,.58f,.85f),new Color(.56f,.65f,1f)});
        [SerializeField] private Gradient ambientColors=Palette(new[]{
            new Color(.38f,.43f,.55f),new Color(.39f,.43f,.53f),new Color(.51f,.44f,.46f),
            new Color(.59f,.67f,.75f),new Color(.62f,.70f,.78f),new Color(.53f,.46f,.42f),
            new Color(.39f,.43f,.53f),new Color(.38f,.43f,.55f)});
        [SerializeField] private Gradient skyColors=Palette(new[]{
            new Color(.055f,.085f,.16f),new Color(.13f,.16f,.28f),new Color(.68f,.43f,.45f),
            new Color(.58f,.77f,.92f),new Color(.53f,.74f,.91f),new Color(.76f,.43f,.32f),
            new Color(.08f,.11f,.21f),new Color(.055f,.085f,.16f)});

        private readonly HashSet<FarmLantern> _lanterns=new HashSet<FarmLantern>();
        private double _hour;
        private bool _initialized;
        public float Hour=>Mathf.Repeat((float)_hour,24f);
        public float StartingHour=>startingHour;
        public float DayLengthMinutes=>dayLengthMinutes;
        public float LanternWeight { get; private set; }

        private void OnEnable()
        {
            if(!Application.isPlaying)return;
            _previousSkybox=RenderSettings.skybox;
            if(worldCamera!=null)_previousClearFlags=worldCamera.clearFlags;
            if(skyboxMaterial!=null)_runtimeSkybox=new Material(skyboxMaterial){name=skyboxMaterial.name+" (Runtime)",hideFlags=HideFlags.DontSave};
            if(!_initialized){_hour=startingHour;_initialized=true;}
            foreach(var lantern in authoredLanterns)if(lantern!=null)_lanterns.Add(lantern);
            ApplyLighting();
        }

        private void OnDisable()
        {
            if(_runtimeSkybox==null)return;
            if(RenderSettings.skybox==_runtimeSkybox)
            {
                RenderSettings.skybox=_previousSkybox;
                if(worldCamera!=null)worldCamera.clearFlags=_previousClearFlags;
            }
            Destroy(_runtimeSkybox);_runtimeSkybox=null;
        }

        private void Update()
        {
            _hour=(_hour+Time.unscaledDeltaTime*24.0/(dayLengthMinutes*60.0))%24.0;
            ApplyLighting();
        }

        private void OnValidate()
        {
            dayLengthMinutes=Mathf.Clamp(dayLengthMinutes,12f,16f);
            startingHour=Mathf.Clamp(startingHour,0f,23.99f);
        }

        /// <summary>Used by save restoration and explicit Editor previews; never advances crop time.</summary>
        public void SetHour(float hour)
        {
            if(float.IsNaN(hour)||float.IsInfinity(hour))return;
            _hour=Mathf.Repeat(hour,24f);
            if(Application.isPlaying)_initialized=true;
            ApplyLighting();
        }

        public void Register(FarmLantern lantern)
        {
            if(lantern==null)return;
            _lanterns.Add(lantern);lantern.SetNightWeight(LanternWeight);
        }
        public void Unregister(FarmLantern lantern)=>_lanterns.Remove(lantern);

        private void ApplyLighting()
        {
            var hour=Hour;var time=hour/24f;
            var intensity=Mathf.Max(0f,sunlight.Evaluate(hour));
            LanternWeight=1f-Mathf.SmoothStep(0f,1f,Mathf.Clamp01(intensity/.8f));
            if(sun!=null)
            {
                sun.transform.rotation=Quaternion.Euler(hour*15f-90f,sunAzimuth,0);
                sun.color=sunColors.Evaluate(time);sun.intensity=intensity;
                RenderSettings.sun=sun;
            }
            if(moon!=null)
            {
                moon.transform.rotation=Quaternion.Euler(hour*15f+90f,sunAzimuth+30f,0);
                moon.color=moonColor;moon.intensity=moonIntensity*LanternWeight;
            }
            RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=ambientColors.Evaluate(time);
            RenderSettings.reflectionIntensity=Mathf.Lerp(.12f,.8f,1f-LanternWeight);
            ApplySkybox(hour);
            if(worldCamera!=null)
            {
                worldCamera.clearFlags=skyboxMaterial!=null?CameraClearFlags.Skybox:CameraClearFlags.SolidColor;
                worldCamera.backgroundColor=skyColors.Evaluate(time);
            }
            // Editor previews use authored references, runtime also accepts new prefab instances.
            if(!Application.isPlaying)
            {
                foreach(var lantern in authoredLanterns)if(lantern!=null&&lantern.isActiveAndEnabled)lantern.SetNightWeight(LanternWeight);
            }
            else foreach(var lantern in _lanterns)if(lantern!=null&&lantern.isActiveAndEnabled)lantern.SetNightWeight(LanternWeight);
        }

        private void ApplySkybox(float hour)
        {
            // Preview edits only our authored material. Play Mode always uses a disposable copy.
            var material=Application.isPlaying?_runtimeSkybox:skyboxMaterial;
            if(material==null)return;
            var daylight=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(5f,8f,hour))
                *(1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(17f,20f,hour)));
            var twilight=Mathf.Max(1f-Mathf.Abs(hour-6f)/2f,1f-Mathf.Abs(hour-18f)/2f);
            twilight=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(twilight));
            material.SetFloat("_Blend",1f-daylight);
            material.SetFloat("_Rotation",skyboxRotation);
            material.SetColor("_DayTint",Color.Lerp(new Color(.5f,.5f,.5f),new Color(.65f,.32f,.20f),twilight));
            RenderSettings.skybox=material;
        }

        private static Gradient Palette(Color[] colors)
        {
            var hours=new[]{0f,5f,6f,9f,12f,17f,19f,24f};
            var keys=new GradientColorKey[hours.Length];
            for(var i=0;i<hours.Length;i++)keys[i]=new GradientColorKey(colors[i],hours[i]/24f);
            var gradient=new Gradient();gradient.SetKeys(keys,new[]{new GradientAlphaKey(1f,0),new GradientAlphaKey(1f,1)});return gradient;
        }
    }
}
