using UnityEngine;

namespace Game
{
    /// <summary>
    /// Feeds the full-screen warp (Warp.shader) every frame: where the orb is on screen, how large its
    /// core and its ring of bent light look from here, and the ripples it sends out. The ring is a
    /// fixed multiple of the core's size, so it grows as the player comes closer, as it would.
    /// </summary>
    public class SpaceWarp : MonoBehaviour
    {
        static readonly int CenterId = Shader.PropertyToID("_WarpCenter");
        static readonly int ShapeId = Shader.PropertyToID("_WarpShape");
        static readonly int RipplesId = Shader.PropertyToID("_WarpRipples");
        static readonly int RippleAmpId = Shader.PropertyToID("_WarpRippleAmp");
        static readonly int NearId = Shader.PropertyToID("_WarpNear");

        [SerializeField] Camera view;
        [SerializeField, Tooltip("The ring of bent light, in core radii.")] float ring = 5f;
        [SerializeField, Tooltip("Twist at the ring, in radians.")] float swirl = 0.35f;
        [SerializeField, Tooltip("How far colours split where the bending is strongest.")] float colourSplit = 0.04f;
        [SerializeField, Tooltip("How fast ripples spread, in metres per second.")] float rippleSpeed = 7f;
        [SerializeField] float rippleLife = 1.6f;
        [SerializeField, Tooltip("Nothing nearer the camera than this is bent (the notebook, the hands).")] float near = 1.2f;

        /// <summary>The core's radius in metres; the orb sets it.</summary>
        public float CoreRadius { get; set; } = 0.5f;
        /// <summary>How much harder space bends than at rest; 0 turns the warp off.</summary>
        public float Strength { get; set; } = 1f;
        /// <summary>Multiplies the swirl; the orb raises it as it destabilises.</summary>
        public float Twist { get; set; } = 1f;
        /// <summary>The radius of the ring of bent light, in metres.</summary>
        public float RingRadius => CoreRadius * ring * Mathf.Max(Strength, 0f);

        readonly float[] age = { -1f, -1f, -1f, -1f };
        readonly float[] power = new float[4];
        int next;

        /// <summary>Sends a ripple outward; strength about 0.01 to 0.04.</summary>
        public void Ripple(float strength)
        {
            age[next] = 0f;
            power[next] = strength;
            next = (next + 1) % age.Length;
        }

        void OnDisable()
        {
            Shader.SetGlobalVector(CenterId, Vector4.zero);
        }

        void LateUpdate()
        {
            if (!view) return;
            Vector3 toOrb = transform.position - view.transform.position;
            float distance = toOrb.magnitude;
            Vector3 viewport = view.WorldToViewportPoint(transform.position);
            // Fade out as the orb goes behind the camera, rather than switching off at once.
            float facing = Vector3.Dot(view.transform.forward, toOrb / Mathf.Max(distance, 1e-4f));
            float visible = Mathf.Clamp01((facing + 0.2f) / 0.4f);

            // Metres at the orb's distance to viewport heights.
            float toView = 1f / (Mathf.Max(distance, 1e-3f) * 2f * Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad));
            float core = CoreRadius * toView;
            float ringRadius = Mathf.Min(RingRadius * toView, 0.6f);

            var radii = Vector4.zero;
            var amps = Vector4.zero;
            for (int i = 0; i < age.Length; i++)
            {
                if (age[i] < 0f) continue;
                age[i] += Time.deltaTime;
                if (age[i] > rippleLife)
                {
                    age[i] = -1f;
                    continue;
                }
                float life = age[i] / rippleLife;
                radii[i] = (RingRadius * 0.6f + rippleSpeed * age[i]) * toView;
                amps[i] = power[i] * (1f - life) * (1f - life) * Mathf.Clamp01(age[i] * 20f);
            }

            Shader.SetGlobalVector(CenterId, new Vector4(viewport.x, viewport.y, visible * Mathf.Clamp01(Strength), view.aspect));
            Shader.SetGlobalVector(ShapeId, new Vector4(core, ringRadius, swirl * Twist, colourSplit));
            Shader.SetGlobalVector(RipplesId, radii);
            Shader.SetGlobalVector(RippleAmpId, amps);
            Shader.SetGlobalFloat(NearId, near);
        }
    }
}
