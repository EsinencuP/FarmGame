using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.World;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Building
{
    public sealed class BuildingView : MonoBehaviour
    {
        public string BuildingId { get; private set; }
        public string DefinitionId { get; private set; }

        public static BuildingView Create(Transform parent, BuildingDefinition definition, BuildingRuntimeState state, GridSystem grid)
        {
            var root = new GameObject(definition.Name);
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<BuildingView>();
            view.BuildingId = state.id;
            view.DefinitionId = definition.Id;
            var width = definition.Width * grid.CellSize - 0.12f;
            var depth = definition.Depth * grid.CellSize - 0.12f;
            var height = definition.Height;
            var color = definition.Id == "house" ? new Color(0.85f, 0.70f, 0.45f)
                : definition.Id == "storage" ? new Color(0.45f, 0.30f, 0.18f)
                : definition.Id == "market" ? new Color(0.85f, 0.48f, 0.15f) : new Color(0.30f, 0.58f, 0.25f);
            Part(root.transform, "Body", new Vector3(0, height * 0.4f, 0), new Vector3(width, height * 0.8f, depth), color);
            Part(root.transform, "Top", new Vector3(0, height * 0.9f, 0), new Vector3(width, height * 0.2f, depth),
                definition.Id == "flowerbed" ? new Color(0.92f, 0.40f, 0.58f) : new Color(0.48f, 0.20f, 0.16f));
            // A visible front makes all four rotations distinguishable.
            Part(root.transform, "Front", new Vector3(0, height * 0.35f, -depth * 0.5f - 0.015f),
                new Vector3(width * 0.28f, height * 0.5f, 0.035f), new Color(0.20f, 0.25f, 0.28f));
            var collider = root.AddComponent<BoxCollider>();
            collider.center = Vector3.up * height * 0.5f;
            collider.size = new Vector3(width, height, depth);
            view.Apply(state, definition, grid);
            return view;
        }

        public void Apply(BuildingRuntimeState state, BuildingDefinition definition, GridSystem grid)
        {
            transform.position = Center(state.x, state.z, state.quarterTurns, definition, grid);
            transform.rotation = Quaternion.Euler(0, state.quarterTurns * 90f, 0);
        }

        public static Vector3 Center(int x, int z, int turns, BuildingDefinition definition, GridSystem grid)
        {
            return grid.CellToWorld(new Vector2Int(x, z)) + new Vector3(
                (definition.RotatedWidth(turns) - 1) * grid.CellSize * 0.5f, 0,
                (definition.RotatedDepth(turns) - 1) * grid.CellSize * 0.5f);
        }

        private static void Part(Transform parent, string name, Vector3 position, Vector3 scale, Color color)
        {
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
