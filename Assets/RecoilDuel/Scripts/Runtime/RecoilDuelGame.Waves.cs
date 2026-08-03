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
        private IEnumerator StartRun()
        {
            ResetTimeEffects();
            state = RunState.Countdown;
            ClearRuntimeObjects();
            waveIndex = 0;
            score = 0;
            friendlyFireKills = 0;
            ricochetKills = 0;
            totalEnemyKills = 0;
            lastMilestoneTier = 0;
            nextDebugUpgradeIndex = 0;
            activePowerups = 0;
            runTime = 0f;
            clearSequenceRunning = false;
            magnetPowerups = false;
            enemiesFrozen = false;
            enemiesConfused = false;
            freezeRoutine = null;
            confusionRoutine = null;
            nextPowerupTime = firstPowerupDelay;
            ResetPlayerProgressionData();
            UpdateDebugButtonLabels();

            CreatePlayer();

            string[] beats = { "3", "2", "1", "DUEL" };
            for (int i = 0; i < beats.Length; i++)
            {
                centerText.text = beats[i];
                centerText.enabled = true;
                yield return new WaitForSeconds(i == beats.Length - 1 ? 0.55f : 0.45f);
            }

            centerText.enabled = false;
            SpawnEnemyWave(initialEnemyCount, true);
            state = RunState.Combat;
        }

        private void RestartRun()
        {
            StopAllCoroutines();
            ResetTimeEffects();
            StartCoroutine(StartRun());
        }

        private IEnumerator GameOver()
        {
            state = RunState.RunOver;
            activeAimLocks.Clear();
            currentAimLocks.Clear();
            aimLockPulseEndsAtUnscaled = 0f;
            gameOverSlowMotionActive = true;
            RefreshTimeScale(Time.unscaledTime);
            centerText.enabled = true;
            centerText.text = "RUN OVER\nTAP TO RESTART";
            yield return new WaitForSecondsRealtime(0.6f);
            gameOverSlowMotionActive = false;
            RefreshTimeScale(Time.unscaledTime);
        }

        private IEnumerator ClearAndDropNextWave()
        {
            clearSequenceRunning = true;
            state = RunState.ArenaClear;
            ClearHostileBullets();
            centerText.enabled = true;
            centerText.text = "ARENA CLEAR";
            score += 200 + waveIndex * 25;
            ShakeCamera(0.12f);
            yield return new WaitForSeconds(arenaClearDuration);

            int nextCount = GetEnemyCountForWave(waveIndex + 1);
            yield return StartCoroutine(SpawnDroppingWave(nextCount));

            centerText.enabled = false;
            clearSequenceRunning = false;
            state = RunState.Combat;

            // Player ricochets can clear a whole incoming group before it activates.
            if (CountActiveEnemies() == 0)
            {
                StartCoroutine(ClearAndDropNextWave());
            }
        }

        private IEnumerator SpawnDroppingWave(int count)
        {
            state = RunState.EnemyDropWarning;
            waveIndex++;
            float[] lanes = PickDropLanes(count);

            for (int i = 0; i < count; i++)
            {
                GameObject warning = GetDropWarning(i);
                warning.transform.position = new Vector3(lanes[i], ArenaHalfHeight - 0.35f, 0f);
                warning.SetActive(true);
            }

            centerText.text = "WARNING";
            yield return new WaitForSeconds(dropWarningDuration);

            state = RunState.EnemyDropping;
            for (int i = 0; i < count; i++)
            {
                PistolController enemy = SpawnEnemy(new Vector2(lanes[i], ArenaHalfHeight + 1f), false, i);
                FallingEnemy falling = enemy.FallingEnemy;
                if (falling == null)
                {
                    falling = enemy.gameObject.AddComponent<FallingEnemy>();
                    enemy.CacheFallingEnemy(falling);
                }

                falling.Begin(
                    enemy,
                    ArenaHalfHeight - 1.15f - i * 0.32f,
                    enemyDropSpeed,
                    enemyActivationDelay + i * enemyActivationStagger);
                yield return new WaitForSeconds(0.045f);
            }

            for (int i = 0; i < dropWarnings.Count; i++)
            {
                dropWarnings[i].SetActive(false);
            }

            float deploymentTimeout = 0f;
            while (CountActivatingEnemies() > 0 && deploymentTimeout < 3f)
            {
                deploymentTimeout += Time.deltaTime;
                yield return null;
            }

            if (CountActivatingEnemies() > 0)
            {
                ForceActivateDroppingEnemies();
            }
            state = RunState.EnemyActivation;
            yield return null;
        }

        private IEnumerator DropPowerup(UpgradeData upgrade)
        {
            if (upgrade == null)
            {
                yield break;
            }

            activePowerups++;
            state = RunState.PowerupDropping;
            float lane = PickSafeLane();
            GameObject warning = GetDropWarning(0);
            warning.transform.position = new Vector3(lane, ArenaHalfHeight - 0.35f, 0f);
            warning.GetComponent<SpriteRenderer>().color = new Color(1f, 0.78f, 0.16f, 0.8f);
            warning.SetActive(true);

            yield return new WaitForSeconds(0.55f);

            GameObject powerup = new GameObject("Major Powerup - " + upgrade.displayName);
            powerup.transform.SetParent(dynamicRoot);
            powerup.transform.position = new Vector3(lane, ArenaHalfHeight + 0.9f, 0f);
            PowerupPickup pickup = powerup.AddComponent<PowerupPickup>();
            pickup.Initialize(this, squareSprite, upgrade);
            warning.SetActive(false);
            warning.GetComponent<SpriteRenderer>().color = new Color(1f, 0.1f, 0.05f, 0.65f);

            if (state == RunState.PowerupDropping)
            {
                state = RunState.Combat;
            }
        }

    }
}

