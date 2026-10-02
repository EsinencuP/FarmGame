using MyLittleFarm.Core;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Animals
{
    /// <summary>Временная low-poly модель курицы с цветом, заданным AnimalDefinition.</summary>
    public sealed class AnimalView : MonoBehaviour
    {
        // Базовая позиция клетки сохраняется, чтобы wander не уводил курицу к началу сцены.
        private Vector3 _baseLocalPosition;
        /// <summary>Создаёт тело, голову и клюв без внешних prefab-ассетов.</summary>
        public static AnimalView Create(Vector3 position, AnimalDefinition definition, Transform parent)
        {
            var root = new GameObject("Animal " + definition.AnimalId);
            root.transform.SetParent(parent, false);
            root.transform.position = position + Vector3.up * 0.31f;

            // Капсула и куб позволяют узнать курицу в макете без внешней модели.
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(0.56f, 0.3f, 0.68f);
            RuntimeMaterials.RemoveCollider(body);
            RuntimeMaterials.Paint(body.GetComponent<Renderer>(), definition.BodyColor);

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 0.3f, 0.32f);
            head.transform.localScale = new Vector3(0.34f, 0.34f, 0.34f);
            RuntimeMaterials.RemoveCollider(head);
            RuntimeMaterials.Paint(head.GetComponent<Renderer>(), definition.BodyColor);

            var beak = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beak.name = "Beak";
            beak.transform.SetParent(root.transform, false);
            beak.transform.localPosition = new Vector3(0f, 0.27f, 0.52f);
            beak.transform.localScale = new Vector3(0.18f, 0.1f, 0.21f);
            RuntimeMaterials.RemoveCollider(beak);
            RuntimeMaterials.Paint(beak.GetComponent<Renderer>(), new Color(0.96f, 0.67f, 0.12f));

            var view = root.AddComponent<AnimalView>();
            view._baseLocalPosition = root.transform.localPosition;
            return view;
        }

        /// <summary>Двигает placeholder в пределах клетки, чтобы животное не застывало.</summary>
        public void SetWanderOffset(float x, float z)
        {
            transform.localPosition = _baseLocalPosition + new Vector3(x, 0f, z);
        }
    }
}
