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
        private void CreatePools()
        {
            projectilePool.Initialize(bulletRoot, circleSprite, bounceMaterial, 48);

            for (int i = 0; i < 8; i++)
            {
                PistolController enemy = CreatePistol("Enemy Gun", TeamId.Enemy, enemyArchetypes[0].gun, new Color(0.95f, 0.1f, 0.08f), enemyRoot);
                enemy.gameObject.SetActive(false);
                enemyPool.Add(enemy);
            }
        }

        private void CreatePlayer()
        {
            player = CreatePistol("Blue Player Gun", TeamId.Player, playerGunData, new Color(0.05f, 0.55f, 1f), dynamicRoot);
            player.transform.position = new Vector3(0f, -2.4f, 0f);
            player.transform.rotation = Quaternion.Euler(0f, 0f, 80f);
            player.Initialize(this, playerGunData, TeamId.Player, true);
            player.Health.AddShield(playerStartingShieldHits);
        }

        private PistolController CreatePistol(string name, TeamId team, GunData gunData, Color color, Transform parent)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent);
            root.transform.localScale = Vector3.one;

            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.mass = gunData.mass;
            body.linearDamping = gunData.linearDamping;
            body.angularDamping = gunData.angularDamping;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
            collider.radius = 0.34f;
            collider.sharedMaterial = bounceMaterial;

            HealthComponent health = root.AddComponent<HealthComponent>();
            PistolController pistol = root.AddComponent<PistolController>();

            GameObject bodySprite = CreateSpriteChild(root.transform, "Body", squareSprite, color, new Vector2(0f, 0f), new Vector2(0.66f, 0.28f), 4);
            GameObject barrelSprite = CreateSpriteChild(root.transform, "Barrel", squareSprite, color * 1.18f, new Vector2(0.38f, 0f), new Vector2(0.46f, 0.16f), 5);
            GameObject gripSprite = CreateSpriteChild(root.transform, "Grip", squareSprite, color * 0.75f, new Vector2(-0.18f, -0.28f), new Vector2(0.22f, 0.42f), 3);
            GameObject generatedArt = CreateSpriteChild(root.transform, "Generated Art", squareSprite, Color.white, Vector2.zero, Vector2.one, 6);
            SpriteRenderer generatedRenderer = generatedArt.GetComponent<SpriteRenderer>();
            Sprite initialArt = team == TeamId.Player
                ? RecoilDuelArtLibrary.GetPlayerChassis(PlayerChassisId.Standard)
                : RecoilDuelArtLibrary.GetEnemyChassis(EnemyArchetypeId.Standard);
            ConfigureGeneratedGunSprite(generatedRenderer, initialArt, bodySprite, barrelSprite, gripSprite);

            GameObject shieldVisual = CreateSpriteChild(root.transform, "Shield", circleSprite, new Color(0.1f, 0.82f, 1f, 0.24f), Vector2.zero, new Vector2(1.05f, 1.05f), 2);
            shieldVisual.SetActive(false);

            GameObject muzzle = new GameObject("MuzzlePoint");
            muzzle.transform.SetParent(root.transform);
            muzzle.transform.localPosition = new Vector3(0.68f, 0f, 0f);

            health.Initialize(this, team, gunData.maxHealth, new[]
            {
                bodySprite.GetComponent<SpriteRenderer>(),
                barrelSprite.GetComponent<SpriteRenderer>(),
                gripSprite.GetComponent<SpriteRenderer>(),
                generatedRenderer
            });
            health.SetShieldVisual(shieldVisual);
            pistol.SetReferences(body, health, muzzle.transform, new[]
            {
                bodySprite.GetComponent<SpriteRenderer>(),
                barrelSprite.GetComponent<SpriteRenderer>(),
                gripSprite.GetComponent<SpriteRenderer>(),
                generatedRenderer
            });
            pistol.Initialize(this, gunData, team, team == TeamId.Player);
            return pistol;
        }

        private static void ConfigureGeneratedGunSprite(SpriteRenderer generatedRenderer, Sprite sprite, params GameObject[] proceduralParts)
        {
            bool hasGeneratedArt = sprite != null;
            generatedRenderer.enabled = hasGeneratedArt;
            generatedRenderer.sprite = hasGeneratedArt ? sprite : generatedRenderer.sprite;
            generatedRenderer.color = Color.white;
            if (hasGeneratedArt)
            {
                float width = Mathf.Max(0.01f, sprite.bounds.size.x);
                float scale = 1.45f / width;
                generatedRenderer.transform.localScale = new Vector3(scale, scale, 1f);
            }

            for (int i = 0; i < proceduralParts.Length; i++)
            {
                proceduralParts[i].SetActive(!hasGeneratedArt);
            }
        }

        private GameObject CreateSpriteChild(Transform parent, string name, Sprite sprite, Color color, Vector2 localPosition, Vector2 localScale, int sortingOrder)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent);
            child.transform.localPosition = localPosition;
            child.transform.localScale = new Vector3(localScale.x, localScale.y, 1f);
            SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return child;
        }

        private void SpawnEnemyWave(int count, bool immediate)
        {
            waveIndex++;
            for (int i = 0; i < count; i++)
            {
                float x = Mathf.Lerp(-1.8f, 1.8f, count == 1 ? 0.5f : i / (float)(count - 1));
                float y = immediate ? 1.3f + i * 0.8f : ArenaHalfHeight + 1f;
                PistolController enemy = SpawnEnemy(new Vector2(x, y), immediate, i);
                if (immediate)
                {
                    enemy.transform.rotation = Quaternion.Euler(0f, 0f, -90f + i * 60f);
                    enemy.ActivateEnemy(0.8f + i * 0.3f);
                }
            }
        }

        private PistolController SpawnEnemy(Vector2 position, bool active, int spawnIndex)
        {
            EnemyArchetypeData selectedArchetype = SelectEnemyArchetype(waveIndex, spawnIndex);
            PistolController enemy = GetInactiveEnemy();
            enemy.transform.position = position;
            enemy.transform.rotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(210f, 330f));
            enemy.gameObject.SetActive(true);
            enemy.Initialize(this, selectedArchetype.gun, TeamId.Enemy, false);
            enemy.SetEnemyChassis(selectedArchetype.archetypeId);
            EnemyBrain brain = enemy.EnemyBrain;
            if (brain == null)
            {
                brain = enemy.gameObject.AddComponent<EnemyBrain>();
                enemy.CacheEnemyBrain(brain);
            }

            brain.Initialize(enemy, this, selectedArchetype);
            brain.SetDifficulty(waveIndex, enemyFireDelayReductionPerWave, enemyMinimumFireDelayMultiplier);
            brain.SetActiveBrain(active);
            enemy.ApplyEnemyWaveScaling(
                waveIndex,
                enemyBaseHealth,
                enemyMaximumHealth,
                enemyHealthIncrease,
                enemyHealthIncreaseEveryWaves,
                enemyFireDelayReductionPerWave,
                enemyMinimumFireDelayMultiplier,
                enemyRecoilIncreasePerWave,
                selectedArchetype.healthMultiplier,
                selectedArchetype.shieldHits);
            return enemy;
        }

        private EnemyArchetypeData SelectEnemyArchetype(int wave, int spawnIndex)
        {
            int unlocked = Mathf.Clamp(ProgressionRules.GetUnlockedEnemyArchetypeCount(wave), 1, enemyArchetypes.Count);
            if (spawnIndex == 0)
            {
                return enemyArchetypes[unlocked - 1];
            }

            int lowerBound = Mathf.Max(0, unlocked - 3);
            return enemyArchetypes[UnityEngine.Random.Range(lowerBound, unlocked)];
        }

        private void ForceActivateDroppingEnemies()
        {
            for (int i = 0; i < enemyPool.Count; i++)
            {
                if (!enemyPool[i].gameObject.activeSelf)
                {
                    continue;
                }

                FallingEnemy falling = enemyPool[i].FallingEnemy;
                if (falling != null && falling.IsDropping)
                {
                    falling.ForceLand();
                }
            }
        }

        private BulletController GetInactiveBullet()
        {
            return projectilePool.Rent(true);
        }

        private BulletController GetInactiveBulletWithoutRecycling()
        {
            return projectilePool.Rent(false);
        }

        private PistolController GetInactiveEnemy()
        {
            for (int i = 0; i < enemyPool.Count; i++)
            {
                if (!enemyPool[i].gameObject.activeSelf)
                {
                    return enemyPool[i];
                }
            }

            PistolController recycled = enemyPool[0];
            recycled.DeactivateForPool();
            return recycled;
        }

        private GameObject GetDropWarning(int index)
        {
            while (dropWarnings.Count <= index)
            {
                GameObject warning = new GameObject("Drop Warning");
                warning.transform.SetParent(dynamicRoot);
                warning.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
                SpriteRenderer renderer = warning.AddComponent<SpriteRenderer>();
                renderer.sprite = triangleSprite;
                renderer.color = new Color(1f, 0.1f, 0.05f, 0.65f);
                renderer.sortingOrder = 8;
                warning.SetActive(false);
                dropWarnings.Add(warning);
            }

            return dropWarnings[index];
        }

        private float[] PickDropLanes(int count)
        {
            float playerX = player != null ? player.transform.position.x : 0f;
            return wavePlanner.PickDropLanes(count, playerX);
        }

        private float PickSafeLane()
        {
            float[] lanes = PickDropLanes(1);
            return lanes[0];
        }

        private int CountActiveEnemies()
        {
            int count = 0;
            for (int i = 0; i < enemyPool.Count; i++)
            {
                if (enemyPool[i].gameObject.activeSelf && enemyPool[i].IsAlive)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountActivatingEnemies()
        {
            int count = 0;
            for (int i = 0; i < enemyPool.Count; i++)
            {
                FallingEnemy falling = enemyPool[i].FallingEnemy;
                if (enemyPool[i].gameObject.activeSelf && falling != null && falling.IsDropping)
                {
                    count++;
                }
            }

            return count;
        }

        private void ClearRuntimeObjects()
        {
            if (player != null)
            {
                Destroy(player.gameObject);
                player = null;
            }

            for (int i = 0; i < enemyPool.Count; i++)
            {
                enemyPool[i].DeactivateForPool();
            }

            projectilePool.DeactivateAll();

            for (int i = dynamicRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = dynamicRoot.GetChild(i);
                if (child.name.StartsWith("Major Powerup", StringComparison.Ordinal))
                {
                    Destroy(child.gameObject);
                }
            }
        }

        private void ClearHostileBullets()
        {
            projectilePool.DeactivateTeam(TeamId.Enemy);
        }

        private void ClampBody(Rigidbody2D body)
        {
            Vector2 position = body.position;
            position.x = Mathf.Clamp(position.x, -ArenaHalfWidth + 0.35f, ArenaHalfWidth - 0.35f);
            position.y = Mathf.Clamp(position.y, -ArenaHalfHeight + 0.35f, ArenaHalfHeight - 0.35f);
            body.position = position;
            body.linearVelocity = Vector2.ClampMagnitude(body.linearVelocity, 8f);
            body.angularVelocity = Mathf.Clamp(body.angularVelocity, -520f, 520f);
        }

    }
}
