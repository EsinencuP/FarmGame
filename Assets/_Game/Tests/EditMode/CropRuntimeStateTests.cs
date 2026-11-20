using MyLittleFarm.Gameplay.Farming;
using NUnit.Framework;

namespace MyLittleFarm.Tests.EditMode
{
    /// <summary>Проверяет расчёт прогресса культуры только по сохранённым временным данным.</summary>
    public sealed class CropRuntimeStateTests
    {
        [TestCase(2, 0)]
        [TestCase(3, 1)]
        public void RegrowingCropUsesAnUnripeStageUntilReady(int stages, int regrowthStage)
        {
            var crop = new CropRuntimeState
            {
                plantedAtUnixMs = 10_000,
                growDurationSeconds = 8f,
                stageCount = stages,
                isRegrowing = true
            };

            Assert.That(crop.GetStage(10_000), Is.EqualTo(regrowthStage));
            Assert.That(crop.GetStage(17_999), Is.EqualTo(regrowthStage));
            Assert.That(crop.GetStage(18_000), Is.EqualTo(stages - 1));
        }

        /// <summary>Подтверждает правильные стадии в начале, середине, перед зрелостью и после неё.</summary>
        [Test]
        public void GrowthStages_AreCalculatedFromUtcTimestamp()
        {
            var crop = new CropRuntimeState
            {
                plantedAtUnixMs = 10_000,
                growDurationSeconds = 8f,
                stageCount = 3
            };

            Assert.That(crop.GetStage(10_000), Is.EqualTo(0));
            Assert.That(crop.GetStage(14_000), Is.EqualTo(1));
            Assert.That(crop.GetStage(17_999), Is.EqualTo(1), "Final visual stage is reserved for mature crops");
            Assert.That(crop.GetStage(18_000), Is.EqualTo(2));
            Assert.That(crop.IsMature(18_000), Is.True);
        }

        /// <summary>Гарантирует ограничение прогресса диапазоном от нуля до единицы.</summary>
        [Test]
        public void GrowthRatio_IsClampedForPastAndFutureTime()
        {
            var crop = new CropRuntimeState
            {
                plantedAtUnixMs = 10_000,
                growDurationSeconds = 10f,
                stageCount = 3
            };

            Assert.That(crop.GetGrowthRatio(5_000), Is.EqualTo(0f));
            Assert.That(crop.GetGrowthRatio(30_000), Is.EqualTo(1f));
        }
    }
}
