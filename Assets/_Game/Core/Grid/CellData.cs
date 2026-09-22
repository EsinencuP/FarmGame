using System;

namespace MyLittleFarm.Core.Grid
{
    /// <summary>
    /// Хранит минимальное изменяемое состояние одной клетки. Подробные данные культуры или
    /// постройки остаются в CropSystem и BuildSystem, а occupantId связывает эти системы с гридом.
    /// </summary>
    [Serializable]
    public struct CellData
    {
        // type определяет правила движения и доступные действия на клетке.
        public CellType type;
        // occupantId содержит стабильный ID культуры или постройки; null означает отсутствие владельца.
        public string occupantId;

        /// <summary>Создаёт свободную клетку указанного типа.</summary>
        public CellData(CellType initialType)
        {
            type = initialType;
            occupantId = null;
        }
    }
}
