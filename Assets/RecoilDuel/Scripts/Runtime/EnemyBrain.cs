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
    public sealed class EnemyBrain : MonoBehaviour
    {
        private PistolController pistol;
        private RecoilDuelGame game;
        private EnemyArchetypeData archetype;
        private bool activeBrain;
        private float nextShotTime;
        private float fireDelayMultiplier = 1f;
        private bool firingSequence;
        private bool heavyShotNext;
        private LineRenderer telegraphLine;

        public bool IsActiveBrain => activeBrain;

        public void Initialize(PistolController owner, RecoilDuelGame ownerGame, EnemyArchetypeData data)
        {
            pistol = owner;
            game = ownerGame;
            archetype = data;
            firingSequence = false;
            heavyShotNext = false;
            nextShotTime = Time.time + UnityEngine.Random.Range(archetype.minFireDelay, archetype.maxFireDelay);
            EnsureTelegraphLine();
        }

        public void SetDifficulty(int wave, float reductionPerWave, float minimumMultiplier)
        {
            fireDelayMultiplier = Mathf.Clamp(
                1f - Mathf.Max(0, wave - 1) * reductionPerWave,
                minimumMultiplier,
                1f);
        }

        public void SetActiveBrain(bool active)
        {
            activeBrain = active;
            if (!active && telegraphLine != null)
            {
                telegraphLine.enabled = false;
            }
        }

        public void EnableBrain()
        {
            activeBrain = true;
            nextShotTime = Time.time + UnityEngine.Random.Range(0.035f, 0.1f) * fireDelayMultiplier;
        }

        private void FixedUpdate()
        {
            if (!activeBrain || game.EnemiesFrozen || !game.IsCombatActive || pistol == null || !pistol.IsAlive)
            {
                return;
            }

            PistolController player = game.PlayerPistol;
            if (player == null || !player.IsAlive)
            {
                return;
            }

            Vector2 toPlayer = player.transform.position - transform.position;
            float targetAngle = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg;
            if (game.EnemiesConfused)
            {
                targetAngle += Mathf.Sin(Time.unscaledTime * 7f + GetInstanceID() * 0.13f) * 85f;
            }
            float nextAngle = Mathf.MoveTowardsAngle(pistol.Body.rotation, targetAngle, 140f * Time.fixedDeltaTime);
            pistol.Body.MoveRotation(nextAngle);
        }

        private void Update()
        {
            if (!activeBrain || firingSequence || game.EnemiesFrozen || !game.IsCombatActive || Time.time < nextShotTime || !pistol.IsAlive)
            {
                return;
            }

            PistolController target = game.PlayerPistol;
            if (target == null || !target.IsAlive)
            {
                return;
            }

            Vector2 toPlayer = (target.transform.position - transform.position).normalized;
            float aimDot = Vector2.Dot(pistol.MuzzleRight, toPlayer);
            if (aimDot >= archetype.requiredAimDot || UnityEngine.Random.value < 0.25f)
            {
                nextShotTime = Time.time + UnityEngine.Random.Range(archetype.minFireDelay, archetype.maxFireDelay) * fireDelayMultiplier;
                StartCoroutine(FireSequence());
            }
        }

        private IEnumerator FireSequence()
        {
            firingSequence = true;
            if (archetype.weaponPattern == EnemyWeaponPattern.Sniper && archetype.telegraphDuration > 0f)
            {
                telegraphLine.enabled = true;
                float elapsed = 0f;
                while (elapsed < archetype.telegraphDuration && activeBrain && pistol.IsAlive)
                {
                    PistolController target = game.PlayerPistol;
                    telegraphLine.SetPosition(0, pistol.Muzzle.position);
                    telegraphLine.SetPosition(1, target != null ? target.transform.position : pistol.Muzzle.position + pistol.Muzzle.right * 10f);
                    elapsed += Time.deltaTime;
                    yield return null;
                }
                telegraphLine.enabled = false;
            }

            if (!activeBrain || !pistol.IsAlive || game.EnemiesFrozen)
            {
                firingSequence = false;
                yield break;
            }

            switch (archetype.weaponPattern)
            {
                case EnemyWeaponPattern.Burst:
                    for (int i = 0; i < archetype.burstCount; i++)
                    {
                        pistol.FirePatternVolley(1, 0f, 1f);
                        if (i < archetype.burstCount - 1)
                        {
                            yield return new WaitForSeconds(archetype.burstSpacing);
                        }
                    }
                    break;
                case EnemyWeaponPattern.Shotgun:
                    pistol.FirePatternVolley(archetype.pelletCount, archetype.spreadDegrees, 1f);
                    break;
                case EnemyWeaponPattern.AlternatingHeavy:
                    heavyShotNext = !heavyShotNext;
                    pistol.FirePatternVolley(1, 0f, heavyShotNext ? 1.25f : 0.8f);
                    break;
                default:
                    pistol.FirePatternVolley(1, 0f, 1f);
                    break;
            }

            firingSequence = false;
        }

        private void EnsureTelegraphLine()
        {
            telegraphLine = GetComponent<LineRenderer>();
            if (telegraphLine == null)
            {
                telegraphLine = gameObject.AddComponent<LineRenderer>();
                telegraphLine.positionCount = 2;
                telegraphLine.useWorldSpace = true;
                telegraphLine.startWidth = 0.025f;
                telegraphLine.endWidth = 0.008f;
                telegraphLine.material = new Material(Shader.Find("Sprites/Default"));
                telegraphLine.startColor = new Color(1f, 0.12f, 0.04f, 0.82f);
                telegraphLine.endColor = new Color(1f, 0.5f, 0.12f, 0.15f);
                telegraphLine.sortingOrder = 11;
            }

            telegraphLine.enabled = false;
        }
    }
}

