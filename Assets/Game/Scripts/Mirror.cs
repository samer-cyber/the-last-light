using System.Collections;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The old sun mirror on the clifftop: a great dish on a short tower that once turned the sun's
    /// light onto the vault. It wakes when its heart is set in and its frozen joint is free; then it
    /// turns to the sun, and with enough light behind it throws a beam down onto the vault.
    /// </summary>
    public class Mirror : MonoBehaviour
    {
        [SerializeField, Tooltip("Turns on the joint.")] Transform dish;
        [SerializeField] Renderer face;
        [SerializeField, Tooltip("Where light gathers in front of the dish.")] Transform focus;
        [SerializeField, Tooltip("A long soft shaft of light from the dish down to the vault.")] LineRenderer beam;
        [SerializeField] Transform vault;
        [SerializeField] Light shine;
        [SerializeField] AudioSource sound;
        [SerializeField] AudioClip wakeClip, turnClip;
        [SerializeField] Vector3 sunDirection = new Vector3(-0.38f, 0.16f, 0.91f);

        public Vector3 Focus => focus ? focus.position : transform.position + Vector3.up * 4f;
        public Vector3 Vault => vault ? vault.position : transform.position;

        static readonly int EmissionId = Shader.PropertyToID("_Emission");

        void Start()
        {
            if (beam) beam.enabled = false;
            if (shine) shine.intensity = 0f;
        }

        /// <summary>Wakes and turns so the sun falls on its face and the beam would fall on the vault.</summary>
        public IEnumerator Turn(float seconds)
        {
            if (sound && wakeClip) sound.PlayOneShot(wakeClip, 0.9f);
            yield return new WaitForSeconds(0.8f);
            if (sound && turnClip) sound.PlayOneShot(turnClip, 0.8f);
            // Its face's normal halfway between the sun and the vault: light in from one, out to the other.
            Vector3 toSun = sunDirection.normalized;
            Vector3 toVault = (Vault - dish.position).normalized;
            Vector3 normal = (toSun + toVault).normalized;
            var from = dish.rotation;
            var to = Quaternion.LookRotation(normal, Vector3.up);
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                dish.rotation = Quaternion.Slerp(from, to, k);
                yield return null;
            }
            dish.rotation = to;
        }

        /// <summary>Its heart is in but its joint is frozen: it wakes, strains to turn, and can't.</summary>
        public IEnumerator Grind()
        {
            if (sound && wakeClip) sound.PlayOneShot(wakeClip, 0.7f);
            yield return new WaitForSeconds(0.6f);
            if (sound && turnClip)
            {
                sound.pitch = 0.6f;
                sound.PlayOneShot(turnClip, 0.9f);
            }
            var rest = dish.localRotation;
            for (float t = 0f; t < 2.4f; t += Time.deltaTime)
            {
                // A few degrees one way, caught, a shudder.
                float strain = Mathf.Sin(Mathf.Clamp01(t / 2.4f) * Mathf.PI);
                dish.localRotation = rest * Quaternion.Euler(Mathf.Sin(t * 47f) * 0.6f * strain, 2.5f * strain + Mathf.Sin(t * 31f) * 0.8f * strain, 0f);
                Shine(0.04f * strain);
                yield return null;
            }
            dish.localRotation = rest;
            Shine(0f);
            if (sound) sound.pitch = 1f;
        }

        /// <summary>How brightly it throws light now, 0 to 1; above 0 the beam shows.</summary>
        public void Shine(float k)
        {
            if (beam)
            {
                beam.enabled = k > 0.001f;
                beam.SetPosition(0, Focus);
                beam.SetPosition(1, Vault + Vector3.up * 1.5f);
                beam.widthCurve = new AnimationCurve(new Keyframe(0f, Mathf.Lerp(0.3f, 1.6f, k)), new Keyframe(1f, Mathf.Lerp(0.6f, 4.5f, k)));
                var warm = new Color(1f, 0.72f, 0.42f);
                beam.startColor = new Color(warm.r, warm.g, warm.b, Mathf.Clamp01(k * 1.2f));
                beam.endColor = new Color(warm.r, warm.g, warm.b, Mathf.Clamp01(k * 0.8f));
            }
            if (shine) shine.intensity = 3f * k;
            if (face)
            {
                var block = new MaterialPropertyBlock();
                face.GetPropertyBlock(block);
                block.SetColor(EmissionId, new Color(1f, 0.75f, 0.45f) * (1.2f * k));
                face.SetPropertyBlock(block);
            }
        }
    }
}
