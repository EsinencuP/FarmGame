namespace MyLittleFarm.Core
{
    /// <summary>Only committed gameplay actions publish these; snapshot restore never does.</summary>
    public readonly struct FarmActivity
    {
        public readonly string Kind, ItemId;
        public readonly long Amount, Value;
        public readonly float Hour;
        public FarmActivity(string kind,string itemId,long amount,long value,float hour)
        { Kind=kind; ItemId=itemId; Amount=amount; Value=value; Hour=hour; }
    }
}
