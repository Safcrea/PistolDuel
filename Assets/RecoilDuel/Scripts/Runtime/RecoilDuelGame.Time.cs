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
        private float GetBulletVisualSize(TeamId team)
        {
            return team == TeamId.Player ? playerBulletVisualSize : enemyBulletVisualSize;
        }

        private float GetBulletHitboxRadius(TeamId team)
        {
            return team == TeamId.Player ? playerBulletHitboxRadius : enemyBulletHitboxRadius;
        }

        private void EvaluateAimLocks()
        {
            currentAimLocks.Clear();
            if (!enableAimLockSlowMotion)
            {
                activeAimLocks.Clear();
                return;
            }

            if (state == RunState.PlayerHitStop)
            {
                return;
            }

            if (!IsCombatActive)
            {
                activeAimLocks.Clear();
                return;
            }

            EvaluateAimLock(player);
            for (int i = 0; i < enemyPool.Count; i++)
            {
                EvaluateAimLock(enemyPool[i]);
            }

            bool enteredNewLock = false;
            foreach (PistolController source in currentAimLocks)
            {
                if (!activeAimLocks.Contains(source))
                {
                    enteredNewLock = true;
                    break;
                }
            }

            activeAimLocks.Clear();
            activeAimLocks.UnionWith(currentAimLocks);
            if (enteredNewLock)
            {
                RequestAimLockPulse();
            }
        }

        private void EvaluateAimLock(PistolController source)
        {
            if (IsAimLockEligible(source) && HasDirectAimLock(source))
            {
                currentAimLocks.Add(source);
            }
        }

        private bool IsAimLockEligible(PistolController pistol)
        {
            if (pistol == null || !pistol.gameObject.activeInHierarchy || !pistol.IsAlive)
            {
                return false;
            }

            if (pistol.Team == TeamId.Player)
            {
                return pistol == player;
            }

            EnemyBrain brain = pistol.EnemyBrain;
            return pistol.Team == TeamId.Enemy && brain != null && brain.IsActiveBrain;
        }

        private bool HasDirectAimLock(PistolController source)
        {
            Vector2 direction = source.MuzzleRight.normalized;
            if (direction.sqrMagnitude < 0.99f)
            {
                return false;
            }

            int hitCount = CastAimLock(source.Muzzle.position, GetBulletHitboxRadius(source.Team), direction);

            Collider2D nearestSolid = null;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D candidate = aimLockCastHits[i].collider;
                if (candidate == null || candidate.isTrigger)
                {
                    continue;
                }

                if (candidate.GetComponentInParent<PistolController>() == source
                    || candidate.GetComponentInParent<BulletController>() != null
                    || candidate.GetComponentInParent<PowerupPickup>() != null)
                {
                    continue;
                }

                if (aimLockCastHits[i].distance < nearestDistance)
                {
                    nearestDistance = aimLockCastHits[i].distance;
                    nearestSolid = candidate;
                }
            }

            if (nearestSolid == null)
            {
                return false;
            }

            PistolController target = nearestSolid.GetComponentInParent<PistolController>();
            return IsAimLockEligible(target) && AimLockRules.IsOpposingCombatTeam(source.Team, target.Team);
        }

        private int CastAimLock(Vector2 origin, float radius, Vector2 direction)
        {
            while (true)
            {
                int hitCount = Physics2D.CircleCast(
                    origin,
                    radius,
                    direction,
                    aimLockContactFilter,
                    aimLockCastHits,
                    AimLockCastDistance);
                if (hitCount < aimLockCastHits.Length)
                {
                    return hitCount;
                }

                Array.Resize(ref aimLockCastHits, aimLockCastHits.Length * 2);
            }
        }

        private void RequestAimLockPulse()
        {
            float now = Time.unscaledTime;
            if (now < nextAimLockPulseAllowedAtUnscaled)
            {
                return;
            }

            aimLockPulseEndsAtUnscaled = now + aimLockDuration;
            nextAimLockPulseAllowedAtUnscaled = aimLockPulseEndsAtUnscaled + aimLockCooldown;
            RefreshTimeScale(now);
        }

        private void RequestHitStop(float duration)
        {
            if (state == RunState.RunOver)
            {
                return;
            }

            if (state != RunState.PlayerHitStop)
            {
                stateBeforeHitStop = state;
                state = RunState.PlayerHitStop;
            }

            hitStopEndsAtUnscaled = Mathf.Max(hitStopEndsAtUnscaled, Time.unscaledTime + duration);
            RefreshTimeScale(Time.unscaledTime);
        }

        private void UpdateTimeEffects()
        {
            float now = Time.unscaledTime;
            if (state == RunState.PlayerHitStop && now >= hitStopEndsAtUnscaled)
            {
                state = stateBeforeHitStop == RunState.PlayerHitStop ? RunState.Combat : stateBeforeHitStop;
                hitStopEndsAtUnscaled = 0f;
            }

            if (!enableAimLockSlowMotion)
            {
                aimLockPulseEndsAtUnscaled = 0f;
            }

            RefreshTimeScale(now);
        }

        private void RefreshTimeScale(float now)
        {
            float targetTimeScale = 1f;
            if (now < hitStopEndsAtUnscaled)
            {
                targetTimeScale = HitStopTimeScale;
            }
            else if (gameOverSlowMotionActive)
            {
                targetTimeScale = GameOverTimeScale;
            }
            else if (enableAimLockSlowMotion && now < aimLockPulseEndsAtUnscaled)
            {
                targetTimeScale = aimLockTimeScale;
            }

            if (!Mathf.Approximately(Time.timeScale, targetTimeScale))
            {
                Time.timeScale = targetTimeScale;
            }
        }

        private void ResetTimeEffects()
        {
            activeAimLocks.Clear();
            currentAimLocks.Clear();
            aimLockPulseEndsAtUnscaled = 0f;
            nextAimLockPulseAllowedAtUnscaled = 0f;
            hitStopEndsAtUnscaled = 0f;
            gameOverSlowMotionActive = false;
            Time.timeScale = 1f;
        }

    }
}

