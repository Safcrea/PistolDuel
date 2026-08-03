using System.Reflection;
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

        [Test]
        public void DefaultProjectile_ExpiresOnThirdWallHit()
        {
            int remaining = ProjectileRules.DefaultWallBounces;

            Assert.That(ProjectileRules.ConsumeWallBounce(ref remaining), Is.False);
            Assert.That(ProjectileRules.ConsumeWallBounce(ref remaining), Is.False);
            Assert.That(ProjectileRules.ConsumeWallBounce(ref remaining), Is.True);
        }

        [TestCase(0, 2)]
        [TestCase(1, 3)]
        [TestCase(4, 6)]
        [TestCase(20, 6)]
        public void ExtraBounce_IncreasesPlayerBudgetAndCapsAtSix(int stacks, int expected)
        {
            Assert.That(ProjectileRules.GetPlayerBounceLimit(stacks), Is.EqualTo(expected));
        }

        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(5, 4)]
        [TestCase(9, 6)]
        [TestCase(14, 8)]
        public void EnemyArchetypes_UnlockByWave(int wave, int expected)
        {
            Assert.That(ProgressionRules.GetUnlockedEnemyArchetypeCount(wave), Is.EqualTo(expected));
        }

        [TestCase(0, 0)]
        [TestCase(6, 1)]
        [TestCase(30, 5)]
        [TestCase(120, 5)]
        public void PlayerChassis_CapsAtSixVisualTiers(int kills, int expectedIndex)
        {
            Assert.That(ProgressionRules.GetPlayerChassisIndex(kills), Is.EqualTo(expectedIndex));
        }

        [TestCase(TeamId.Player, TeamId.Enemy, true)]
        [TestCase(TeamId.Enemy, TeamId.Player, true)]
        [TestCase(TeamId.Enemy, TeamId.Enemy, false)]
        [TestCase(TeamId.Player, TeamId.Player, false)]
        [TestCase(TeamId.Neutral, TeamId.Player, false)]
        public void AimLock_OnlyAcceptsOpposingPlayerAndEnemyTeams(TeamId source, TeamId target, bool expected)
        {
            Assert.That(AimLockRules.IsOpposingCombatTeam(source, target), Is.EqualTo(expected));
        }

        [Test]
        public void BulletLaunch_ScalesVisualWithoutScalingHitbox()
        {
            GameObject bulletObject = new GameObject("Bullet Test");
            PhysicsMaterial2D material = new PhysicsMaterial2D("Bullet Test Material");
            BulletData data = ScriptableObject.CreateInstance<BulletData>();
            try
            {
                data.visualScale = 1.5f;
                BulletController bullet = bulletObject.AddComponent<BulletController>();
                bullet.Build(null, material);
                bullet.Launch(
                    Vector3.zero,
                    Vector2.right,
                    TeamId.Player,
                    bulletObject,
                    data,
                    Color.white,
                    null,
                    0.2f,
                    0.065f);

                Assert.That(bullet.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(bullet.transform.Find("Visual").localScale.x, Is.EqualTo(0.3f).Within(0.0001f));
                Assert.That(bullet.GetComponent<CircleCollider2D>().radius, Is.EqualTo(0.065f).Within(0.0001f));
                Assert.That((bullet.GetComponent<Rigidbody2D>().constraints & RigidbodyConstraints2D.FreezeRotation) != 0, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(data);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(bulletObject);
            }
        }

        [TestCase(1f, 0f)]
        [TestCase(0f, 1f)]
        [TestCase(-1f, 0f)]
        [TestCase(0f, -1f)]
        [TestCase(1f, 1f)]
        [TestCase(-1f, 1f)]
        public void BulletVisual_FollowsLiveTrajectoryWithoutRotatingPhysicsRoot(float velocityX, float velocityY)
        {
            GameObject bulletObject = new GameObject("Heading Test");
            PhysicsMaterial2D material = new PhysicsMaterial2D("Heading Test Material");
            BulletData data = ScriptableObject.CreateInstance<BulletData>();
            try
            {
                BulletController bullet = bulletObject.AddComponent<BulletController>();
                bullet.Build(null, material);
                bullet.Launch(Vector3.zero, Vector2.right, TeamId.Player, bulletObject, data, Color.white, null, 0.2f, 0.065f);

                Vector2 trajectory = new Vector2(velocityX, velocityY).normalized;
                Rigidbody2D body = bullet.GetComponent<Rigidbody2D>();
                body.linearVelocity = trajectory * data.speed;
                InvokeLateUpdate(bullet);

                Vector2 visualHeading = bullet.transform.Find("Visual").right;
                Assert.That(Vector2.Dot(visualHeading, trajectory), Is.GreaterThan(0.9999f));
                Assert.That(body.rotation, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(data);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(bulletObject);
            }
        }

        [Test]
        public void BulletVisual_KeepsLastHeadingAtRestAndResetsWhenReused()
        {
            GameObject bulletObject = new GameObject("Pooled Heading Test");
            PhysicsMaterial2D material = new PhysicsMaterial2D("Pooled Heading Test Material");
            BulletData data = ScriptableObject.CreateInstance<BulletData>();
            try
            {
                BulletController bullet = bulletObject.AddComponent<BulletController>();
                bullet.Build(null, material);
                bullet.Launch(Vector3.zero, Vector2.up, TeamId.Enemy, bulletObject, data, Color.white, null, 0.2f, 0.065f);

                Transform visual = bullet.transform.Find("Visual");
                Quaternion headingAtRest = visual.localRotation;
                bullet.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                InvokeLateUpdate(bullet);
                Assert.That(Quaternion.Angle(visual.localRotation, headingAtRest), Is.LessThan(0.0001f));

                bullet.gameObject.SetActive(false);
                bullet.Launch(Vector3.zero, Vector2.left, TeamId.Enemy, bulletObject, data, Color.white, null, 0.2f, 0.065f);
                Assert.That(Vector2.Dot(visual.right, Vector2.left), Is.GreaterThan(0.9999f));
            }
            finally
            {
                Object.DestroyImmediate(data);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(bulletObject);
            }
        }

        [Test]
        public void PistolShotGravity_AppliesBoundedDownwardAcceleration()
        {
            GameObject pistolObject = new GameObject("Shot Gravity Test");
            GameObject muzzleObject = new GameObject("Muzzle");
            try
            {
                muzzleObject.transform.SetParent(pistolObject.transform);
                Rigidbody2D body = pistolObject.AddComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                PistolController pistol = pistolObject.AddComponent<PistolController>();
                pistol.SetReferences(body, null, muzzleObject.transform, null);

                InvokeInstanceMethod(pistol, "ConfigureShotGravity", true, 10f, 1f, 3f);
                InvokeInstanceMethod(pistol, "StartShotGravity");
                InvokeInstanceMethod(pistol, "ApplyShotGravity", 0.2f);
                Assert.That(body.linearVelocity.y, Is.EqualTo(-2f).Within(0.0001f));

                InvokeInstanceMethod(pistol, "ApplyShotGravity", 0.2f);
                Assert.That(body.linearVelocity.y, Is.EqualTo(-3f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(pistolObject);
            }
        }

        private static void InvokeLateUpdate(BulletController bullet)
        {
            MethodInfo lateUpdate = typeof(BulletController).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(lateUpdate, Is.Not.Null);
            lateUpdate.Invoke(bullet, null);
        }

        private static void InvokeInstanceMethod(object owner, string methodName, params object[] arguments)
        {
            MethodInfo method = owner.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(method, Is.Not.Null);
            method.Invoke(owner, arguments);
        }
    }
}
