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
        private void CreateRuntimeData()
        {
            upgrades.Clear();
            killMilestoneUpgrades.Clear();
            enemyArchetypes.Clear();
            enemyGunDefinitions.Clear();
            enemyBulletDefinitions.Clear();

            playerBulletData = ScriptableObject.CreateInstance<BulletData>();
            playerBulletData.speed = playerBulletSpeed;
            playerBulletData.damage = playerBulletDamage;
            playerBulletData.maxRicochets = playerStartingRicochets;
            playerBulletData.lifetime = 4.5f;
            playerBulletData.ownerImmunityDuration = 0.12f;
            playerBulletData.artId = BulletArtId.PlayerStandard;

            playerGunData = ScriptableObject.CreateInstance<GunData>();
            playerGunData.gunId = "player_standard";
            playerGunData.mass = 1f;
            playerGunData.recoilForce = playerRecoilForce;
            playerGunData.fireCooldown = playerFireCooldown;
            playerGunData.maxHealth = playerMaxHealth;
            playerGunData.bullet = playerBulletData;

            CreateEnemyCatalog();

            upgrades.Add(CreateUpgrade(PowerupId.RapidFire, "rapid_fire", "Rapid Fire", UpgradeEffectType.FireRate, 5));
            upgrades.Add(CreateUpgrade(PowerupId.DoubleShot, "double_shot", "Double Shot", UpgradeEffectType.MultiShot, 1));
            upgrades.Add(CreateUpgrade(PowerupId.PiercingBullet, "piercing_bullet", "Piercing Bullet", UpgradeEffectType.Penetration, 3));
            upgrades.Add(CreateUpgrade(PowerupId.ExplosiveBullet, "explosive_bullet", "Explosive Bullet", UpgradeEffectType.Damage, 1));
            upgrades.Add(CreateUpgrade(PowerupId.ExtraBounce, "extra_bounce", "Extra Bounce", UpgradeEffectType.Ricochet, 4));
            upgrades.Add(CreateUpgrade(PowerupId.Shield, "shield", "Shield", UpgradeEffectType.Shield, 5));
            upgrades.Add(CreateUpgrade(PowerupId.RepairKit, "repair_kit", "Repair Kit", UpgradeEffectType.Repair, 5));
            upgrades.Add(CreateUpgrade(PowerupId.Confusion, "confusion", "Confusion", UpgradeEffectType.Special, 1));
            upgrades.Add(CreateUpgrade(PowerupId.EnemyFreeze, "enemy_freeze", "Enemy Freeze", UpgradeEffectType.Special, 1));
            upgrades.Add(CreateUpgrade(PowerupId.Shockwave, "shockwave", "Shockwave", UpgradeEffectType.Special, 5));
            upgrades.Add(CreateUpgrade(PowerupId.PowerupMagnet, "powerup_magnet", "Power-up Magnet", UpgradeEffectType.Special, 1));
            upgrades.Add(CreateUpgrade(PowerupId.HeavyBullet, "heavy_bullet", "Heavy Bullet", UpgradeEffectType.Damage, 3));
            upgrades.Add(CreateUpgrade(PowerupId.ShotgunBlast, "shotgun_blast", "Shotgun Blast", UpgradeEffectType.Special, 1));
            upgrades.Add(CreateUpgrade(PowerupId.RicochetCannon, "ricochet_cannon", "Ricochet Cannon", UpgradeEffectType.Ricochet, 1));
            upgrades.Add(CreateUpgrade(PowerupId.RevolverEvolution, "revolver_evolution", "Revolver Evolution", UpgradeEffectType.Special, 1));

            PowerupId[] milestoneOrder =
            {
                PowerupId.RapidFire,
                PowerupId.DoubleShot,
                PowerupId.ExtraBounce,
                PowerupId.PiercingBullet,
                PowerupId.HeavyBullet
            };
            for (int i = 0; i < milestoneOrder.Length; i++)
            {
                killMilestoneUpgrades.Add(upgrades.Find(upgrade => upgrade.powerupId == milestoneOrder[i]));
            }

            majorDropTiming = ScriptableObject.CreateInstance<MajorDropTimingData>();
            _ = MajorDropScheduler.RollNextMajorDropDelaySeconds(majorDropTiming, majorDropRandom);
        }

        private void CreateEnemyCatalog()
        {
            enemyArchetypes.Add(CreateEnemyArchetype(EnemyArchetypeId.Standard, "standard", 1, 1, 1f, EnemyWeaponPattern.Single, 0.9f, 1.55f, 1f, 1f, 1f));
            enemyArchetypes.Add(CreateEnemyArchetype(EnemyArchetypeId.Compact, "compact", 2, 1, 1f, EnemyWeaponPattern.Single, 0.55f, 0.9f, 0.75f, 1.08f, 0.8f));
            enemyArchetypes.Add(CreateEnemyArchetype(EnemyArchetypeId.Heavy, "heavy", 4, 3, 2f, EnemyWeaponPattern.Single, 1.4f, 2f, 1.25f, 0.86f, 1.65f));
            enemyArchetypes.Add(CreateEnemyArchetype(EnemyArchetypeId.Revolver, "revolver", 5, 2, 1.5f, EnemyWeaponPattern.Burst, 1.05f, 1.55f, 0.5f, 1f, 1.05f, 3, 0.12f));
            enemyArchetypes.Add(CreateEnemyArchetype(EnemyArchetypeId.Smg, "smg", 7, 3, 1f, EnemyWeaponPattern.Burst, 0.95f, 1.35f, 0.3f, 1.12f, 0.72f, 4, 0.08f));
            enemyArchetypes.Add(CreateEnemyArchetype(EnemyArchetypeId.Shotgun, "shotgun", 9, 3, 2f, EnemyWeaponPattern.Shotgun, 1.35f, 1.85f, 0.3f, 0.9f, 1.45f, 1, 0.1f, 5, 22f));
            enemyArchetypes.Add(CreateEnemyArchetype(EnemyArchetypeId.ArmoredElite, "armored_elite", 12, 5, 3f, EnemyWeaponPattern.AlternatingHeavy, 1f, 1.55f, 1f, 0.95f, 1.55f, 1, 0.1f, 1, 0f, 1));
            enemyArchetypes.Add(CreateEnemyArchetype(EnemyArchetypeId.SniperElite, "sniper_elite", 14, 5, 2f, EnemyWeaponPattern.Sniper, 1.8f, 2.4f, 1.5f, 1.35f, 1.25f, 1, 0.1f, 1, 0f, 0, 1f));
        }

        private EnemyArchetypeData CreateEnemyArchetype(
            EnemyArchetypeId id,
            string enemyId,
            int unlockWave,
            int threatCost,
            float healthMultiplier,
            EnemyWeaponPattern pattern,
            float minDelay,
            float maxDelay,
            float damageMultiplier,
            float speedMultiplier,
            float recoilMultiplier,
            int burstCount = 1,
            float burstSpacing = 0.1f,
            int pelletCount = 1,
            float spreadDegrees = 0f,
            int shieldHits = 0,
            float telegraphDuration = 0f)
        {
            BulletData bullet = ScriptableObject.CreateInstance<BulletData>();
            bullet.speed = enemyBulletSpeed * speedMultiplier;
            bullet.damage = enemyBulletDamage * damageMultiplier;
            bullet.maxRicochets = ProjectileRules.DefaultWallBounces;
            bullet.lifetime = 4.2f;
            bullet.ownerImmunityDuration = 0.16f;
            bullet.artId = GetEnemyBulletArt(id);
            enemyBulletDefinitions.Add(bullet);

            GunData gun = ScriptableObject.CreateInstance<GunData>();
            gun.gunId = "enemy_" + enemyId;
            gun.mass = 1.05f * Mathf.Lerp(0.85f, 1.35f, Mathf.InverseLerp(1f, 3f, healthMultiplier));
            gun.recoilForce = enemyRecoilForce * recoilMultiplier;
            gun.fireCooldown = minDelay;
            gun.maxHealth = enemyBaseHealth * healthMultiplier;
            gun.bullet = bullet;
            enemyGunDefinitions.Add(gun);

            EnemyArchetypeData archetype = ScriptableObject.CreateInstance<EnemyArchetypeData>();
            archetype.enemyId = enemyId;
            archetype.archetypeId = id;
            archetype.unlockWave = unlockWave;
            archetype.threatCost = threatCost;
            archetype.healthMultiplier = healthMultiplier;
            archetype.shieldHits = shieldHits;
            archetype.weaponPattern = pattern;
            archetype.gun = gun;
            archetype.minFireDelay = minDelay;
            archetype.maxFireDelay = maxDelay;
            archetype.requiredAimDot = pattern == EnemyWeaponPattern.Sniper ? 0.96f : 0.78f;
            archetype.predictionStrength = pattern == EnemyWeaponPattern.Sniper ? 0.35f : 0.1f;
            archetype.burstCount = burstCount;
            archetype.burstSpacing = burstSpacing;
            archetype.pelletCount = pelletCount;
            archetype.spreadDegrees = spreadDegrees;
            archetype.telegraphDuration = telegraphDuration;
            archetype.bulletDamageMultiplier = damageMultiplier;
            archetype.bulletSpeedMultiplier = speedMultiplier;
            archetype.recoilMultiplier = recoilMultiplier;
            return archetype;
        }

        private static BulletArtId GetEnemyBulletArt(EnemyArchetypeId id)
        {
            switch (id)
            {
                case EnemyArchetypeId.Heavy:
                case EnemyArchetypeId.ArmoredElite:
                    return BulletArtId.Heavy;
                case EnemyArchetypeId.SniperElite:
                    return BulletArtId.Sniper;
                default:
                    return BulletArtId.EnemyStandard;
            }
        }

        private static UpgradeData CreateUpgrade(PowerupId powerupId, string id, string displayName, UpgradeEffectType effectType, int maxStacks)
        {
            UpgradeData upgrade = ScriptableObject.CreateInstance<UpgradeData>();
            upgrade.powerupId = powerupId;
            upgrade.upgradeId = id;
            upgrade.displayName = displayName;
            upgrade.effectType = effectType;
            upgrade.maxStacks = maxStacks;
            upgrade.icon = RecoilDuelArtLibrary.GetPowerup(powerupId);
            return upgrade;
        }

        private void ResetPlayerProgressionData()
        {
            playerBulletData.speed = playerBulletSpeed;
            playerBulletData.damage = playerBulletDamage;
            playerBulletData.maxRicochets = playerStartingRicochets;
            playerBulletData.penetration = 0;
            playerBulletData.bounceRetention = 0.96f;
            playerBulletData.visualScale = 1f;
            playerBulletData.shockForce = 0f;
            playerBulletData.splitOnFirstRicochet = false;
            playerBulletData.explosive = false;
            playerBulletData.artId = BulletArtId.PlayerStandard;
            activeWeaponMode = WeaponMode.Standard;
            upgradeStacks.Clear();
            enemiesFrozen = false;
            enemiesConfused = false;
        }

        private UpgradeData GetRandomUpgrade()
        {
            return upgrades[UnityEngine.Random.Range(0, upgrades.Count)];
        }

        private int GetEnemyCountForWave(int wave)
        {
            int growth = Mathf.Max(0, wave - 1) / wavesPerExtraEnemy;
            return Mathf.Clamp(initialEnemyCount + growth, initialEnemyCount, maximumEnemiesPerWave);
        }

    }
}

