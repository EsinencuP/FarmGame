using System;

namespace MyLittleFarm.Gameplay.Building
{
    /// <summary>Сериализуемое изменяемое состояние одной размещённой постройки.</summary>
    [Serializable]
    public sealed class BuildingRuntimeState
    {
        internal const string StarterHouseId = "starter-house";
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
        // Older saves omit the field and use the historical prices for their current appearance level.
        public int paidUpgradeCost;
        public string customText;
        // A positive duration keeps the site pending even at zero, if its final footprint is blocked.
        public int constructionDurationSeconds;
        public float constructionSecondsRemaining;
        public bool IsUnderConstruction => constructionDurationSeconds > 0;
        internal bool IsStarterHouse => id == StarterHouseId && definitionId == "house"
            && paidCost == 0 && !IsUnderConstruction;
        internal int UpgradeInvestment => paidUpgradeCost != 0
            ? paidUpgradeCost : FarmDecorationCatalog.PreviousHouseUpgradeInvestment(appearanceLevel);
        public int Refund => (int)(((long)paidCost + UpgradeInvestment) / 2);

        /// <summary>Создаёт отделённую копию, чтобы внешний код не изменил внутреннее состояние layout.</summary>
        public BuildingRuntimeState Copy() => (BuildingRuntimeState)MemberwiseClone();
    }
}
