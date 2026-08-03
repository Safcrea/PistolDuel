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
    public sealed class FallingEnemy : MonoBehaviour
    {
        private PistolController pistol;
        private float landingY;
        private float fallSpeed;
        private float activationDelay;
        private bool landed;
        private SpriteRenderer[] renderers;
        private Color[] landingColors;

        public bool IsDropping { get; private set; }

        public void Begin(PistolController owner, float targetY, float speed, float delay)
        {
            StopAllCoroutines();
            pistol = owner;
            landingY = targetY;
            fallSpeed = speed;
            activationDelay = delay;
            landed = false;
            IsDropping = true;
            renderers = GetComponentsInChildren<SpriteRenderer>();
            landingColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                landingColors[i] = renderers[i].color;
            }

            pistol.Body.linearVelocity = Vector2.down * fallSpeed;
            pistol.Body.angularVelocity = UnityEngine.Random.Range(-140f, 140f);
        }

        public void CancelDrop()
        {
            StopAllCoroutines();
            IsDropping = false;
            landed = true;
        }

        public void ForceLand()
        {
            if (!IsDropping || pistol == null)
            {
                return;
            }

            transform.position = new Vector3(transform.position.x, landingY, transform.position.z);
            pistol.Body.linearVelocity = Vector2.zero;
            pistol.Body.angularVelocity = 0f;
            CancelDrop();
            EnemyBrain brain = pistol.EnemyBrain;
            if (brain != null)
            {
                brain.EnableBrain();
            }
        }

        private void Update()
        {
            if (!IsDropping || landed || pistol == null)
            {
                return;
            }

            if (transform.position.y <= landingY)
            {
                landed = true;
                pistol.Body.linearVelocity = Vector2.zero;
                pistol.Body.angularVelocity = 0f;
                StartCoroutine(ActivateAfterDelay());
            }
        }

        private IEnumerator ActivateAfterDelay()
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].color = Color.white;
            }

            yield return new WaitForSeconds(0.07f);

            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].color = landingColors[i];
            }

            yield return new WaitForSeconds(activationDelay);
            EnemyBrain brain = pistol.EnemyBrain;
            if (brain != null)
            {
                brain.EnableBrain();
            }

            IsDropping = false;
        }
    }
}

