using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>A thing the drone made: its id (MADE_3), what the model described, its pieces, its weight, and the words that asked for it.</summary>
    public class Built : MonoBehaviour
    {
        public readonly List<GameObject> Pieces = new List<GameObject>();
        public string Id { get; private set; }
        public ThingSpec Spec { get; private set; }
        public string Words { get; private set; }
        /// <summary>Kilograms.</summary>
        public float Mass { get; set; }
        public string Name => Spec?.name ?? "thing";
        public string Form => Spec?.form ?? "BLOCK";
        public string Look => Spec?.look ?? "STONE";

        public void Set(string id, ThingSpec spec, string words)
        {
            Id = id;
            Spec = spec;
            Words = words;
        }

        /// <summary>The middle of its pieces as they are now.</summary>
        public Vector3 Centre
        {
            get
            {
                var sum = Vector3.zero;
                int n = 0;
                foreach (var p in Pieces)
                {
                    if (!p) continue;
                    sum += p.transform.position;
                    n++;
                }
                return n == 0 ? transform.position : sum / n;
            }
        }

        /// <summary>A few words for the drone's moment: "a stone bridge (bridge), 3 m long".</summary>
        public string Describe()
        {
            var bounds = new Bounds(Centre, Vector3.zero);
            foreach (var p in Pieces)
                if (p && p.TryGetComponent<Renderer>(out var r)) bounds.Encapsulate(r.bounds);
            string extra = "";
            var spec = Spec;
            if (spec != null)
            {
                if (spec.heat == "HOT" || spec.heat == "WARM") extra += ", " + spec.heat.ToLowerInvariant();
                if (spec.floats) extra += ", floating";
                if (spec.bouncy) extra += ", springy";
                if (spec.glow != "NONE") extra += ", glowing";
            }
            return $"{Name} ({Form.ToLowerInvariant()}, {Look.ToLowerInvariant()}, about {Mathf.Max(bounds.size.x, bounds.size.z):F0} m across and {bounds.size.y:F0} m tall, {Mass:F0} kg{extra})";
        }
    }
}
