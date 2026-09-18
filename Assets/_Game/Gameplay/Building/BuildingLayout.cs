using System;
using System.Collections.Generic;

namespace MyLittleFarm.Gameplay.Building
{
    // Engine-independent placement and occupancy. Failed operations never change the layout.
    public sealed class BuildingLayout
    {
        private readonly int _width;
        private readonly int _depth;
        private readonly Dictionary<string, BuildingDefinition> _definitions = new Dictionary<string, BuildingDefinition>();
        private readonly Dictionary<string, BuildingRuntimeState> _buildings = new Dictionary<string, BuildingRuntimeState>();
        private readonly Dictionary<long, string> _occupied = new Dictionary<long, string>();

        public int Count => _buildings.Count;

        public BuildingLayout(int width, int depth, IEnumerable<BuildingDefinition> definitions)
        {
            if (width < 1 || depth < 1) throw new ArgumentOutOfRangeException(nameof(width));
            _width = width; _depth = depth;
            foreach (var definition in definitions) _definitions.Add(definition.Id, definition);
        }

        private static long Key(int x, int z) => ((long)x << 32) | (uint)z;

        public bool IsOccupied(int x, int z) => _occupied.ContainsKey(Key(x, z));
        public string BuildingAt(int x, int z) => _occupied.TryGetValue(Key(x, z), out var id) ? id : null;
        public BuildingDefinition Definition(string id) => id != null && _definitions.TryGetValue(id, out var value) ? value : null;
        public BuildingRuntimeState Get(string id) => id != null && _buildings.TryGetValue(id, out var value) ? value.Copy() : null;

        public bool CanPlace(string definitionId, int x, int z, int turns, Func<int, int, bool> blocked,
            string ignoreId, out string reason)
        {
            var definition = Definition(definitionId);
            if (definition == null || turns < 0 || turns > 3)
            { reason = "Неизвестный объект или поворот"; return false; }
            var width = definition.RotatedWidth(turns);
            var depth = definition.RotatedDepth(turns);
            if (x < 0 || z < 0 || width > _width || depth > _depth || x > _width - width || z > _depth - depth)
            { reason = "За границей участка"; return false; }
            for (var dz = 0; dz < depth; dz++)
                for (var dx = 0; dx < width; dx++)
                {
                    if ((_occupied.TryGetValue(Key(x + dx, z + dz), out var owner) && owner != ignoreId)
                        || (blocked != null && blocked(x + dx, z + dz)))
                    { reason = "Клетка занята"; return false; }
                }
            reason = string.Empty;
            return true;
        }

        public bool TryPlace(BuildingRuntimeState state, Func<int, int, bool> blocked, out string reason)
        {
            if (state == null || string.IsNullOrWhiteSpace(state.id) || _buildings.ContainsKey(state.id)
                || state.paidCost < 0 || Definition(state.definitionId) == null
                || state.paidCost != Definition(state.definitionId).Price)
            { reason = "Некорректные данные постройки"; return false; }
            if (!CanPlace(state.definitionId, state.x, state.z, state.quarterTurns, blocked, null, out reason)) return false;
            var owned = state.Copy();
            _buildings.Add(owned.id, owned);
            SetOccupancy(owned, true);
            return true;
        }

        public bool TryMove(string id, int x, int z, int turns, Func<int, int, bool> blocked, out string reason)
        {
            if (id == null || !_buildings.TryGetValue(id, out var state))
            { reason = "Постройка не найдена"; return false; }
            if (!CanPlace(state.definitionId, x, z, turns, blocked, id, out reason)) return false;
            SetOccupancy(state, false);
            state.x = x; state.z = z; state.quarterTurns = turns;
            SetOccupancy(state, true);
            return true;
        }

        public bool TryRemove(string id, out BuildingRuntimeState removed)
        {
            removed = null;
            if (id == null || !_buildings.TryGetValue(id, out var state)) return false;
            SetOccupancy(state, false);
            _buildings.Remove(id);
            removed = state.Copy();
            return true;
        }

        public List<BuildingRuntimeState> Capture()
        {
            var result = new List<BuildingRuntimeState>(_buildings.Count);
            foreach (var state in _buildings.Values) result.Add(state.Copy());
            return result;
        }

        private void SetOccupancy(BuildingRuntimeState state, bool occupied)
        {
            var definition = Definition(state.definitionId);
            for (var dz = 0; dz < definition.RotatedDepth(state.quarterTurns); dz++)
                for (var dx = 0; dx < definition.RotatedWidth(state.quarterTurns); dx++)
                {
                    var key = Key(state.x + dx, state.z + dz);
                    if (occupied) _occupied.Add(key, state.id);
                    else _occupied.Remove(key);
                }
        }
    }
}
