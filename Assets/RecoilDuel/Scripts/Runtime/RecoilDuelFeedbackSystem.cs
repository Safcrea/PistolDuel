using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RecoilDuel
{
    internal sealed class RecoilDuelFeedbackSystem
    {
        private const int InitialSparkPoolSize = 64;
        private const int InitialLabelPoolSize = 8;

        private readonly List<BurstSpark> sparkPool = new List<BurstSpark>(InitialSparkPoolSize);
        private readonly List<FloatingLabel> labelPool = new List<FloatingLabel>(InitialLabelPoolSize);
        private readonly List<ToneEntry> toneCache = new List<ToneEntry>(2);

        private Transform sparkRoot;
        private Canvas canvas;
        private Camera camera;
        private AudioSource audioSource;
        private Sprite sparkSprite;
        private Font labelFont;

        public void Initialize(Transform owner, Transform vfxRoot, Canvas ownerCanvas, Camera ownerCamera, AudioSource ownerAudio, Sprite circleSprite)
        {
            sparkRoot = vfxRoot;
            canvas = ownerCanvas;
            camera = ownerCamera;
            audioSource = ownerAudio;
            sparkSprite = circleSprite;
            labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GetOrCreateTone(owner, 680f, 0.035f);

            for (int i = 0; i < InitialSparkPoolSize; i++)
            {
                CreateSpark();
            }

            for (int i = 0; i < InitialLabelPoolSize; i++)
            {
                CreateLabel();
            }
        }

        public void SpawnBurst(Vector3 position, Color color, int count)
        {
            for (int i = 0; i < count; i++)
            {
                BurstSpark spark = GetSpark();
                spark.transform.position = position;
                spark.transform.localScale = Vector3.one * Random.Range(0.045f, 0.09f);
                spark.gameObject.SetActive(true);
                spark.Begin(color, Random.insideUnitCircle.normalized * Random.Range(1.4f, 4.5f), 0.22f);
            }
        }

        public void ShowFloatingLabel(string message, Vector3 worldPosition, Color color)
        {
            FloatingLabel label = GetLabel();
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(camera, worldPosition);
            label.transform.position = screenPoint;
            label.gameObject.SetActive(true);
            label.Begin(message, color);
        }

        public void PlayTone(Transform owner, float frequency, float duration)
        {
            audioSource.PlayOneShot(GetOrCreateTone(owner, frequency, duration));
        }

        public IEnumerator CameraShake(float amount, float duration)
        {
            Vector3 basePosition = new Vector3(0f, 0f, -10f);
            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                Vector2 offset = Random.insideUnitCircle * amount * (1f - timer / duration);
                camera.transform.position = basePosition + new Vector3(offset.x, offset.y, 0f);
                yield return null;
            }

            camera.transform.position = basePosition;
        }

        public void Dispose()
        {
            for (int i = 0; i < toneCache.Count; i++)
            {
                Object.Destroy(toneCache[i].Clip);
            }

            toneCache.Clear();
        }

        private BurstSpark GetSpark()
        {
            for (int i = 0; i < sparkPool.Count; i++)
            {
                if (!sparkPool[i].gameObject.activeSelf)
                {
                    return sparkPool[i];
                }
            }

            return CreateSpark();
        }

        private BurstSpark CreateSpark()
        {
            GameObject particle = new GameObject("Pooled Burst Spark");
            particle.transform.SetParent(sparkRoot);
            SpriteRenderer renderer = particle.AddComponent<SpriteRenderer>();
            renderer.sprite = sparkSprite;
            renderer.sortingOrder = 12;
            BurstSpark spark = particle.AddComponent<BurstSpark>();
            spark.Build(renderer);
            particle.SetActive(false);
            sparkPool.Add(spark);
            return spark;
        }

        private FloatingLabel GetLabel()
        {
            for (int i = 0; i < labelPool.Count; i++)
            {
                if (!labelPool[i].gameObject.activeSelf)
                {
                    return labelPool[i];
                }
            }

            return CreateLabel();
        }

        private FloatingLabel CreateLabel()
        {
            GameObject labelObject = new GameObject("Pooled Floating Label");
            labelObject.transform.SetParent(canvas.transform, false);
            Text text = labelObject.AddComponent<Text>();
            text.font = labelFont;
            text.fontSize = 34;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            RectTransform rect = text.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(420f, 80f);
            FloatingLabel label = labelObject.AddComponent<FloatingLabel>();
            label.Build(text);
            labelObject.SetActive(false);
            labelPool.Add(label);
            return label;
        }

        private AudioClip GetOrCreateTone(Transform owner, float frequency, float duration)
        {
            for (int i = 0; i < toneCache.Count; i++)
            {
                if (Mathf.Approximately(toneCache[i].Frequency, frequency)
                    && Mathf.Approximately(toneCache[i].Duration, duration))
                {
                    return toneCache[i].Clip;
                }
            }

            AudioClip clip = CreateTone(owner, frequency, duration);
            toneCache.Add(new ToneEntry(frequency, duration, clip));
            return clip;
        }

        private static AudioClip CreateTone(Transform owner, float frequency, float duration)
        {
            AudioClip clip = AudioClip.Create(owner.name + " Shot Tone", Mathf.CeilToInt(44100f * duration), 1, 44100, false);
            float[] samples = new float[clip.samples];
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * i / 44100f) * (1f - i / (float)samples.Length) * 0.18f;
            }

            clip.SetData(samples, 0);
            return clip;
        }

        private readonly struct ToneEntry
        {
            public readonly float Frequency;
            public readonly float Duration;
            public readonly AudioClip Clip;

            public ToneEntry(float frequency, float duration, AudioClip clip)
            {
                Frequency = frequency;
                Duration = duration;
                Clip = clip;
            }
        }
    }
}
