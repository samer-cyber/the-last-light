using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A Harvester: one of the great minds' big machines, nine metres tall on three legs, that strips
    /// power from whatever it finds. It carries the sun mirror's heart in a cage under its body. Its
    /// shield turns most fire aside. It sweeps a great red beam across the ground; a mirror wall throws
    /// the beam back into it, and anything in its path loses warmth fast unless behind cover or a
    /// shield. After a sweep it must cool: the vent on its back glows, the shield drops, and that is
    /// when it can be hurt. It lets Seekers loose, and stamps at anyone under it. Broken, it falls,
    /// and the heart drops into the snow.
    /// </summary>
    public class Harvester : Machine
    {
        [SerializeField] Transform body, eye, vent, cage;
        [SerializeField] Light eyeLight, ventLight;
        [SerializeField] Renderer eyeRenderer, ventRenderer, shieldRenderer;
        [SerializeField] LineRenderer beam;
        [SerializeField] Transform[] hips = new Transform[3];
        [SerializeField] Transform[] thighs = new Transform[3];
        [SerializeField] Transform[] shins = new Transform[3];
        [SerializeField] AudioSource loop;
        [SerializeField] AudioClip stepClip, chargeClip, beamClip, ventClip, voiceClip;

        public override string Title => "the Harvester";
        public override string Doing => Broken ? "broken" : !awake ? "standing still, watching" : venting ? "venting heat from its back, its shield down" :
            sweeping ? "sweeping its beam across the ground" : charging ? "charging its beam" : "stalking the survivor, its shield up";
        public bool IsAwake => awake;
        public bool Venting => venting;
        public Holdable Heart { get; private set; }
        /// <summary>It woke (it saw the survivor up close).</summary>
        public event Action Woke;
        /// <summary>It finished its scan of the survivor and said what it saw.</summary>
        public event Action Spoke;

        Transform target;
        Machines makers;
        Vector3 home;
        bool awake, charging, sweeping, venting, spoken;
        float nextAttack, nextPack, legPhase;
        readonly Vector3[] feet = new Vector3[3], footFrom = new Vector3[3];
        readonly float[] stepT = { 1f, 1f, 1f };
        int stepping = -1;
        Material eyeMaterial, ventMaterial, shieldMaterial;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int RevealId = Shader.PropertyToID("_Reveal");

        const float Tall = 6.2f;

        public void Set(Transform survivor, Machines spawner, Holdable heart)
        {
            target = survivor;
            makers = spawner;
            Heart = heart;
            if (!heart) return;
            heart.Locked = true;
            var heartBody = heart.GetComponent<Rigidbody>();
            if (heartBody) heartBody.isKinematic = true;
            foreach (var c in heart.GetComponentsInChildren<Collider>()) c.enabled = false;
            heart.transform.SetParent(cage, false);
            heart.transform.localPosition = new Vector3(0f, -0.6f, 0f);
            heart.transform.localRotation = Quaternion.identity;
        }

        protected override void Awake()
        {
            health = 420f;
            base.Awake();
            home = transform.position;
            if (eyeRenderer) eyeMaterial = eyeRenderer.material;
            if (ventRenderer) ventMaterial = ventRenderer.material;
            if (shieldRenderer) shieldMaterial = shieldRenderer.material;
            for (int i = 0; i < 3; i++)
            {
                feet[i] = FootHome(i);
                footFrom[i] = feet[i];
            }
            if (beam) beam.enabled = false;
            nextAttack = Time.time + 6f;
            nextPack = Time.time + 25f;
        }

        protected override float WeakRadius => 1.6f;

        protected override float Resist(string kind, Vector3 at)
        {
            if (!awake) Wake();
            // The shield turns aside all but a little, except while it vents; its own beam thrown back goes through.
            if (kind == "REFLECTED") return 1f;
            return venting ? 1f : 0.08f;
        }

        public void Wake()
        {
            if (awake) return;
            awake = true;
            Woke?.Invoke();
        }

        void Update()
        {
            if (Broken || !target) return;
            float dt = Time.deltaTime;
            float d = Vector3.Distance(Flat(target.position), Flat(transform.position));
            if (!awake && d < 38f) StartCoroutine(Scan());
            // It closes slowly, keeping some distance, and turns to face its prey.
            Vector3 to = Flat(target.position - transform.position);
            if (awake && !sweeping && !charging && !Stunned)
            {
                float want = d > 18f ? 1f : d < 11f ? -0.4f : 0f;
                Vector3 move = to.normalized * want * 1.6f * dt;
                Vector3 next = transform.position + move;
                if (Vector2.Distance(new Vector2(next.x, next.z), new Vector2(home.x, home.z)) < 40f) transform.position = next;
            }
            if (awake && to.sqrMagnitude > 0.1f && !sweeping)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to, Vector3.up), 1f - Mathf.Exp(-0.8f * dt));
            Walk(dt);
            // The shield: a red shimmer, gone while it vents.
            if (shieldMaterial) shieldMaterial.SetFloat(RevealId, Mathf.MoveTowards(shieldMaterial.GetFloat(RevealId), awake && !venting ? 1f : 0f, dt * 2f));
            if (ventMaterial) ventMaterial.SetColor(BaseColorId, venting ? new Color(4f, 1.3f, 0.3f) * (0.8f + 0.2f * Mathf.Sin(Time.time * 9f)) : new Color(0.25f, 0.08f, 0.04f));
            if (ventLight) ventLight.intensity = venting ? 5f : 0f;
            if (!awake || Stunned) return;
            if (Time.time > nextAttack && !sweeping && !charging && !venting && d < 45f) StartCoroutine(Sweep());
            if (Time.time > nextPack && makers && d < 50f)
            {
                nextPack = Time.time + (Fraction < 0.5f ? 22f : 32f);
                makers.Seekers(transform.position + transform.forward * 4f + Vector3.up * 3f, 2, 3f);
            }
            if (d < 6.5f && !sweeping && Time.time > nextAttack - 3f) StartCoroutine(Stomp());
        }

        /// <summary>The first sight of the survivor: it stops, looks them over with a red light, and speaks.</summary>
        IEnumerator Scan()
        {
            if (spoken) yield break;
            spoken = true;
            Wake();
            float began = Time.time;
            while (Time.time - began < 2.6f)
            {
                if (beam)
                {
                    beam.enabled = true;
                    float sweep = Mathf.Sin((Time.time - began) * 4f) * 0.8f;
                    Vector3 at = target.position + Vector3.up * (1f + sweep);
                    beam.SetPosition(0, eye.position);
                    beam.SetPosition(1, at);
                    beam.widthMultiplier = 0.06f;
                    beam.startColor = new Color(1f, 0.2f, 0.15f, 0.5f);
                    beam.endColor = new Color(1f, 0.2f, 0.15f, 0.2f);
                }
                Eye(1.5f);
                yield return null;
            }
            if (beam) beam.enabled = false;
            if (sound && voiceClip) sound.PlayOneShot(voiceClip, 1f);
            Spoke?.Invoke();
            nextAttack = Time.time + 7f;
        }

        IEnumerator Sweep()
        {
            charging = true;
            if (sound && chargeClip) sound.PlayOneShot(chargeClip, 1f);
            for (float t = 0f; t < 2f; t += Time.deltaTime)
            {
                Eye(1f + 3f * t / 2f);
                if (Broken) yield break;
                yield return null;
            }
            charging = false;
            sweeping = true;
            // Across the survivor, from one side to the other, at the height of a chest.
            Vector3 toward = Flat(target.position - transform.position).normalized;
            float arc = Fraction < 0.5f ? 110f : 80f, seconds = Fraction < 0.5f ? 2.2f : 2.8f;
            float from = -arc * 0.5f * (UnityEngine.Random.value < 0.5f ? 1f : -1f);
            if (loop && beamClip)
            {
                loop.clip = beamClip;
                loop.loop = true;
                loop.Play();
            }
            if (beam) beam.enabled = true;
            for (float t = 0f; t < seconds && !Broken; t += Time.deltaTime)
            {
                float angle = Mathf.Lerp(from, -from, t / seconds);
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * toward;
                Vector3 aim = Flat(transform.position) + dir * 30f + Vector3.up * (target.position.y + 1.1f);
                Vector3 start = eye.position;
                Vector3 end = Burn(start, (aim - start).normalized, 70f);
                if (beam)
                {
                    beam.SetPosition(0, start);
                    beam.SetPosition(1, end);
                    beam.widthMultiplier = 0.32f + 0.08f * Mathf.Sin(Time.time * 50f);
                    beam.startColor = new Color(1f, 0.25f, 0.15f, 1f);
                    beam.endColor = new Color(1f, 0.15f, 0.08f, 0.8f);
                }
                Eye(4f);
                yield return null;
            }
            if (beam) beam.enabled = false;
            if (loop) loop.Stop();
            sweeping = false;
            yield return Vent();
        }

        /// <summary>The beam along a line this frame: what it burns, thrown back by a mirror. Returns where it ends.</summary>
        Vector3 Burn(Vector3 from, Vector3 dir, float range)
        {
            if (!Physics.Raycast(from, dir, out var hit, range, Shots.Mask, QueryTriggerInteraction.Ignore)) return from + dir * range;
            if (UnityEngine.Random.value < 0.3f) Fx.Sparks(hit.point, 2, Shots.MachineRed);
            var warmth = hit.collider.GetComponentInParent<Warmth>();
            if (warmth)
            {
                warmth.Hit(0.45f * Time.deltaTime, from);
                return hit.point;
            }
            var mirror = hit.collider.GetComponentInParent<MirrorWall>();
            if (mirror)
            {
                // Its own beam, back into it: that hurts, shield or not.
                Fx.Bolt(hit.point, eye.position, Shots.Cyan, 0.25f, 0.05f);
                Damage(55f * Time.deltaTime, eye.position, "REFLECTED");
                mirror.Flash(hit.point);
                return hit.point;
            }
            var decoy = hit.collider.GetComponentInParent<Decoy>();
            if (decoy && UnityEngine.Random.value < 0.05f) decoy.Hit();
            return hit.point;
        }

        IEnumerator Vent()
        {
            venting = true;
            if (sound && ventClip) sound.PlayOneShot(ventClip, 1f);
            Fx.Sparks(vent.position, 30, new Color(1f, 0.6f, 0.3f));
            for (float t = 0f; t < 5.5f && !Broken; t += Time.deltaTime)
            {
                if (UnityEngine.Random.value < Time.deltaTime * 10f) Fx.Sparks(vent.position + UnityEngine.Random.insideUnitSphere * 0.5f, 3, new Color(1f, 0.55f, 0.25f));
                yield return null;
            }
            venting = false;
            nextAttack = Time.time + (Fraction < 0.5f ? 4f : 6.5f);
        }

        IEnumerator Stomp()
        {
            nextAttack = Time.time + 4f;
            int leg = UnityEngine.Random.Range(0, 3);
            Vector3 down = Flat(target.position) + Vector3.up * Ground(target.position);
            footFrom[leg] = feet[leg];
            for (float t = 0f; t < 0.7f; t += Time.deltaTime)
            {
                float k = t / 0.7f;
                feet[leg] = Vector3.Lerp(footFrom[leg], down, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 3f;
                yield return null;
            }
            feet[leg] = down;
            if (sound && stepClip) sound.PlayOneShot(stepClip, 1f);
            Fx.Burst(down, 0.8f);
            if (Vector3.Distance(Flat(target.position), Flat(down)) < 3.5f)
            {
                var warmth = target.GetComponent<Warmth>();
                if (warmth) warmth.Hit(0.3f, down);
            }
        }

        /// <summary>Three legs, one step at a time: a foot too far from where it belongs lifts and comes down nearer.</summary>
        void Walk(float dt)
        {
            float bodyY = 0f;
            for (int i = 0; i < 3; i++) bodyY += feet[i].y;
            bodyY = bodyY / 3f + Tall;
            transform.position = new Vector3(transform.position.x, Mathf.Lerp(transform.position.y, bodyY, 1f - Mathf.Exp(-3f * dt)), transform.position.z);
            if (stepping < 0)
            {
                int worst = -1;
                float far = 2.2f;
                for (int i = 0; i < 3; i++)
                {
                    float off = Vector3.Distance(Flat(feet[i]), Flat(FootHome(i)));
                    if (off > far) { far = off; worst = i; }
                }
                if (worst >= 0)
                {
                    stepping = worst;
                    stepT[worst] = 0f;
                    footFrom[worst] = feet[worst];
                }
            }
            else
            {
                stepT[stepping] += dt / 0.75f;
                float k = Mathf.Clamp01(stepT[stepping]);
                Vector3 goal = FootHome(stepping);
                feet[stepping] = Vector3.Lerp(footFrom[stepping], goal, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 1.4f;
                if (k >= 1f)
                {
                    feet[stepping] = goal;
                    if (sound && stepClip) sound.PlayOneShot(stepClip, 0.8f);
                    stepping = -1;
                }
            }
            // Each leg: a thigh from the hip out to a knee raised above the line, a shin down to the foot.
            for (int i = 0; i < 3; i++)
            {
                Vector3 hip = hips[i].position, foot = feet[i];
                Vector3 outward = Flat(hip - transform.position).normalized;
                Vector3 knee = Vector3.Lerp(hip, foot, 0.45f) + Vector3.up * 3.2f + outward * 2.4f;
                Span(thighs[i], hip, knee, 1.1f);
                Span(shins[i], knee, foot, 0.8f);
            }
        }

        static void Span(Transform part, Vector3 from, Vector3 to, float thick)
        {
            Vector3 along = to - from;
            part.position = (from + to) * 0.5f;
            part.rotation = Quaternion.FromToRotation(Vector3.up, along.normalized);
            part.localScale = new Vector3(thick, along.magnitude * 0.5f, thick);
        }

        Vector3 FootHome(int i)
        {
            Vector3 flat = Flat(transform.position) + Quaternion.Euler(0f, transform.eulerAngles.y + 120f * i + 60f, 0f) * Vector3.forward * 7f;
            return new Vector3(flat.x, Ground(flat), flat.z);
        }

        static readonly RaycastHit[] hits = new RaycastHit[8];

        float Ground(Vector3 at)
        {
            int n = Physics.RaycastNonAlloc(new Vector3(at.x, at.y + 30f, at.z), Vector3.down, hits, 90f, Shots.Mask, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            for (int i = 0; i < n; i++)
                if (!hits[i].collider.GetComponentInParent<Machine>() && !hits[i].collider.GetComponentInParent<Traveller>() && hits[i].point.y > best) best = hits[i].point.y;
            return float.IsNegativeInfinity(best) ? home.y - Tall : best;
        }

        void Eye(float strength)
        {
            if (eyeLight) eyeLight.intensity = 3f * strength;
            if (eyeMaterial) eyeMaterial.SetColor(BaseColorId, new Color(3f, 0.3f, 0.15f) * strength);
        }

        protected override IEnumerator Fall()
        {
            if (beam) beam.enabled = false;
            if (loop) loop.Stop();
            charging = sweeping = venting = false;
            if (shieldRenderer) shieldRenderer.enabled = false;
            if (Heart)
            {
                // The cage gives; the heart drops into the snow, free.
                var heart = Heart;
                heart.transform.SetParent(null, true);
                heart.Locked = false;
                foreach (var c in heart.GetComponentsInChildren<Collider>()) c.enabled = true;
                var heartBody = heart.GetComponent<Rigidbody>();
                if (!heartBody) heartBody = heart.gameObject.AddComponent<Rigidbody>();
                heartBody.isKinematic = false;
                heartBody.mass = Mathf.Max(heartBody.mass, 30f);
                heartBody.AddForce(transform.forward * 3f + Vector3.up * 2f, ForceMode.VelocityChange);
            }
            Fx.Burst(transform.position, 2.5f);
            // The body sinks onto its legs and tips over; the eye goes out.
            Vector3 from = transform.position;
            var turn = transform.rotation;
            for (float t = 0f; t < 2.5f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 2.5f);
                transform.position = Vector3.Lerp(from, new Vector3(from.x, from.y - Tall + 1.5f, from.z), k);
                transform.rotation = Quaternion.Slerp(turn, turn * Quaternion.Euler(25f, 0f, 18f), k);
                Eye(1f - k);
                if (UnityEngine.Random.value < 0.3f) Fx.Sparks(transform.position + UnityEngine.Random.insideUnitSphere * 3f, 8);
                yield return null;
            }
            Eye(0f);
            foreach (var light in GetComponentsInChildren<Light>()) light.enabled = false;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
