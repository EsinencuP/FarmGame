using System;

namespace MyLittleFarm.Gameplay.Building
{
    [Serializable]
    public sealed class BuildingRuntimeState
    {
        public string id;
        public string definitionId;
        public int x;
        public int z;
        public int quarterTurns;
        public int paidCost;

        public BuildingRuntimeState Copy() => (BuildingRuntimeState)MemberwiseClone();
    }
}
