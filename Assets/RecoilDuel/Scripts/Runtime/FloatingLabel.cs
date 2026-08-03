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
    public sealed class FloatingLabel : MonoBehaviour
    {
        private Text label;
        private float age;

        public void Build(Text text)
        {
            label = text;
        }

        public void Begin(string message, Color color)
        {
            age = 0f;
            label.text = message;
            label.color = color;
        }

        private void Update()
        {
            age += Time.unscaledDeltaTime;
            transform.position += Vector3.up * (45f * Time.unscaledDeltaTime);
            if (label != null)
            {
                Color color = label.color;
                color.a = Mathf.Clamp01(1f - age / 0.8f);
                label.color = color;
            }

            if (age >= 0.8f)
            {
                gameObject.SetActive(false);
            }
        }
    }
}

