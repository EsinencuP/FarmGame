using UnityEngine;
using MyLittleFarm.Core;

namespace MyLittleFarm.Gameplay.Farming
{
    /// <summary>Отображает стадию роста культуры размером и цветом временной модели.</summary>
    public sealed class CropView : MonoBehaviour
    {
        // Цвета соответствуют ростку, растущему растению и зрелому урожаю.
        private static readonly Color[] StageColors =
        {
            new Color(0.30f, 0.72f, 0.20f),
            new Color(0.18f, 0.60f, 0.12f),
            new Color(0.95f, 0.42f, 0.08f)
        };

        // Renderer меняет материал, currentStage предотвращает повторное обновление без изменений.
        private Renderer _renderer;
        private int _currentStage = -1;
        private CropDefinition _definition;
        private GameObject _stageObject;

        public static CropView Create(Vector3 position, CropDefinition definition = null, Transform parent = null)
        {
            // Создаёт декоративный цилиндр без физического коллайдера.
            var root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            root.name = definition == null ? "Crop" : definition.DisplayName;
            root.transform.SetParent(parent, false);
            root.transform.position = position + Vector3.up * 0.1f;
            RuntimeMaterials.RemoveCollider(root);
            var view = root.AddComponent<CropView>();
            view._renderer = root.GetComponent<Renderer>();
            view._definition = definition;
            view.SetStage(0, definition == null ? 3 : definition.StageCount);
            return view;
        }

        public void SetStage(int stage, int stageCount)
        {
            // Нормализованная стадия одновременно управляет высотой, шириной и цветом.
            if (_currentStage == stage)
            {
                return;
            }

            _currentStage = stage;
            // При наличии модели стадии меняется дочерний объект; без неё остаётся цветной цилиндр.
            if (_stageObject != null) Destroy(_stageObject);
            var prefabs = _definition == null ? null : _definition.StagePrefabs;
            var prefab = prefabs != null && stage >= 0 && stage < prefabs.Length ? prefabs[stage] : null;
            if (prefab != null)
            {
                _stageObject = Instantiate(prefab, transform);
                _renderer.enabled = false;
            }
            else _renderer.enabled = true;
            var normalized = stageCount <= 1 ? 1f : stage / (float)(stageCount - 1);
            var height = Mathf.Lerp(0.22f, 1.15f, normalized);
            transform.localScale = new Vector3(0.28f + normalized * 0.22f, height * 0.5f, 0.28f + normalized * 0.22f);
            transform.position = new Vector3(transform.position.x, height * 0.5f + 0.05f, transform.position.z);
            var color = stage >= stageCount - 1 && _definition != null
                ? _definition.MatureColor : StageColors[Mathf.Clamp(stage, 0, StageColors.Length - 1)];
            RuntimeMaterials.Paint(_renderer, color);
        }
    }
}
