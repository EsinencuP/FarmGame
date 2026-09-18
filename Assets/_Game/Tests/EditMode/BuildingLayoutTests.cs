using NUnit.Framework;

namespace MyLittleFarm.Tests.EditMode
{
    /// <summary>Подключает общий набор контрактных проверок BuildingLayout к Unity Test Runner.</summary>
    public sealed class BuildingLayoutTests
    {
        /// <summary>Проверяет базовые случаи и длинную воспроизводимую последовательность операций.</summary>
        [Test]
        public void PlacementContractsAndRandomizedOccupancyRemainConsistent()
        {
            Assert.That(BuildingContractChecks.Run(), Is.GreaterThan(64000));
        }
    }
}
