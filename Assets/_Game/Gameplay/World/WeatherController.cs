using System;
using UnityEngine;
using UnityEngine.Audio;

namespace MyLittleFarm.Gameplay.World
{
    public enum WeatherState { Sunny, Cloudy, LightRain }

    [Serializable]
    public sealed class WeatherSaveData
    {
        public int targetState;
        public float cloudCover;
        public float rainIntensity;
        public float transitionRemaining;
        public float holdRemaining;
        public float cloudRotation;
        public int randomState;
    }

    /// <summary>Visual weather and a session-only integral of the extra growth earned under rain.</summary>
    [DefaultExecutionOrder(-250)]
    public sealed class WeatherController : MonoBehaviour
    {
        [SerializeField] private DayNightCycle lighting;
        [SerializeField] private Transform focus;
        [SerializeField] private ParticleSystem rainParticles;
        [SerializeField] private AudioSource rainAudio;
        [SerializeField] private AudioMixerGroup ambienceBus;
        [Header("Weather schedule (real seconds)")]
        [SerializeField] private WeatherState startingState=WeatherState.Sunny;
        [SerializeField] private bool automaticWeather=true;
        [SerializeField, Range(30f,60f)] private float transitionSeconds=45f;
        [SerializeField] private Vector2 holdSeconds=new Vector2(180f,300f);
        [SerializeField] private int randomSeed=271828;
        [Header("Presentation")]
        [SerializeField, Range(.01f,.4f)] private float cloudDegreesPerSecond=.12f;
        [SerializeField, Range(-40f,0f)] private float rainGainDb=-12f;
        [SerializeField, Min(0f)] private float fullRainEmission=900f;

        private WeatherState _target;
        private float _cloudCover, _rainIntensity, _fromCloud, _fromRain;
        private float _transitionElapsed, _transitionDuration, _holdRemaining, _cloudRotation;
        private uint _random;
        private bool _initialized;
        public WeatherState TargetState=>_target;
        public float CloudCover=>_cloudCover;
        public float RainIntensity=>_rainIntensity;
        public float TransitionRemaining=>Mathf.Max(0f,_transitionDuration-_transitionElapsed);
        public float GrowthMultiplier=>1f+.2f*_rainIntensity;
        // Never restored from disk: per-crop earned seconds are saved separately, once.
        public double GrowthBonusSeconds { get; private set; }

        private void OnEnable()
        {
            if(!Application.isPlaying)return;
            Initialize();
            if(rainAudio!=null){rainAudio.outputAudioMixerGroup=ambienceBus;rainAudio.loop=true;rainAudio.playOnAwake=false;}
            if (MyLittleFarm.Audio.FarmAudio.IsReady && rainAudio != null) rainAudio.Stop();
            ApplyPresentation();
        }

        private void Initialize()
        {
            if(!_initialized)
            {
                _random=unchecked((uint)randomSeed);if(_random==0)_random=271828;
                _target=startingState;Weights(_target,out _cloudCover,out _rainIntensity);
                _holdRemaining=NextHold();_initialized=true;
            }
        }

        private void Update()
        {
            if (Time.timeScale <= 0) return;
            // Walk through transition/hold boundaries even on a slow frame. Integrate the
            // smoothstep exactly, so fading rain earns only its actual fractional bonus.
            var remaining=(double)Time.unscaledDeltaTime;
            while(remaining>0)
            {
                if(TransitionRemaining>0)
                {
                    var step=Math.Min(remaining,TransitionRemaining);
                    var a=(double)_transitionElapsed/_transitionDuration;
                    _transitionElapsed=Mathf.Min(_transitionDuration,_transitionElapsed+(float)step);
                    var b=(double)_transitionElapsed/_transitionDuration;
                    float targetCloud,targetRain;Weights(_target,out targetCloud,out targetRain);
                    GrowthBonusSeconds+=.2*(_fromRain*step+(targetRain-_fromRain)*_transitionDuration*(SmoothIntegral(b)-SmoothIntegral(a)));
                    var blend=Mathf.SmoothStep(0f,1f,(float)b);
                    _cloudCover=Mathf.Lerp(_fromCloud,targetCloud,blend);
                    _rainIntensity=Mathf.Lerp(_fromRain,targetRain,blend);
                    remaining-=step;
                }
                else if(automaticWeather)
                {
                    var step=Math.Min(remaining,Math.Max(0,_holdRemaining));
                    GrowthBonusSeconds+=.2*_rainIntensity*step;
                    _holdRemaining=Mathf.Max(0,_holdRemaining-(float)step);remaining-=step;
                    if(_holdRemaining<=0)RequestState(NextState());
                }
                else {GrowthBonusSeconds+=.2*_rainIntensity*remaining;remaining=0;}
            }
            _cloudRotation=Mathf.Repeat(_cloudRotation+cloudDegreesPerSecond*Time.unscaledDeltaTime,360f);
            ApplyPresentation();
        }

        private static double SmoothIntegral(double t)=>t*t*t-.5*t*t*t*t;

        public void RequestState(WeatherState state)
        {
            if(!Enum.IsDefined(typeof(WeatherState),state))return;
            Initialize();
            _fromCloud=_cloudCover;_fromRain=_rainIntensity;_target=state;
            _transitionElapsed=0;_transitionDuration=transitionSeconds;_holdRemaining=NextHold();
        }

        private WeatherState NextState()
        {
            if(_target==WeatherState.Sunny||_target==WeatherState.LightRain)return WeatherState.Cloudy;
            return Next01()<.5f?WeatherState.Sunny:WeatherState.LightRain;
        }
        private float NextHold()=>Mathf.Lerp(holdSeconds.x,holdSeconds.y,Next01());
        private float Next01()
        {
            if(_random==0)_random=271828;
            _random^=_random<<13;_random^=_random>>17;_random^=_random<<5;
            return (_random&0xFFFFFF)/16777216f;
        }
        private static void Weights(WeatherState state,out float clouds,out float rain)
        {clouds=state==WeatherState.Sunny?0f:state==WeatherState.Cloudy?.8f:1f;rain=state==WeatherState.LightRain?1f:0f;}

        private void ApplyPresentation()
        {
            if(lighting!=null)lighting.SetWeather(_cloudCover,_rainIntensity,_cloudRotation);
            if(rainParticles!=null)
            {
                if(focus!=null)rainParticles.transform.position=focus.position+Vector3.up*10f;
                var emission=rainParticles.emission;emission.rateOverTime=fullRainEmission*_rainIntensity;
                if(Application.isPlaying)
                {
                    if(_rainIntensity>.001f&&!rainParticles.isPlaying)rainParticles.Play();
                    else if(_rainIntensity<=.001f&&rainParticles.isPlaying)rainParticles.Stop(true,ParticleSystemStopBehavior.StopEmitting);
                }
            }
            if (Application.isPlaying && MyLittleFarm.Audio.FarmAudio.IsReady)
                MyLittleFarm.Audio.FarmAudio.SetRain(_rainIntensity, rainGainDb);
            else if(rainAudio!=null&&Application.isPlaying)
            {
                // The source's fade follows precipitation; the exposed gain is measured in dB.
                rainAudio.volume=Mathf.Pow(10f,rainGainDb/20f)*_rainIntensity;
                if(_rainIntensity>.001f&&!rainAudio.isPlaying)rainAudio.Play();
                else if(_rainIntensity<=.001f&&rainAudio.isPlaying)rainAudio.Stop();
            }
        }

        public WeatherSaveData Capture()
        {
            Initialize();
            return new WeatherSaveData {
                targetState=(int)_target,cloudCover=_cloudCover,rainIntensity=_rainIntensity,
                transitionRemaining=TransitionRemaining,holdRemaining=_holdRemaining,
                cloudRotation=_cloudRotation,randomState=unchecked((int)_random)};
        }

        public void Restore(WeatherSaveData data)
        {
            if(!IsValid(data))throw new ArgumentException("Invalid weather snapshot.");
            _target=(WeatherState)data.targetState;_cloudCover=_fromCloud=data.cloudCover;_rainIntensity=_fromRain=data.rainIntensity;
            _transitionDuration=data.transitionRemaining;_transitionElapsed=0;_holdRemaining=data.holdRemaining;
            _cloudRotation=data.cloudRotation;_random=unchecked((uint)data.randomState);_initialized=true;
            ApplyPresentation();
        }
        public static bool IsValid(WeatherSaveData data)=>data!=null&&data.targetState>=0&&data.targetState<=2
            &&InRange(data.cloudCover,0,1)&&InRange(data.rainIntensity,0,1)&&data.rainIntensity<=data.cloudCover+.001f
            &&InRange(data.transitionRemaining,0,60)&&InRange(data.holdRemaining,0,86400)
            &&InRange(data.cloudRotation,0,360)&&data.randomState!=0;
        private static bool InRange(float x,float min,float max)=>!float.IsNaN(x)&&!float.IsInfinity(x)&&x>=min&&x<=max;

        /// <summary>Explicit Edit Mode preview. Never advances the clock, crops or audio playback.</summary>
        public void Preview(WeatherState state)
        {
            if(Application.isPlaying)return;
            Weights(state,out _cloudCover,out _rainIntensity);ApplyPresentation();
            if(lighting!=null)lighting.SetHour(lighting.Hour);
            if(rainParticles!=null)
            {
                rainParticles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                if(_rainIntensity>0){rainParticles.Simulate(.8f,true,true,false);rainParticles.Pause();}
            }
        }
        public void RestorePreview()
        {
            if(Application.isPlaying)return;
            _cloudRotation=0;Preview(startingState);
            if(rainParticles!=null)rainParticles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        private void OnDisable()
        {
            if(rainParticles!=null)rainParticles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            if(rainAudio!=null)rainAudio.Stop();
            if(lighting!=null)lighting.SetWeather(0,0,0);
        }
        private void OnValidate()
        {
            transitionSeconds=Mathf.Clamp(transitionSeconds,30,60);
            holdSeconds.x=Mathf.Clamp(holdSeconds.x,30,86400);holdSeconds.y=Mathf.Clamp(holdSeconds.y,holdSeconds.x,86400);
        }
    }
}
