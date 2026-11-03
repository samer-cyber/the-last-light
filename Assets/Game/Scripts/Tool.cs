using UnityEngine;

namespace Game
{
    /// <summary>
    /// A hand tool the drone made: a pick or hammer that breaks ice in a few blows, a torch that melts
    /// it while held to it, a lamp to see by. Used with the left mouse button (or F) while held.
    /// </summary>
    public class Tool : MonoBehaviour
    {
        public enum Use { None, Break, Heat, Light }

        public Use Kind = Use.Break;
        [Tooltip("The working end: where the flame or the blow is.")] public Transform Tip;
        [SerializeField] LineRenderer flame;
        [SerializeField] Light lamp;

        /// <summary>When the last blow was struck, for the swing.</summary>
        public float StruckAt { get; private set; } = -10f;
        public bool Burning { get; private set; }

        /// <summary>A blow at what is in front (breaking tools).</summary>
        public void Strike(Frozen target)
        {
            if (Time.time - StruckAt < 0.45f) return;
            StruckAt = Time.time;
            if (target) target.Strike();
        }

        /// <summary>Holds the flame to a point this frame (heating tools); null puts it out.</summary>
        public void Burn(Vector3? at, Frozen target)
        {
            Burning = at.HasValue;
            if (flame)
            {
                flame.enabled = Burning;
                if (Burning)
                {
                    Vector3 from = Tip ? Tip.position : transform.position;
                    flame.SetPosition(0, from);
                    flame.SetPosition(1, Vector3.Lerp(from, at.Value, 0.97f));
                }
            }
            if (Burning && target) target.Warm(5f);
        }

        public void SetFlame(LineRenderer line, Light light)
        {
            flame = line;
            lamp = light;
            if (flame) flame.enabled = false;
        }

        void Update()
        {
            if (lamp && Kind == Use.Heat) lamp.intensity = Burning ? 2.5f + Mathf.Sin(Time.time * 30f) * 0.4f : 0.6f;
        }
    }
}
