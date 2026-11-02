using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The last mind: a small orb of light that floats at the traveller's shoulder. What it holds turns
    /// around it in a ring, piece by piece (stone, ice, metal); its own light is the light it holds,
    /// so it dims when it spends light and brightens when it takes in an ember. It shimmers while it
    /// thinks, and answers with one mark beside itself: a stroke (yes, done) or a ring (no), a little
    /// different each time.
    /// </summary>
    public class Companion : MonoBehaviour
    {
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int PulseId = Shader.PropertyToID("_Pulse");
        static readonly int InstabilityId = Shader.PropertyToID("_Instability");
        static readonly int MidId = Shader.PropertyToID("_Mid");
        static readonly int HotId = Shader.PropertyToID("_Hot");

        [SerializeField] Transform traveller;
        [SerializeField, Tooltip("Faces the way the traveller walks.")] Transform travellerBody;
        [SerializeField] Renderer core;
        [SerializeField] Renderer corona;
        [SerializeField] Renderer glow;
        [SerializeField] Light shine;
        [SerializeField] LineRenderer mark;
        [SerializeField] AudioSource hum;
        [SerializeField] AudioSource voice;
        [SerializeField] AudioClip yesClip, noClip, cannotClip, takeClip, emberClip, thinkClip;
        [SerializeField, Tooltip("A piece of each matter as it turns in the ring: stone, ice, metal, light.")] Mesh pieceMesh;
        [SerializeField] Material[] pieceMaterials = new Material[4];
        [SerializeField] Vector3 offset = new Vector3(0.85f, 2.05f, -0.45f);
        [SerializeField] int startLight = 3;
        [SerializeField, Tooltip("Pieces shown in the ring at most; more are held but not shown.")] int shownAtMost = 60;

        public event Action Changed;

        /// <summary>Shimmers while it reads what was said.</summary>
        public bool Thinking { get; set; }
        /// <summary>When set, it drifts toward this point (to show a plan), else it keeps to the shoulder.</summary>
        public Vector3? Attending { get; set; }
        public int LightHeld => Count(Matter.Light);
        /// <summary>0 at the shoulder; toward 1 it rises above the traveller and grows into the orb.</summary>
        public float Ascent { get; set; }

        readonly int[] held = new int[4];
        readonly List<(Transform piece, Matter matter, float angle, float radius, float tilt)> ring = new List<(Transform, Matter, float, float, float)>();
        Vector3 velocity;
        float coreIntensity, coronaIntensity, glowIntensity, shineIntensity, shineRange;
        float think, markShown = -10f, flare, dim;
        Material coreMat, coronaMat, glowMat;
        Vector3 coreScale, coronaScale, glowScale;
        Color warmMid, warmHot;
        readonly System.Random random = new System.Random(5);

        void Awake()
        {
            coreMat = core.material;
            coronaMat = corona.material;
            glowMat = glow.material;
            coreIntensity = coreMat.GetFloat(IntensityId);
            coronaIntensity = coronaMat.GetFloat(IntensityId);
            glowIntensity = glowMat.GetFloat(IntensityId);
            shineIntensity = shine.intensity;
            shineRange = shine.range;
            coreScale = core.transform.localScale;
            coronaScale = corona.transform.localScale;
            glowScale = glow.transform.localScale;
            warmMid = coreMat.GetColor(MidId);
            warmHot = coreMat.GetColor(HotId);
            if (mark) mark.enabled = false;
        }

        void Start()
        {
            Add(Matter.Light, startLight);
            Snap();
        }

        public int Count(Matter m) => held[(int)m];

        /// <summary>Takes in a loose piece: it flies here from where it lay, then joins the ring.</summary>
        public void Give(Matter matter, int units, Transform from)
        {
            StartCoroutine(Fly(from, units, matter));
        }

        /// <summary>Adds held material at once (tests, and pieces given back).</summary>
        public void Add(Matter matter, int units)
        {
            held[(int)matter] += units;
            for (int i = 0; i < units; i++) AddPiece(matter);
            Changed?.Invoke();
        }

        /// <summary>Takes pieces out of the ring for building; their places now, to fly from.</summary>
        public List<Vector3> Take(Matter matter, int units)
        {
            var from = new List<Vector3>();
            units = Mathf.Min(units, held[(int)matter]);
            held[(int)matter] -= units;
            for (int i = ring.Count - 1; i >= 0 && from.Count < units; i--)
            {
                if (ring[i].matter != matter) continue;
                from.Add(ring[i].piece.position);
                Destroy(ring[i].piece.gameObject);
                ring.RemoveAt(i);
            }
            while (from.Count < units) from.Add(transform.position);
            Changed?.Invoke();
            return from;
        }

        /// <summary>Takes in an ember: a lot of light at once, and a flare.</summary>
        public void Ember(int light)
        {
            Add(Matter.Light, light);
            flare = 1f;
            Play(emberClip, 0.9f);
        }

        /// <summary>One stroke (yes, done), or a ring (no). Never the same twice.</summary>
        public void Mark(bool yes)
        {
            if (!mark) return;
            markShown = Time.time;
            mark.enabled = true;
            float lean = (float)(random.NextDouble() - 0.5) * 18f;
            float size = 0.42f + (float)random.NextDouble() * 0.12f;
            float wobble = (float)random.NextDouble() * 10f;
            int count = yes ? 18 : 48;
            mark.positionCount = count;
            mark.loop = !yes;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                Vector3 p;
                if (yes)
                {
                    // A stroke from top to bottom, slightly bent, thinner at its end.
                    p = new Vector3(Mathf.Sin(t * 2.4f + wobble) * 0.03f, (0.5f - t) * size * 1.6f, 0f);
                    p = Quaternion.Euler(0f, 0f, lean) * p;
                }
                else
                {
                    float a = t * Mathf.PI * 2f;
                    float r = size * (0.42f + 0.03f * Mathf.Sin(a * 3f + wobble));
                    p = new Vector3(Mathf.Cos(a) * r * 0.82f, Mathf.Sin(a) * r, 0f);
                }
                mark.SetPosition(i, p);
            }
            Play(yes ? yesClip : noClip, 0.8f);
        }

        /// <summary>A soft falling breath: it cannot do what was asked.</summary>
        public void Cannot()
        {
            Mark(false);
            Play(cannotClip, 0.7f);
        }

        /// <summary>It does not know, or did not understand: its light sinks for a moment.</summary>
        public void Unsure()
        {
            dim = 1f;
            Play(cannotClip, 0.4f);
        }

        /// <summary>Jumps to its place by the traveller (after a fall, at the start).</summary>
        public void Snap()
        {
            transform.position = Home();
            velocity = Vector3.zero;
        }

        Vector3 Home()
        {
            var facing = travellerBody ? Quaternion.Euler(0f, travellerBody.eulerAngles.y, 0f) : Quaternion.identity;
            return traveller.position + facing * offset;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Vector3 home = Home() + new Vector3(0f, Mathf.Sin(Time.time * 1.1f) * 0.08f, 0f);
            if (Attending.HasValue)
            {
                // Halfway out toward what it is showing, but never far from the traveller.
                Vector3 toward = Attending.Value - home;
                home += Vector3.ClampMagnitude(toward * 0.35f, 3.5f) + Vector3.up * 0.6f;
            }
            if (Ascent > 0f)
            {
                home = traveller.position + Vector3.up * (2.2f + 9f * Ascent) + (travellerBody ? travellerBody.forward : Vector3.forward) * (2f + 5f * Ascent);
                float grow = 1f + 6f * Ascent * Ascent;
                core.transform.localScale = coreScale * grow;
                corona.transform.localScale = coronaScale * grow;
                glow.transform.localScale = glowScale * grow;
                // Its warm light cools toward the colours of the orb at the beginning.
                coreMat.SetColor(MidId, Color.Lerp(warmMid, new Color(0.5f, 0.06f, 0.7f), Ascent));
                coreMat.SetColor(HotId, Color.Lerp(warmHot, new Color(0.2f, 0.75f, 1f), Ascent));
            }
            transform.position = Vector3.SmoothDamp(transform.position, home, ref velocity, 0.45f, 12f);

            // Light: what it holds of it, eased; a shimmer while thinking; a flare after an ember.
            think = Mathf.MoveTowards(think, Thinking ? 1f : 0f, dt * 2.5f);
            flare = Mathf.MoveTowards(flare, 0f, dt * 0.35f);
            dim = Mathf.MoveTowards(dim, 0f, dt * 0.8f);
            float strength = Mathf.Clamp(0.35f + 0.11f * LightHeld, 0.35f, 2.2f) * (1f - 0.6f * Mathf.Sin(dim * Mathf.PI * 0.5f)) * (1f + 3f * Ascent);
            float shimmer = think * (0.5f + 0.5f * Mathf.Sin(Time.time * 17f)) * 0.6f;
            float pulse = Mathf.Clamp01(shimmer + flare);
            coreMat.SetFloat(IntensityId, coreIntensity * strength);
            coreMat.SetFloat(PulseId, pulse);
            coreMat.SetFloat(InstabilityId, think * 0.4f);
            coronaMat.SetFloat(IntensityId, coronaIntensity * strength);
            coronaMat.SetFloat(PulseId, pulse);
            glowMat.SetFloat(IntensityId, glowIntensity * strength);
            glowMat.SetFloat(PulseId, pulse);
            shine.intensity = shineIntensity * strength * (1f + flare * 1.5f + shimmer * 0.3f);
            shine.range = shineRange * Mathf.Sqrt(strength);
            if (hum)
            {
                hum.volume = Mathf.Lerp(hum.volume, 0.25f + 0.25f * strength + think * 0.2f, dt * 3f);
                hum.pitch = Mathf.Lerp(hum.pitch, 0.9f + 0.08f * strength + think * 0.08f, dt * 3f);
            }

            TurnRing(dt);
            ShowMark();
        }

        void TurnRing(float dt)
        {
            for (int i = 0; i < ring.Count; i++)
            {
                var (piece, matter, angle, radius, tilt) = ring[i];
                angle += dt * (0.7f + 0.25f * (i % 3)) / radius;
                ring[i] = (piece, matter, angle, radius, tilt);
                // Rising, it draws what it holds into itself.
                float r = radius * (1f - 0.9f * Ascent);
                var turn = Quaternion.Euler(tilt, 0f, 18f);
                Vector3 local = turn * new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
                piece.position = Vector3.Lerp(piece.position, transform.position + local, 1f - Mathf.Exp(-10f * dt));
                piece.Rotate(new Vector3(37f, 53f, 21f) * dt, Space.Self);
            }
        }

        void ShowMark()
        {
            if (!mark || !mark.enabled) return;
            float age = Time.time - markShown;
            var camera = Camera.main;
            if (camera)
            {
                mark.transform.position = transform.position + camera.transform.right * 0.75f + Vector3.up * 0.15f;
                mark.transform.rotation = camera.transform.rotation;
            }
            float alpha = Mathf.Clamp01(age * 5f) * Mathf.Clamp01((2.6f - age) / 0.6f);
            var colour = new Color(1f, 0.92f, 0.78f, alpha);
            mark.startColor = mark.endColor = colour;
            if (age > 2.6f) mark.enabled = false;
        }

        void AddPiece(Matter matter)
        {
            if (ring.Count >= shownAtMost || pieceMesh == null) return;
            var piece = new GameObject(Matters.Word(matter)).transform;
            piece.SetParent(transform.parent, false);
            piece.position = transform.position;
            piece.localScale = Vector3.one * (matter == Matter.Light ? 0.07f : 0.1f);
            piece.gameObject.AddComponent<MeshFilter>().sharedMesh = pieceMesh;
            var renderer = piece.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = pieceMaterials[(int)matter];
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            float radius = 0.48f + 0.09f * (ring.Count % 4) + (float)random.NextDouble() * 0.05f;
            float tilt = -8f + 16f * (float)random.NextDouble();
            ring.Add((piece, matter, (float)random.NextDouble() * Mathf.PI * 2f, radius, tilt));
        }

        IEnumerator Fly(Transform piece, int units, Matter matter)
        {
            Vector3 start = piece.position;
            Vector3 startScale = piece.localScale;
            Play(takeClip, 0.7f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.55f)
            {
                float k = t * t * (3f - 2f * t);
                Vector3 arc = Vector3.up * Mathf.Sin(k * Mathf.PI) * 1.2f;
                piece.position = Vector3.Lerp(start, transform.position, k) + arc;
                piece.localScale = Vector3.Lerp(startScale, startScale * 0.15f, k);
                piece.Rotate(new Vector3(200f, 300f, 0f) * Time.deltaTime);
                yield return null;
            }
            Destroy(piece.gameObject);
            Add(matter, units);
            flare = Mathf.Max(flare, 0.25f);
        }

        void Play(AudioClip clip, float volume)
        {
            if (voice && clip) voice.PlayOneShot(clip, volume);
        }

        /// <summary>One line for the mind's instructions: what it holds now.</summary>
        public string Holding()
        {
            var parts = new List<string>();
            foreach (var m in Matters.All) parts.Add($"{Matters.Word(m)} {held[(int)m]}");
            return string.Join(", ", parts);
        }
    }
}
