using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The drone: a white shell the size of a head with one round eye, made by the vault's people. It
    /// keeps to the survivor's shoulder and looks where they look; when they speak or write to it, it
    /// comes round in front of them and turns its eye to them, the iris opening to listen. While it
    /// thinks the iris narrows and turns. It answers in chirps (<see cref="DroneVoice"/>) with its words
    /// written under the view, and a gesture for its mood: a nod, a shake, a tilt, a spin, a droop. It
    /// flies off to look, make and carry, and its eye throws a beam at what it shapes or holds.
    /// </summary>
    public class Drone : MonoBehaviour
    {
        [SerializeField] Traveller traveller;
        [SerializeField] Transform travellerBody;
        [SerializeField] FollowCamera follow;
        [SerializeField, Tooltip("Turns to face; the eye looks along its forward.")] Transform shell;
        [SerializeField] Transform iris;
        [SerializeField] Renderer irisRenderer;
        [SerializeField] Renderer glow;
        [SerializeField] Light shine;
        [SerializeField] LineRenderer beam;
        [SerializeField] Eye eye;
        [SerializeField] AudioSource hum;
        [SerializeField] AudioSource voice;
        [SerializeField] AudioClip moveClip;
        [SerializeField, Tooltip("Speaks in words (ElevenLabs) rather than chirps, when it can.")] bool spoken = true;
        [SerializeField, Range(0f, 1f), Tooltip("Its lines peak at full scale: this keeps them clear of clipping, and above the footsteps.")] float voiceVolume = 0.85f;
        [SerializeField, Tooltip("Its own small sounds before a line now and then, by mood, and a hum while it thinks.")] AudioClip curiousChirp, nervousChirp, happyChirp, sadChirp, thinkingHum;
        [SerializeField, Tooltip("Letters a second the subtitles leave time for: the next line waits until this one could be read.")] float readingSpeed = 15f;
        [SerializeField, Tooltip("Snow flung off the shell when it shakes.")] ParticleSystem snowOff;
        [SerializeField] Vector3 offset = new Vector3(0.8f, 1.95f, -0.2f);
        [SerializeField] Color warm = new Color(1f, 0.62f, 0.25f);
        [SerializeField, Tooltip("How far it will go from the survivor.")] float range = 40f;

        public Eye Eye => eye;
        public float Range => range;
        /// <summary>The survivor holds the key to speak, or is writing: it comes round to face them.</summary>
        public bool Listening { get; set; }
        /// <summary>The voice's loudness while listening, 0 to 1.</summary>
        public float Level { get; set; }
        public bool Thinking { get; set; }
        public bool Speaking => speakingUntil > Time.time;
        /// <summary>The line it is saying now, or null.</summary>
        public string Saying { get; private set; }
        /// <summary>A line is being fetched or said, or one waits its turn: a quick line now would be dropped.</summary>
        public bool Busy => heldUntil > Time.time || First() != int.MaxValue;

        /// <summary>
        /// What a line is, which decides its turn: an answer to the survivor cuts in on anything else; a line said
        /// again after being cut comes next; a story line (scripted) waits while the survivor speaks or is answered;
        /// an aside (a ramble, an idle thought) is the least and is never said again.
        /// </summary>
        public enum Kind { Aside, Story, Resume, Answer }
        /// <summary>The survivor is speaking, or being answered: stories and asides wait.</summary>
        public bool Quiet { get; set; }
        /// <summary>A story line was cut off (by the survivor speaking, or by an answer): the line and its mood, to say again.</summary>
        public event System.Action<string, string> Cut;
        /// <summary>Its light, 1 normally; the ending burns it high, then out.</summary>
        public float Brightness { get; set; } = 1f;
        /// <summary>Its eye's colour; warm unless changed.</summary>
        public Color Colour { get; set; }
        /// <summary>When set, it flies here and looks at <see cref="LookAt"/> instead of keeping to the shoulder.</summary>
        public Vector3? GoTo { get; set; }
        public Vector3? LookAt { get; set; }
        /// <summary>No longer flies: it drops and lies where it falls (the end).</summary>
        public bool Fallen { get; set; }

        Vector3 velocity, glowScale;
        float think, open = 1f, speakingUntil, gestureAt = -10f, chirp, speakStart, moodUntil, heldUntil;
        int tickets;
        // Lines waiting for the voice, by ticket, with when each was last seen waiting.
        readonly Dictionary<int, float> waiting = new Dictionary<int, float>();
        readonly Dictionary<int, Kind> kinds = new Dictionary<int, Kind>();
        int speakingTicket = -1, hushedTicket = -1;
        Kind speakingKind;
        string speakingMood;
        Hud speakingHud;
        float[] envelope;
        string gesture = "", mood = "CALM";
        Color moodColour;
        Material irisMaterial;
        float shineIntensity;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>How it feels now (it eases back to calm a while after a line).</summary>
        public string Mood => moodUntil > Time.time ? mood : "CALM";

        void Awake()
        {
            Colour = warm;
            moodColour = warm;
            irisMaterial = irisRenderer ? irisRenderer.material : null;
            shineIntensity = shine ? shine.intensity : 1f;
            if (glow) glowScale = glow.transform.localScale;
            if (beam) beam.enabled = false;
        }

        void Start() => Snap();

        /// <summary>Jumps to its place by the survivor (after a fall, at the start).</summary>
        public void Snap()
        {
            transform.position = Home();
            velocity = Vector3.zero;
        }

        /// <summary>A machine is near: it keeps the survivor between itself and the machine.</summary>
        public Vector3? Cover { get; set; }
        /// <summary>Its rounds while nothing is asked of it: the pods' panels near the survivor, checked one by one.</summary>
        public Transform[] Chores { get; set; }
        /// <summary>Outside the vault: close and low behind the survivor's shoulder, glancing about, now and then hiding.</summary>
        public bool Timid { get; set; }
        /// <summary>Ducked behind the survivor just now (outside).</summary>
        public bool Hiding => Timid && Time.time < hideUntil;

        Transform chore;
        float choreUntil, restUntil = -1f, scanFrom = -1f, hideUntil, nextHide = 12f;
        bool scanning;

        /// <summary>Where its rounds take it now, if anywhere: in front of a panel, a little above it.</summary>
        Vector3? Chore()
        {
            float now = Time.time;
            bool called = Listening || Thinking || GoTo.HasValue || Cover.HasValue || Fallen || Speaking && speakingKind != Kind.Aside;
            if (Chores == null || Chores.Length == 0 || called)
            {
                if (chore) restUntil = now + Random.Range(4f, 8f);
                EndChore();
                return null;
            }
            Vector3 me = traveller.transform.position;
            if (chore && (now > choreUntil || Vector3.Distance(chore.position, me) > 9f))
            {
                // Another panel next to it, or back to the survivor's shoulder for a while.
                bool more = Random.value < 0.55f;
                EndChore();
                if (!more) restUntil = now + Random.Range(7f, 13f);
            }
            if (!chore)
            {
                if (now < restUntil) return null;
                var near = Chores.Where(c => c && Vector3.Distance(c.position, me) < 7.5f).ToArray();
                if (near.Length == 0) return null;
                chore = near[Random.Range(0, near.Length)];
                choreUntil = now + Random.Range(4.5f, 7f);
                scanFrom = -1f;
            }
            Vector3 spot = chore.position + chore.forward * 0.55f + Vector3.up * 0.14f;
            if (scanFrom < 0f && (transform.position - spot).sqrMagnitude < 0.1f) scanFrom = now;
            // Once there it scans the panel: a pale beam that sweeps down its screen.
            float scan = scanFrom < 0f ? -1f : now - scanFrom;
            if (scan >= 0f && scan < 1.6f)
            {
                scanning = true;
                Beam(chore.position + chore.up * (0.09f - 0.12f * Mathf.PingPong(scan * 1.6f, 1f)), new Color(0.6f, 0.9f, 1f));
            }
            else if (scanning)
            {
                scanning = false;
                Beam(null);
            }
            return spot;
        }

        void EndChore()
        {
            if (scanning) Beam(null);
            scanning = false;
            chore = null;
            scanFrom = -1f;
        }

        // Outside it keeps glancing about, at the open snow and the sky, as if something might come.
        Vector3 Glance()
        {
            Vector3 forward = travellerBody ? Flat(travellerBody.forward).normalized : Vector3.forward;
            float t = Time.time;
            float turn = 55f * Mathf.Sin(t * 0.55f) + 25f * Mathf.Sin(t * 1.7f + 1.3f);
            return traveller.transform.position + Quaternion.Euler(0f, turn, 0f) * forward * 8f + Vector3.up * (1.5f + 1.4f * Mathf.Max(0f, Mathf.Sin(t * 0.37f)));
        }

        Vector3 Home()
        {
            if (Cover.HasValue)
            {
                Vector3 away = Flat(traveller.transform.position - Cover.Value);
                away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.back;
                return traveller.transform.position + away * 0.75f + Vector3.Cross(Vector3.up, away) * 0.35f + Vector3.up * 1.45f;
            }
            var facing = travellerBody ? Quaternion.Euler(0f, travellerBody.eulerAngles.y, 0f) : Quaternion.identity;
            if (Timid)
            {
                // Close behind the shoulder, low; now and then it ducks behind their head for a moment.
                if (Time.time > nextHide)
                {
                    hideUntil = Time.time + Random.Range(1.8f, 3.2f);
                    nextHide = hideUntil + Random.Range(8f, 16f);
                }
                var tucked = Hiding ? new Vector3(0.22f, 1.5f, -0.45f) : new Vector3(0.45f, 1.55f, -0.38f);
                return traveller.transform.position + facing * tucked;
            }
            return traveller.transform.position + facing * offset;
        }

        /// <summary>In front of the survivor, a little to the side, at the height of their face, where the camera sees it.</summary>
        Vector3 Before()
        {
            var camera = follow.View.transform;
            Vector3 forward = Flat(camera.forward).normalized;
            Vector3 right = Flat(camera.right).normalized;
            return traveller.transform.position + forward * 1.15f + right * 0.95f + Vector3.up * 1.7f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (Fallen)
            {
                Fall(dt);
                return;
            }
            Vector3 camera = follow.View.transform.position;
            // It turns to the survivor when they talk to it and when it answers; its asides it says over its work.
            bool facing = Listening || Speaking && !GoTo.HasValue && !(chore && speakingKind == Kind.Aside);
            Vector3? job = facing ? null : Chore();
            Vector3 target = GoTo ?? (facing ? Before() : job ?? Home());
            float t = Time.time;
            string feeling = Mood;
            float lively = feeling is "HAPPY" or "PLAYFUL" ? (Speaking ? 1f : 0.5f) : 0f;
            float low = feeling is "SAD" or "HURT" ? 1f : 0f;
            target += new Vector3(Mathf.Sin(t * (0.9f + 1.5f * lively)) * (0.03f + 0.05f * lively),
                Mathf.Sin(t * (1.7f + 2.8f * lively) * (1f - 0.35f * low)) * (0.06f + 0.08f * lively), 0f);
            if (low > 0f && !GoTo.HasValue) target += Vector3.down * 0.1f;
            float gestureAge = Time.time - gestureAt;
            if (gesture == "DROOP" && gestureAge >= 0f && gestureAge < 2.6f) target += Vector3.down * 0.18f;
            if (gesture == "BACK" && gestureAge >= 0f && gestureAge < 3.5f && !GoTo.HasValue)
            {
                // Away from the survivor, a little higher: out of reach.
                Vector3 away = Flat(transform.position - traveller.transform.position);
                target += (away.sqrMagnitude > 0.01f ? away.normalized : Vector3.back) * 1.6f + Vector3.up * 0.5f;
            }
            if (gesture == "BOUNCE" && gestureAge >= 0f) target += Vector3.up * Mathf.Abs(Mathf.Sin(gestureAge * 9f)) * 0.14f * Mathf.Clamp01(1.2f - gestureAge);
            // Far errands are flown fast; close by it drifts, unhurried.
            float speed = GoTo.HasValue ? Mathf.Clamp(Vector3.Distance(transform.position, target) * 0.8f, 6f, 18f) : 5.5f;
            Vector3 from = transform.position;
            Vector3 next = Vector3.SmoothDamp(from, target, ref velocity, GoTo.HasValue ? 0.75f : 0.45f, speed);
            transform.position = Clear(from, next);

            // Where the eye looks: at the survivor's face while talking, at what it was sent to, else where they look.
            // Talking with them, it looks at the survivor's face (not at the camera behind them).
            Vector3 face = traveller.transform.position + Vector3.up * 1.62f;
            Vector3 look = facing ? face : LookAt ?? (job.HasValue ? chore.position : Timid && !Speaking ? Glance()
                : traveller.AimPoint ?? transform.position + (travellerBody ? travellerBody.forward : Vector3.forward) * 6f);
            Vector3 to = look - transform.position;
            var aim = to.sqrMagnitude > 0.01f ? Quaternion.LookRotation(to, Vector3.up) : shell.rotation;
            // It leans into its flight a little.
            Vector3 local = Quaternion.Inverse(aim) * velocity;
            aim *= Quaternion.Euler(Mathf.Clamp(local.z * 3f, -14f, 14f), 0f, Mathf.Clamp(-local.x * 4f, -18f, 18f));
            shell.rotation = Quaternion.Slerp(shell.rotation, aim * Gesture(), 1f - Mathf.Exp(-7f * dt));

            // The iris: wide to listen, narrow and turning to think, pulsing with its chirps.
            think = Mathf.MoveTowards(think, Thinking ? 1f : 0f, dt * 3f);
            HumWhileThinking();
            if (Speaking && envelope != null && envelope.Length > 0)
            {
                int at = Mathf.Clamp((int)((Time.time - speakStart) * 60f), 0, envelope.Length - 1);
                chirp = Mathf.Lerp(chirp, envelope[at], 1f - Mathf.Exp(-30f * dt));
            }
            else chirp = Speaking ? 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(Time.time * 19f)) : Mathf.MoveTowards(chirp, 0f, dt * 4f);
            // The eye's colour follows the mood: pale when frightened, red when angry, dim blue when sad.
            Color wantColour = Mood switch
            {
                "SCARED" => new Color(0.75f, 0.85f, 1f),
                "ANGRY" => new Color(1f, 0.22f, 0.12f),
                "SAD" or "HURT" => new Color(0.45f, 0.62f, 1f),
                "HAPPY" or "PLAYFUL" => new Color(1f, 0.78f, 0.32f),
                _ => Colour,
            };
            if (Colour != warm) wantColour = Colour;
            moodColour = Color.Lerp(moodColour, wantColour, 1f - Mathf.Exp(-3f * dt));
            float wanted = Listening ? 1.25f + Level * 0.35f : Thinking ? 0.7f : Mood == "SCARED" || Cover.HasValue || Hiding ? 0.6f : Mood == "ANGRY" ? 0.75f : 1f;
            open = Mathf.Lerp(open, wanted, 1f - Mathf.Exp(-8f * dt));
            if (iris)
            {
                iris.localScale = new Vector3(open, open, 1f);
                iris.localRotation = Quaternion.Euler(0f, 0f, iris.localEulerAngles.z + dt * (40f + 400f * think));
            }
            float flicker = think * (0.15f * Mathf.Sin(Time.time * 23f));
            float strength = Brightness * (1f + 0.35f * chirp + flicker + (Listening ? 0.2f + Level * 0.6f : 0f));
            if (irisMaterial) irisMaterial.SetColor(BaseColorId, moodColour * (2.2f * strength));
            if (glow)
            {
                glow.transform.localScale = glowScale * (0.8f + 0.3f * strength);
                var block = new MaterialPropertyBlock();
                glow.GetPropertyBlock(block);
                block.SetFloat(IntensityId, 0.5f * strength);
                block.SetColor(ColorId, moodColour);
                glow.SetPropertyBlock(block);
            }
            if (shine)
            {
                shine.intensity = shineIntensity * strength;
                shine.color = moodColour;
            }
            if (hum)
            {
                float moving = Mathf.Clamp01(velocity.magnitude / 6f);
                hum.volume = Mathf.Lerp(hum.volume, 0.18f + 0.22f * moving + think * 0.1f, dt * 3f);
                hum.pitch = Mathf.Lerp(hum.pitch, 0.95f + 0.25f * moving + think * 0.1f, dt * 3f);
            }
        }

        SphereCollider probe;
        const float Body = 0.3f;

        /// <summary>
        /// Where it can be on its way from one point to the next: it slides along what it meets, and is pushed out of
        /// anything that came into it (a door swinging, a thing made beside it). Never the survivor or what it carries.
        /// </summary>
        Vector3 Clear(Vector3 from, Vector3 to)
        {
            if (!probe)
            {
                var holder = new GameObject("Drone Body") { layer = gameObject.layer };
                holder.transform.SetParent(transform, false);
                probe = holder.AddComponent<SphereCollider>();
                probe.isTrigger = true;
                probe.radius = Body;
            }
            int mask = ~(1 << gameObject.layer) & ~(1 << traveller.gameObject.layer) & ~(1 << 2);
            Vector3 move = to - from;
            float distance = move.magnitude;
            if (distance > 1e-4f && Physics.SphereCast(from, Body, move / distance, out var hit, distance, mask, QueryTriggerInteraction.Ignore) && !Ours(hit.collider))
            {
                Vector3 rest = Vector3.ProjectOnPlane(move, hit.normal) * (1f - hit.distance / distance);
                to = from + move / distance * Mathf.Max(0f, hit.distance - 0.02f) + rest;
                velocity = Vector3.ProjectOnPlane(velocity, hit.normal);
            }
            foreach (var c in Physics.OverlapSphere(to, Body, mask, QueryTriggerInteraction.Ignore))
            {
                if (Ours(c)) continue;
                if (Physics.ComputePenetration(probe, to, Quaternion.identity, c, c.transform.position, c.transform.rotation, out var direction, out float depth))
                    to += direction * (depth + 0.01f);
            }
            return to;
        }

        /// <summary>Part of the survivor, of itself, or of something it or the survivor carries.</summary>
        bool Ours(Collider c)
        {
            var t = c.transform;
            if (t.IsChildOf(transform) || t.IsChildOf(traveller.transform)) return true;
            var held = c.GetComponentInParent<Holdable>();
            return held && held.Held;
        }

        void Fall(float dt)
        {
            velocity += Vector3.down * 9f * dt;
            Vector3 next = transform.position + velocity * dt;
            if (Physics.Raycast(transform.position, Vector3.down, out var hit, Mathf.Max(0.3f, -velocity.y * dt + 0.25f), ~(1 << gameObject.layer), QueryTriggerInteraction.Ignore))
            {
                next = hit.point + Vector3.up * 0.2f;
                velocity = Vector3.zero;
            }
            transform.position = next;
            shell.rotation = Quaternion.Slerp(shell.rotation, Quaternion.Euler(70f, shell.eulerAngles.y, 25f), 1f - Mathf.Exp(-3f * dt));
            if (irisMaterial) irisMaterial.SetColor(BaseColorId, Colour * (2.2f * Brightness));
            if (shine) shine.intensity = shineIntensity * Brightness;
            if (hum) hum.volume = Mathf.Lerp(hum.volume, 0f, dt * 2f);
        }

        /// <summary>A small motion for how it feels.</summary>
        public void Feel(string mood) => Feel(mood, 0f);

        /// <summary>A small motion for how it feels, some seconds from now (so it comes with the words).</summary>
        public void Feel(string mood, float after)
        {
            this.mood = mood;
            moodUntil = Time.time + (mood is "SCARED" or "ANGRY" or "HURT" or "SAD" ? 40f : 15f);
            gesture = mood switch
            {
                "HAPPY" => Random.value < 0.5f ? "SPIN" : "BOUNCE",
                "PLAYFUL" => "SPIN",
                "SAD" or "HURT" => "DROOP",
                "SORRY" => "SHAKE",
                "SURE" => "NOD",
                "CURIOUS" => "TILT",
                "WORRIED" => "SHIVER",
                "SCARED" => "BACK",
                "ANGRY" => "SNAP",
                _ => "",
            };
            gestureAt = Time.time + after;
        }

        /// <summary>Shakes the snow off its shell, like a dog out of water.</summary>
        public void ShakeOffSnow()
        {
            gesture = "WET";
            gestureAt = Time.time;
            if (snowOff) snowOff.Play();
        }

        /// <summary>Backs off out of reach, frightened.</summary>
        public void BackAway()
        {
            gesture = "BACK";
            gestureAt = Time.time;
        }

        public void Nod() { gesture = "NOD"; gestureAt = Time.time; }
        public void Shake() { gesture = "SHAKE"; gestureAt = Time.time; }

        Quaternion Gesture()
        {
            float age = Time.time - gestureAt;
            if (age < 0f) return Quaternion.identity;
            switch (gesture)
            {
                case "NOD" when age < 0.9f: return Quaternion.Euler(Mathf.Sin(age * 14f) * 18f * (1f - age / 0.9f), 0f, 0f);
                case "SHAKE" when age < 1f: return Quaternion.Euler(0f, Mathf.Sin(age * 16f) * 22f * (1f - age), 0f);
                case "TILT" when age < 1.8f: return Quaternion.Euler(0f, 0f, 22f * Mathf.Sin(Mathf.Clamp01(age / 1.8f) * Mathf.PI));
                case "SPIN" when age < 0.8f: return Quaternion.Euler(0f, 0f, 360f * Mathf.SmoothStep(0f, 1f, age / 0.8f));
                case "DROOP" when age < 2.2f: return Quaternion.Euler(24f * Mathf.Sin(Mathf.Clamp01(age / 2.2f) * Mathf.PI), 0f, 0f);
                case "SHIVER" when age < 1f: return Quaternion.Euler(0f, Mathf.Sin(age * 60f) * 4f, Mathf.Sin(age * 53f) * 3f);
                case "BACK" when age < 3.5f: return Quaternion.Euler(-10f * Mathf.Clamp01(age * 3f), Mathf.Sin(age * 50f) * 3f * (1f - age / 3.5f), 0f);
                case "SNAP" when age < 0.7f: return Quaternion.Euler(-14f * Mathf.Sin(age / 0.7f * Mathf.PI), Mathf.Sin(age * 30f) * 10f * (1f - age / 0.7f), 0f);
                case "WET" when age < 1.1f: return Quaternion.Euler(0f, 0f, Mathf.Sin(age * 38f) * 32f * (1f - age / 1.1f));
                default: return Quaternion.identity;
            }
        }

        /// <summary>
        /// Says a line: its voice from the shell and the words under the view, typed as it goes. One line at a
        /// time, so it never talks over itself: a line waits while another is fetched or said, and a quick one
        /// (said in the moment, like a warning) is dropped instead of waiting.
        /// </summary>
        public IEnumerator Speak(string line, string mood, Hud hud, string name, bool quick = false, AudioClip given = null, Kind kind = Kind.Answer)
        {
            if (string.IsNullOrWhiteSpace(line) || quick && Busy) yield break;
            // In turn: the most urgent kind first, then the earliest (one whose coroutine was stopped is passed over).
            int ticket = ++tickets;
            waiting[ticket] = Time.time;
            kinds[ticket] = kind;
            AudioClip clip = given;
            // An answer fetches its voice while it waits, so it can cut in the moment it is ready.
            bool fetched = clip || !spoken;
            bool fetching = false;
            if (!fetched && kind == Kind.Answer)
            {
                fetching = true;
                StartCoroutine(SparkVoice.Clip(line, mood, 8f, c => { clip = c; fetched = true; }));
            }
            while (true)
            {
                waiting[ticket] = Time.time;
                bool first = First() == ticket;
                bool allowed = kind >= Kind.Resume || !Quiet;
                if (first && allowed && kind == Kind.Answer && fetched && Saying != null && speakingKind < Kind.Answer) Interrupt();
                if (first && allowed && heldUntil <= Time.time && (fetched || !fetching)) break;
                yield return null;
            }
            waiting.Remove(ticket);
            kinds.Remove(ticket);
            // Held while the voice is fetched (that gives up after 8 s), then until the line has been said.
            heldUntil = Time.time + 12f;
            if (!clip && spoken && !fetching) yield return SparkVoice.Clip(line, mood, 8f, c => clip = c);
            bool chirps = !clip;
            if (chirps) clip = DroneVoice.Chirps(line, mood);
            envelope = chirps ? null : SparkVoice.Envelope(clip);
            float seconds = clip.length;
            // Now and then, a sound of its own first: a curious warble, a nervous double beep, a happy trill.
            var chirp = given || quick ? null : Chirp(mood);
            if (chirp && voice)
            {
                voice.PlayOneShot(chirp, 0.5f);
                yield return new WaitForSeconds(Mathf.Min(chirp.length, 0.9f));
            }
            if (voice)
            {
                voice.pitch = 1f;
                voice.PlayOneShot(clip, chirps ? 0.75f : voiceVolume);
            }
            Feel(mood, mood is "SAD" or "HURT" ? Mathf.Min(0.7f, seconds * 0.3f) : mood is "HAPPY" or "PLAYFUL" ? 0.05f : 0.25f);
            Saying = line;
            Debug.Log($"Spark says ({kind}): {line}");
            speakingTicket = ticket;
            speakingKind = kind;
            speakingMood = mood;
            speakingHud = hud;
            speakStart = Time.time;
            speakingUntil = Time.time + seconds;
            // A breath before the next line, and time to have read this one.
            heldUntil = Mathf.Max(speakingUntil + 1.1f, speakStart + line.Length / readingSpeed);
            for (float t = 0f; t < seconds && hushedTicket != ticket; t += Time.deltaTime)
            {
                hud.Voice(name, line, Mathf.Clamp01(t / (seconds * 0.9f)));
                yield return null;
            }
            if (speakingTicket == ticket) speakingTicket = -1;
            if (hushedTicket == ticket) yield break;
            Saying = null;
            hud.Voice(name, line, 1f);
            hud.VoiceFade(2.2f + line.Length * 0.03f);
        }

        /// <summary>The survivor began to speak: it stops mid-word. A story line is said again after they are answered.</summary>
        public void Hush() => Interrupt();

        void Interrupt()
        {
            if (Saying == null || speakingTicket < 0) return;
            if (voice) voice.Stop();
            hushedTicket = speakingTicket;
            Debug.Log($"Spark cut off ({speakingKind}): {Saying}");
            if (speakingKind == Kind.Story) Cut?.Invoke(Saying, speakingMood);
            Saying = null;
            speakingTicket = -1;
            speakingUntil = Time.time;
            heldUntil = Time.time + 0.25f;
            envelope = null;
            if (speakingHud) speakingHud.VoiceGone();
        }

        float chirpedAt = -100f;

        /// <summary>One of its own sounds for a mood, about one line in three and never two lines running.</summary>
        AudioClip Chirp(string mood)
        {
            if (Time.time - chirpedAt < 12f || UnityEngine.Random.value > 0.4f) return null;
            var clip = mood switch
            {
                "CURIOUS" => curiousChirp,
                "SCARED" or "WORRIED" => nervousChirp,
                "HAPPY" or "PLAYFUL" or "SURE" => happyChirp,
                "SAD" or "HURT" or "SORRY" => sadChirp,
                _ => null,
            };
            if (clip) chirpedAt = Time.time;
            return clip;
        }

        bool hummed;

        /// <summary>While it thinks: a low hum with small clicks, once for each time it starts thinking.</summary>
        void HumWhileThinking()
        {
            if (Thinking && !hummed && thinkingHum && voice && Time.time - chirpedAt > 6f)
            {
                voice.PlayOneShot(thinkingHum, 0.35f);
                chirpedAt = Time.time;
            }
            hummed = Thinking;
        }

        /// <summary>The earliest line still waiting for the voice, or int.MaxValue if none.</summary>
        int First()
        {
            int first = int.MaxValue;
            var best = Kind.Aside;
            foreach (var w in waiting)
            {
                if (Time.time - w.Value >= 1f) continue;
                var k = kinds.TryGetValue(w.Key, out var known) ? known : Kind.Story;
                if (first == int.MaxValue || k > best || k == best && w.Key < first)
                {
                    first = w.Key;
                    best = k;
                }
            }
            return first;
        }

        /// <summary>A short sound for listening, or for having heard.</summary>
        public void Blip(bool rising)
        {
            if (voice) voice.PlayOneShot(DroneVoice.Blip(rising), 0.5f);
        }

        /// <summary>Takes a picture with its eye of a point, with the shutter's sound and a blink.</summary>
        /// <summary>Its shutter and a blink: it took in what the survivor sees.</summary>
        public void Blink()
        {
            if (voice) voice.PlayOneShot(DroneVoice.Shutter(), 0.45f);
            open = 0.2f;
        }

        public byte[] Picture(Vector3 lookAt)
        {
            if (voice) voice.PlayOneShot(DroneVoice.Shutter(), 0.6f);
            open = 0.2f;
            return eye ? eye.Take(lookAt) : null;
        }

        /// <summary>Flies to a point looking at another, and waits till it is there (or gives up).</summary>
        public IEnumerator Fly(Vector3 to, Vector3 lookAt, float within = 0.5f, float timeout = 8f)
        {
            GoTo = to;
            LookAt = lookAt;
            if (voice && moveClip) voice.PlayOneShot(moveClip, 0.4f);
            for (float t = 0f; t < timeout && Vector3.Distance(transform.position, to) > within; t += Time.deltaTime) yield return null;
        }

        /// <summary>Back to the survivor's side.</summary>
        public void Release()
        {
            GoTo = null;
            LookAt = null;
        }

        /// <summary>A beam from its eye to a point, or none.</summary>
        public void Beam(Vector3? to, Color? colour = null)
        {
            if (!beam) return;
            beam.enabled = to.HasValue;
            if (!to.HasValue) return;
            beam.SetPosition(0, iris ? iris.position : transform.position);
            beam.SetPosition(1, to.Value);
            var c = colour ?? Colour;
            beam.startColor = new Color(c.r, c.g, c.b, 0.9f);
            beam.endColor = new Color(c.r, c.g, c.b, 0.35f);
        }

        /// <summary>A place to look at something from: near it, a little above, on the survivor's side.</summary>
        public Vector3 Vantage(Vector3 thing, float size)
        {
            Vector3 from = traveller.transform.position + Vector3.up * 1.6f;
            Vector3 away = Flat(from - thing);
            if (away.sqrMagnitude < 0.01f) away = Vector3.back;
            float distance = Mathf.Clamp(size * 1.3f + 2f, 2.5f, 9f);
            return thing + away.normalized * distance + Vector3.up * Mathf.Clamp(size * 0.4f + 1.2f, 1.2f, 4f);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
