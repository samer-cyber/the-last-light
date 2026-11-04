using System.Collections;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A Seeker: one of the great minds' small machines, a black shell with one red eye, that hunts
    /// heat in packs. It drifts on patrol until something warm is near, then closes, circles its prey
    /// at a distance and fires; now and then it dives in close. It goes for the warmest thing it can
    /// smell, so a fire, a decoy or a flare draws it off the survivor. A shock drops it, stunned.
    /// Its eye is its weak point. Spark darting round it draws its aim for a while.
    /// </summary>
    public class Seeker : Machine
    {
        [SerializeField] Transform eye;
        [SerializeField] Light eyeLight;
        [SerializeField] Renderer eyeRenderer;
        [SerializeField] AudioSource hum;
        [SerializeField] AudioClip fireClip, chargeClip;
        [SerializeField] float smell = 30f;
        [SerializeField] float damage = 0.06f;

        public override string Title => "a Seeker";
        public override string Doing => Broken ? "broken" : Stunned ? "stunned, dropped to the snow" : distractedUntil > Time.time ? "chasing you, Spark" : prey ? (prey.IsSurvivor ? "hunting the survivor" : "drawn to " + prey.name.ToLowerInvariant()) : "on patrol";
        public HeatSource Prey => prey;
        /// <summary>It has seen something warm and is closing on it.</summary>
        public bool Hunting => prey && !Broken;

        Vector3 home, velocity;
        HeatSource prey;
        float nextShot, circle, diveUntil, distractedUntil, bob;
        Vector3 distraction;
        Material eyeMaterial;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        protected override void Awake()
        {
            base.Awake();
            home = transform.position;
            circle = Random.value * 360f;
            bob = Random.value * 10f;
            nextShot = Time.time + 2f + Random.value * 2f;
            if (eyeRenderer) eyeMaterial = eyeRenderer.material;
        }

        public void Home(Vector3 at) => home = at;

        static float packShotAt = -10f;
        float calmUntil;

        /// <summary>It loses the scent for a while (after the survivor goes down).</summary>
        public void Calm(float seconds) => calmUntil = Time.time + seconds;

        /// <summary>Spark darts round it: it chases Spark instead for a while.</summary>
        public void Distract(Vector3 at, float seconds)
        {
            distraction = at;
            distractedUntil = Time.time + seconds;
        }

        void Update()
        {
            if (Broken) return;
            float dt = Time.deltaTime;
            if (Stunned)
            {
                // Down on the snow, sparking.
                velocity = Vector3.MoveTowards(velocity, Vector3.down * 4f, dt * 10f);
                Move(dt, 0.4f);
                if (Random.value < dt * 6f) Fx.Sparks(transform.position + Random.insideUnitSphere * 0.2f, 3, Shots.Cyan);
                Eye(0.15f);
                return;
            }
            prey = Time.time < calmUntil ? null : HeatSource.Strongest(transform.position, smell);
            Vector3 target;
            float height = 3f + Mathf.Sin(Time.time * 0.9f + bob) * 0.5f;
            if (distractedUntil > Time.time)
            {
                circle += dt * 140f;
                target = distraction + Quaternion.Euler(0f, circle, 0f) * Vector3.forward * 4f;
                height = 2.6f;
                Eye(1f);
            }
            else if (prey)
            {
                // Round its prey at a distance; now and then, a dive in close.
                circle += dt * (25f + 10f * Mathf.Sin(Time.time * 0.3f + bob));
                float range = diveUntil > Time.time ? 2.2f : 9f;
                if (diveUntil < Time.time - 6f && Random.value < dt * 0.15f) diveUntil = Time.time + 2.5f;
                target = prey.transform.position + Quaternion.Euler(0f, circle, 0f) * Vector3.forward * range;
                if (diveUntil > Time.time) height = 1.7f;
                Eye(1f);
                // One shot at a time from the whole pack: they take turns.
                if (Time.time > nextShot && Time.time > packShotAt + 1.6f && Vector3.Distance(prey.transform.position, transform.position) < 28f)
                {
                    packShotAt = Time.time;
                    StartCoroutine(Fire(prey));
                }
            }
            else
            {
                circle += dt * 12f;
                target = home + Quaternion.Euler(0f, circle, 0f) * Vector3.forward * 7f;
                Eye(0.6f);
            }
            float ground = Ground(target);
            target.y = ground + height;
            Vector3 wanted = Vector3.ClampMagnitude(target - transform.position, 1f) * 6f;
            velocity = Vector3.Lerp(velocity, wanted, 1f - Mathf.Exp(-2.5f * dt));
            Move(dt, 1f);
            Vector3 look = prey ? prey.transform.position + Vector3.up - transform.position : velocity;
            if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look, Vector3.up), 1f - Mathf.Exp(-5f * dt));
            if (hum) hum.pitch = Mathf.Lerp(hum.pitch, prey ? 1.25f : 0.9f, dt * 2f);
        }

        void Move(float dt, float limit)
        {
            Vector3 next = transform.position + velocity * dt;
            float ground = Ground(next);
            if (next.y < ground + 0.25f) next.y = ground + 0.25f;
            transform.position = next;
        }

        static readonly RaycastHit[] hits = new RaycastHit[8];

        /// <summary>The ground under a point: the highest surface below 40 m above it that is not a machine.</summary>
        static float Ground(Vector3 at)
        {
            int n = Physics.RaycastNonAlloc(at + Vector3.up * 40f, Vector3.down, hits, 120f, Shots.Mask & ~(1 << LayerMask.NameToLayer("Traveller")), QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            for (int i = 0; i < n; i++)
                if (!hits[i].collider.GetComponentInParent<Machine>() && hits[i].point.y > best) best = hits[i].point.y;
            return float.IsNegativeInfinity(best) ? at.y : best;
        }

        IEnumerator Fire(HeatSource at)
        {
            nextShot = Time.time + 3.5f + Random.value * 2f;
            if (sound && chargeClip) sound.PlayOneShot(chargeClip, 0.35f);
            // A breath before it fires: the eye flares.
            for (float t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                Eye(1f + 3f * t / 0.35f);
                yield return null;
            }
            if (Broken || Stunned || !at) yield break;
            Vector3 mark = at.transform.position + (at.IsSurvivor ? Vector3.up * 1.2f : Vector3.up * 0.4f);
            // Not quite perfect: a little spread, so moving helps.
            mark += Random.insideUnitSphere * 0.8f;
            if (sound && fireClip) sound.PlayOneShot(fireClip, 0.6f);
            Shots.FromMachine(this, eye ? eye.position : transform.position, mark, damage);
        }

        void Eye(float strength)
        {
            if (eyeLight) eyeLight.intensity = 1.6f * strength;
            if (eyeMaterial) eyeMaterial.SetColor(BaseColorId, new Color(3f, 0.35f, 0.2f) * Mathf.Max(0.1f, strength));
        }

        protected override float WeakRadius => 0.3f;
    }
}
