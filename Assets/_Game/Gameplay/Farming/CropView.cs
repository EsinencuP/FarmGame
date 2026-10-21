using UnityEngine;
using MyLittleFarm.Core;

namespace MyLittleFarm.Gameplay.Farming
{
    /// <summary>Displays each authored plant stage at its own proportional size, with a fallback for script-only scenes.</summary>
    public sealed class CropView : MonoBehaviour
    {
        // Цвета соответствуют ростку, растущему растению и зрелому урожаю.
        private static readonly Color[] StageColors =
        {
            new Color(0.30f, 0.72f, 0.20f),
            new Color(0.18f, 0.60f, 0.12f),
            new Color(0.95f, 0.42f, 0.08f)
        };

        // Stage changes rebuild only the visual representation, never gameplay state.
        private int _currentStage = -1;
        private CropDefinition _definition;
        private GameObject _stageObject;
        private GameObject _placeholder;

        public static CropView Create(Vector3 position, CropDefinition definition = null, Transform parent = null)
        {
            var root = new GameObject();
            root.name = definition == null ? "Crop" : definition.DisplayName;
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var view = root.AddComponent<CropView>();
            view._placeholder = new GameObject("Seedling preview");
            view._placeholder.transform.SetParent(root.transform, false);
            view._definition = definition;
            view.SetStage(0, definition == null ? 3 : definition.StageCount);
            return view;
        }

        public void SetStage(int stage, int stageCount)
        {
            stageCount = Mathf.Max(1, stageCount);
            stage = Mathf.Clamp(stage, 0, stageCount - 1);
            if (_currentStage == stage)
            {
                return;
            }

            _currentStage = stage;
            // Hide immediately: Destroy is deferred in Play Mode and otherwise both stages render this frame.
            if (_stageObject != null)
            {
                _stageObject.SetActive(false);
                if (Application.isPlaying) Destroy(_stageObject);
                else DestroyImmediate(_stageObject);
                _stageObject = null;
            }
            var prefabs = _definition == null ? null : _definition.StagePrefabs;
            var prefab = prefabs != null && stage >= 0 && stage < prefabs.Length ? prefabs[stage] : null;
            if (prefab != null)
            {
                _stageObject = VisualPrefab.Create(prefab, transform, _definition.StageSize(stage));
                _stageObject.name = "Stage " + stage + " - " + prefab.name;
                _placeholder.SetActive(false);
                return;
            }
            _placeholder.SetActive(true);
            var normalized = stageCount <= 1 ? 1f : stage / (float)(stageCount - 1);
            var size = _definition == null ? new Vector3(0.4f, 0.2f + normalized * 0.55f, 0.4f)
                : _definition.StageSize(stage);
            _placeholder.transform.localScale = size;
            for (var i = _placeholder.transform.childCount - 1; i >= 0; i--)
            {
                var child = _placeholder.transform.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            var green = StageColors[Mathf.Clamp(stage, 0, 1)];
            SeedlingPart("Stem", PrimitiveType.Cylinder, new Vector3(0f, 0.42f, 0f),
                new Vector3(0.065f, 0.4f, 0.065f), green);
            var leafCount = stage == 0 ? 2 : 6;
            for (var i = 0; i < leafCount; i++)
            {
                var angle = i * 360f / leafCount;
                var rotation = Quaternion.Euler(-25f, angle, 0f);
                var leaf = SeedlingPart("Leaf " + i, PrimitiveType.Sphere,
                    rotation * new Vector3(0f, 0f, 0.2f) + Vector3.up * (0.3f + i % 3 * 0.15f),
                    new Vector3(0.17f, 0.06f, 0.45f), green);
                leaf.transform.localRotation = rotation;
            }
            if (stage >= stageCount - 1)
                SeedlingPart("Ready marker (plant art pending)", PrimitiveType.Sphere,
                    new Vector3(0.38f, 0.07f, 0.38f), Vector3.one * 0.13f,
                    _definition == null ? StageColors[2] : _definition.MatureColor);
        }

        private GameObject SeedlingPart(string name, PrimitiveType shape, Vector3 position, Vector3 scale, Color color)
        {
            var part = GameObject.CreatePrimitive(shape);
            part.name = name;
            part.transform.SetParent(_placeholder.transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            RuntimeMaterials.RemoveCollider(part);
            RuntimeMaterials.Paint(part.GetComponent<Renderer>(), color);
            return part;
        }
    }
}
