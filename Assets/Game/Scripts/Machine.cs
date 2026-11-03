using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// One of the great minds' machines: it can be hurt (hits on its weak point count more), stunned
    /// by a shock, and broken, when it bursts into sparks and falls. Every machine alive is in
    /// <see cref="All"/>, so the game can tell Spark what is near and what it is doing.
    /// </summary>
    public abstract class Machine : MonoBehaviour
    {
        public static readonly List<Machine> All = new List<Machine>();

        [SerializeField] protected float health = 30f;
        [SerializeField, Tooltip("Hits here count double (or more).")] protected Transform weakPoint;
        [SerializeField] protected float weakMultiplier = 2.5f;
        [SerializeField] protected AudioSource sound;
        [SerializeField] protected AudioClip hurtClip, breakClip;

        public string Id { get; set; }
        public abstract string Title { get; }
        /// <summary>What it is doing, in a few words for Spark ("hunting you", "stunned").</summary>
        public abstract string Doing { get; }
        public bool Broken { get; private set; }
        public bool Stunned => stunnedUntil > Time.time;
        public float Health => health;
        public float Fraction => Mathf.Clamp01(health / maxHealth);
        public Transform WeakPoint => weakPoint;
        /// <summary>The weak point has been seen closely (Spark looked): it shows.</summary>
        public bool Exposed { get; private set; }
        public event Action<Machine> Broke;
        public event Action<Machine, float> Hurt;

        protected float stunnedUntil, maxHealth;
        protected float hurtAt = -10f;

        protected virtual void Awake() => maxHealth = health;
        protected virtual void OnEnable() => All.Add(this);
        protected virtual void OnDisable() => All.Remove(this);

        /// <summary>A hit: how hard, where, and of what kind (BLADE, BEAM, BOLT, SPREAD, REFLECTED).</summary>
        public virtual void Damage(float amount, Vector3 at, string kind)
        {
            if (Broken || amount <= 0f) return;
            if (weakPoint && Vector3.Distance(at, weakPoint.position) < WeakRadius) amount *= weakMultiplier;
            amount *= Resist(kind, at);
            if (amount <= 0f) return;
            health -= amount;
            hurtAt = Time.time;
            Hurt?.Invoke(this, amount);
            Fx.Sparks(at, 6 + (int)Mathf.Min(amount, 20f));
            if (sound && hurtClip && UnityEngine.Random.value < 0.6f) sound.PlayOneShot(hurtClip, 0.5f);
            if (health <= 0f) StartCoroutine(Break());
        }

        protected virtual float WeakRadius => 0.45f;

        /// <summary>How much of a hit gets through (a shield, armour); 1 is all.</summary>
        protected virtual float Resist(string kind, Vector3 at) => 1f;

        /// <summary>A shock: it drops still and sparks for a while.</summary>
        public virtual void Stun(float seconds) => stunnedUntil = Mathf.Max(stunnedUntil, Time.time + seconds);

        /// <summary>Spark looked at it closely: its weak point shows from now on.</summary>
        public virtual void Expose() => Exposed = true;

        IEnumerator Break()
        {
            Broken = true;
            All.Remove(this);
            if (sound && breakClip) sound.PlayOneShot(breakClip, 0.9f);
            Fx.Burst(transform.position, 1f);
            Broke?.Invoke(this);
            yield return Fall();
        }

        /// <summary>What it does as it breaks: falls and lies dark, by default.</summary>
        protected virtual IEnumerator Fall()
        {
            var body = GetComponent<Rigidbody>();
            if (!body) body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = false;
            body.useGravity = true;
            body.mass = 30f;
            body.AddTorque(UnityEngine.Random.insideUnitSphere * 40f, ForceMode.Impulse);
            foreach (var light in GetComponentsInChildren<Light>()) light.enabled = false;
            yield return new WaitForSeconds(20f);
            Destroy(gameObject);
        }

        /// <summary>The machine nearest a point, alive, within a distance; null if none.</summary>
        public static Machine Nearest(Vector3 at, float within)
        {
            Machine best = null;
            float bestDistance = within;
            foreach (var m in All)
            {
                if (!m || m.Broken) continue;
                float d = Vector3.Distance(m.transform.position, at);
                if (d < bestDistance) { bestDistance = d; best = m; }
            }
            return best;
        }
    }

    /// <summary>
    /// Anything warm the machines can smell: the survivor, a fire, a decoy, a flare. They go for the
    /// strongest within their reach, weighed by how near it is.
    /// </summary>
    public class HeatSource : MonoBehaviour
    {
        public static readonly List<HeatSource> All = new List<HeatSource>();

        [Tooltip("How strongly it draws them: the survivor 1, a fire 1.5, a decoy 2.5, a flare 4.")] public float Pull = 1f;
        [Tooltip("When above 0, it stops drawing them at this time.")] public float Until;
        public bool IsSurvivor;

        public bool Active => isActiveAndEnabled && (Until <= 0f || Time.time < Until);

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// <summary>What draws a machine at a point most, within its reach.</summary>
        public static HeatSource Strongest(Vector3 from, float reach)
        {
            HeatSource best = null;
            float bestScore = 0f;
            foreach (var h in All)
            {
                if (!h || !h.Active) continue;
                float d = Vector3.Distance(h.transform.position, from);
                if (d > reach * Mathf.Sqrt(h.Pull)) continue;
                float score = h.Pull / Mathf.Max(4f, d);
                if (score > bestScore) { bestScore = score; best = h; }
            }
            return best;
        }
    }

    /// <summary>The look of the fight: laser bolts, beams, sparks and bursts, drawn and gone in a moment.</summary>
    public static class Fx
    {
        static Material line, spark;

        /// <summary>Set once by the director from materials the scene was built with.</summary>
        public static void Use(Material lineMaterial, Material sparkMaterial)
        {
            line = lineMaterial;
            spark = sparkMaterial;
        }

        /// <summary>A bolt of light from one point to another, fading fast.</summary>
        public static void Bolt(Vector3 from, Vector3 to, Color colour, float width = 0.06f, float seconds = 0.12f)
        {
            if (!line) return;
            var go = new GameObject("Bolt");
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = line;
            lr.positionCount = 2;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.widthMultiplier = width;
            lr.startColor = colour;
            lr.endColor = new Color(colour.r, colour.g, colour.b, colour.a * 0.6f);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var fade = go.AddComponent<FadeLine>();
            fade.Seconds = seconds;
            var light = new GameObject("Flash").AddComponent<Light>();
            light.transform.SetParent(go.transform, false);
            light.transform.position = from;
            light.type = LightType.Point;
            light.color = colour;
            light.range = 5f;
            light.intensity = 3f;
            fade.Light = light;
        }

        /// <summary>A shower of sparks at a point.</summary>
        public static void Sparks(Vector3 at, int count, Color? colour = null)
        {
            if (!spark) return;
            var go = new GameObject("Sparks");
            go.transform.position = at;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.2f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
            main.startColor = colour ?? new Color(1f, 0.7f, 0.35f);
            main.gravityModifier = 1.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = spark;
            ps.Play();
        }

        /// <summary>A machine bursting: a flash, sparks everywhere, smoke.</summary>
        public static void Burst(Vector3 at, float size)
        {
            Sparks(at, (int)(60 * size));
            Sparks(at, (int)(20 * size), new Color(1f, 0.35f, 0.2f));
            var light = new GameObject("Burst").AddComponent<Light>();
            light.transform.position = at;
            light.type = LightType.Point;
            light.color = new Color(1f, 0.55f, 0.3f);
            light.range = 12f * size;
            light.intensity = 6f;
            var fade = light.gameObject.AddComponent<FadeLine>();
            fade.Seconds = 0.6f;
            fade.Light = light;
        }
    }

    /// <summary>Fades a line (and its flash) out over a moment, then removes it.</summary>
    public class FadeLine : MonoBehaviour
    {
        public float Seconds = 0.12f;
        public Light Light;
        LineRenderer lr;
        float born, width, intensity;
        Color start, end;

        void Start()
        {
            born = Time.time;
            lr = GetComponent<LineRenderer>();
            if (lr)
            {
                width = lr.widthMultiplier;
                start = lr.startColor;
                end = lr.endColor;
            }
            if (Light) intensity = Light.intensity;
        }

        void Update()
        {
            float k = Mathf.Clamp01((Time.time - born) / Seconds);
            if (lr)
            {
                lr.widthMultiplier = width * (1f - k * 0.7f);
                lr.startColor = new Color(start.r, start.g, start.b, start.a * (1f - k));
                lr.endColor = new Color(end.r, end.g, end.b, end.a * (1f - k));
            }
            if (Light) Light.intensity = intensity * (1f - k);
            if (k >= 1f) Destroy(gameObject);
        }
    }
}
