using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Something hot: a fire, a stove, a warm stone, a torch in use. Ice near it melts (<see cref="Frozen"/>),
    /// fastest close by and not at all past its reach. A small light flickers with it.
    /// </summary>
    public class Heat : MonoBehaviour
    {
        public static readonly List<Heat> All = new List<Heat>();

        [Tooltip("Metres it reaches.")] public float Radius = 4f;
        [Tooltip("How fast it melts ice right beside it, per second.")] public float Power = 2f;
        [SerializeField] Light flame;

        float baseIntensity;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Start()
        {
            if (flame) baseIntensity = flame.intensity;
        }

        void Update()
        {
            if (!flame) return;
            float t = Time.time * 9f + GetInstanceID();
            flame.intensity = baseIntensity * (0.85f + 0.1f * Mathf.Sin(t) + 0.08f * Mathf.Sin(t * 2.7f));
        }

        /// <summary>How fast it melts ice at a point.</summary>
        public float At(Vector3 point)
        {
            float d = Vector3.Distance(transform.position, point);
            if (d >= Radius) return 0f;
            float k = 1f - d / Radius;
            return Power * k * k;
        }

        public static float Total(Vector3 point)
        {
            float sum = 0f;
            foreach (var h in All) if (h && h.isActiveAndEnabled) sum += h.At(point);
            return sum;
        }

        /// <summary>Makes a thing hot: a warm or hot glow and the melting that goes with it.</summary>
        public static Heat Add(GameObject thing, bool hot, float size)
        {
            var heat = thing.AddComponent<Heat>();
            heat.Radius = (hot ? 5f : 3f) * Mathf.Clamp(size, 0.6f, 2f);
            heat.Power = hot ? 3f : 1f;
            var light = new GameObject("Heat").AddComponent<Light>();
            light.transform.SetParent(thing.transform, false);
            light.transform.localPosition = Vector3.up * 0.4f;
            light.type = LightType.Point;
            light.color = hot ? new Color(1f, 0.45f, 0.15f) : new Color(1f, 0.62f, 0.32f);
            light.range = heat.Radius * 1.4f;
            light.intensity = hot ? 3.2f : 1.4f;
            light.shadows = LightShadows.None;
            heat.flame = light;
            return heat;
        }
    }
}
