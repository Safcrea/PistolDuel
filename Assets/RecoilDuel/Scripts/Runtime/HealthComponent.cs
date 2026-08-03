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
    public sealed class HealthComponent : MonoBehaviour
    {
        private RecoilDuelGame game;
        private SpriteRenderer[] renderers;
        private Color[] originalColors;
        private GameObject shieldVisual;
        private float invulnerableUntil;
        private Coroutine flashRoutine;

        public TeamId Team { get; private set; }
        public float CurrentHealth { get; private set; }
        public float MaxHealth { get; private set; }
        public bool IsAlive => CurrentHealth > 0f;
        public int LastHitRicochetCount { get; private set; }
        public int ShieldHits { get; private set; }

        public void Initialize(RecoilDuelGame owner, TeamId team, float maxHealth, SpriteRenderer[] spriteRenderers)
        {
            game = owner;
            renderers = spriteRenderers;
            originalColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                originalColors[i] = renderers[i].color;
            }

            ResetHealth(team, maxHealth);
        }

        public void SetShieldVisual(GameObject visual)
        {
            shieldVisual = visual;
            UpdateShieldVisual();
        }

        public void ResetHealth(TeamId team, float maxHealth)
        {
            Team = team;
            MaxHealth = maxHealth;
            CurrentHealth = maxHealth;
            ShieldHits = 0;
            LastHitRicochetCount = 0;
            invulnerableUntil = 0f;
            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    renderers[i].color = originalColors[i];
                }
            }

            UpdateShieldVisual();
        }

        public bool ApplyDamage(DamageInfo damage, int ricochetCount)
        {
            if (!IsAlive || Time.time < invulnerableUntil)
            {
                return false;
            }

            if (Team == TeamId.Player && game.InfiniteHealth)
            {
                StartDamageFlash();
                game.OnGunDamaged(this, damage);
                return true;
            }

            if (ShieldHits > 0)
            {
                ShieldHits--;
                invulnerableUntil = Time.time + 0.18f;
                UpdateShieldVisual();
                StartDamageFlash();
                game.OnGunDamaged(this, damage);
                return true;
            }

            CurrentHealth -= damage.Damage;
            LastHitRicochetCount = ricochetCount;
            invulnerableUntil = Team == TeamId.Player ? Time.time + game.PlayerInvulnerabilityDuration : 0f;

            StartDamageFlash();
            game.OnGunDamaged(this, damage);

            if (CurrentHealth <= 0f)
            {
                game.OnGunDestroyed(this, damage);
            }

            return true;
        }

        public bool Repair(float amount)
        {
            if (CurrentHealth >= MaxHealth)
            {
                return false;
            }

            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
            return true;
        }

        public void AddShield(int hits)
        {
            ShieldHits = Mathf.Min(5, ShieldHits + Mathf.Max(0, hits));
            UpdateShieldVisual();
        }

        private void StartDamageFlash()
        {
            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
            }

            flashRoutine = StartCoroutine(FlashWhite());
        }

        private void UpdateShieldVisual()
        {
            if (shieldVisual != null)
            {
                shieldVisual.SetActive(ShieldHits > 0);
            }
        }

        private IEnumerator FlashWhite()
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].color = Color.white;
            }

            yield return new WaitForSecondsRealtime(0.055f);

            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].color = originalColors[i];
            }
        }
    }
}

