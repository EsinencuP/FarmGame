using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Building
{
    /// <summary>Creates the building collider and displays either imported art or a prototype model.</summary>
    public sealed class BuildingView : MonoBehaviour
    {
        // Идентификаторы связывают видимый объект с экземпляром layout и его типом из каталога.
        public string BuildingId { get; private set; }
        public string DefinitionId { get; private set; }

        public static BuildingView Create(Transform parent, BuildingDefinition definition, BuildingRuntimeState state,
            GridSystem grid, GameObject visualPrefab = null)
        {
            // Корень содержит общий коллайдер, а дочерние примитивы отвечают только за внешний вид.
            var root = new GameObject(definition.Name);
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<BuildingView>();
            view.BuildingId = state.id;
            view.DefinitionId = definition.Id;
            // Размеры модели вычисляются из клеточного следа без малого зазора между соседями.
            var width = definition.Width * grid.CellSize - 0.12f;
            var depth = definition.Depth * grid.CellSize - 0.12f;
            var height = definition.Height;
            if (visualPrefab != null)
            {
                VisualPrefab.CreateBuilding(visualPrefab, root.transform, new Vector3(width, height, depth));
            }
            else if (definition.Id == ApplePresses.BuildingId)
            {
                BuildApplePress(root.transform, width, height, depth);
            }
            else
            {
                // Keep the recognizable prototype shape until this definition has art assigned.
                var color = definition.Id == "house" ? new Color(0.85f, 0.70f, 0.45f)
                    : definition.Id == "storage" ? new Color(0.45f, 0.30f, 0.18f)
                    : definition.Id == "market" ? new Color(0.85f, 0.48f, 0.15f)
                    : definition.Id == "coop" ? new Color(0.76f, 0.57f, 0.35f)
                    : new Color(0.30f, 0.58f, 0.25f);
                Part(root.transform, "Body", new Vector3(0, height * 0.4f, 0), new Vector3(width, height * 0.8f, depth), color);
                Part(root.transform, "Top", new Vector3(0, height * 0.9f, 0), new Vector3(width, height * 0.2f, depth),
                    definition.Id == "flowerbed" ? new Color(0.92f, 0.40f, 0.58f) : new Color(0.48f, 0.20f, 0.16f));
                Part(root.transform, "Front", new Vector3(0, height * 0.35f, -depth * 0.5f - 0.015f),
                    new Vector3(width * 0.28f, height * 0.5f, 0.035f), new Color(0.20f, 0.25f, 0.28f));
            }
            var collider = root.AddComponent<BoxCollider>();
            collider.center = Vector3.up * height * 0.5f;
            collider.size = new Vector3(width, height, depth);
            if (VisualPrefab.TryGetLocalBounds(root, root.transform, out var bounds))
            {
                collider.center = bounds.center;
                collider.size = bounds.size;
            }
            var target = root.AddComponent<MyLittleFarm.Gameplay.World.InteractionTarget>();
            target.Configure(definition.Id == "market"
                ? MyLittleFarm.Gameplay.World.InteractionTargetKind.Market
                : MyLittleFarm.Gameplay.World.InteractionTargetKind.Building, state.id);
            view.Apply(state, definition, grid);
            return view;
        }

        public void Apply(BuildingRuntimeState state, BuildingDefinition definition, GridSystem grid)
        {
            // Положение задаётся центром всего следа, поворот применяется вокруг вертикальной оси.
            transform.position = Center(state.x, state.z, state.quarterTurns, definition, grid);
            transform.rotation = Quaternion.Euler(0, state.quarterTurns * 90f, 0);
            foreach(var sign in GetComponentsInChildren<FarmSign>(true))sign.SetText(state.customText);
        }

        public static Vector3 Center(int x, int z, int turns, BuildingDefinition definition, GridSystem grid)
        {
            // Начальная клетка обозначает угол следа; смещение переносит объект в его геометрический центр.
            return grid.CellToWorld(new Vector2Int(x, z)) + new Vector3(
                (definition.RotatedWidth(turns) - 1) * grid.CellSize * 0.5f, 0,
                (definition.RotatedDepth(turns) - 1) * grid.CellSize * 0.5f);
        }

        private static void BuildApplePress(Transform parent, float width, float height, float depth)
        {
            var wood = new Color(.47f, .29f, .16f);
            var oak = new Color(.68f, .46f, .27f);
            var iron = new Color(.25f, .28f, .26f);
            Part(parent, "Base", new Vector3(0, .13f, 0), new Vector3(width, .26f, depth), wood);
            Part(parent, "Press bed", new Vector3(0, .36f, 0), new Vector3(width * .72f, .19f, depth * .58f), oak);
            for (var side = -1; side <= 1; side += 2)
                Part(parent, "Upright", new Vector3(side * width * .38f, height * .49f, 0),
                    new Vector3(width * .15f, height * .88f, depth * .28f), wood);
            Part(parent, "Crossbeam", new Vector3(0, height * .89f, 0), new Vector3(width * .92f, height * .22f, depth * .30f), oak);
            Part(parent, "Basket", new Vector3(0, .67f, 0), new Vector3(width * .52f, .45f, depth * .52f), oak);
            Part(parent, "Basket band", new Vector3(0, .76f, 0), new Vector3(width * .55f, .08f, depth * .55f), iron);
            Part(parent, "Screw", new Vector3(0, height * .75f, 0), new Vector3(.14f, height * .39f, .14f), iron);
            Part(parent, "Lever", new Vector3(0, height * .99f, 0), new Vector3(width * .76f, .08f, .12f), iron);
            Part(parent, "Juice spout", new Vector3(0, .44f, -depth * .35f), new Vector3(.17f, .11f, depth * .30f), iron);
        }

        private static void Part(Transform parent, string name, Vector3 position, Vector3 scale, Color color)
        {
            // Создаёт декоративную деталь без коллайдера и назначает разделяемый материал.
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            RuntimeMaterials.RemoveCollider(part);
            RuntimeMaterials.Paint(part.GetComponent<Renderer>(), color);
        }
    }
}
