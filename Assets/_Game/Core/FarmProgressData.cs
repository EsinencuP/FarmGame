using System;
using System.Collections.Generic;

namespace MyLittleFarm.Core
{
    [Serializable]
    public sealed class FarmProgressData
    {
        public long revenue, expenses, refunds, plantings, harvests, harvestedUnits, apples, eggs, bonusHarvests, rareSeeds;
        public int highestBalance;
        public bool shepherdHired;
        public List<CropHistory> crops=new List<CropHistory>();
        public List<string> achievements=new List<string>();
        public List<string> decorations=new List<string>();
        public List<SeedCacheState> caches=new List<SeedCacheState>();
    }
    [Serializable] public sealed class CropHistory { public string cropId; public long planted; }
    [Serializable] public sealed class SeedCacheState { public string id; public long nextAvailableUnixMs; public bool discovered; }
}
