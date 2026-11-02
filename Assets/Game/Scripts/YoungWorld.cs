using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The young Earth and what can happen to it. It keeps the world's state (a moon or not, molten or
    /// crusted, seas, ice, a greenhouse, where its orbit lies, a magnetic field) and plays each act the
    /// entity can do as a short scene that changes it, with the consequences a wrong moment brings:
    /// water boils off molten rock, a head-on blow makes no moon, seas freeze under the faint Sun, the
    /// Sun's wind thins the air of a world with no field. The five needs are conditions on this state,
    /// not steps in an order.
    /// </summary>
    public class YoungWorld : MonoBehaviour
    {
        public enum Need { Moon, Crust, Seas, Warmth, Shield }

        /// <summary>What an act did: whether it did what was asked, and a few words for the margin.</summary>
        public struct Outcome
        {
            public bool Worked;
            public string Margin;
        }

        [Header("Bodies")]
        [SerializeField] Planet earth;
        [SerializeField] Planet theia;
        [SerializeField] Planet moon;
        [SerializeField] Orbit moonOrbit;
        [SerializeField, Tooltip("The Sun's body; its corona and glow are children, so they scale with it.")] Transform sun;
        [SerializeField] Light sunlight;
        [SerializeField] Transform viewer;
        [SerializeField] FloatingPlayer player;
        [SerializeField] Hud hud;

        [Header("Effects")]
        [SerializeField] ParticleSystem ejecta;
        [SerializeField] ParticleSystem debrisRing;
        [SerializeField, Tooltip("Icy rocks streaming in from the outer belt.")] ParticleSystem iceStream;
        [SerializeField] ParticleSystem volcanoes;
        [SerializeField, Tooltip("Pushes the Sun's wind around the planet once it has a field.")] ParticleSystemForceField shield;

        [Header("Sound")]
        [SerializeField] AudioSource effects;
        [SerializeField] AudioClip approachClip;
        [SerializeField] AudioClip boomClip;
        [SerializeField] AudioClip moonClip;
        [SerializeField] AudioClip timeClip;
        [SerializeField] AudioClip iceClip;
        [SerializeField] AudioClip volcanoClip;
        [SerializeField] AudioClip auroraClip;
        [SerializeField] AudioClip rewindClip;
        [SerializeField] AudioClip orbitClip;

        public bool HasMoon { get; private set; }
        public bool TheiaPresent => theia.gameObject.activeSelf;
        public bool Molten { get; private set; } = true;
        /// <summary>How full the seas are, 0 to 1.</summary>
        public float Water { get; private set; }
        /// <summary>How much of the seas is frozen, 0 to 1.</summary>
        public float Ice { get; private set; }
        public bool Greenhouse { get; private set; }
        /// <summary>-1 moved closer to the Sun, 0 its own orbit, 1 moved farther out.</summary>
        public int OrbitShift { get; private set; }
        public bool Shield { get; private set; }
        /// <summary>An act is playing.</summary>
        public bool Busy { get; private set; }

        public bool Met(Need need) => need switch
        {
            Need.Moon => HasMoon,
            Need.Crust => !Molten,
            Need.Seas => Water >= 0.6f && Ice < 0.5f && OrbitShift == 0,
            Need.Warmth => Greenhouse && OrbitShift == 0,
            Need.Shield => Shield,
            _ => false,
        };

        public static readonly Need[] Needs = { Need.Moon, Need.Crust, Need.Seas, Need.Warmth, Need.Shield };

        public bool AllMet => Array.TrueForAll(Needs, Met);

        public Need? FirstUnmet => Array.Exists(Needs, n => !Met(n)) ? Array.Find(Needs, n => !Met(n)) : (Need?)null;

        // Whether the surface is molten from the great impact (fully) rather than from its making (in part).
        bool remelted;
        // The Sun's wind has thinned the air while time passed with no field.
        bool stripped;
        Vector3 theiaStart, sunScale, moonScale;
        float sunLight;

        void Awake()
        {
            theiaStart = theia.transform.position;
            sunScale = sun.localScale;
            sunLight = sunlight.intensity;
            moonScale = moon.transform.localScale;
            moon.gameObject.SetActive(false);
            if (shield) shield.gameObject.SetActive(false);
        }

        void Start() => Look(0f);

        /// <summary>What is true now, for the oracle's "moment".</summary>
        public string Describe()
        {
            var text = new StringBuilder();
            text.AppendLine(TheiaPresent ? "- Theia: a Mars-sized world on Earth's path; it has not struck Earth."
                : "- Theia: gone; it struck Earth a glancing blow and the Moon formed from the debris.");
            text.AppendLine(HasMoon ? "- The Moon: a large young Moon orbits close to Earth." : "- The Moon: Earth has no moon.");
            text.AppendLine(Molten ? "- The surface: molten rock, glowing." : "- The surface: a solid dark crust.");
            text.AppendLine(Water <= 0f ? "- Water: none on the surface." : Ice >= 0.5f ? "- Water: seas, mostly frozen." : Ice > 0f ? "- Water: seas, beginning to freeze at the poles." : "- Water: liquid seas.");
            text.AppendLine(Greenhouse ? "- The air: thick with carbon dioxide, a greenhouse that keeps the faint Sun's warmth." : "- The air: thin; under the faint young Sun it keeps little warmth.");
            text.AppendLine(OrbitShift < 0 ? "- Earth's orbit: moved closer to the Sun; too hot for seas." : OrbitShift > 0 ? "- Earth's orbit: moved farther from the Sun; too cold for liquid seas." : "- Earth's orbit: its own.");
            text.AppendLine(Shield ? "- A magnetic field: yes; auroras glow at the poles and the Sun's wind bends around Earth."
                : "- A magnetic field: none; the Sun's wind strikes the air directly and strips it away over time.");
            return text.ToString();
        }

        // ---- The acts ----

        /// <summary>Theia strikes Earth: a glancing blow makes the Moon; a head-on one makes none, and time runs back.</summary>
        public IEnumerator Impact(bool glancing, Action<Outcome> done)
        {
            Busy = true;
            var before = Save();
            Vector3 centre = earth.transform.position;
            float earthRadius = Radius(earth.transform), theiaRadius = Radius(theia.transform);
            Vector3 start = theia.transform.position;
            Vector3 toViewer = (viewer.position - centre).normalized;
            // It strikes where the player sees it side-on: on the limb, on the side it comes from.
            Vector3 limb = Vector3.ProjectOnPlane(start - centre, toViewer).normalized;
            if (limb.sqrMagnitude < 0.5f) limb = Vector3.Cross(toViewer, Vector3.up).normalized;
            Vector3 contactDirection = (limb + toViewer * 0.3f).normalized;
            Vector3 contact = centre + contactDirection * earthRadius;
            Vector3 along = Vector3.Cross(contactDirection, Vector3.up).normalized;
            if (Vector3.Dot(along, start - contact) < 0f) along = -along;
            Vector3 end = glancing
                ? centre + contactDirection * (earthRadius + theiaRadius * 0.5f) - along * theiaRadius * 0.6f
                : centre + (start - centre).normalized * (earthRadius + theiaRadius * 0.4f);
            Vector3 bend = glancing ? contact + along * 150f + contactDirection * 60f : Vector3.Lerp(start, end, 0.5f);

            if (effects && approachClip) effects.PlayOneShot(approachClip);
            const float approach = 8f;
            // Pulled in by Earth, it heats and its cracks glow, so it can be seen falling.
            theia.Ease("_Molten", 0.35f, approach);
            theia.Ease("_Glow", 4f, approach);
            for (float t = 0f; t < approach; t += Time.deltaTime)
            {
                float k = t / approach;
                theia.transform.position = Bezier(start, bend, end, Mathf.Pow(k, 2.2f));
                player.Shake = 0.004f + 0.05f * k * k;
                player.TurnToward(Vector3.Lerp(theia.transform.position, contact, 0.5f), 0.5f + 1.5f * k);
                yield return null;
            }

            // The blow: light, the surface melts again, and anything on it is lost.
            theia.gameObject.SetActive(false);
            if (effects && boomClip) effects.PlayOneShot(boomClip);
            player.Kick(0.45f);
            player.Shake = 0.06f;
            StartCoroutine(Flash(0.85f, 2f));
            ejecta.transform.SetPositionAndRotation(contact, Quaternion.LookRotation(glancing ? (-along + contactDirection * 0.7f).normalized : contactDirection));
            ejecta.Play();
            bool lostSurface = !before.molten || before.water > 0f;
            Molten = remelted = true;
            Water = Ice = 0f;
            Look(1.5f);
            earth.Ease("_Glow", 2.6f, 1.2f);

            if (!glancing)
            {
                // The debris falls back into Earth. No moon; the entity lets time run backwards.
                yield return new WaitForSeconds(4.5f);
                player.Shake = 0f;
                if (effects && rewindClip) effects.PlayOneShot(rewindClip);
                yield return Fade(1f, 0.9f);
                Restore(before);
                theia.transform.position = theiaStart;
                theia.Ease("_Molten", 0f, 0f);
                theia.Ease("_Glow", 0.6f, 0f);
                theia.gameObject.SetActive(true);
                ejecta.Clear();
                Look(0f);
                yield return Fade(0f, 1.6f);
                Busy = false;
                done(new Outcome { Worked = false, Margin = "no moon. it fell back" });
                yield break;
            }

            // A ring of glowing debris; out of it, the Moon gathers.
            debrisRing.Play();
            yield return new WaitForSeconds(4f);
            player.Shake = 0.01f;
            Vector3 side = Vector3.ProjectOnPlane(Quaternion.AngleAxis(55f, Vector3.up) * toViewer, Vector3.up).normalized;
            moon.transform.localScale = Vector3.zero;
            moon.gameObject.SetActive(true);
            moonOrbit.HoldFrom(centre + side * moonOrbit.Radius);
            if (effects && moonClip) effects.PlayOneShot(moonClip);
            const float gather = 8f;
            for (float t = 0f; t < gather; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / gather);
                moon.transform.localScale = moonScale * k;
                yield return null;
            }
            moon.transform.localScale = moonScale;
            debrisRing.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            moon.Ease("_Molten", 0.1f, 40f);
            moon.Ease("_Glow", 1.5f, 40f);
            earth.Ease("_Glow", 1.2f, 8f);
            HasMoon = true;
            player.Shake = 0f;
            Busy = false;
            done(new Outcome { Worked = true, Margin = lostSurface ? "a moon. the surface melted again" : "a moon" });
        }

        /// <summary>Millions of years in a few seconds: molten rock crusts over; seas freeze or thin as the world allows.</summary>
        public IEnumerator PassTime(Action<Outcome> done)
        {
            Busy = true;
            if (effects && timeClip) effects.PlayOneShot(timeClip);
            hud.Note("millions of years pass", 6f);
            var said = new List<string>();
            if (Molten)
            {
                Molten = remelted = false;
                said.Add("a crust");
            }
            if (Water > 0f && (OrbitShift > 0 || (!Greenhouse && OrbitShift == 0)) && Ice < 0.75f)
            {
                Ice = OrbitShift > 0 ? 1f : 0.75f;
                said.Add("the seas froze");
            }
            if (!Shield && (Water > 0f || Greenhouse))
            {
                Water *= 0.5f;
                stripped = true;
                said.Add("the sun's wind thinned the air");
            }
            const float length = 9f;
            Look(length * 0.8f);
            float moonFrom = moonOrbit.Radius, moonTo = Mathf.Min(moonFrom + 8f, 60f);
            for (float t = 0f; t < length; t += Time.deltaTime)
            {
                float k = Mathf.Sin(Mathf.PI * t / length);
                earth.Hurry = 1f + 80f * k;
                moonOrbit.Hurry = 1f + 30f * k;
                if (HasMoon) moonOrbit.Radius = Mathf.Lerp(moonFrom, moonTo, t / length);
                yield return null;
            }
            earth.Hurry = moonOrbit.Hurry = 1f;
            Busy = false;
            done(new Outcome { Worked = true, Margin = said.Count > 0 ? string.Join(". ", said) : "time passed" });
        }

        /// <summary>Icy rocks (or comets) from the outer belt rain onto Earth: seas on a crust, steam lost to space on molten rock.</summary>
        public IEnumerator SendIce(bool comets, Action<Outcome> done)
        {
            Busy = true;
            if (effects && iceClip) effects.PlayOneShot(iceClip);
            iceStream.Play();
            yield return new WaitForSeconds(2.5f);
            Outcome outcome;
            if (Molten || OrbitShift < 0)
            {
                // It flashes to steam on the hot rock and drifts off into space.
                earth.Ease("_Steam", 0.5f, 2f);
                yield return new WaitForSeconds(4f);
                earth.Ease("_Steam", 0f, 4f);
                outcome = new Outcome { Worked = false, Margin = "it boiled away" };
            }
            else
            {
                earth.Ease("_Steam", 0.9f, 3.5f);
                yield return new WaitForSeconds(4.5f);
                Water = comets ? 0.85f : 1f;
                stripped = false;
                Ice = OrbitShift > 0 ? 1f : 0f;
                earth.Ease("_Steam", 0.08f, 5f);
                Look(6f);
                yield return new WaitForSeconds(5f);
                outcome = new Outcome { Worked = true, Margin = OrbitShift > 0 ? "seas, frozen at once" : comets ? "seas. they taste wrong?" : "rain. seas" };
                // Under the faint young Sun, with thin air, the new seas start to freeze at the poles.
                if (OrbitShift == 0 && !Greenhouse)
                {
                    Ice = 0.4f;
                    earth.Ease("_Ice", Ice, 40f);
                }
            }
            iceStream.Stop();
            Busy = false;
            done(outcome);
        }

        /// <summary>The volcanoes breathe out carbon dioxide and steam: the air thickens and keeps the warmth.</summary>
        public IEnumerator Outgas(Action<Outcome> done)
        {
            Busy = true;
            if (effects && volcanoClip) effects.PlayOneShot(volcanoClip);
            volcanoes.Play();
            yield return new WaitForSeconds(2f);
            Greenhouse = true;
            if (OrbitShift <= 0) Ice = 0f;
            Look(5f);
            yield return new WaitForSeconds(5f);
            volcanoes.Stop();
            Busy = false;
            done(new Outcome { Worked = true, Margin = OrbitShift > 0 ? "thicker air. still frozen" : "thicker air. it holds the warmth" });
        }

        /// <summary>Earth's orbit moves toward the Sun or away (or back): the Sun grows or shrinks, and the seas boil or freeze.</summary>
        public IEnumerator Move(bool closer, Action<Outcome> done)
        {
            int to = Mathf.Clamp(OrbitShift + (closer ? -1 : 1), -1, 1);
            if (to == OrbitShift)
            {
                done(new Outcome { Worked = false, Margin = closer ? "no closer" : "no farther" });
                yield break;
            }
            Busy = true;
            if (effects && orbitClip) effects.PlayOneShot(orbitClip);
            OrbitShift = to;
            float size = to < 0 ? 1.5f : to > 0 ? 0.65f : 1f;
            Vector3 scaleFrom = sun.localScale, scaleTo = sunScale * size;
            float lightFrom = sunlight.intensity, lightTo = sunLight * size * size;
            const float length = 5f;
            for (float t = 0f; t < length; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / length);
                sun.localScale = Vector3.Lerp(scaleFrom, scaleTo, k);
                sunlight.intensity = Mathf.Lerp(lightFrom, lightTo, k);
                player.Shake = 0.012f * Mathf.Sin(Mathf.PI * t / length);
                yield return null;
            }
            player.Shake = 0f;
            string margin;
            if (to < 0)
            {
                margin = Water > 0f ? "the seas boiled away" : "closer to the sun";
                if (Water > 0f)
                {
                    earth.Ease("_Steam", 0.6f, 2f);
                    StartCoroutine(After(5f, () => earth.Ease("_Steam", 0f, 4f)));
                }
                Water = Ice = 0f;
            }
            else if (to > 0)
            {
                margin = Water > 0f ? "the seas froze" : "farther from the sun";
                if (Water > 0f) Ice = 1f;
            }
            else
            {
                margin = "back on its own path";
                Ice = Water > 0f && !Greenhouse ? 0.4f : 0f;
            }
            Look(4f);
            Busy = false;
            done(new Outcome { Worked = true, Margin = margin });
        }

        /// <summary>The iron core churns: a magnetic field, auroras at the poles, and the Sun's wind turned aside.</summary>
        public IEnumerator StirCore(Action<Outcome> done)
        {
            Busy = true;
            if (effects && auroraClip) effects.PlayOneShot(auroraClip);
            Shield = true;
            Look(4f);
            if (shield)
            {
                shield.gameObject.SetActive(true);
                for (float t = 0f; t < 4f; t += Time.deltaTime)
                {
                    shield.gravity = -6f * (t / 4f);
                    yield return null;
                }
                shield.gravity = -6f;
            }
            else yield return new WaitForSeconds(4f);
            Busy = false;
            done(new Outcome { Worked = true, Margin = "a magnetic field. auroras" });
        }

        /// <summary>The end of the age: weather, small ice caps, blue air. A world that could live.</summary>
        public void Bloom()
        {
            earth.Ease("_Clouds", 0.6f, 9f);
            earth.Ease("_Ice", 0.12f, 9f);
            earth.Ease("_Glow", 0.4f, 9f);
            earth.Ease("_Steam", 0f, 4f);
            earth.EaseHaze(new Color(0.35f, 0.58f, 1f), 9f);
            earth.EaseAir("_Thickness", 1.3f, 9f);
        }

        // ---- The look ----

        /// <summary>Eases the planet's look to its state.</summary>
        void Look(float seconds)
        {
            // Before the great impact the magma ocean shows only in cracks and low basins; after it, nearly
            // everywhere, with rafts of crust floating on it (fully molten, Earth reads as a second sun).
            earth.Ease("_Molten", Molten ? (remelted ? 0.62f : 0.3f) : 0.04f, seconds);
            earth.Ease("_Glow", Molten ? (remelted ? 1.2f : 2.6f) : 1.2f, seconds);
            earth.Ease("_Sea", Water * 0.92f, seconds);
            earth.Ease("_Ice", Ice, seconds);
            Color haze = Molten ? new Color(1f, 0.45f, 0.22f)
                : Greenhouse ? (Water > 0f ? new Color(0.8f, 0.7f, 0.55f) : new Color(1f, 0.72f, 0.4f))
                : Water > 0f ? new Color(0.35f, 0.55f, 1f) : new Color(0.55f, 0.5f, 0.48f);
            earth.EaseHaze(haze, seconds);
            earth.EaseAir("_Thickness", ((Greenhouse ? 1.5f : 0.8f) + (Molten ? 0.3f : 0f)) * (stripped ? 0.55f : 1f), seconds);
            earth.EaseAir("_Aurora", Shield ? 1f : 0f, seconds);
        }

        IEnumerator Flash(float peak, float seconds)
        {
            for (float t = 0f; t < 0.08f; t += Time.deltaTime)
            {
                hud.White = peak * t / 0.08f;
                yield return null;
            }
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = 1f - t / seconds;
                hud.White = peak * k * k;
                yield return null;
            }
            hud.White = 0f;
        }

        IEnumerator Fade(float to, float seconds)
        {
            float from = currentWhite;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                currentWhite = Mathf.Lerp(from, to, t / seconds);
                hud.White = currentWhite;
                yield return null;
            }
            currentWhite = to;
            hud.White = to;
        }

        float currentWhite;

        static IEnumerator After(float seconds, Action then)
        {
            yield return new WaitForSeconds(seconds);
            then();
        }

        struct Snapshot
        {
            public bool molten, remelted, greenhouse, shield, stripped;
            public float water, ice;
            public int orbit;
        }

        Snapshot Save() => new Snapshot
        {
            molten = Molten, remelted = remelted, greenhouse = Greenhouse, shield = Shield, stripped = stripped,
            water = Water, ice = Ice, orbit = OrbitShift,
        };

        void Restore(Snapshot s)
        {
            Molten = s.molten;
            remelted = s.remelted;
            Greenhouse = s.greenhouse;
            Shield = s.shield;
            stripped = s.stripped;
            Water = s.water;
            Ice = s.ice;
            OrbitShift = s.orbit;
        }

        static float Radius(Transform body) => body.lossyScale.x * 0.5f;

        static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t) => Vector3.Lerp(Vector3.Lerp(a, b, t), Vector3.Lerp(b, c, t), t);
    }
}
