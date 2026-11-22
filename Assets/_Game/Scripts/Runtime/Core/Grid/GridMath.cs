using System;

namespace MyLittleFarm.Core.Grid
{
    /// <summary>Считает адреса чанков и локальных клеток одинаково для положительных и отрицательных координат.</summary>
    public static class GridMath
    {
        /// <summary>Делит целое число с округлением вниз, как требуют координаты клеток.</summary>
        public static int FloorDivide(int value, int size)
        {
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
            var quotient = value / size;
            return value % size < 0 ? quotient - 1 : quotient;
        }

        /// <summary>Возвращает локальный индекс от нуля до размера чанка минус один.</summary>
        public static int LocalIndex(int value, int size)
        {
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
            var remainder = value % size;
            return remainder < 0 ? remainder + size : remainder;
        }
    }
}
