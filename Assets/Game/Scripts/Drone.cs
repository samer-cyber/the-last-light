using System.Collections;
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
        [SerializeField, Range(0f, 1f)] float voiceVolume = 0.9f;
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
        float think, open = 1f, speakingUntil, gestureAt = -10f, chirp, speakStart, moodUntil;
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

        Vector3 Home()
        {
            if (Cover.HasValue)
            {
                Vector3 away = Flat(traveller.transform.position - Cover.Value);
                away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.back;
                return traveller.transform.position + away * 0.75f + Vector3.Cross(Vector3.up, away) * 0.35f + Vector3.up * 1.45f;
            }
            var facing = travellerBody ? Quaternion.Euler(0f, travellerBody.eulerAngles.y, 0f) : Quaternion.identity;
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
            bool facing = Listening || Speaking && !GoTo.HasValue;
            Vector3 target = GoTo ?? (facing ? Before() : Home());
            float t = Time.time;
            target += new Vector3(Mathf.Sin(t * 0.9f) * 0.03f, Mathf.Sin(t * 1.7f) * 0.06f, 0f);
            if (gesture == "DROOP" && Time.time - gestureAt < 2.2f) target += Vector3.down * 0.18f;
            if (gesture == "BACK" && Time.time - gestureAt < 3.5f && !GoTo.HasValue)
            {
                // Away from the survivor, a little higher: out of reach.
                Vector3 away = Flat(transform.position - traveller.transform.position);
                target += (away.sqrMagnitude > 0.01f ? away.normalized : Vector3.back) * 1.6f + Vector3.up * 0.5f;
            }
            if (gesture == "BOUNCE") target += Vector3.up * Mathf.Abs(Mathf.Sin((Time.time - gestureAt) * 9f)) * 0.14f * Mathf.Clamp01(1.2f - (Time.time - gestureAt));
            // Far errands are flown fast; close by it drifts.
            float speed = GoTo.HasValue ? Mathf.Clamp(Vector3.Distance(transform.position, target) * 0.9f, 9f, 24f) : 7f;
            transform.position = Vector3.SmoothDamp(transform.position, target, ref velocity, GoTo.HasValue ? 0.6f : 0.35f, speed);

            // Where the eye looks: at the survivor's face while talking, at what it was sent to, else where they look.
            Vector3 look = facing ? camera : LookAt ?? traveller.AimPoint ?? transform.position + (travellerBody ? travellerBody.forward : Vector3.forward) * 6f;
            Vector3 to = look - transform.position;
            var aim = to.sqrMagnitude > 0.01f ? Quaternion.LookRotation(to, Vector3.up) : shell.rotation;
            // It leans into its flight a little.
            Vector3 local = Quaternion.Inverse(aim) * velocity;
            aim *= Quaternion.Euler(Mathf.Clamp(local.z * 3f, -14f, 14f), 0f, Mathf.Clamp(-local.x * 4f, -18f, 18f));
            shell.rotation = Quaternion.Slerp(shell.rotation, aim * Gesture(), 1f - Mathf.Exp(-7f * dt));

            // The iris: wide to listen, narrow and turning to think, pulsing with its chirps.
            think = Mathf.MoveTowards(think, Thinking ? 1f : 0f, dt * 3f);
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
            float wanted = Listening ? 1.25f + Level * 0.35f : Thinking ? 0.7f : Mood == "SCARED" || Cover.HasValue ? 0.6f : Mood == "ANGRY" ? 0.75f : 1f;
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
        public void Feel(string mood)
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
            gestureAt = Time.time;
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

        /// <summary>Says a line: chirps from its shell and the words under the view, typed as it goes.</summary>
        public IEnumerator Speak(string line, string mood, Hud hud, string name)
        {
            if (string.IsNullOrWhiteSpace(line)) yield break;
            AudioClip clip = null;
            if (spoken) yield return SparkVoice.Clip(line, mood, 8f, c => clip = c);
            bool chirps = !clip;
            if (chirps) clip = DroneVoice.Chirps(line, mood);
            envelope = chirps ? null : SparkVoice.Envelope(clip);
            float seconds = clip.length;
            Feel(mood);
            if (voice)
            {
                voice.pitch = 1f;
                voice.PlayOneShot(clip, chirps ? 0.75f : voiceVolume);
            }
            speakStart = Time.time;
            speakingUntil = Time.time + seconds;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                hud.Voice(name, line, Mathf.Clamp01(t / (seconds * 0.9f)));
                yield return null;
            }
            hud.Voice(name, line, 1f);
            hud.VoiceFade(2.2f + line.Length * 0.03f);
        }

        /// <summary>A short sound for listening, or for having heard.</summary>
        public void Blip(bool rising)
        {
            if (voice) voice.PlayOneShot(DroneVoice.Blip(rising), 0.5f);
        }

        /// <summary>Takes a picture with its eye of a point, with the shutter's sound and a blink.</summary>
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
