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
    public sealed class BulletController : MonoBehaviour
    {
        private Rigidbody2D body;
        private CircleCollider2D circleCollider;
        private SpriteRenderer spriteRenderer;
        private Transform visualTransform;
        private Sprite fallbackSprite;
        private RecoilDuelGame game;
        private BulletData data;
        private GameObject owner;
        private float spawnTime;
        private float expiresAt;
        private int bouncesRemaining;
        private int ricochetCount;
        private int penetrationsRemaining;
        private float damageMultiplier;
        private bool canSplit;
        private Collider2D ignoredCollider;

        public TeamId OwnerTeam { get; private set; }
        public GameObject SourceOwner => owner;
        public BulletData Data => data;

        public void Build(Sprite sprite, PhysicsMaterial2D bounceMaterial)
        {
            body = gameObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            circleCollider = gameObject.AddComponent<CircleCollider2D>();
            circleCollider.sharedMaterial = bounceMaterial;
            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(transform, false);
            visualTransform = visual.transform;
            spriteRenderer = visual.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            fallbackSprite = sprite;
            spriteRenderer.sortingOrder = 10;
            transform.localScale = Vector3.one;
        }

        public void Launch(
            Vector3 position,
            Vector2 direction,
            TeamId team,
            GameObject source,
            BulletData bulletData,
            Color color,
            RecoilDuelGame ownerGame,
            float visualSize,
            float hitboxRadius,
            bool allowSplit = true,
            float launchDamageMultiplier = 1f)
        {
            StopAllCoroutines();
            RestoreIgnoredCollision();
            game = ownerGame;
            data = bulletData;
            owner = source;
            OwnerTeam = team;
            spawnTime = Time.time;
            expiresAt = Time.time + bulletData.lifetime;
            bouncesRemaining = bulletData.maxRicochets;
            penetrationsRemaining = bulletData.penetration;
            ricochetCount = 0;
            damageMultiplier = launchDamageMultiplier;
            canSplit = allowSplit;
            transform.position = position;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            float resolvedVisualSize = Mathf.Max(0.01f, visualSize) * Mathf.Max(0.01f, bulletData.visualScale);
            visualTransform.localScale = new Vector3(resolvedVisualSize, resolvedVisualSize, 1f);
            circleCollider.radius = Mathf.Max(0.001f, hitboxRadius);
            spriteRenderer.color = color;
            Sprite artSprite = RecoilDuelArtLibrary.GetBullet(bulletData.artId);
            if (artSprite != null)
            {
                spriteRenderer.sprite = artSprite;
                spriteRenderer.color = Color.white;
            }
            else
            {
                spriteRenderer.sprite = fallbackSprite;
            }
            gameObject.SetActive(true);
            Vector2 launchVelocity = direction.normalized * bulletData.speed;
            body.linearVelocity = launchVelocity;
            body.angularVelocity = 0f;
            UpdateVisualHeading(launchVelocity);
        }

        private void Update()
        {
            if (Time.time >= expiresAt)
            {
                game.OnBulletExpired(this);
            }
        }

        private void LateUpdate()
        {
            UpdateVisualHeading(body.linearVelocity);
        }

        private void UpdateVisualHeading(Vector2 velocity)
        {
            if (velocity.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
            visualTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            HealthComponent health = collision.collider.GetComponentInParent<HealthComponent>();
            if (health != null)
            {
                TryDamage(health, collision.GetContact(0).point, collision.collider);
                return;
            }

            if (canSplit && ricochetCount == 0 && data.splitOnFirstRicochet)
            {
                Vector2 reflected = Vector2.Reflect(body.linearVelocity.normalized, collision.GetContact(0).normal);
                game.SpawnSplitRicochets(this, reflected);
                canSplit = false;
            }

            ricochetCount++;
            game.AwardRicochet(this);
            if (ProjectileRules.ConsumeWallBounce(ref bouncesRemaining))
            {
                game.OnBulletExpired(this);
                return;
            }

            body.linearVelocity *= data.bounceRetention;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PowerupPickup pickup = other.GetComponentInParent<PowerupPickup>();
            if (pickup != null && OwnerTeam == TeamId.Player)
            {
                pickup.Collect();
                game.OnBulletExpired(this);
            }
        }

        private void TryDamage(HealthComponent health, Vector2 hitPoint, Collider2D hitCollider)
        {
            if (health.gameObject == owner && Time.time - spawnTime < data.ownerImmunityDuration)
            {
                return;
            }

            if (OwnerTeam == TeamId.Player && health.Team == TeamId.Player)
            {
                return;
            }

            Vector2 travelVelocity = body.linearVelocity;
            DamageInfo damage = new DamageInfo(data.damage * damageMultiplier, OwnerTeam, owner, hitPoint, travelVelocity.normalized);
            bool damaged = health.ApplyDamage(damage, ricochetCount);
            if (damaged)
            {
                if (data.explosive)
                {
                    game.ApplyExplosion(hitPoint, this, health);
                }

                if (data.shockForce > 0f)
                {
                    game.ApplyShockPulse(hitPoint, data.shockForce);
                }

                if (penetrationsRemaining > 0 && health.Team == TeamId.Enemy)
                {
                    penetrationsRemaining--;
                    StartCoroutine(PassThrough(hitCollider, travelVelocity));
                }
                else
                {
                    game.OnBulletExpired(this);
                }
            }
        }

        private IEnumerator PassThrough(Collider2D hitCollider, Vector2 travelVelocity)
        {
            ignoredCollider = hitCollider;
            Physics2D.IgnoreCollision(circleCollider, hitCollider, true);
            transform.position += (Vector3)(travelVelocity.normalized * 0.18f);
            yield return new WaitForFixedUpdate();
            body.linearVelocity = travelVelocity * 0.82f;
            yield return new WaitForSeconds(0.08f);
            if (circleCollider != null && hitCollider != null)
            {
                Physics2D.IgnoreCollision(circleCollider, hitCollider, false);
            }
            ignoredCollider = null;
        }

        private void OnDisable()
        {
            RestoreIgnoredCollision();
        }

        private void RestoreIgnoredCollision()
        {
            if (circleCollider != null && ignoredCollider != null)
            {
                Physics2D.IgnoreCollision(circleCollider, ignoredCollider, false);
            }

            ignoredCollider = null;
        }

        public void FadeAndDisable()
        {
            game.OnBulletExpired(this);
        }
    }
}

