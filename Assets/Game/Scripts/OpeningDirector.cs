using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Game
{
    /// <summary>
    /// The opening from start to end: out of black, the orb flashing its message, hints for a
    /// player who is stuck, and once the message is understood the build-up, the collapse and the
    /// flash of white.
    /// </summary>
    public class OpeningDirector : MonoBehaviour
    {
        [SerializeField] FloatingPlayer player;
        [SerializeField] Orb orb;
        [SerializeField] MorseEmitter emitter;
        [SerializeField] MorseVoice voice;
        [SerializeField] Notepad notepad;
        [SerializeField] Hud hud;
        [SerializeField] Volume volume;
        [SerializeField] AudioSource drone;
        [SerializeField, Tooltip("The roar of bent space, louder as the player nears the orb.")] AudioSource nearSound;
        [SerializeField] AudioSource effects;
        [SerializeField] AudioClip risingClip;
        [SerializeField, Tooltip("Swells to its loudest at the end of the collapse: the explosion played backwards.")] AudioClip gatherClip;
        [SerializeField] AudioClip breakClip;
        [SerializeField, Range(0f, 1f)] float droneVolume = 0.12f;
        [SerializeField, Range(0f, 1f)] float risingVolume = 0.6f;
        [SerializeField, Range(0f, 1f)] float nearVolume = 0.8f;
        [SerializeField, Tooltip("Distance at which the orb starts to be felt, and at which it is felt fully, in metres.")] Vector2 feltBetween = new Vector2(28f, 4f);
        [SerializeField, Tooltip("Seconds without opening the notepad before the first hint about the message.")] float hintAfter = 24f;
        [SerializeField, Tooltip("Seconds with the notepad up before the Morse key turns up.")] float keyAfter = 40f;
        [SerializeField, Tooltip("The words on the white after the flash.")] string afterwards = "13.8 billion years ago";

        enum Phase { Arriving, Listening, Breaking, Ended }

        Phase phase;
        int misses;
        bool keyShown;
        Bloom bloom;
        ChromaticAberration aberration;
        LensDistortion distortion;
        ColorAdjustments grading;
        FilmGrain grain;
        Vignette vignette;
        float bloomRest, aberrationRest, grainRest, vignetteRest, fovRest;

        void Start()
        {
            var profile = volume.profile;
            profile.TryGet(out bloom);
            profile.TryGet(out aberration);
            profile.TryGet(out distortion);
            profile.TryGet(out grading);
            profile.TryGet(out grain);
            profile.TryGet(out vignette);
            bloomRest = bloom.intensity.value;
            aberrationRest = aberration.intensity.value;
            grainRest = grain.intensity.value;
            vignetteRest = vignette.intensity.value;
            fovRest = player.View.fieldOfView;
            if (nearSound && nearSound.clip)
            {
                nearSound.volume = 0f;
                nearSound.Play();
            }
            notepad.Guessed += OnGuess;
            emitter.StepStarted += OnSymbol;
            StartCoroutine(Arrive());
            StartCoroutine(Hints());
        }

        void OnDestroy()
        {
            if (notepad) notepad.Guessed -= OnGuess;
            if (emitter) emitter.StepStarted -= OnSymbol;
        }

        void Update()
        {
            if (phase == Phase.Arriving || phase == Phase.Listening) FeelTheOrb();
            if (phase == Phase.Listening && !keyShown && (misses > 0 || notepad.SecondsOpen > keyAfter))
            {
                keyShown = true;
                notepad.ShowKey();
            }
            var keys = Keyboard.current;
            if (keys == null) return;
            if (phase == Phase.Ended && keys.rKey.wasPressedThisFrame)
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (keys.f9Key.wasPressedThisFrame) Solve();
#endif
        }

        /// <summary>Plays the ending as if the message had been understood (F9 in the editor).</summary>
        public void Solve()
        {
            if (phase == Phase.Listening) StartCoroutine(Break());
        }

        /// <summary>0 far from the orb, 1 as close as one can get.</summary>
        float Closeness()
        {
            float distance = Vector3.Distance(player.View.transform.position, orb.transform.position);
            return Mathf.Clamp01(Mathf.InverseLerp(feltBetween.x, feltBetween.y, distance));
        }

        /// <summary>Up close, space bends harder, the view strains and trembles, and the roar rises.</summary>
        void FeelTheOrb()
        {
            float c = Closeness(), c2 = c * c;
            aberration.intensity.value = aberrationRest + 0.35f * c2;
            vignette.intensity.value = vignetteRest + 0.18f * c2;
            grain.intensity.value = grainRest + 0.3f * c2;
            player.Shake = (0.004f + 0.05f * orb.Pulse) * c2;
            player.View.fieldOfView = fovRest + 8f * c2;
            orb.Warp = 1f + 0.6f * c2;
            if (nearSound) nearSound.volume = nearVolume * Mathf.Pow(c, 1.5f);
        }

        /// <summary>Close to the orb, every symbol it sends pushes the player back, like a heartbeat.</summary>
        void OnSymbol(MorseCode.StepKind kind)
        {
            if (phase != Phase.Listening || (kind != MorseCode.StepKind.Dot && kind != MorseCode.StepKind.Dash)) return;
            float c = Closeness();
            if (c < 0.2f) return;
            Vector3 away = (player.transform.position - orb.transform.position).normalized;
            player.Push(away * ((kind == MorseCode.StepKind.Dash ? 1.4f : 0.8f) * c * c));
            player.Kick(0.03f * c);
        }

        void OnGuess(string text)
        {
            if (phase != Phase.Listening) return;
            switch (Answer.Judge(text, emitter.Message))
            {
                case Answer.Verdict.Right:
                    notepad.UnderlineLast();
                    Solve();
                    break;
                case Answer.Verdict.Close:
                    notepad.StrikeLast();
                    orb.Stir();
                    misses++;
                    break;
                default:
                    notepad.StrikeLast();
                    orb.Dim();
                    misses++;
                    break;
            }
        }

        IEnumerator Arrive()
        {
            phase = Phase.Arriving;
            player.Scripted = true;
            hud.Black = 1f;
            if (drone && drone.clip)
            {
                drone.volume = 0f;
                drone.Play();
            }
            emitter.Begin(5f);
            yield return new WaitForSeconds(0.8f);
            yield return Tween(6f, k =>
            {
                hud.Black = 1f - Smooth(k);
                if (drone) drone.volume = droneVolume * k;
                if (k > 0.35f && phase == Phase.Arriving)
                {
                    player.Scripted = false;
                    phase = Phase.Listening;
                }
            });
        }

        IEnumerator Hints()
        {
            yield return new WaitForSeconds(3.5f);
            hud.ShowHint("[Mouse] look   [W A S D] drift   [Space] rise   [Ctrl] sink");
            yield return WaitFor(12f, () => player.Travelled > 8f);
            hud.HideHint();
            yield return WaitFor(hintAfter, () => notepad.HasBeenOpened);
            if (notepad.HasBeenOpened || phase != Phase.Listening) yield break;
            hud.ShowHint("it is trying to tell you something");
            yield return WaitFor(5f, () => notepad.HasBeenOpened);
            hud.HideHint();
            yield return WaitFor(1.5f, () => notepad.HasBeenOpened);
            if (notepad.HasBeenOpened || phase != Phase.Listening) yield break;
            hud.ShowHint("[Tab] take notes");
            while (!notepad.HasBeenOpened && phase == Phase.Listening) yield return null;
            hud.HideHint();
        }

        IEnumerator Break()
        {
            phase = Phase.Breaking;
            hud.HideHint();
            notepad.Available = false;
            emitter.Stop();
            yield return new WaitForSeconds(1.1f);
            notepad.Close();
            player.Scripted = true;
            const float build = 9f, collapse = 0.75f;
            if (effects && risingClip) effects.PlayOneShot(risingClip, risingVolume);
            // The gathering sound ends exactly as the orb falls to a point.
            if (effects && gatherClip) StartCoroutine(PlayAfter(gatherClip, build + collapse - gatherClip.length));

            float fovFrom = player.View.fieldOfView;
            float nearFrom = nearSound ? nearSound.volume : 0f;

            // It understood. It gathers itself, faster and faster.
            yield return Tween(build, k =>
            {
                float e = k * k;
                orb.Swell = e;
                player.View.fieldOfView = Mathf.Lerp(fovFrom, fovRest + 6f * e, Smooth(k));
                if (nearSound) nearSound.volume = Mathf.Lerp(nearFrom, nearVolume, e);
                player.TurnToward(orb.transform.position, 1.2f + 2f * k);
                player.Pull(orb.transform.position, 0.5f + 3f * e);
                player.Shake = 0.015f + 0.16f * e;
                orb.Instability = e;
                orb.Warp = 1f + 1.4f * e;
                // The dust holds its orbit at first, then whirls in over the last seconds.
                orb.Pull = 1f + 30f * e * e;
                orb.Spin = 1f + 6f * e;
                orb.Flare = 0.15f + 0.85f * e * Flicker(Time.time * (3f + 25f * e));
                voice.Drive = Mathf.Clamp01(k * 1.5f) * (0.55f + 0.45f * e);
                voice.Pitch = 1f + 1.2f * e;
                bloom.intensity.value = Mathf.Lerp(bloomRest, bloomRest * 1.6f, e);
                aberration.intensity.value = Mathf.Lerp(aberrationRest, 0.85f, e);
                distortion.intensity.value = Mathf.Lerp(0f, -0.32f, e);
                grain.intensity.value = Mathf.Lerp(grainRest, 0.55f, e);
                if (drone) drone.volume = droneVolume * (1f + 0.6f * e);
            });

            // It falls into itself, and for a breath there is almost nothing.
            yield return Tween(collapse, k =>
            {
                float e = k * k * k;
                orb.Collapse = e;
                orb.Flare = 1f;
                player.Shake = 0.17f * (1f - e);
                distortion.intensity.value = Mathf.Lerp(-0.32f, 0.35f, e);
                grading.postExposure.value = Mathf.Lerp(0f, -1.2f, e);
                voice.Drive = 1f - e;
                voice.Pitch = 2.2f + 1.5f * e;
                if (drone) drone.volume = droneVolume * 1.6f * (1f - e);
                if (nearSound) nearSound.volume = nearVolume * (1f - e);
            });
            voice.Muted = true;
            yield return new WaitForSeconds(0.35f);

            // Light.
            orb.Explode();
            if (effects && breakClip) effects.PlayOneShot(breakClip);
            player.Kick(0.5f);
            yield return Tween(1.8f, k =>
            {
                grading.postExposure.value = Mathf.Lerp(3.5f, 6f, k);
                bloom.intensity.value = Mathf.Lerp(14f, 20f, k);
                distortion.intensity.value = Mathf.Lerp(0.35f, 0f, k);
                aberration.intensity.value = Mathf.Lerp(1f, 0.3f, k);
                hud.White = Smooth(Mathf.Clamp01(k * 1.25f));
            });
            yield return new WaitForSeconds(2.5f);
            hud.ShowTitle(afterwards.ToUpperInvariant());
            yield return new WaitForSeconds(5f);
            hud.ShowSubtitle("the prologue ends here   [R] begin again");
            phase = Phase.Ended;
        }

        IEnumerator PlayAfter(AudioClip clip, float seconds)
        {
            if (seconds > 0f) yield return new WaitForSeconds(seconds);
            effects.PlayOneShot(clip);
        }

        static float Flicker(float x) => Mathf.Clamp01(Mathf.PerlinNoise(x, 0.37f) * 1.7f - 0.35f);

        static float Smooth(float k) => k * k * (3f - 2f * k);

        static IEnumerator Tween(float seconds, Action<float> step)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                step(t / seconds);
                yield return null;
            }
            step(1f);
        }

        static IEnumerator WaitFor(float seconds, Func<bool> done)
        {
            for (float t = 0f; t < seconds && !done(); t += Time.deltaTime) yield return null;
        }
    }
}
