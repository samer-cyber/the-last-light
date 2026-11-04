using System.Collections;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A sleeper's pod whose lights can fail while you watch: they stutter between amber and red, a
    /// relay clicks off with a hiss, and they settle on red as the light inside dims.
    /// </summary>
    public class PodLights : MonoBehaviour
    {
        [SerializeField, Tooltip("The glow it has now, and the one it fails to.")] Material from, to;
        [SerializeField] Light inside;
        [SerializeField] AudioSource voice;
        [SerializeField] AudioClip failClip;
        [SerializeField, Tooltip("Its panel's screen and lamp, failing with it.")] Material screenFrom, screenTo, lampFrom, lampTo;

        public bool Failed { get; private set; }

        public IEnumerator Fail()
        {
            if (Failed) yield break;
            Failed = true;
            var renderers = GetComponentsInChildren<Renderer>(true);
            if (voice && failClip) voice.PlayOneShot(failClip, 1f);
            Color warm = inside ? inside.color : Color.white;
            Color dying = new Color(1f, 0.18f, 0.12f);
            float bright = inside ? inside.intensity : 0f;
            float[] beats = { 0.3f, 0.2f, 0.35f, 0.14f, 0.25f, 0.5f, 0.18f, 0.6f, 0.3f };
            for (int i = 0; i < beats.Length; i++)
            {
                bool red = i % 2 == 0;
                Swap(renderers, red ? from : to, red ? to : from);
                Swap(renderers, red ? screenFrom : screenTo, red ? screenTo : screenFrom);
                Swap(renderers, red ? lampFrom : lampTo, red ? lampTo : lampFrom);
                if (inside)
                {
                    // Each time it fails toward red it flares, as if trying, then gives.
                    inside.color = red ? dying : warm;
                    inside.intensity = bright * (red ? 2.4f : 1f);
                    inside.range = red ? 2.6f : 1.7f;
                }
                yield return new WaitForSeconds(beats[i]);
            }
            for (float t = 0f; t < 2.5f; t += Time.deltaTime)
            {
                if (inside)
                {
                    inside.intensity = Mathf.Lerp(bright * 2.4f, bright * 0.45f, t / 2.5f);
                    inside.range = Mathf.Lerp(2.6f, 1.7f, t / 2.5f);
                }
                yield return null;
            }
        }

        static void Swap(Renderer[] renderers, Material a, Material b)
        {
            if (!a || !b) return;
            foreach (var r in renderers)
            {
                var materials = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                    if (materials[i] == a)
                    {
                        materials[i] = b;
                        changed = true;
                    }
                if (changed) r.sharedMaterials = materials;
            }
        }
    }
}
