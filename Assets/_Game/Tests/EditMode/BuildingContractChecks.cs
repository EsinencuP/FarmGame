using System;
using System.Collections.Generic;
using MyLittleFarm.Gameplay.Building;

namespace MyLittleFarm.Tests.EditMode
{
    /// <summary>
    /// Общие контрактные проверки настоящей модели BuildingLayout. Один и тот же код запускается
    /// Unity Test Runner и автономным Verify-Scripts.ps1 без подмены реализации.
    /// </summary>
    public static class BuildingContractChecks
    {
        /// <summary>Выполняет детерминированные проверки и возвращает число успешных утверждений.</summary>
        public static int Run()
        {
            // Счётчик подтверждает, что длинный случайный сценарий действительно был выполнен полностью.
            var assertions = 0;
            Action<bool, string> check = (condition, message) =>
            {
                assertions++;
                if (!condition) throw new InvalidOperationException(message);
            };
            // Начальные проверки покрывают поворот, отрицательные координаты, занятость и владение копиями.
            var layout = new BuildingLayout(BuildingDefinition.Catalog);
            var storage = BuildingDefinition.Catalog[1];
            check(storage.RotatedWidth(1) == 1 && storage.RotatedDepth(1) == 2, "Quarter turn swaps dimensions");
            check(storage.RotatedWidth(2) == 2 && storage.RotatedDepth(2) == 1, "Half turn preserves dimensions");
            check(layout.CanPlace("storage", 7, 7, 0, null, null, out _), "World layout has no artificial edge");
            check(layout.CanPlace("storage", -7, -6, 1, null, null, out _), "Negative world coordinates are supported");
            check(!layout.CanPlace("house", int.MaxValue, 0, 0, null, null, out _), "Overflow-safe bounds");
            check(!layout.CanPlace("house", 0, 0, 4, null, null, out _), "Invalid rotation");
            check(!layout.CanPlace("missing", 0, 0, 0, null, null, out _), "Unknown definition");
            check(!layout.CanPlace("house", 0, 0, 0, (x, z) => x == 1 && z == 1, null, out _), "Whole footprint checks crops/terrain");
            var first = State("first", "storage", 1, 1, 0);
            check(layout.TryPlace(first, null, out _), "Place storage");
            first.x = 7;
            check(layout.Get("first").x == 1, "Placed state owned by layout");
            check(layout.IsOccupied(1, 1) && layout.IsOccupied(2, 1), "Both footprint cells occupied");
            check(!layout.TryPlace(State("overlap", "house", 2, 1, 0), null, out _), "Overlap rejected");
            check(!layout.TryPlace(State("first", "flowerbed", 5, 5, 0), null, out _), "Duplicate id rejected");
            var invalidCost = State("invalid", "flowerbed", 5, 5, 0);
            invalidCost.paidCost = int.MaxValue;
            check(!layout.TryPlace(invalidCost, null, out _), "Invalid paid cost rejected");
            check(!layout.TryMove("first", int.MaxValue, 7, 0, null, out _), "Overflowing move rejected");
            check(layout.IsOccupied(1, 1) && layout.IsOccupied(2, 1) && layout.Count == 1, "Failed move preserves old cells");
            check(layout.CanPlace("storage", 2, 1, 0, null, "first", out _), "Preview ignores own footprint");
            check(layout.Get("first").x == 1, "Preview/cancel does not move original");
            check(layout.TryMove("first", 2, 1, 1, null, out _), "Move with partial old overlap");
            check(!layout.IsOccupied(1, 1) && layout.IsOccupied(2, 1) && layout.IsOccupied(2, 2), "Move releases only old cells");
            var snapshot = layout.Capture();
            snapshot[0].x = 0;
            check(layout.Get("first").x == 2, "Capture is detached");
            var restored = new BuildingLayout(BuildingDefinition.Catalog);
            foreach (var state in layout.Capture()) check(restored.TryPlace(state, null, out _), "Snapshot restores");
            check(restored.BuildingAt(2, 2) == "first", "Restored rotated occupancy");
            check(layout.TryRemove("first", out var removed) && removed.paidCost / 2 == 6, "Remove carries refund basis");
            check(layout.Count == 0 && !layout.IsOccupied(2, 2), "Remove releases cells");
            check(!layout.TryRemove("first", out _), "No second deletion/refund");

            // Фиксированный seed делает 1000 операций воспроизводимыми, независимый словарь служит эталоном занятости.
            var random = new Random(1729);
            for (var step = 0; step < 1000; step++)
            {
                var states = layout.Capture();
                if (states.Count == 0 || random.Next(3) == 0)
                {
                    var definition = BuildingDefinition.Catalog[random.Next(4)];
                    layout.TryPlace(State("random-" + step, definition.Id, random.Next(-8, 9), random.Next(-8, 9), random.Next(4)), null, out _);
                }
                else
                {
                    var state = states[random.Next(states.Count)];
                    if (random.Next(3) == 0) layout.TryRemove(state.id, out _);
                    else layout.TryMove(state.id, random.Next(-8, 9), random.Next(-8, 9), random.Next(4), null, out _);
                }
                var expected = new Dictionary<long, string>();
                foreach (var state in layout.Capture())
                {
                    var definition = layout.Definition(state.definitionId);
                    for (var z = state.z; z < state.z + definition.RotatedDepth(state.quarterTurns); z++)
                        for (var x = state.x; x < state.x + definition.RotatedWidth(state.quarterTurns); x++)
                        {
                            var key = ((long)x << 32) | (uint)z;
                            check(!expected.ContainsKey(key), "No duplicate ownership");
                            expected[key] = state.id;
                        }
                }
                for (var z = -10; z <= 10; z++)
                    for (var x = -10; x <= 10; x++)
                    {
                        expected.TryGetValue(((long)x << 32) | (uint)z, out var owner);
                        check(layout.BuildingAt(x, z) == owner, "Occupancy matches independent oracle");
                    }
            }
            return assertions;
        }

        private static BuildingRuntimeState State(string id, string definitionId, int x, int z, int turns)
        {
            // Фабрика тестового состояния подставляет настоящую цену из рабочего каталога.
            var price = 0;
            foreach (var definition in BuildingDefinition.Catalog)
                if (definition.Id == definitionId) price = definition.Price;
            return new BuildingRuntimeState { id = id, definitionId = definitionId, x = x, z = z, quarterTurns = turns, paidCost = price };
        }
    }
}
