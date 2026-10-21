using MyLittleFarm.Core;
using UnityEngine;

namespace MyLittleFarm.Gameplay.World
{
    /// <summary>A reusable decorative lantern with a real Point Light and instance-local emissive glass.</summary>
    public sealed class FarmLantern : MonoBehaviour
    {
        [SerializeField] private Light pointLight;
        [SerializeField] private Renderer glass;
        [SerializeField, Min(0f)] private float nightIntensity=3f;
        [SerializeField, ColorUsage(false,true)] private Color nightEmission=new Color(4.5f,2.5f,.8f);
        private DayNightCycle _cycle;
        private MaterialPropertyBlock _block;
        private float _lastWeight=-1f;
        private static readonly int EmissionColor=Shader.PropertyToID("_EmissionColor");

        private void OnValidate()
        {
            nightIntensity=Mathf.Max(0f,nightIntensity);
            _lastWeight=-1f;
        }

        private void OnEnable()
        {
            var root=GetComponentInParent<GameBootstrap>();
            _cycle=root==null?null:root.GetComponentInChildren<DayNightCycle>(true);
            if(_cycle!=null)_cycle.Register(this);
            else SetNightWeight(0f);
        }

        private void OnDisable()
        {
            if(_cycle!=null)_cycle.Unregister(this);
            SetNightWeight(0f);_cycle=null;
        }

        public void SetNightWeight(float weight)
        {
            weight=Mathf.Clamp01(weight);if(weight<.001f)weight=0f;
            if(Mathf.Abs(weight-_lastWeight)<.001f)return;
            _lastWeight=weight;
            if(pointLight!=null){pointLight.intensity=nightIntensity*weight;pointLight.enabled=weight>.005f;}
            if(glass==null)return;
            if(_block==null)_block=new MaterialPropertyBlock();
            glass.GetPropertyBlock(_block);_block.SetColor(EmissionColor,nightEmission*weight);glass.SetPropertyBlock(_block);
        }
    }
}
