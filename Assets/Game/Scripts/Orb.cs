using UnityEngine;

namespace Game
{
    /// <summary>
    /// The orb: a tiny, blinding core of everything there will be, growing slowly, bending space
    /// around it (through <see cref="SpaceWarp"/>) and lighting its dust with every symbol of the
    /// message. The director raises its instability until it collapses and bursts.
    /// </summary>
    public class Orb : MonoBehaviour
    {
        static readonly int PulseId = Shader.PropertyToID("_Pulse");
        static readonly int InstabilityId = Shader.PropertyToID("_Instability");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        [SerializeField] MorseEmitter emitter;
        [SerializeField] SpaceWarp warp;
        [SerializeField, Tooltip("Scaled to the core's diameter; the core, corona and glow sit under it.")] Transform body;
        [SerializeField] Renderer core;
        [SerializeField] Renderer corona;
        [SerializeField] Renderer glow;
        [SerializeField] ParticleSystem dust;
        [SerializeField] ParticleSystem haze;
        [SerializeField] ParticleSystem rings;
        [SerializeField] ParticleSystem burst;
        [SerializeField, Tooltip("A directional light kept pointing from the orb to the player: the orb lighting the hands and the notebook.")] Light shine;
        [SerializeField] Transform viewer;
        [SerializeField] float shineIntensity = 1.4f;
        [SerializeField, Tooltip("Core radius at the start, in metres.")] float startRadius = 0.4f;
        [SerializeField, Tooltip("Radius it grows toward, in metres.")] float grownRadius = 0.85f;
        [SerializeField, Tooltip("Seconds to grow most of the way.")] float growTime = 240f;
        [SerializeField] Color dotRing = new Color(0.55f, 0.8f, 1f);
        [SerializeField] Color dashRing = new Color(0.85f, 0.6f, 1f);

        /// <summary>0 calm, 1 about to break.</summary>
        public float Instability { get; set; }
        /// <summary>Light on top of the message's, 0 to 1.</summary>
        public float Flare { get; set; }
        /// <summary>0 full size, 1 fallen to a point.</summary>
        public float Collapse { get; set; }
        /// <summary>How much harder it bends space than at rest.</summary>
        public float Warp { get; set; } = 1f;
        /// <summary>Extra size while it gathers itself, 0 to 1.</summary>
        public float Swell { get; set; }
        /// <summary>How much faster the dust falls in than at rest.</summary>
        public float Pull { get; set; } = 1f;
        /// <summary>How much faster the dust turns than at rest.</summary>
        public float Spin { get; set; } = 1f;

        /// <summary>The core's radius now, in metres.</summary>
        public float Radius { get; private set; }
        public float Pulse { get; private set; }

        Material coreMat, coronaMat, glowMat, dustMat;
        float coreIntensity, coronaIntensity, glowIntensity, dustIntensity;
        float dustInflow, hazeInflow;
        ParticleSystem.MinMaxCurve dustTurn, hazeTurn;
        float dim, stir, shudder;

        void Awake()
        {
            coreMat = core.material;
            coronaMat = corona.material;
            glowMat = glow.material;
            dustMat = dust.GetComponent<ParticleSystemRenderer>().material;
            coreIntensity = coreMat.GetFloat(IntensityId);
            coronaIntensity = coronaMat.GetFloat(IntensityId);
            glowIntensity = glowMat.GetFloat(IntensityId);
            dustIntensity = dustMat.GetFloat(IntensityId);
            dustInflow = dust.velocityOverLifetime.radial.constant;
            hazeInflow = haze.velocityOverLifetime.radial.constant;
            dustTurn = dust.velocityOverLifetime.orbitalY;
            hazeTurn = haze.velocityOverLifetime.orbitalY;
            Radius = startRadius;
        }

        void OnEnable()
        {
            if (emitter) emitter.StepStarted += OnStep;
        }

        void OnDisable()
        {
            if (emitter) emitter.StepStarted -= OnStep;
        }

        void OnStep(MorseCode.StepKind kind)
        {
            if (kind == MorseCode.StepKind.Dot) Send(dotRing, 1.3f, 0.010f);
            else if (kind == MorseCode.StepKind.Dash) Send(dashRing, 2.2f, 0.018f);
        }

        /// <summary>Sends a wave out from the ring of bent light: a faint ring of light and a ripple in space.</summary>
        void Send(Color colour, float seconds, float ripple)
        {
            var emit = new ParticleSystem.EmitParams
            {
                position = Vector3.zero,
                applyShapeToPosition = false,
                velocity = Vector3.zero,
                // The ring texture's ring sits at 0.43 of its width.
                startSize = warp.RingRadius * 0.8f / 0.43f,
                startLifetime = seconds,
                startColor = colour,
            };
            rings.Emit(emit, 1);
            warp.Ripple(ripple);
        }

        /// <summary>Darkens for a moment, as if it heard something wrong.</summary>
        public void Dim() => dim = 1f;

        /// <summary>Flickers with interest: what was written came close.</summary>
        public void Stir()
        {
            stir = 1f;
            Send(dashRing, 1.6f, 0.03f);
        }

        /// <summary>The end of it: the orb is gone, space lets go, and light and dust fly outward.</summary>
        public void Explode()
        {
            body.gameObject.SetActive(false);
            warp.Strength = 0f;
            dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            haze.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            rings.Clear();
            burst.Play();
        }

        void Update()
        {
            float dt = Time.deltaTime, t = Time.time;
            dim = Mathf.MoveTowards(dim, 0f, dt * 0.7f);
            stir = Mathf.MoveTowards(stir, 0f, dt * 1.2f);
            float flicker = stir * Mathf.PerlinNoise(t * 18f, 2.1f);
            Pulse = Mathf.Clamp01(emitter.Envelope + Flare + flicker) * (1f - 0.7f * dim);

            // It grows slowly while the player watches, more as it gathers itself, and falls to a point at the end.
            float grown = Mathf.Lerp(startRadius, grownRadius, 1f - Mathf.Exp(-Time.timeSinceLevelLoad / growTime));
            float shiver = Instability * Instability * (Mathf.PerlinNoise(t * 13f, 0.4f) - 0.5f) * 0.12f;
            float breathe = 1f + 0.02f * Mathf.Sin(t * 0.9f) + 0.06f * Pulse + shiver;
            Radius = grown * (1f + 0.8f * Swell) * breathe * Mathf.Lerp(1f, 0.02f, Collapse);
            body.localScale = Vector3.one * (2f * Radius);

            float light = 1f - 0.5f * dim;
            Drive(coreMat, coreIntensity * light);
            Drive(coronaMat, coronaIntensity * light);
            Drive(glowMat, glowIntensity * light * (1f + 3f * Collapse));
            dustMat.SetFloat(IntensityId, dustIntensity * (1f + 1.1f * Pulse) * light);

            if (shine && viewer)
            {
                shine.transform.rotation = Quaternion.LookRotation(viewer.position - transform.position);
                shine.intensity = body.gameObject.activeSelf ? shineIntensity * (0.45f + 1.4f * Pulse + 1.5f * Instability) * (1f - 0.8f * Collapse) : 0f;
            }

            if (warp.Strength > 0f) warp.Strength = Warp * (1f + 0.2f * Pulse);
            // As it destabilises, space shudders: ripples come faster and stronger, and it twists.
            warp.Twist = 1f + 5f * Instability;
            if (Instability > 0.15f && warp.Strength > 0f && (shudder -= dt) <= 0f)
            {
                warp.Ripple(0.008f + 0.03f * Instability);
                shudder = Mathf.Lerp(0.5f, 0.1f, Instability);
            }
            warp.CoreRadius = Radius;

            var dustVelocity = dust.velocityOverLifetime;
            dustVelocity.radial = dustInflow * Pull;
            dustVelocity.orbitalY = Faster(dustTurn, Spin);
            var hazeVelocity = haze.velocityOverLifetime;
            hazeVelocity.radial = hazeInflow * Pull;
            hazeVelocity.orbitalY = Faster(hazeTurn, Spin);
        }

        static ParticleSystem.MinMaxCurve Faster(ParticleSystem.MinMaxCurve turn, float factor) =>
            new ParticleSystem.MinMaxCurve(turn.constantMin * factor, turn.constantMax * factor);

        void Drive(Material material, float intensity)
        {
            material.SetFloat(PulseId, Pulse);
            material.SetFloat(InstabilityId, Instability);
            material.SetFloat(IntensityId, intensity);
        }
    }
}
