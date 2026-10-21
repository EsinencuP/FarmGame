using MyLittleFarm.Core;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Animals
{
    /// <summary>Small stylized chicken assembled from simple low-poly shapes.</summary>
    public sealed class AnimalView : MonoBehaviour
    {
        // Базовая позиция клетки сохраняется, чтобы wander не уводил курицу к началу сцены.
        private Vector3 _baseLocalPosition;
        private Vector3 _targetOffset;
        private Vector3 _offsetVelocity;
        [SerializeField, Min(0.01f)] private float wanderSmoothTime = 0.22f;
        /// <summary>Creates a recognizable chicken while keeping the animal footprint independent of visuals.</summary>
        public static AnimalView Create(Vector3 position, AnimalDefinition definition, Transform parent, string animalId = null)
        {
            var root = new GameObject("Animal " + definition.AnimalId);
            root.transform.SetParent(parent, false);
            root.transform.position = position + Vector3.up * 0.4f;

            Part(root.transform, "Body", PrimitiveType.Sphere, Vector3.zero,
                new Vector3(0.56f, 0.42f, 0.68f), definition.BodyColor);
            Part(root.transform, "Head", PrimitiveType.Sphere, new Vector3(0f, 0.28f, 0.31f),
                Vector3.one * 0.36f, definition.BodyColor);
            var wingColor = Color.Lerp(definition.BodyColor, Color.gray, 0.12f);
            Part(root.transform, "Left Wing", PrimitiveType.Sphere, new Vector3(-0.27f, 0.04f, -0.03f),
                new Vector3(0.18f, 0.25f, 0.36f), wingColor);
            Part(root.transform, "Right Wing", PrimitiveType.Sphere, new Vector3(0.27f, 0.04f, -0.03f),
                new Vector3(0.18f, 0.25f, 0.36f), wingColor);
            Part(root.transform, "Beak", PrimitiveType.Cube, new Vector3(0f, 0.24f, 0.54f),
                new Vector3(0.16f, 0.09f, 0.18f), new Color(0.96f, 0.67f, 0.12f));
            var red = new Color(0.85f, 0.12f, 0.1f);
            for (var i = 0; i < 3; i++)
                Part(root.transform, "Comb " + i, PrimitiveType.Sphere,
                    new Vector3(0f, 0.49f, 0.18f + i * 0.11f),
                    new Vector3(0.11f, 0.14f + i * 0.025f, 0.11f), red);
            foreach (var side in new[] { -1f, 1f })
            {
                Part(root.transform, side < 0 ? "Left Eye" : "Right Eye", PrimitiveType.Sphere,
                    new Vector3(side * 0.16f, 0.33f, 0.43f), Vector3.one * 0.065f,
                    new Color(0.04f, 0.04f, 0.04f));
                Part(root.transform, side < 0 ? "Left Leg" : "Right Leg", PrimitiveType.Cylinder,
                    new Vector3(side * 0.14f, -0.28f, 0f), new Vector3(0.045f, 0.12f, 0.045f),
                    new Color(0.9f, 0.55f, 0.1f));
                Part(root.transform, side < 0 ? "Left Foot" : "Right Foot", PrimitiveType.Cube,
                    new Vector3(side * 0.14f, -0.38f, 0.09f), new Vector3(0.14f, 0.04f, 0.21f),
                    new Color(0.9f, 0.55f, 0.1f));
            }
            for (var i = -1; i <= 1; i++)
                Part(root.transform, "Tail " + i, PrimitiveType.Sphere,
                    new Vector3(i * 0.12f, 0.16f + Mathf.Abs(i) * 0.05f, -0.39f),
                    new Vector3(0.14f, 0.35f, 0.17f), definition.BodyColor);

            var view = root.AddComponent<AnimalView>();
            view._baseLocalPosition = root.transform.localPosition;
            root.AddComponent<MyLittleFarm.Gameplay.World.InteractionTarget>().Configure(
                MyLittleFarm.Gameplay.World.InteractionTargetKind.Animal, animalId, true);
            return view;
        }

        private static void Part(Transform parent, string name, PrimitiveType shape,
            Vector3 position, Vector3 scale, Color color)
        {
            var part = GameObject.CreatePrimitive(shape);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            RuntimeMaterials.RemoveCollider(part);
            RuntimeMaterials.Paint(part.GetComponent<Renderer>(), color);
        }

        /// <summary>Moves the chicken inside its assigned cell.</summary>
        public void SetWanderOffset(float x, float z)
        {
            _targetOffset = new Vector3(x, 0f, z);
        }

        private void LateUpdate()
        {
            transform.localPosition = Vector3.SmoothDamp(transform.localPosition,
                _baseLocalPosition + _targetOffset, ref _offsetVelocity,
                wanderSmoothTime, Mathf.Infinity, Time.deltaTime);
        }
    }
}
