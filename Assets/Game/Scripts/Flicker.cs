using UnityEngine;

namespace Game
{
    /// <summary>A light on its last legs: it holds, stutters, drops out, comes back.</summary>
    public class Flicker : MonoBehaviour
    {
        [SerializeField] Light lamp;
        [SerializeField, Range(0f, 1f)] float steadiness = 0.75f;
        float full, next, target;

        void Start()
        {
            if (!lamp) lamp = GetComponent<Light>();
            full = lamp ? lamp.intensity : 1f;
            target = full;
        }

        void Update()
        {
            if (!lamp) return;
            if (Time.time > next)
            {
                bool stutter = Random.value > steadiness;
                target = stutter ? full * Random.Range(0f, 0.4f) : full * Random.Range(0.8f, 1f);
                next = Time.time + (stutter ? Random.Range(0.03f, 0.12f) : Random.Range(0.3f, 2.5f));
            }
            lamp.intensity = Mathf.Lerp(lamp.intensity, target, 1f - Mathf.Exp(-30f * Time.deltaTime));
        }
    }
}
