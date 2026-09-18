using System.Collections.Generic;
using UnityEngine;

namespace MyLittleFarm.Core
{
    // One material per color per farm, with explicit lifetime ownership.
    public sealed class RuntimeMaterials : MonoBehaviour
    {
        private readonly Dictionary<Color, Material> _materials = new Dictionary<Color, Material>();

        public Material Get(Color color)
        {
            if (_materials.TryGetValue(color, out var material)) return material;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { color = color, enableInstancing = true };
            _materials.Add(color, material);
            return material;
        }

        public static void Paint(Renderer renderer, Color color)
        {
            var owner = renderer.GetComponentInParent<RuntimeMaterials>();
            if (owner == null) owner = renderer.transform.root.gameObject.AddComponent<RuntimeMaterials>();
            renderer.sharedMaterial = owner.Get(color);
        }

        public static void RemoveCollider(GameObject target)
        {
            var collider = target.GetComponent<Collider>();
            if (collider == null) return;
            collider.enabled = false;
            if (Application.isPlaying) Destroy(collider);
            else DestroyImmediate(collider);
        }

        private void OnDestroy()
        {
            foreach (var material in _materials.Values)
            {
                if (Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
            }
            _materials.Clear();
        }
    }
}
