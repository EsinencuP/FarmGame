using System.Collections.Generic;
using UnityEngine;

namespace MyLittleFarm.Core
{
    /// <summary>
    /// Кэширует материалы по цвету, чтобы десятки клеток не создавали отдельные копии материалов.
    /// Компонент также владеет их временем жизни и освобождает их при уничтожении фермы.
    /// </summary>
    public sealed class RuntimeMaterials : MonoBehaviour
    {
        // Ключ — цвет, значение — один разделяемый материал этого цвета.
        private readonly Dictionary<Color, Material> _materials = new Dictionary<Color, Material>();

        public Material Get(Color color)
        {
            // Возвращает существующий материал либо лениво создаёт его при первом запросе цвета.
            if (_materials.TryGetValue(color, out var material)) return material;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { color = color, enableInstancing = true };
            _materials.Add(color, material);
            return material;
        }

        public static void Paint(Renderer renderer, Color color)
        {
            // Находит владельца кэша в корне объекта и назначает sharedMaterial без копирования.
            var owner = renderer.GetComponentInParent<RuntimeMaterials>();
            if (owner == null) owner = renderer.transform.root.gameObject.AddComponent<RuntimeMaterials>();
            renderer.sharedMaterial = owner.Get(color);
        }

        public static void RemoveCollider(GameObject target)
        {
            // Сразу выключает коллайдер и удаляет его подходящим для Play/Edit Mode способом.
            var collider = target.GetComponent<Collider>();
            if (collider == null) return;
            collider.enabled = false;
            if (Application.isPlaying) Destroy(collider);
            else DestroyImmediate(collider);
        }

        private void OnDestroy()
        {
            // Освобождает только материалы, созданные этим кэшем.
            foreach (var material in _materials.Values)
            {
                if (Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
            }
            _materials.Clear();
        }
    }
}
