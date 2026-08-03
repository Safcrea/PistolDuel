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
        private bool WasPrimaryPressed()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return false;
            }

            bool mousePressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            bool touchPressed = Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
            bool keyboardRestart = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
            return mousePressed || touchPressed || keyboardRestart;
        }

        private void FeedbackShot(PistolController source)
        {
            SpawnBurst(source.Muzzle.position, source.Team == TeamId.Player ? new Color(0.4f, 0.85f, 1f) : new Color(1f, 0.3f, 0.16f), 5);
            ShakeCamera(source.Team == TeamId.Player ? 0.055f : 0.035f);

            if (source.Team == TeamId.Player)
            {
                PlayTone(680f, 0.035f);
                Vibrate(0.08f);
            }
        }

        private void SpawnBurst(Vector3 position, Color color, int count)
        {
            feedbackSystem.SpawnBurst(position, color, count);
        }

        private void ShowFloatingLabel(string text, Vector3 worldPosition, Color color)
        {
            feedbackSystem.ShowFloatingLabel(text, worldPosition, color);
        }

        private void ShakeCamera(float amount)
        {
            StartCoroutine(CameraShake(amount, 0.12f));
        }

        private IEnumerator CameraShake(float amount, float duration)
        {
            yield return feedbackSystem.CameraShake(amount, duration);
        }

        private void PlayTone(float frequency, float duration)
        {
            feedbackSystem.PlayTone(transform, frequency, duration);
        }

        private void Vibrate(float cooldown)
        {
            if (Time.unscaledTime - lastHapticTime < cooldown)
            {
                return;
            }

            lastHapticTime = Time.unscaledTime;
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }

        private void UpdateUi()
        {
            scoreText.text = "Score " + score;
            statusText.text = "WAVE " + waveIndex + "  KILLS " + totalEnemyKills + "  MK " + (player != null ? player.UpgradeTier : 0);
            if (player != null)
            {
                string shield = player.Health.ShieldHits > 0 ? " +" + player.Health.ShieldHits + " SH" : string.Empty;
                hpText.text = infiniteHealth
                    ? "HP INF" + shield
                    : "HP " + Mathf.Max(0f, player.Health.CurrentHealth).ToString("0.#") + "/" + player.Health.MaxHealth.ToString("0.#") + shield;
            }
        }
    }
}
