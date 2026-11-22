using MyLittleFarm.Core;
using UnityEngine;

namespace MyLittleFarm.Gameplay.World
{
    public enum InteractionTargetKind { Animal, Tree, Market, Building, Obstacle, SeedCache, Helper, Fishing }

    /// <summary>Pointer target. Trigger pick volumes never block movement or placement.</summary>
    public sealed class InteractionTarget : MonoBehaviour
    {
        public const string PickLayerName = "FarmInteraction";
        [SerializeField] private InteractionTargetKind kind;
        [SerializeField] private string targetId;
        [SerializeField, HideInInspector] private BoxCollider pickVolume;
        public InteractionTargetKind Kind => kind;
        public string TargetId => targetId;

        public void Configure(InteractionTargetKind targetKind, string id = null, bool addPickVolume = false)
        {
            kind = targetKind;
            targetId = id;
            if (addPickVolume && VisualPrefab.TryGetLocalBounds(gameObject, transform, out var bounds))
            {
                if (pickVolume == null)
                    foreach (var existing in GetComponents<BoxCollider>())
                        if (existing.isTrigger) { pickVolume = existing; break; }
                if (pickVolume == null) pickVolume = gameObject.AddComponent<BoxCollider>();
                pickVolume.isTrigger = true;
                pickVolume.center = bounds.center;
                pickVolume.size = bounds.size;
            }
            ApplyPickLayer();
        }

        private void OnEnable() => ApplyPickLayer();

        public void SetPickBounds(Bounds bounds)
        {
            if (pickVolume == null) pickVolume = gameObject.AddComponent<BoxCollider>();
            pickVolume.isTrigger = true; pickVolume.center = bounds.center; pickVolume.size = bounds.size;
            ApplyPickLayer();
        }

        /// <summary>Both authored and dynamically spawned targets use the same raycast layer; physics rules are unchanged.</summary>
        public void ApplyPickLayer()
        {
            var layer = LayerMask.NameToLayer(PickLayerName);
            if (layer < 0) return;
            gameObject.layer = layer;
            foreach (var collider in GetComponentsInChildren<Collider>(true))
                if (collider.GetComponentInParent<InteractionTarget>() == this) collider.gameObject.layer = layer;
        }

        public bool TryGetBounds(out Bounds bounds)
        {
            if ((kind == InteractionTargetKind.Animal || kind == InteractionTargetKind.Fishing) && pickVolume != null)
            {
                // Physics bounds can lag behind transform changes made between physics steps.
                var frame = pickVolume.transform;
                var half = pickVolume.size * 0.5f;
                var x = frame.TransformVector(Vector3.right * half.x);
                var y = frame.TransformVector(Vector3.up * half.y);
                var z = frame.TransformVector(Vector3.forward * half.z);
                var extents = new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                    Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
                    Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
                bounds = new Bounds(frame.TransformPoint(pickVolume.center), extents * 2f);
                return true;
            }
            return VisualPrefab.TryGetBounds(gameObject, out bounds);
        }
    }
}
