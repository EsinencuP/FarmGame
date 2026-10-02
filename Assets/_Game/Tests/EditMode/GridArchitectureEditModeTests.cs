using NUnit.Framework;

namespace MyLittleFarm.Tests.EditMode
{
    /// <summary>Запускает автономные контракты архитектуры также в Unity Test Runner.</summary>
    public sealed class GridArchitectureEditModeTests
    {
        /// <summary>Проверяет координаты и раздельные массивы на рабочем коде проекта.</summary>
        [Test]
        public void LayeredChunkAndCoordinatesStayConsistent()
        {
            Assert.That(GridArchitectureContractChecks.Run(), Is.GreaterThan(8193));
        }
    }
}
