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
        public void ApplyUpgrade(UpgradeData upgrade, bool milestone = false)
        {
            if (player == null || upgrade == null)
            {
                return;
            }

            bool permanent = IsPermanentPowerup(upgrade.powerupId);
            int currentStacks = upgradeStacks.TryGetValue(upgrade.powerupId, out int stacks) ? stacks : 0;
            if (permanent && currentStacks >= upgrade.maxStacks)
            {
                ShowFloatingLabel(upgrade.displayName.ToUpperInvariant() + " MAX", player.transform.position + Vector3.up * 0.55f, Color.cyan);
                if (!milestone)
                {
                    nextPowerupTime = runTime + powerupIntervalAfterCollection;
                }
                return;
            }

            switch (upgrade.powerupId)
            {
                case PowerupId.RapidFire:
                    player.ApplyFireRateMultiplier(0.82f);
                    player.ShowAttachment(AttachmentArtId.RapidFire);
                    break;
                case PowerupId.DoubleShot:
                    player.AddProjectile();
                    player.ShowAttachment(AttachmentArtId.DoubleShot);
                    break;
                case PowerupId.PiercingBullet:
                    playerBulletData.penetration = Mathf.Min(3, playerBulletData.penetration + 1);
                    player.ShowAttachment(AttachmentArtId.Piercing);
                    break;
                case PowerupId.ExplosiveBullet:
                    playerBulletData.explosive = true;
                    break;
                case PowerupId.ExtraBounce:
                    playerBulletData.maxRicochets = ProjectileRules.GetPlayerBounceLimit(playerStartingRicochets, currentStacks + 1);
                    playerBulletData.bounceRetention = Mathf.Min(1f, playerBulletData.bounceRetention + 0.01f);
                    break;
                case PowerupId.Shield:
                    player.Health.AddShield(1);
                    break;
                case PowerupId.RepairKit:
                    if (!player.Health.Repair(1f))
                    {
                        player.Health.AddShield(1);
                    }
                    break;
                case PowerupId.Confusion:
                    RestartTimedEffect(ref confusionRoutine, ConfuseEnemies(8f));
                    break;
                case PowerupId.EnemyFreeze:
                    RestartTimedEffect(ref freezeRoutine, FreezeEnemies(5f));
                    break;
                case PowerupId.Shockwave:
                    ApplyPlayerShockwave(player.transform.position, 8f);
                    break;
                case PowerupId.PowerupMagnet:
                    magnetPowerups = true;
                    break;
                case PowerupId.HeavyBullet:
                    playerBulletData.damage = Mathf.Min(3.5f, playerBulletData.damage + 0.5f);
                    playerBulletData.speed = Mathf.Max(17f, playerBulletData.speed * 0.94f);
                    playerBulletData.visualScale = Mathf.Min(1.8f, playerBulletData.visualScale + 0.2f);
                    player.ApplyRecoilMultiplier(1.12f);
                    player.ShowAttachment(AttachmentArtId.HeavyBullet);
                    break;
                case PowerupId.ShotgunBlast:
                    SetWeaponMode(WeaponMode.ShotgunBlast);
                    break;
                case PowerupId.RicochetCannon:
                    SetWeaponMode(WeaponMode.RicochetCannon);
                    break;
                case PowerupId.RevolverEvolution:
                    SetWeaponMode(WeaponMode.RevolverEvolution);
                    break;
            }

            if (permanent)
            {
                upgradeStacks[upgrade.powerupId] = currentStacks + 1;
            }

            RefreshPlayerBulletArt();
            score += 250;
            ShowFloatingLabel(upgrade.displayName.ToUpperInvariant(), player.transform.position + Vector3.up * 0.55f, new Color(1f, 0.8f, 0.18f));
            ShakeCamera(0.18f);
            Vibrate(0.45f);
            if (!milestone)
            {
                nextPowerupTime = runTime + powerupIntervalAfterCollection;
            }
        }

        private static bool IsPermanentPowerup(PowerupId id)
        {
            switch (id)
            {
                case PowerupId.RapidFire:
                case PowerupId.DoubleShot:
                case PowerupId.PiercingBullet:
                case PowerupId.ExplosiveBullet:
                case PowerupId.ExtraBounce:
                case PowerupId.PowerupMagnet:
                case PowerupId.HeavyBullet:
                    return true;
                default:
                    return false;
            }
        }

        private void SetWeaponMode(WeaponMode mode)
        {
            activeWeaponMode = mode;
            playerBulletData.splitOnFirstRicochet = mode == WeaponMode.RicochetCannon;
            player.ConfigureWeaponMode(mode);
        }

        private void RefreshPlayerBulletArt()
        {
            if (activeWeaponMode == WeaponMode.RicochetCannon)
            {
                playerBulletData.artId = BulletArtId.Ricochet;
            }
            else if (playerBulletData.explosive)
            {
                playerBulletData.artId = BulletArtId.Explosive;
            }
            else if (playerBulletData.penetration > 0)
            {
                playerBulletData.artId = BulletArtId.Piercing;
            }
            else if (upgradeStacks.ContainsKey(PowerupId.HeavyBullet))
            {
                playerBulletData.artId = BulletArtId.Heavy;
            }
            else
            {
                playerBulletData.artId = BulletArtId.PlayerStandard;
            }
        }

        private void RestartTimedEffect(ref Coroutine activeRoutine, IEnumerator routine)
        {
            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
            }

            activeRoutine = StartCoroutine(routine);
        }

        private IEnumerator FreezeEnemies(float duration)
        {
            enemiesFrozen = true;
            yield return new WaitForSecondsRealtime(duration);
            enemiesFrozen = false;
            freezeRoutine = null;
        }

        private IEnumerator ConfuseEnemies(float duration)
        {
            enemiesConfused = true;
            yield return new WaitForSecondsRealtime(duration);
            enemiesConfused = false;
            confusionRoutine = null;
        }

        private void ApplyPlayerShockwave(Vector2 center, float force)
        {
            for (int i = 0; i < enemyPool.Count; i++)
            {
                if (enemyPool[i].gameObject.activeSelf && enemyPool[i].IsAlive)
                {
                    PushFromPoint(enemyPool[i].Body, center, force);
                }
            }

            projectilePool.DeactivateTeam(TeamId.Enemy);

            SpawnBurst(center, Color.cyan, 14);
        }

        public void OnPowerupRemoved()
        {
            activePowerups = Mathf.Max(0, activePowerups - 1);
        }

        public void AwardRicochet(BulletController bullet)
        {
            if (bullet.OwnerTeam == TeamId.Player)
            {
                score += 5;
            }
        }

    }
}
