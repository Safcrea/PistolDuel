using NUnit.Framework;
using UnityEngine;

namespace RecoilDuel.Tests
{
    public sealed class RecoilDuelSchedulerTests
    {
        [Test]
        public void MajorDropScheduler_RespectsHardPity()
        {
            MajorDropTimingData timing = ScriptableObject.CreateInstance<MajorDropTimingData>();
            timing.earlyTwoMinuteChance = 0f;
            timing.hardPityMinutes = 15f;

            for (int seed = 0; seed < 200; seed++)
            {
                float delay = MajorDropScheduler.RollNextMajorDropDelaySeconds(timing, new System.Random(seed));
                Assert.LessOrEqual(delay, 15f * 60f);
                Assert.GreaterOrEqual(delay, 5f * 60f);
            }

            Object.DestroyImmediate(timing);
        }

        [Test]
        public void MajorDropScheduler_CanRollEarlyWindow()
        {
            MajorDropTimingData timing = ScriptableObject.CreateInstance<MajorDropTimingData>();
            timing.earlyTwoMinuteChance = 1f;
            timing.earlyWindowMinutes = new Vector2(1.8f, 2.3f);

            float delay = MajorDropScheduler.RollNextMajorDropDelaySeconds(timing, new System.Random(11));

            Assert.GreaterOrEqual(delay, 1.8f * 60f);
            Assert.LessOrEqual(delay, 2.3f * 60f);
            Object.DestroyImmediate(timing);
        }

        [TestCase(0, 0)]
        [TestCase(5, 0)]
        [TestCase(6, 1)]
        [TestCase(11, 1)]
        [TestCase(12, 2)]
        [TestCase(60, 10)]
        public void UpgradeTier_AdvancesEverySixKills(int kills, int expectedTier)
        {
            Assert.That(ProgressionRules.GetUpgradeTier(kills), Is.EqualTo(expectedTier));
        }

        [TestCase(1, 2)]
        [TestCase(2, 2)]
        [TestCase(3, 3)]
        [TestCase(5, 4)]
        [TestCase(7, 5)]
        [TestCase(50, 5)]
        public void WaveEnemyCount_GrowsAndCapsWithoutEnding(int wave, int expectedCount)
        {
            Assert.That(ProgressionRules.GetEnemyCountForWave(wave), Is.EqualTo(expectedCount));
        }
    }
}
