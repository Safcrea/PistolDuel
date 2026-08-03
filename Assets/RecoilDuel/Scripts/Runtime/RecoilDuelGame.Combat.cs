using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace RecoilDuel
{
    public sealed partial class RecoilDuelGame
    {
        internal void ConfigureGunShotGravity(PistolController pistol)
        {
            bool isPlayer = pistol.Team == TeamId.Player;
            pistol.ConfigureShotGravity(
                isPlayer ? enablePlayerShotGravity : enableEnemyShotGravity,
                isPlayer ? playerShotGravityAcceleration : enemyShotGravityAcceleration,
                gunShotGravityDuration,
                maximumGunFallSpeed);
        }

        public BulletController SpawnBullet(PistolController source, BulletData bulletData)
        {
            return SpawnBullet(source, bulletData, source.MuzzleRight, true);
        }

        public BulletController SpawnBullet(PistolController source, BulletData bulletData, Vector2 direction, bool playFeedback, float damageMultiplier = 1f)
        {
            BulletController bullet = GetInactiveBullet();
            bullet.Launch(
                source.Muzzle.position,
                direction,
                source.Team,
                source.gameObject,
                bulletData,
                source.Team == TeamId.Player ? new Color(0.25f, 0.75f, 1f) : new Color(1f, 0.22f, 0.13f),
                this,
                GetBulletVisualSize(source.Team),
                GetBulletHitboxRadius(source.Team),
                true,
                damageMultiplier);

            if (playFeedback)
            {
                FeedbackShot(source);
            }

            return bullet;
        }

        public void OnBulletExpired(BulletController bullet)
        {
            bullet.gameObject.SetActive(false);
        }

        public void SpawnSplitRicochets(BulletController source, Vector2 reflectedDirection)
        {
            for (int i = -1; i <= 1; i += 2)
            {
                BulletController split = GetInactiveBulletWithoutRecycling();
                if (split == null)
                {
                    return;
                }

                Vector2 direction = Quaternion.Euler(0f, 0f, i * 17f) * reflectedDirection;
                split.Launch(
                    source.transform.position + (Vector3)(direction.normalized * 0.12f),
                    direction,
                    source.OwnerTeam,
                    source.SourceOwner,
                    source.Data,
                    new Color(0.45f, 0.9f, 1f),
                    this,
                    GetBulletVisualSize(source.OwnerTeam),
                    GetBulletHitboxRadius(source.OwnerTeam),
                    false,
                    0.65f);
            }
        }

        public void ApplyShockPulse(Vector2 hitPoint, float force)
        {
            if (force <= 0f)
            {
                return;
            }

            if (player != null && player.IsAlive)
            {
                PushFromPoint(player.Body, hitPoint, force);
            }

            for (int i = 0; i < enemyPool.Count; i++)
            {
                if (enemyPool[i].gameObject.activeSelf && enemyPool[i].IsAlive)
                {
                    PushFromPoint(enemyPool[i].Body, hitPoint, force);
                }
            }

            SpawnBurst(hitPoint, Color.cyan, 8);
        }

        public void ApplyExplosion(Vector2 hitPoint, BulletController source, HealthComponent directTarget)
        {
            BulletData data = source.Data;
            int hitCount = GetExplosionHits(hitPoint, data.explosionRadius);
            explosionDamagedTargets.Clear();
            for (int i = 0; i < hitCount; i++)
            {
                HealthComponent health = explosionOverlapHits[i].GetComponentInParent<HealthComponent>();
                if (health == null || health == directTarget || health.Team != TeamId.Enemy || !explosionDamagedTargets.Add(health))
                {
                    continue;
                }

                Vector2 direction = (Vector2)health.transform.position - hitPoint;
                if (direction.sqrMagnitude < 0.01f)
                {
                    direction = Vector2.up;
                }

                DamageInfo splash = new DamageInfo(
                    data.damage * data.explosionDamageMultiplier,
                    source.OwnerTeam,
                    source.SourceOwner,
                    hitPoint,
                    direction.normalized);
                health.ApplyDamage(splash, 0);
            }

            SpawnBurst(hitPoint, new Color(1f, 0.55f, 0.12f), 12);
        }

        private int GetExplosionHits(Vector2 center, float radius)
        {
            while (true)
            {
                int hitCount = Physics2D.OverlapCircle(center, radius, explosionContactFilter, explosionOverlapHits);
                if (hitCount < explosionOverlapHits.Length)
                {
                    return hitCount;
                }

                Array.Resize(ref explosionOverlapHits, explosionOverlapHits.Length * 2);
            }
        }

        private static void PushFromPoint(Rigidbody2D body, Vector2 point, float force)
        {
            Vector2 offset = body.position - point;
            float distance = offset.magnitude;
            if (distance < 0.05f || distance > 2.4f)
            {
                return;
            }

            body.AddForce(offset.normalized * force * (1f - distance / 2.4f), ForceMode2D.Impulse);
        }

        public void OnGunDamaged(HealthComponent target, DamageInfo damage)
        {
            RequestHitStop(damage.Damage >= 2f ? 0.08f : 0.035f);
            ShakeCamera(0.08f + damage.Damage * 0.04f);

            if (target.Team == TeamId.Player)
            {
                Vibrate(0.28f);
            }
        }

        public void OnGunDestroyed(HealthComponent target, DamageInfo damage)
        {
            SpawnBurst(target.transform.position, target.Team == TeamId.Player ? Color.cyan : Color.red, 14);

            if (target.Team == TeamId.Player)
            {
                StartCoroutine(GameOver());
                return;
            }

            PistolController pistol = target.GetComponent<PistolController>();
            if (pistol != null)
            {
                pistol.DeactivateForPool();
            }

            if (damage.SourceTeam == TeamId.Enemy)
            {
                friendlyFireKills++;
                score += 150;
                ShowFloatingLabel("CROSSFIRE", target.transform.position, new Color(1f, 0.72f, 0.22f));
            }
            else
            {
                score += 100;
                if (target.LastHitRicochetCount > 0)
                {
                    ricochetKills++;
                    score += 50;
                    ShowFloatingLabel("RICOCHET", target.transform.position, Color.cyan);
                }
            }

            totalEnemyKills++;
            int earnedTier = ProgressionRules.GetUpgradeTier(totalEnemyKills, killsPerUpgrade);
            if (earnedTier > lastMilestoneTier)
            {
                lastMilestoneTier = earnedTier;
                UpgradeData milestoneUpgrade = killMilestoneUpgrades[(earnedTier - 1) % killMilestoneUpgrades.Count];
                ApplyUpgrade(milestoneUpgrade, true);
                player.SetPlayerProgressionTier(earnedTier);
                ShowFloatingLabel(killsPerUpgrade + " KILLS - WEAPON MK " + earnedTier, player.transform.position + Vector3.up * 0.85f, Color.cyan);
            }

            if (!clearSequenceRunning && CountActiveEnemies() == 0 && state != RunState.RunOver)
            {
                StartCoroutine(ClearAndDropNextWave());
            }
        }

    }
}
