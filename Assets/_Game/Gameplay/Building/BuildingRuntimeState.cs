using System;

namespace MyLittleFarm.Gameplay.Building
{
    /// <summary>Сериализуемое изменяемое состояние одной размещённой постройки.</summary>
    [Serializable]
    public sealed class BuildingRuntimeState
    {
        // Уникальный id отличает два объекта одного типа; definitionId связывает объект с каталогом.
        public string id;
        public string definitionId;
        // x/z — начальная клетка следа, quarterTurns — поворот кратно 90 градусам.
        public int x;
        public int z;
        public int quarterTurns;
        // Фактически уплаченная стоимость используется для расчёта возврата при удалении.
        public int paidCost;
        // Zero is the old-save default. House tiers retain the same reserved footprint.
        public int appearanceLevel;
        public string customText;
        public int Refund=>(paidCost+FarmDecorationCatalog.HouseUpgradeInvestment(appearanceLevel))/2;

        /// <summary>Создаёт отделённую копию, чтобы внешний код не изменил внутреннее состояние layout.</summary>
        public BuildingRuntimeState Copy() => (BuildingRuntimeState)MemberwiseClone();
    }
}
