using UnityEngine;

namespace Game
{
    /// <summary>
    /// The orb: lights its body with the Morse message, bends space around itself, sends a ring
    /// out with each symbol and keeps its dust turning. The director raises its instability until
    /// it collapses and bursts.
    /// </summary>
    public class Orb : MonoBehaviour
    {
        static readonly int PulseId = Shader.PropertyToID("_Pulse");
        static readonly int InstabilityId = Shader.PropertyToID("_Instability");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int OrbRadiusId = Shader.PropertyToID("_OrbRadius");
        static readonly int EinsteinId = Shader.PropertyToID("_Einstein");

        [SerializeField] MorseEmitter emitter;
        [SerializeField] Transform body;
        [SerializeField] Renderer core;
        [SerializeField] Renderer shell;
        [SerializeField] Renderer halo;
        [SerializeField] Renderer lens;
        [SerializeField] ParticleSystem dust;
        [SerializeField] ParticleSystem haze;
        [SerializeField] ParticleSystem rings;
        [SerializeField] ParticleSystem burst;
        [SerializeField] float radius = 8f;
        [SerializeField, Tooltip("Radius of the ring of bent light, in orb radii.")] float einstein = 1.3f;
        [SerializeField] Color dotRing = new Color(0.55f, 0.8f, 1f);
        [SerializeField] Color dashRing = new Color(1f, 0.55f, 1f);

        /// <summary>0 calm, 1 about to break.</summary>
        public float Instability { get; set; }
        /// <summary>Light on top of the message's, 0 to 1.</summary>
        public float Flare { get; set; }
        /// <summary>0 full size, 1 fallen to a point.</summary>
        public float Collapse { get; set; }
        /// <summary>How much harder it bends space than at rest.</summary>
        public float Warp { get; set; } = 1f;
        /// <summary>How much faster the dust falls in than at rest.</summary>
        public float Pull { get; set; } = 1f;
        /// <summary>How much faster the dust turns than at rest.</summary>
        public float Spin { get; set; } = 1f;

        public float Radius => radius;
        public float Pulse { get; private set; }

        Material coreMat, shellMat, haloMat, lensMat;
        float coreIntensity, shellIntensity, haloIntensity;
        float dustInflow, hazeInflow;
        ParticleSystem.MinMaxCurve dustTurn, hazeTurn;
        float dim, stir;

        void Awake()
        {
            coreMat = core.material;
            shellMat = shell.material;
            haloMat = halo.material;
            lensMat = lens.material;
            coreIntensity = coreMat.GetFloat(IntensityId);
            shellIntensity = shellMat.GetFloat(IntensityId);
            haloIntensity = haloMat.GetFloat(IntensityId);
            dustInflow = dust.velocityOverLifetime.radial.constant;
            hazeInflow = haze.velocityOverLifetime.radial.constant;
            dustTurn = dust.velocityOverLifetime.orbitalY;
            hazeTurn = haze.velocityOverLifetime.orbitalY;
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
            if (kind == MorseCode.StepKind.Dot) Ring(dotRing, 1.3f);
            else if (kind == MorseCode.StepKind.Dash) Ring(dashRing, 2.2f);
        }

        /// <summary>Sends a ring of light out from the surface.</summary>
        public void Ring(Color colour, float seconds)
        {
            var emit = new ParticleSystem.EmitParams
            {
                position = Vector3.zero,
                applyShapeToPosition = false,
                velocity = Vector3.zero,
                // The ring texture's ring sits at 0.43 of its width; start it on the surface.
                startSize = body.localScale.x * 0.5f / 0.43f,
                startLifetime = seconds,
                startColor = colour,
            };
            rings.Emit(emit, 1);
        }

        /// <summary>Darkens for a moment, as if it heard something wrong.</summary>
        public void Dim() => dim = 1f;

        /// <summary>Flickers with interest: what was written came close.</summary>
        public void Stir()
        {
            stir = 1f;
            Ring(dashRing, 1.6f);
        }

        /// <summary>The end of it: the orb is gone, and its light and dust fly outward.</summary>
        public void Explode()
        {
            body.gameObject.SetActive(false);
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

            float shiver = Instability * Instability * (Mathf.PerlinNoise(t * 13f, 0.4f) - 0.5f) * 0.05f;
            float breathe = 1f + 0.012f * Mathf.Sin(t * 0.8f) + 0.025f * Pulse + shiver;
            float diameter = 2f * radius * breathe * Mathf.Lerp(1f, 0.01f, Collapse);
            body.localScale = Vector3.one * diameter;

            float glow = 1f - 0.5f * dim;
            Drive(coreMat, coreIntensity * glow);
            Drive(shellMat, shellIntensity * glow);
            Drive(haloMat, haloIntensity * glow * (1f + 2f * Collapse));
            lensMat.SetFloat(OrbRadiusId, diameter * 0.5f);
            lensMat.SetFloat(EinsteinId, einstein * Warp * (1f + 0.15f * Pulse));

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
