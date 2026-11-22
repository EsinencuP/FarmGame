using System;
using System.Collections.Generic;
using MyLittleFarm.Core;

namespace MyLittleFarm.Gameplay.Farming
{
    // Medium is the default value of the field in pre-v20 item stacks.
    // M стоит первым и получает значение 0. Старые стопки без записанного размера читаются как M.
    public enum HarvestSize { M, S, L, XL }

    public static class HarvestQuality
    {
        public const int MaximumMoisture = 100;
        public const int ManualMoisture = 70;
        public static IReadOnlyList<HarvestSize> Sizes { get; } =
            Array.AsReadOnly(new[] { HarvestSize.S, HarvestSize.M, HarvestSize.L, HarvestSize.XL });

        // Проверяю, умеет ли этот предмет храниться по размерам.
        public static bool IsSized(string itemId) =>
            itemId == "apple" || itemId == "pear" || Array.IndexOf(FarmCatalog.CropIds, itemId) >= 0;

        // Проверяю, что значение помещается в список допустимых размеров.
        public static bool IsValidSize(HarvestSize size) => size >= HarvestSize.M && size <= HarvestSize.XL;
        // У размера M остаётся обычный ID, а к другим добавляю суффикс, например tomato|XL.
        public static string StackId(string itemId, HarvestSize size) =>
            size == HarvestSize.M ? itemId : itemId + "|" + size;

        // Разбираю ID стопки и возвращаю отдельно товар и его размер.
        public static (string itemId, HarvestSize size) ParseStack(string stackId)
        {
            if (stackId != null)
            {
                var separator = stackId.LastIndexOf('|');
                if (separator > 0)
                {
                    var id = stackId.Substring(0, separator);
                    var suffix = stackId.Substring(separator + 1);
                    if (IsSized(id) && Enum.TryParse<HarvestSize>(suffix, out var size) &&
                        IsValidSize(size) && size != HarvestSize.M && size.ToString() == suffix)
                        return (id, size);
                }
            }
            return (stackId, HarvestSize.M);
        }

        // Умножаю базовую цену на коэффициенты рынка и размера. Округляю только итог.
        public static int Price(int basePrice, int marketPercent, HarvestSize size)
        {
            if (basePrice <= 0) return 0;
            // Подставляю нужный процент: S — 50, M — 100, L — 120, XL — 150.
            var sizePercent = size switch { HarvestSize.S => 50, HarvestSize.L => 120, HarvestSize.XL => 150, _ => 100 };
            return (int)Math.Clamp(((long)basePrice * marketPercent * sizePercent + 5_000) / 10_000, 1, int.MaxValue);
        }

        // Суммирую ручной полив и долю цикла под дождём. Влажность держу от 0 до 100.
        public static int Moisture(int manual, float rainySeconds, float duration) =>
            Math.Clamp(manual + (int)Math.Round(100d * Math.Clamp(rainySeconds / Math.Max(.1f, duration), 0f, 1f)), 0, MaximumMoisture);

        // Считаю четыре шанса. При большем поливе растут шансы L и XL, а M добирает остаток до 100.
        public static (int s, int m, int l, int xl) Chances(int moisture)
        {
            var small = 55 - moisture / 2;
            var large = 8 + moisture * 28 / 100;
            var extraLarge = 2 + moisture * 18 / 100;
            return (small, 100 - small - large - extraLarge, large, extraLarge);
        }

        // Готовлю короткую подсказку о влажности, шансе крупного урожая и клавише полива.
        public static string GrowthHint(int moisture, bool watered)
        {
            var (_, _, large, extraLarge) = Chances(moisture);
            return $"Влажность {moisture}% · L/XL {large + extraLarge}%" +
                (watered ? "" : " · " + InputReader_AI.KeyLabel(InputReader_AI.Command.Water) + " — полить");
        }

        // Получаю стабильный бросок 0–99 для конкретного растения и цикла. Потом сверяю его с шансами.
        public static HarvestSize Select(string cropId, int x, int z, long cycleStart, int moisture)
        {
            // Fixed hash keeps the same cycle roll between sessions and on different runtimes.
            uint hash;
            unchecked
            {
                hash = 2166136261u;
                foreach (var letter in cropId) hash = (hash ^ letter) * 16777619u;
                hash = (hash ^ (uint)x) * 16777619u;
                hash = (hash ^ (uint)z) * 16777619u;
                hash = (hash ^ (uint)cycleStart) * 16777619u;
                hash = (hash ^ (uint)(cycleStart >> 32)) * 16777619u;
                hash ^= hash >> 16;
                hash *= 2246822519u;
                hash ^= hash >> 13;
            }
            // У числа беру остаток от деления на 100. Так получается бросок от 0 до 99.
            var roll = (int)(hash % 100);
            var (small, medium, large, _) = Chances(moisture);
            if (roll < small) return HarvestSize.S;
            if (roll < small + medium) return HarvestSize.M;
            return roll < small + medium + large ? HarvestSize.L : HarvestSize.XL;
        }
    }
}
