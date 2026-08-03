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
    public sealed class PistolController : MonoBehaviour
    {
        private RecoilDuelGame game;
        private GunData gunData;
        private float fireCooldownMultiplier = 1f;
        private float recoilMultiplier = 1f;
        private float stabilizerMultiplier = 1f;
        private float spinKick;
        private float nextFireTime;
        private int projectileCount = 1;
        private int revolverChamber;
        private WeaponMode weaponMode;
        private SpriteRenderer[] renderers;
        private EnemyBrain enemyBrain;
        private FallingEnemy fallingEnemy;
        private bool shotGravityEnabled;
        private float shotGravityAcceleration;
        private float shotGravityDuration;
        private float maximumShotGravityFallSpeed;
        private float shotGravityTimeRemaining;

        public Rigidbody2D Body { get; private set; }
        public HealthComponent Health { get; private set; }
        public Transform Muzzle { get; private set; }
        public TeamId Team { get; private set; }
        public bool IsPlayer { get; private set; }
        public bool IsAlive => Health != null && Health.IsAlive;
        public Vector2 MuzzleRight => Muzzle.right;
        public int UpgradeTier { get; private set; }
        public EnemyBrain EnemyBrain => enemyBrain;
        public FallingEnemy FallingEnemy => fallingEnemy;

        public void CacheEnemyBrain(EnemyBrain brain)
        {
            enemyBrain = brain;
        }

        public void CacheFallingEnemy(FallingEnemy falling)
        {
            fallingEnemy = falling;
        }

        public void SetReferences(Rigidbody2D body, HealthComponent health, Transform muzzle, SpriteRenderer[] spriteRenderers)
        {
            Body = body;
            Health = health;
            Muzzle = muzzle;
            renderers = spriteRenderers;
        }

        public void Initialize(RecoilDuelGame owner, GunData data, TeamId team, bool playerControlled)
        {
            game = owner;
            gunData = data;
            Team = team;
            IsPlayer = playerControlled;
            fireCooldownMultiplier = 1f;
            recoilMultiplier = 1f;
            stabilizerMultiplier = 1f;
            spinKick = 0f;
            projectileCount = 1;
            revolverChamber = 0;
            weaponMode = WeaponMode.Standard;
            UpgradeTier = 0;
            nextFireTime = Time.time + 0.15f;
            shotGravityTimeRemaining = 0f;
            Body.mass = data.mass;
            Body.linearDamping = data.linearDamping;
            Body.angularDamping = data.angularDamping;
            Body.linearVelocity = Vector2.zero;
            Body.angularVelocity = 0f;
            Health.ResetHealth(team, data.maxHealth);
        }

        public bool TryFire()
        {
            if (!IsAlive || Time.time < nextFireTime)
            {
                return false;
            }

            nextFireTime = Time.time + gunData.fireCooldown * fireCooldownMultiplier;
            int shotCount = projectileCount;
            float spread = projectileCount == 1 ? 0f : projectileCount == 2 ? 8f : 10f;
            float damageMultiplier = 1f;

            if (weaponMode == WeaponMode.ShotgunBlast)
            {
                shotCount = projectileCount > 1 ? 7 : 5;
                spread = 22f;
                damageMultiplier = 0.55f;
            }
            else if (weaponMode == WeaponMode.RevolverEvolution)
            {
                shotCount = 1;
                spread = 0f;
                revolverChamber = (revolverChamber + 1) % 6;
                damageMultiplier = revolverChamber == 0 ? 1.8f : 1.08f;
            }

            FireVolley(shotCount, spread, damageMultiplier);

            return true;
        }

        public void FirePatternVolley(int shotCount, float spread, float damageMultiplier)
        {
            if (!IsAlive)
            {
                return;
            }

            FireVolley(Mathf.Max(1, shotCount), Mathf.Max(0f, spread), Mathf.Max(0.05f, damageMultiplier));
        }

        private void FireVolley(int shotCount, float spread, float damageMultiplier)
        {
            for (int i = 0; i < shotCount; i++)
            {
                float t = shotCount == 1 ? 0.5f : i / (float)(shotCount - 1);
                float angle = Mathf.Lerp(-spread, spread, t);
                Vector2 direction = Quaternion.Euler(0f, 0f, angle) * MuzzleRight;
                game.SpawnBullet(this, gunData.bullet, direction, i == 0, damageMultiplier);
            }

            Body.AddForceAtPosition(-MuzzleRight * gunData.recoilForce * recoilMultiplier, Muzzle.position, ForceMode2D.Impulse);
            Body.AddTorque(UnityEngine.Random.Range(-0.18f - spinKick, 0.18f + spinKick), ForceMode2D.Impulse);
            Body.angularVelocity *= stabilizerMultiplier;
            StartShotGravity();
        }

        private void FixedUpdate()
        {
            ApplyShotGravity(Time.fixedDeltaTime);
        }

        internal void ConfigureShotGravity(bool enabled, float acceleration, float duration, float maximumFallSpeed)
        {
            shotGravityEnabled = enabled;
            shotGravityAcceleration = Mathf.Max(0f, acceleration);
            shotGravityDuration = Mathf.Max(0f, duration);
            maximumShotGravityFallSpeed = Mathf.Max(0.1f, maximumFallSpeed);

            if (!shotGravityEnabled)
            {
                shotGravityTimeRemaining = 0f;
            }
        }

        private void StartShotGravity()
        {
            if (game != null)
            {
                game.ConfigureGunShotGravity(this);
            }

            shotGravityTimeRemaining = shotGravityEnabled ? shotGravityDuration : 0f;
        }

        private void ApplyShotGravity(float deltaTime)
        {
            if (!shotGravityEnabled || shotGravityTimeRemaining <= 0f || Body == null)
            {
                return;
            }

            float gravityStep = Mathf.Min(Mathf.Max(0f, deltaTime), shotGravityTimeRemaining);
            Vector2 velocity = Body.linearVelocity;
            velocity.y = Mathf.Max(
                velocity.y - shotGravityAcceleration * gravityStep,
                -maximumShotGravityFallSpeed);
            Body.linearVelocity = velocity;
            shotGravityTimeRemaining = Mathf.Max(0f, shotGravityTimeRemaining - deltaTime);
        }

        public void ConfigureWeaponMode(WeaponMode mode)
        {
            weaponMode = mode;
            revolverChamber = 0;
        }

        public void ApplyFireRateMultiplier(float multiplier)
        {
            fireCooldownMultiplier = Mathf.Clamp(fireCooldownMultiplier * multiplier, 0.35f, 1f);
        }

        public void ApplyRecoilMultiplier(float multiplier)
        {
            recoilMultiplier = Mathf.Clamp(recoilMultiplier * multiplier, 0.65f, 2.4f);
        }

        public void ApplyStabilizer(float multiplier)
        {
            stabilizerMultiplier = Mathf.Clamp(stabilizerMultiplier * multiplier, 0.35f, 1f);
            Body.angularDamping = Mathf.Min(2.2f, Body.angularDamping + 0.35f);
        }

        public void AddSpinKick(float amount)
        {
            spinKick = Mathf.Min(0.8f, spinKick + amount);
        }

        public void AddProjectile()
        {
            projectileCount = Mathf.Min(3, projectileCount + 1);
        }

        public void SetPlayerProgressionTier(int tier)
        {
            UpgradeTier = Mathf.Max(0, tier);
            PlayerChassisId id = (PlayerChassisId)Mathf.Clamp(tier, 0, 5);
            SetGeneratedArt(RecoilDuelArtLibrary.GetPlayerChassis(id));
        }

        public void SetEnemyChassis(EnemyArchetypeId id)
        {
            SetGeneratedArt(RecoilDuelArtLibrary.GetEnemyChassis(id));
        }

        public void ShowAttachment(AttachmentArtId id)
        {
            Sprite sprite = RecoilDuelArtLibrary.GetAttachment(id);
            if (sprite == null || transform.Find("Attachment " + id) != null)
            {
                return;
            }

            GameObject attachment = new GameObject("Attachment " + id);
            attachment.transform.SetParent(transform);
            attachment.transform.localPosition = new Vector3(0.05f, 0.14f, 0f);
            SpriteRenderer renderer = attachment.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.sortingOrder = 7;
            float scale = 0.65f / Mathf.Max(0.01f, sprite.bounds.size.x);
            attachment.transform.localScale = new Vector3(scale, scale, 1f);
        }

        private void SetGeneratedArt(Sprite sprite)
        {
            if (renderers == null || renderers.Length < 4)
            {
                return;
            }

            bool hasArt = sprite != null;
            for (int i = 0; i < 3; i++)
            {
                renderers[i].gameObject.SetActive(!hasArt);
            }

            SpriteRenderer generated = renderers[3];
            generated.enabled = hasArt;
            if (!hasArt)
            {
                return;
            }

            generated.sprite = sprite;
            generated.color = Color.white;
            float scale = 1.45f / Mathf.Max(0.01f, sprite.bounds.size.x);
            generated.transform.localScale = new Vector3(scale, scale, 1f);
            Muzzle.localPosition = new Vector3(0.72f, 0f, 0f);
        }

        public void ApplyEnemyWaveScaling(
            int wave,
            float baseHealth,
            float maximumHealth,
            float healthIncrease,
            int healthIncreaseEveryWaves,
            float fireDelayReductionPerWave,
            float minimumFireDelayMultiplier,
            float recoilIncreasePerWave,
            float archetypeHealthMultiplier,
            int shieldHits)
        {
            if (Team != TeamId.Enemy)
            {
                return;
            }

            int completedWaveSteps = Mathf.Max(0, wave - 1) / Mathf.Max(1, healthIncreaseEveryWaves);
            fireCooldownMultiplier = Mathf.Clamp(
                1f - Mathf.Max(0, wave - 1) * fireDelayReductionPerWave,
                minimumFireDelayMultiplier,
                1f);
            recoilMultiplier = Mathf.Min(2.4f, 1f + Mathf.Max(0, wave - 1) * recoilIncreasePerWave);
            float health = (baseHealth + completedWaveSteps * healthIncrease) * archetypeHealthMultiplier;
            Health.ResetHealth(TeamId.Enemy, Mathf.Min(maximumHealth, health));
            Health.AddShield(shieldHits);
        }

        public void ActivateEnemy(float delay)
        {
            EnemyBrain brain = enemyBrain;
            if (brain != null)
            {
                brain.SetActiveBrain(false);
                StartCoroutine(EnableEnemyAfterDelay(brain, delay));
            }
        }

        public void DeactivateForPool()
        {
            StopAllCoroutines();
            shotGravityTimeRemaining = 0f;
            EnemyBrain brain = enemyBrain;
            if (brain != null)
            {
                brain.SetActiveBrain(false);
            }

            FallingEnemy falling = fallingEnemy;
            if (falling != null)
            {
                falling.CancelDrop();
            }

            gameObject.SetActive(false);
        }

        private IEnumerator EnableEnemyAfterDelay(EnemyBrain brain, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (brain != null && gameObject.activeSelf)
            {
                brain.EnableBrain();
            }
        }
    }
}
