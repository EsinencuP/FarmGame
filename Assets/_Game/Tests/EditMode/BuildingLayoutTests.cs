using NUnit.Framework;

namespace MyLittleFarm.Tests.EditMode
{
    public sealed class BuildingLayoutTests
    {
        [Test]
        public void PlacementContractsAndRandomizedOccupancyRemainConsistent()
        {
            Assert.That(BuildingContractChecks.Run(), Is.GreaterThan(64000));
        }
    }
}
