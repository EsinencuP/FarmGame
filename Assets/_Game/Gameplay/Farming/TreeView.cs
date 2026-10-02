using MyLittleFarm.Core;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Farming
{
    /// <summary>Минимальное визуальное представление дерева, заменяемое моделью через TreeDefinition.</summary>
    public sealed class TreeView : MonoBehaviour
    {
        // Крона меняет цвет, когда дерево готово отдать плоды.
        private Renderer _foliage;
        private Color _normalColor;
        private Color _readyColor;
        // Последний показанный режим исключает повторную смену материала при каждом опросе времени.
        private bool _fruitReady;

        /// <summary>Создаёт placeholder-дерево из ствола и кроны без отдельного prefab-файла.</summary>
        public static TreeView Create(Vector3 position, TreeDefinition definition, Transform parent)
        {
            var root = new GameObject("Tree " + definition.TreeId);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(root.transform, false);
            trunk.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            trunk.transform.localScale = new Vector3(0.22f, 0.6f, 0.22f);
            RuntimeMaterials.RemoveCollider(trunk);
            RuntimeMaterials.Paint(trunk.GetComponent<Renderer>(), new Color(0.34f, 0.18f, 0.08f));
            var canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            canopy.name = "Foliage";
            canopy.transform.SetParent(root.transform, false);
            canopy.transform.localPosition = new Vector3(0f, 1.55f, 0f);
            canopy.transform.localScale = new Vector3(1.3f, 1.1f, 1.3f);
            RuntimeMaterials.RemoveCollider(canopy);
            var view = root.AddComponent<TreeView>();
            view._foliage = canopy.GetComponent<Renderer>();
            view._normalColor = definition.FoliageColor;
            view._readyColor = definition.FruitReadyColor;
            RuntimeMaterials.Paint(view._foliage, view._normalColor);
            return view;
        }

        /// <summary>Переключает цвет кроны между ростом и готовым повторным сбором.</summary>
        public void SetFruitReady(bool ready)
        {
            if (_foliage == null || _fruitReady == ready) return;
            _fruitReady = ready;
            RuntimeMaterials.Paint(_foliage, ready ? _readyColor : _normalColor);
        }
    }
}
