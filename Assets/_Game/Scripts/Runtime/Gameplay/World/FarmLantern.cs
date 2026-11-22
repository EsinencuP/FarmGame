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
        private bool _pathLight;
        private static readonly int EmissionColor=Shader.PropertyToID("_EmissionColor");
        public Vector3 LightPosition => pointLight == null ? transform.position : pointLight.transform.position;
        public float LightRange => pointLight == null ? 0 : pointLight.range;
        public bool CanCastShadows => !_pathLight;

        public void UseAsPathLight(GameObject pathLanternPrefab)
        {
            if (_pathLight || pathLanternPrefab == null) return;
            var original = GetComponentsInChildren<Renderer>(true);
            var pathModel = Instantiate(pathLanternPrefab, transform, false);
            var lens = pathModel.transform.Find("Lantern Lens")?.GetComponent<Renderer>();
            if (lens == null) { Destroy(pathModel); return; }
            _pathLight = true;
            foreach (var renderer in original) renderer.enabled = false;
            glass = lens;
            nightIntensity = 1.65f;
            nightEmission = new Color(3.2f, 1.85f, .68f);
            if (TryGetComponent<BoxCollider>(out var collider))
            {
                collider.center = Vector3.up * .2f;
                collider.size = new Vector3(.31f, .40f, .31f);
            }
            if (pointLight != null)
            {
                pointLight.transform.localPosition = Vector3.up * .30f;
                pointLight.range = 3.4f;
                pointLight.renderMode = LightRenderMode.Auto;
                pointLight.shadows = LightShadows.None;
            }
            ConfigureShadowLight();
            _lastWeight = -1f;
            SetNightWeight(_cycle == null ? 0f : _cycle.LanternWeight);
        }

        public void ConfigureShadowLight()
        {
            if (pointLight != null)
            {
#if UNITY_EDITOR
                pointLight.lightmapBakeType = LightmapBakeType.Realtime;
#endif
                pointLight.renderMode = _pathLight ? LightRenderMode.Auto : LightRenderMode.ForcePixel;
                pointLight.shadowBias = .025f; pointLight.shadowNormalBias = .12f;
                pointLight.shadowNearPlane = .08f; pointLight.shadowStrength = 1;
                pointLight.shadowResolution = UnityEngine.Rendering.LightShadowResolution.Medium;
            }
            // This stylised fixture uses solid cubes for glass and housing. Those placeholder
            // meshes surround the bulb and would project enormous black wedges onto the ground.
            // Keep the post/foot as real occluders; the small luminous housing does not cast.
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                if (renderer == glass || renderer.name == "Lantern Base" || renderer.name == "Lantern Cap" || renderer.name == "Corner Frame")
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        public void SetShadowEnabled(bool enabled)
        { if (pointLight != null) pointLight.shadows = enabled && CanCastShadows ? LightShadows.Soft : LightShadows.None; }

        private void OnValidate()
        {
            nightIntensity=Mathf.Max(0f,nightIntensity);
            _lastWeight=-1f;
        }

        private void OnEnable()
        {
            ConfigureShadowLight();
            var root=GetComponentInParent<GameBootstrap>();
            _cycle=root==null?null:root.GetComponentInChildren<DayNightCycle>(true);
            if(_cycle!=null)_cycle.Register(this);
            else SetNightWeight(0f);
        }

        private void OnDisable()
        {
            if(_cycle!=null)_cycle.Unregister(this);
            SetShadowEnabled(false);
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
