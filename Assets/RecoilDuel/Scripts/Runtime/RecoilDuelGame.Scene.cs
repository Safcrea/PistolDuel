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
        private void CreateSprites()
        {
            squareSprite = CreateSprite("RecoilDuelSquare", 32, (texture, x, y) => Color.white);
            circleSprite = CreateSprite("RecoilDuelCircle", 32, (texture, x, y) =>
            {
                float dx = x - texture.width * 0.5f;
                float dy = y - texture.height * 0.5f;
                return dx * dx + dy * dy <= 14f * 14f ? Color.white : Color.clear;
            });
            triangleSprite = CreateSprite("RecoilDuelTriangle", 32, (texture, x, y) =>
            {
                float normalizedX = Mathf.Abs((x / 31f) - 0.5f);
                float normalizedY = y / 31f;
                return normalizedX < normalizedY * 0.45f + 0.08f ? Color.white : Color.clear;
            });
        }

        private static Sprite CreateSprite(string name, int size, Func<Texture2D, int, int, Color> colorFunc)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = name;
            texture.filterMode = FilterMode.Point;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    texture.SetPixel(x, y, colorFunc(texture, x, y));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 32f);
        }

        private void CreateRoots()
        {
            dynamicRoot = new GameObject("Runtime Objects").transform;
            dynamicRoot.SetParent(transform);
            bulletRoot = new GameObject("Bullet Pool").transform;
            bulletRoot.SetParent(transform);
            enemyRoot = new GameObject("Enemy Pool").transform;
            enemyRoot.SetParent(transform);
            vfxRoot = new GameObject("VFX Pool").transform;
            vfxRoot.SetParent(transform);
            bounceMaterial = new PhysicsMaterial2D("Recoil Duel Bounce") { bounciness = 1f, friction = 0f };
        }

        private void SetupCamera()
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                mainCamera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
                cameraObject.AddComponent<AudioListener>();
            }

            mainCamera.transform.position = new Vector3(0f, 0f, -10f);
            mainCamera.orthographic = true;
            mainCamera.orthographicSize = ArenaHalfHeight + 0.45f;
            mainCamera.backgroundColor = new Color(0.045f, 0.055f, 0.075f);
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
        }

        private void SetupAudio()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.volume = 0.35f;
        }

        private void SetupArena()
        {
            GameObject arena = new GameObject("Portrait Arena");
            arena.transform.SetParent(transform);
            Sprite arenaArt = RecoilDuelArtLibrary.GetArenaBackground();

            CreateWall(arena.transform, "Left Wall", new Vector2(-ArenaHalfWidth - 0.12f, 0f), new Vector2(0.24f, ArenaHalfHeight * 2.05f), arenaArt == null);
            CreateWall(arena.transform, "Right Wall", new Vector2(ArenaHalfWidth + 0.12f, 0f), new Vector2(0.24f, ArenaHalfHeight * 2.05f), arenaArt == null);
            CreateWall(arena.transform, "Top Wall", new Vector2(0f, ArenaHalfHeight + 0.12f), new Vector2(ArenaHalfWidth * 2.25f, 0.24f), arenaArt == null);
            CreateWall(arena.transform, "Bottom Wall", new Vector2(0f, -ArenaHalfHeight - 0.12f), new Vector2(ArenaHalfWidth * 2.25f, 0.24f), arenaArt == null);

            GameObject floor = new GameObject("Dark Arena Floor");
            floor.transform.SetParent(arena.transform);
            floor.transform.localPosition = Vector3.zero;
            SpriteRenderer floorRenderer = floor.AddComponent<SpriteRenderer>();
            floorRenderer.sprite = arenaArt != null ? arenaArt : squareSprite;
            floorRenderer.color = arenaArt != null ? Color.white : new Color(0.08f, 0.095f, 0.12f);
            floorRenderer.sortingOrder = -10;
            if (arenaArt != null)
            {
                float scaleX = ArenaHalfWidth * 2f / Mathf.Max(0.01f, arenaArt.bounds.size.x);
                float scaleY = ArenaHalfHeight * 2f / Mathf.Max(0.01f, arenaArt.bounds.size.y);
                floor.transform.localScale = new Vector3(scaleX, scaleY, 1f);
            }
            else
            {
                floor.transform.localScale = new Vector3(ArenaHalfWidth * 2f, ArenaHalfHeight * 2f, 1f);
            }
        }

        private void CreateWall(Transform parent, string name, Vector2 position, Vector2 size, bool showVisual)
        {
            GameObject wall = new GameObject(name);
            wall.transform.SetParent(parent);
            wall.transform.position = position;
            wall.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = wall.AddComponent<SpriteRenderer>();
            renderer.sprite = squareSprite;
            renderer.color = new Color(0.18f, 0.2f, 0.25f);
            renderer.sortingOrder = -5;
            renderer.enabled = showVisual;
            BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
            collider.sharedMaterial = bounceMaterial;
        }

    }
}

