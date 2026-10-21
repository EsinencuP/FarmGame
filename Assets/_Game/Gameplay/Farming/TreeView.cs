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
        private GameObject _readyMarker;
        // Последний показанный режим исключает повторную смену материала при каждом опросе времени.
        private bool _fruitReady;

        /// <summary>Создаёт placeholder-дерево из ствола и кроны без отдельного prefab-файла.</summary>
        public static TreeView Create(Vector3 position, TreeDefinition definition, Transform parent)
        {
            var root = new GameObject("Tree " + definition.TreeId);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var view = root.AddComponent<TreeView>();
            view._normalColor = definition.FoliageColor;
            view._readyColor = definition.FruitReadyColor;
            if (definition.VisualPrefab != null)
            {
                var treeVisual = VisualPrefab.Create(definition.VisualPrefab, root.transform, new Vector3(1.8f, 2.6f, 1.8f));
                VisualPrefab.TryGetBounds(treeVisual, out var treeBounds);
                var fruitGroup = new GameObject("Ready Fruit");
                fruitGroup.transform.SetParent(root.transform, false);
                if (definition.FruitPrefab != null)
                {
                    var positions = new[]
                    {
                        new Vector3(-0.4f, 0.72f, -0.35f),
                        new Vector3(0.44f, 0.78f, -0.2f),
                        new Vector3(0.05f, 0.63f, -0.45f)
                    };
                    foreach (var fruitPosition in positions)
                    {
                        var fruit = VisualPrefab.Create(definition.FruitPrefab, fruitGroup.transform,
                            Vector3.one * 0.28f);
                        fruit.transform.localPosition += root.transform.InverseTransformPoint(
                            new Vector3(treeBounds.center.x, treeBounds.min.y, treeBounds.center.z))
                            + Vector3.Scale(fruitPosition, treeBounds.size);
                        foreach (var renderer in fruit.GetComponentsInChildren<Renderer>())
                            RuntimeMaterials.Paint(renderer, view._readyColor);
                    }
                }
                else
                {
                    var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    marker.transform.SetParent(fruitGroup.transform, false);
                    marker.transform.localPosition = new Vector3(0f, 2.25f, 0f);
                    marker.transform.localScale = Vector3.one * 0.25f;
                    RuntimeMaterials.RemoveCollider(marker);
                    RuntimeMaterials.Paint(marker.GetComponent<Renderer>(), view._readyColor);
                }
                fruitGroup.SetActive(false);
                view._readyMarker = fruitGroup;
                root.AddComponent<MyLittleFarm.Gameplay.World.InteractionTarget>().Configure(
                    MyLittleFarm.Gameplay.World.InteractionTargetKind.Tree, null, true);
                return view;
            }
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
            view._foliage = canopy.GetComponent<Renderer>();
            RuntimeMaterials.Paint(view._foliage, view._normalColor);
            root.AddComponent<MyLittleFarm.Gameplay.World.InteractionTarget>().Configure(
                MyLittleFarm.Gameplay.World.InteractionTargetKind.Tree, null, true);
            return view;
        }

        /// <summary>Переключает цвет кроны между ростом и готовым повторным сбором.</summary>
        public void SetFruitReady(bool ready)
        {
            if (_fruitReady == ready) return;
            _fruitReady = ready;
            if (_readyMarker != null) _readyMarker.SetActive(ready);
            else if (_foliage != null) RuntimeMaterials.Paint(_foliage, ready ? _readyColor : _normalColor);
        }
    }
}
