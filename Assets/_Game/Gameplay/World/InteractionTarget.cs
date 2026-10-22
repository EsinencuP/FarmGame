using MyLittleFarm.Core;
using UnityEngine;

namespace MyLittleFarm.Gameplay.World
{
    public enum InteractionTargetKind { Animal, Tree, Market, Building, Obstacle, SeedCache, Helper }

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

        /// <summary>Both authored and dynamically spawned targets use the same raycast layer; physics rules are unchanged.</summary>
        public void ApplyPickLayer()
        {
            var layer = LayerMask.NameToLayer(PickLayerName);
            if (layer < 0) return;
            gameObject.layer = layer;
            foreach (var collider in GetComponentsInChildren<Collider>(true))
                if (collider.GetComponentInParent<InteractionTarget>() == this) collider.gameObject.layer = layer;
        }

        public bool TryGetBounds(out Bounds bounds) => VisualPrefab.TryGetBounds(gameObject, out bounds);
    }
}
