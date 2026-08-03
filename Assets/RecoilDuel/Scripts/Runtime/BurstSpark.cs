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
    public sealed class BurstSpark : MonoBehaviour
    {
        private Vector2 velocity;
        private float lifetime;
        private float age;
        private SpriteRenderer spriteRenderer;

        public void Build(SpriteRenderer renderer)
        {
            spriteRenderer = renderer;
        }

        public void Begin(Color color, Vector2 startVelocity, float duration)
        {
            age = 0f;
            velocity = startVelocity;
            lifetime = duration;
            spriteRenderer.color = color;
        }

        private void Update()
        {
            age += Time.unscaledDeltaTime;
            transform.position += (Vector3)(velocity * Time.unscaledDeltaTime);
            if (spriteRenderer != null)
            {
                Color color = spriteRenderer.color;
                color.a = Mathf.Clamp01(1f - age / lifetime);
                spriteRenderer.color = color;
            }

            if (age >= lifetime)
            {
                gameObject.SetActive(false);
            }
        }
    }
}

