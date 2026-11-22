using System;
using System.Collections.Generic;
using MyLittleFarm.Core;

namespace MyLittleFarm.Gameplay.Farming
{
    // Medium is the default value of the field in pre-v20 item stacks.
    public enum HarvestSize { M, S, L, XL }

    public static class HarvestQuality
    {
        public const int MaximumMoisture = 100;
        public const int ManualMoisture = 70;
        public static IReadOnlyList<HarvestSize> Sizes { get; } =
            Array.AsReadOnly(new[] { HarvestSize.S, HarvestSize.M, HarvestSize.L, HarvestSize.XL });

        public static bool IsSized(string itemId) =>
            itemId == "apple" || itemId == "pear" || Array.IndexOf(FarmCatalog.CropIds, itemId) >= 0;

        public static bool IsValidSize(HarvestSize size) => size >= HarvestSize.M && size <= HarvestSize.XL;
        public static string StackId(string itemId, HarvestSize size) =>
            size == HarvestSize.M ? itemId : itemId + "|" + size;

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

        public static int Price(int basePrice, int marketPercent, HarvestSize size)
        {
            if (basePrice <= 0) return 0;
            var sizePercent = size switch { HarvestSize.S => 50, HarvestSize.L => 120, HarvestSize.XL => 150, _ => 100 };
            return (int)Math.Clamp(((long)basePrice * marketPercent * sizePercent + 5_000) / 10_000, 1, int.MaxValue);
        }

        public static int Moisture(int manual, float rainySeconds, float duration) =>
            Math.Clamp(manual + (int)Math.Round(100d * Math.Clamp(rainySeconds / Math.Max(.1f, duration), 0f, 1f)), 0, MaximumMoisture);

        public static (int s, int m, int l, int xl) Chances(int moisture)
        {
            var small = 55 - moisture / 2;
            var large = 8 + moisture * 28 / 100;
            var extraLarge = 2 + moisture * 18 / 100;
            return (small, 100 - small - large - extraLarge, large, extraLarge);
        }

        public static string GrowthHint(int moisture, bool watered)
        {
            var (_, _, large, extraLarge) = Chances(moisture);
            return $"Влажность {moisture}% · L/XL {large + extraLarge}%" +
                (watered ? "" : " · " + InputReader.KeyLabel(InputReader.Command.Water) + " — полить");
        }

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
            var roll = (int)(hash % 100);
            var (small, medium, large, _) = Chances(moisture);
            if (roll < small) return HarvestSize.S;
            if (roll < small + medium) return HarvestSize.M;
            return roll < small + medium + large ? HarvestSize.L : HarvestSize.XL;
        }
    }
}
