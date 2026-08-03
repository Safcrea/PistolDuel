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
    public sealed class PowerupPickup : MonoBehaviour
    {
        private RecoilDuelGame game;
        private Rigidbody2D body;
        private UpgradeData upgrade;
        private bool collected;
        private bool initialized;

        public void Initialize(RecoilDuelGame owner, Sprite sprite, UpgradeData upgrade)
        {
            game = owner;
            this.upgrade = upgrade;
            initialized = true;
            body = gameObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.linearVelocity = Vector2.down * 2.2f;
            body.angularVelocity = 90f;
            CircleCollider2D collider = gameObject.AddComponent<CircleCollider2D>();
            collider.radius = 0.32f;
            collider.isTrigger = true;
            SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
            Sprite generatedIcon = upgrade.icon != null ? upgrade.icon : RecoilDuelArtLibrary.GetPowerup(upgrade.powerupId);
            renderer.sprite = generatedIcon != null ? generatedIcon : sprite;
            renderer.color = generatedIcon != null ? Color.white : GetUpgradeColor(upgrade.effectType);
            renderer.sortingOrder = 9;
            float visualWidth = generatedIcon != null ? generatedIcon.bounds.size.x : 1f;
            float scale = 0.68f / Mathf.Max(0.01f, visualWidth);
            transform.localScale = new Vector3(scale, scale, 1f);
            Destroy(gameObject, 28f);
        }

        private void Update()
        {
            if (game.MagnetPowerups && game.PlayerPistol != null)
            {
                Vector2 toPlayer = game.PlayerPistol.transform.position - transform.position;
                if (toPlayer.sqrMagnitude < 25f)
                {
                    body.linearVelocity = Vector2.Lerp(body.linearVelocity, toPlayer.normalized * 4.5f, Time.deltaTime * 4f);
                    return;
                }
            }

            if (transform.position.y <= -4.8f)
            {
                body.linearVelocity = Vector2.zero;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<PistolController>()?.Team == TeamId.Player)
            {
                Collect();
            }
        }

        public void Collect()
        {
            if (collected)
            {
                return;
            }

            collected = true;
            game.ApplyUpgrade(upgrade);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (initialized && game != null)
            {
                initialized = false;
                game.OnPowerupRemoved();
            }
        }

        private static Color GetUpgradeColor(UpgradeEffectType effectType)
        {
            switch (effectType)
            {
                case UpgradeEffectType.Damage:
                case UpgradeEffectType.Recoil:
                    return new Color(1f, 0.35f, 0.18f);
                case UpgradeEffectType.Repair:
                case UpgradeEffectType.Shield:
                    return new Color(0.15f, 1f, 0.62f);
                case UpgradeEffectType.Ricochet:
                case UpgradeEffectType.Penetration:
                    return new Color(0.2f, 0.85f, 1f);
                default:
                    return new Color(1f, 0.76f, 0.15f);
            }
        }
    }
}

