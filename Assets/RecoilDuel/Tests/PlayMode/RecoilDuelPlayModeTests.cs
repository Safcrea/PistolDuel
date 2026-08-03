using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RecoilDuel.Tests
{
    public sealed class RecoilDuelPlayModeTests
    {
        [UnityTest]
        public IEnumerator BulletVisualTracksVelocityDuringRenderedFrames()
        {
            GameObject bulletObject = new GameObject("PlayMode Bullet Heading Test");
            PhysicsMaterial2D material = new PhysicsMaterial2D("PlayMode Bullet Material");
            BulletData data = ScriptableObject.CreateInstance<BulletData>();
            try
            {
                BulletController bullet = bulletObject.AddComponent<BulletController>();
                bullet.Build(null, material);
                bullet.Launch(Vector3.zero, Vector2.right, TeamId.Player, bulletObject, data, Color.white, null, 0.2f, 0.065f);

                Rigidbody2D body = bullet.GetComponent<Rigidbody2D>();
                body.linearVelocity = new Vector2(-4f, 7f);
                yield return null;

                Vector2 expected = body.linearVelocity.normalized;
                Vector2 visualHeading = bullet.transform.Find("Visual").right;
                Assert.That(Vector2.Dot(visualHeading, expected), Is.GreaterThan(0.999f));
                Assert.That(body.rotation, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                Object.Destroy(data);
                Object.Destroy(material);
                Object.Destroy(bulletObject);
            }
        }

        [UnityTest]
        public IEnumerator RuntimeBootstrapCreatesPlayableManagerAndPlayer()
        {
            yield return null;
            RecoilDuelGame game = Object.FindFirstObjectByType<RecoilDuelGame>();
            Assert.That(game, Is.Not.Null);

            yield return null;
            Assert.That(game.PlayerPistol, Is.Not.Null);
            Assert.That(game.PlayerPistol.IsAlive, Is.True);
        }

        [UnityTest]
        public IEnumerator PlayerShotStartsManagerConfiguredGravity()
        {
            RecoilDuelGame game = Object.FindFirstObjectByType<RecoilDuelGame>();
            Assert.That(game, Is.Not.Null);

            while (game.PlayerPistol == null)
            {
                yield return null;
            }

            PistolController pistol = game.PlayerPistol;
            Quaternion originalRotation = pistol.transform.rotation;
            Vector2 originalVelocity = pistol.Body.linearVelocity;
            float originalAcceleration = GetPrivateField<float>(game, "playerShotGravityAcceleration");
            float originalDuration = GetPrivateField<float>(game, "gunShotGravityDuration");
            float originalFallSpeed = GetPrivateField<float>(game, "maximumGunFallSpeed");
            bool originalEnabled = GetPrivateField<bool>(game, "enablePlayerShotGravity");

            try
            {
                SetPrivateField(game, "enablePlayerShotGravity", true);
                SetPrivateField(game, "playerShotGravityAcceleration", 20f);
                SetPrivateField(game, "gunShotGravityDuration", 0.5f);
                SetPrivateField(game, "maximumGunFallSpeed", 8f);
                SetPrivateField(pistol, "nextFireTime", Time.time - 1f);
                pistol.transform.rotation = Quaternion.identity;
                pistol.Body.linearVelocity = Vector2.zero;
                pistol.Body.angularVelocity = 0f;

                Assert.That(pistol.TryFire(), Is.True);
                yield return new WaitForFixedUpdate();

                Assert.That(pistol.Body.linearVelocity.y, Is.LessThan(-0.1f));
            }
            finally
            {
                SetPrivateField(game, "enablePlayerShotGravity", originalEnabled);
                SetPrivateField(game, "playerShotGravityAcceleration", originalAcceleration);
                SetPrivateField(game, "gunShotGravityDuration", originalDuration);
                SetPrivateField(game, "maximumGunFallSpeed", originalFallSpeed);
                SetPrivateField(pistol, "shotGravityTimeRemaining", 0f);
                pistol.transform.rotation = originalRotation;
                pistol.Body.linearVelocity = originalVelocity;
            }
        }

        [UnityTest]
        public IEnumerator ClearingEnemiesAdvancesAndActivatesNextWave()
        {
            RecoilDuelGame game = Object.FindFirstObjectByType<RecoilDuelGame>();
            Assert.That(game, Is.Not.Null);

            float readyDeadline = Time.realtimeSinceStartup + 5f;
            while (!game.IsCombatActive && Time.realtimeSinceStartup < readyDeadline)
            {
                yield return null;
            }
            Assert.That(game.IsCombatActive, Is.True);

            int startingWave = GetPrivateProperty<int>(game, "waveIndex");
            List<PistolController> enemies = GetPrivateField<List<PistolController>>(game, "enemyPool");
            DamageInfo lethalDamage = new DamageInfo(1000f, TeamId.Player, game.PlayerPistol.gameObject, Vector2.zero, Vector2.up);
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i].gameObject.activeSelf && enemies[i].IsAlive)
                {
                    enemies[i].Health.ApplyDamage(lethalDamage, 0);
                }
            }

            float nextWaveDeadline = Time.realtimeSinceStartup + 7f;
            bool nextWaveActivated = false;
            while (Time.realtimeSinceStartup < nextWaveDeadline)
            {
                int currentWave = GetPrivateProperty<int>(game, "waveIndex");
                for (int i = 0; i < enemies.Count; i++)
                {
                    if (currentWave > startingWave
                        && enemies[i].gameObject.activeSelf
                        && enemies[i].IsAlive
                        && enemies[i].EnemyBrain != null
                        && enemies[i].EnemyBrain.IsActiveBrain)
                    {
                        nextWaveActivated = true;
                        break;
                    }
                }

                if (nextWaveActivated)
                {
                    break;
                }

                yield return null;
            }

            Assert.That(GetPrivateProperty<int>(game, "waveIndex"), Is.GreaterThan(startingWave));
            Assert.That(nextWaveActivated, Is.True);
        }

        private static T GetPrivateField<T>(object owner, string name)
        {
            FieldInfo field = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (T)field.GetValue(owner);
        }

        private static T GetPrivateProperty<T>(object owner, string name)
        {
            PropertyInfo property = owner.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null);
            return (T)property.GetValue(owner);
        }

        private static void SetPrivateField(object owner, string name, object value)
        {
            FieldInfo field = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(owner, value);
        }
    }
}
