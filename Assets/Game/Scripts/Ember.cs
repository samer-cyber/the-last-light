using System;
using System.Collections;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// One of the last embers of light, hanging over its resting place and breathing slowly. When the
    /// traveller comes close it flies into the mind, which takes in its light.
    /// </summary>
    public class Ember : MonoBehaviour
    {
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int PulseId = Shader.PropertyToID("_Pulse");

        [SerializeField] Renderer core;
        [SerializeField] Renderer glow;
        [SerializeField] Light shine;
        [SerializeField] AudioSource hum;
        [SerializeField, Tooltip("Units of light it gives the mind.")] int gift = 6;
        [SerializeField] float reach = 2.3f;

        public event Action<Ember> Taken;
        public bool Gone { get; private set; }

        Transform traveller;
        Companion mind;
        Material coreMat, glowMat;
        float coreIntensity, glowIntensity, shineIntensity;
        Vector3 rest;

        public void Watch(Transform who, Companion by)
        {
            traveller = who;
            mind = by;
        }

        void Awake()
        {
            coreMat = core.material;
            glowMat = glow.material;
            coreIntensity = coreMat.GetFloat(IntensityId);
            glowIntensity = glowMat.GetFloat(IntensityId);
            shineIntensity = shine.intensity;
            rest = transform.position;
        }

        void Update()
        {
            if (Gone) return;
            float breath = 0.5f + 0.5f * Mathf.Sin(Time.time * 1.3f + rest.x);
            transform.position = rest + Vector3.up * Mathf.Sin(Time.time * 0.8f + rest.z) * 0.12f;
            coreMat.SetFloat(PulseId, breath * 0.4f);
            glowMat.SetFloat(IntensityId, glowIntensity * (0.8f + 0.4f * breath));
            shine.intensity = shineIntensity * (0.85f + 0.3f * breath);
            if (traveller && mind && Vector3.Distance(traveller.position + Vector3.up, transform.position) < reach) StartCoroutine(Fly());
        }

        IEnumerator Fly()
        {
            Gone = true;
            if (hum) hum.Stop();
            Vector3 start = transform.position;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 1.1f)
            {
                float k = t * t * (3f - 2f * t);
                transform.position = Vector3.Lerp(start, mind.transform.position, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 1.5f;
                transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.2f, k);
                yield return null;
            }
            mind.Ember(gift);
            Taken?.Invoke(this);
            gameObject.SetActive(false);
        }
    }
}
