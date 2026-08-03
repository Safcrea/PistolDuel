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
        private void SetupUi()
        {
            GameObject canvasObject = new GameObject("Recoil Duel UI");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1080f, 1920f);
            canvasObject.AddComponent<GraphicRaycaster>();

            scoreText = CreateText("Score", new Vector2(32f, -30f), TextAnchor.UpperLeft, 42, Color.white);
            hpText = CreateText("HP", new Vector2(-32f, -30f), TextAnchor.UpperRight, 42, new Color(0.34f, 0.84f, 1f));
            statusText = CreateText("Status", new Vector2(0f, -30f), TextAnchor.UpperCenter, 32, new Color(0.86f, 0.9f, 1f));
            centerText = CreateText("Center", Vector2.zero, TextAnchor.MiddleCenter, 78, Color.white);
            centerText.enabled = false;

            if (showDebugButtons)
            {
                Button infiniteHealthButton = CreateButton("Infinite Health", new Vector2(32f, 36f), new Vector2(330f, 94f), TextAnchor.LowerLeft);
                infiniteHealthButtonText = infiniteHealthButton.GetComponentInChildren<Text>();
                infiniteHealthButton.onClick.AddListener(ToggleInfiniteHealth);

                Button powerupButton = CreateButton("Drop Powerup", new Vector2(-32f, 36f), new Vector2(410f, 94f), TextAnchor.LowerRight);
                powerupButtonText = powerupButton.GetComponentInChildren<Text>();
                powerupButton.onClick.AddListener(DropNextDebugPowerup);

                if (EventSystem.current == null)
                {
                    GameObject eventSystemObject = new GameObject("Recoil Duel Event System");
                    eventSystemObject.AddComponent<EventSystem>();
                    InputSystemUIInputModule inputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
                    inputModule.AssignDefaultActions();
                }

                UpdateDebugButtonLabels();
            }
        }

        private Button CreateButton(string name, Vector2 anchoredPosition, Vector2 size, TextAnchor anchor)
        {
            GameObject buttonObject = new GameObject(name + " Button");
            buttonObject.transform.SetParent(canvas.transform, false);
            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.08f, 0.12f, 0.17f, 0.94f);
            Button button = buttonObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.72f, 0.9f, 1f);
            colors.pressedColor = new Color(0.42f, 0.72f, 0.9f);
            button.colors = colors;

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            Vector2 anchorPoint = anchor == TextAnchor.LowerLeft ? Vector2.zero : new Vector2(1f, 0f);
            rect.anchorMin = anchorPoint;
            rect.anchorMax = anchorPoint;
            rect.pivot = anchorPoint;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            GameObject labelObject = new GameObject("Label");
            labelObject.transform.SetParent(buttonObject.transform, false);
            Text label = labelObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 26;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            RectTransform labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 6f);
            labelRect.offsetMax = new Vector2(-12f, -6f);
            return button;
        }

        private void ToggleInfiniteHealth()
        {
            infiniteHealth = !infiniteHealth;
            UpdateDebugButtonLabels();
            if (player != null)
            {
                ShowFloatingLabel(infiniteHealth ? "INFINITE HEALTH ON" : "INFINITE HEALTH OFF", player.transform.position + Vector3.up * 0.7f, Color.cyan);
            }
        }

        private void DropNextDebugPowerup()
        {
            if (!IsCombatActive || state == RunState.PowerupDropping || upgrades.Count == 0)
            {
                return;
            }

            UpgradeData upgrade = upgrades[nextDebugUpgradeIndex];
            nextDebugUpgradeIndex = (nextDebugUpgradeIndex + 1) % upgrades.Count;
            UpdateDebugButtonLabels();
            StartCoroutine(DropPowerup(upgrade));
        }

        private void UpdateDebugButtonLabels()
        {
            if (infiniteHealthButtonText != null)
            {
                infiniteHealthButtonText.text = infiniteHealth ? "INFINITE HEALTH: ON" : "INFINITE HEALTH: OFF";
                infiniteHealthButtonText.color = infiniteHealth ? Color.cyan : Color.white;
            }

            if (powerupButtonText != null && upgrades.Count > 0)
            {
                powerupButtonText.text = "DROP: " + upgrades[nextDebugUpgradeIndex].displayName.ToUpperInvariant();
            }
        }

        private Text CreateText(string name, Vector2 anchoredPosition, TextAnchor anchor, int size, Color color)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(canvas.transform, false);
            Text text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
            RectTransform rect = text.GetComponent<RectTransform>();
            rect.anchorMin = AnchorFor(anchor);
            rect.anchorMax = AnchorFor(anchor);
            rect.pivot = AnchorFor(anchor);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = name == "Center" ? new Vector2(900f, 420f) : new Vector2(520f, 120f);
            return text;
        }

        private static Vector2 AnchorFor(TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperLeft:
                    return new Vector2(0f, 1f);
                case TextAnchor.UpperRight:
                    return new Vector2(1f, 1f);
                case TextAnchor.UpperCenter:
                    return new Vector2(0.5f, 1f);
                default:
                    return new Vector2(0.5f, 0.5f);
            }
        }

    }
}

