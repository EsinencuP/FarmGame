using System;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Building
{
    /// <summary>The reserved barn footprint includes its fenced paddock. Slots rotate with the building.</summary>
    public static class BarnHabitat
    {
        public const string BuildingId = "barn";
        public const int Width = 9, Depth = 6, Capacity = 2, Price = 60;
        public const string PrefabResource = "FarmContent/Barn";

        public static bool TryCell(BuildingRuntimeState barn, int slot, out Vector2Int cell)
        {
            cell = default;
            if (barn == null || barn.definitionId != BuildingId || slot < 0 || slot >= Capacity
                || barn.quarterTurns < 0 || barn.quarterTurns > 3) return false;
            var x = 7; var z = 1 + slot * 3;
            var rx = barn.quarterTurns == 0 ? x : barn.quarterTurns == 1 ? z
                : barn.quarterTurns == 2 ? Width - 1 - x : Depth - 1 - z;
            var rz = barn.quarterTurns == 0 ? z : barn.quarterTurns == 1 ? Width - 1 - x
                : barn.quarterTurns == 2 ? Depth - 1 - z : x;
            var wx = (long)barn.x + rx; var wz = (long)barn.z + rz;
            if (wx < int.MinValue || wx > int.MaxValue || wz < int.MinValue || wz > int.MaxValue) return false;
            cell = new Vector2Int((int)wx, (int)wz);
            return true;
        }

        // Keep the paddock accessible: fences and grazing area do not turn into one solid building box.
        public static GameObject SolidModel(GameObject root)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == "Barn solid") return child.gameObject;
            return root;
        }
    }
}
