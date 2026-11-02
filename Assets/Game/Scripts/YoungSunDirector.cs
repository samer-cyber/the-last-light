using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Game
{
    /// <summary>
    /// The young solar system from start to end. The player speaks (hold Q) or writes (Tab) to the
    /// entity; the oracle reads what they meant, and the director answers in the only ways the entity
    /// has: a 1 or a 0 in the stars, a shooting star, a stream of digits pointing at something, or an
    /// act that changes the young Earth. The game, not the model, decides what an act does. Hints are
    /// the player's own thoughts in the notes, aimed at the first need the world still lacks, and come
    /// only when they are stuck. When all five needs hold, the planet turns blue.
    /// </summary>
    public class YoungSunDirector : MonoBehaviour
    {
        [SerializeField] FloatingPlayer player;
        [SerializeField] YoungWorld world;
        [SerializeField] Oracle oracle;
        [SerializeField] SkyAnswer sky;
        [SerializeField] PointingStream stream;
        [SerializeField] Journal journal;
        [SerializeField] PushToTalk talk;
        [SerializeField] Hud hud;
        [SerializeField] AudioSource ambience;
        [SerializeField] AudioSource music;
        [SerializeField, Range(0f, 1f)] float ambienceVolume = 0.6f;
        [SerializeField, Range(0f, 1f)] float musicVolume = 0.3f;
        [Header("What the stream can point at")]
        [SerializeField] Transform earth;
        [SerializeField] Transform theia;
        [SerializeField] Transform iceBelt;
        [SerializeField] Transform sun;
        [SerializeField, Tooltip("Seconds stuck before the next hint.")] float hintAfter = 45f;

        // Thoughts the player has when stuck, by the need still missing; plainer each time.
        static readonly Dictionary<YoungWorld.Need, string[]> Hints = new Dictionary<YoungWorld.Need, string[]>
        {
            [YoungWorld.Need.Moon] = new[] { "that red world shares our path.", "if it struck us from the side... a moon?", "ask it to crash the red world into us." },
            [YoungWorld.Need.Crust] = new[] { "nothing can stay on rock that is still liquid.", "it needs time to cool.", "ask it to let time pass." },
            [YoungWorld.Need.Seas] = new[] { "there is ice far out, past the red planet.", "could the ice be brought here?", "ask it to send icy rocks from the belt." },
            [YoungWorld.Need.Warmth] = new[] { "the sun is faint. the seas are freezing.", "the volcanoes breathe. could the air keep more warmth?", "ask it to let the volcanoes thicken the air." },
            [YoungWorld.Need.Shield] = new[] { "the sun's wind is blowing the air away.", "the core is liquid iron. stirred iron...", "ask it to stir the core into a magnetic field." },
        };

        static readonly Dictionary<YoungWorld.Need, string> NeedNames = new Dictionary<YoungWorld.Need, string>
        {
            [YoungWorld.Need.Moon] = "a moon",
            [YoungWorld.Need.Crust] = "a crust",
            [YoungWorld.Need.Seas] = "seas",
            [YoungWorld.Need.Warmth] = "warmth",
            [YoungWorld.Need.Shield] = "a shield",
        };

        readonly Queue<(string words, bool spoken)> waiting = new Queue<(string, bool)>();
        readonly Dictionary<YoungWorld.Need, string> metBy = new Dictionary<YoungWorld.Need, string>();
        readonly List<(string words, string what)> failed = new List<(string, string)>();
        bool handling, ended, lawTaught, signsTaught, arrived;
        int hintLevel, failures, asked;
        float stuckSince;
        string logPath;

        void Start()
        {
            // The opening's lens must not carry over into this scene.
            Shader.SetGlobalVector("_WarpCenter", Vector4.zero);
            logPath = Path.Combine(Application.persistentDataPath, "young-sun-log.jsonl");
            journal.Asked += words => waiting.Enqueue((words, false));
            talk.Spoken += wav => StartCoroutine(Hear(wav));
            talk.Trouble += Hint;
            StartCoroutine(Arrive());
            StartCoroutine(Answering());
            StartCoroutine(Hints_());
        }

        void Update()
        {
            talk.Available = arrived && !ended && !journal.IsOpen;
            var keys = Keyboard.current;
            if (keys == null) return;
            if (ended && keys.rKey.wasPressedThisFrame) SceneManager.LoadScene(0);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Acts without the oracle, for testing.
            if (keys.f1Key.wasPressedThisFrame) Do("THEIA_GLANCE");
            if (keys.f2Key.wasPressedThisFrame) Do("PASS_TIME");
            if (keys.f3Key.wasPressedThisFrame) Do("SEND_ASTEROIDS");
            if (keys.f4Key.wasPressedThisFrame) Do("OUTGAS");
            if (keys.f5Key.wasPressedThisFrame) Do("STIR_CORE");
            if (keys.f6Key.wasPressedThisFrame) Do("MOVE_CLOSER");
            if (keys.f7Key.wasPressedThisFrame) Do("MOVE_FARTHER");
            if (keys.f8Key.wasPressedThisFrame) Do("THEIA_HEADON");
#endif
        }

        /// <summary>Asks as if the player had written it (tests use this).</summary>
        public void Ask(string words) => waiting.Enqueue((words, false));

        /// <summary>Carries out an act directly, as if the oracle had read it (tests and debug keys).</summary>
        public void Do(string action)
        {
            if (!handling && !world.Busy) StartCoroutine(Act(journal.Note("(" + action.ToLowerInvariant() + ")"), action, action));
        }

        IEnumerator Arrive()
        {
            hud.Black = 0f;
            hud.White = 1f;
            if (ambience && ambience.clip)
            {
                ambience.volume = 0f;
                ambience.Play();
            }
            yield return new WaitForSeconds(0.6f);
            hud.ShowTitle("4.5 BILLION YEARS AGO");
            yield return new WaitForSeconds(3.2f);
            player.Scripted = true;
            hud.HideTitle();
            for (float t = 0f; t < 5f; t += Time.deltaTime)
            {
                float k = t / 5f;
                hud.White = 1f - k * k * (3f - 2f * k);
                if (ambience) ambience.volume = ambienceVolume * k;
                yield return null;
            }
            hud.White = 0f;
            player.Scripted = false;
            arrived = true;
            stuckSince = Time.time;
            if (music && music.clip)
            {
                music.volume = 0f;
                music.Play();
                StartCoroutine(Tween(8f, k => music.volume = musicVolume * k));
            }
            yield return new WaitForSeconds(2.5f);
            hud.ShowHint("[Q] hold and speak to it   [Tab] write to it");
            while (asked == 0 && !ended) yield return null;
            hud.HideHint();
        }

        /// <summary>Speech, turned into words by Muse Voice Transcribe, then asked like anything written.</summary>
        IEnumerator Hear(byte[] wav)
        {
            hud.Waiting("…");
            string words = null, error = null;
            yield return MetaApi.Transcribe(wav, 15f, (w, e) => { words = w; error = e; });
            hud.StopListening();
            if (string.IsNullOrWhiteSpace(words))
            {
                if (error != null) Debug.LogWarning("Transcription failed: " + error);
                Hint(error != null ? "it could not hear you.   [Tab] write instead" : "it heard nothing.   hold [Q] and speak");
                yield break;
            }
            waiting.Enqueue((words.Trim(), true));
        }

        IEnumerator Answering()
        {
            while (true)
            {
                while (waiting.Count == 0 || ended || world.Busy) yield return null;
                var (words, spoken) = waiting.Dequeue();
                handling = true;
                yield return Handle(words, spoken);
                handling = false;
            }
        }

        IEnumerator Handle(string words, bool spoken)
        {
            asked++;
            var entry = journal.Note(words);
            if (spoken) hud.Said(words);
            sky.Gather(true);
            Reading reading = default;
            yield return oracle.Read(words, world.Describe(), r => reading = r);
            sky.Gather(false);
            Log(words, spoken, reading);

            switch (reading.Kind)
            {
                case "request":
                    yield return Act(entry, reading.Action, words);
                    break;
                case "next":
                    yield return Point(entry, Needed());
                    break;
                case "open" when reading.PointAt != "NONE":
                    yield return Point(entry, reading.PointAt);
                    break;
                case "question":
                    yield return Answer(entry, reading.Answer);
                    break;
                default:
                    yield return Answer(entry, Sign.Unknowable);
                    break;
            }
        }

        IEnumerator Answer(Journal.Entry entry, Sign sign)
        {
            journal.Mark(entry, sign);
            if (sign == Sign.Unknowable) GameMemory.Unknowables.Add(entry.Text);
            yield return sky.Show(sign);
            if (!signsTaught && sign != Sign.Unknowable)
            {
                signsTaught = true;
                journal.Thought("1 is yes. 0 is no?");
            }
        }

        /// <summary>The target of the stream when the player asks what to do: the first need still missing.</summary>
        string Needed() => world.FirstUnmet switch
        {
            YoungWorld.Need.Moon => world.TheiaPresent ? "THEIA" : "EARTH",
            YoungWorld.Need.Crust => "EARTH",
            YoungWorld.Need.Seas => world.OrbitShift != 0 ? "SUN" : "ICE_BELT",
            YoungWorld.Need.Warmth => world.OrbitShift != 0 ? "SUN" : "VOLCANOES",
            YoungWorld.Need.Shield => "CORE",
            _ => "EARTH",
        };

        IEnumerator Point(Journal.Entry entry, string target)
        {
            float earthRadius = earth.lossyScale.x * 0.5f;
            (Vector3 at, float around, string what) = target switch
            {
                "THEIA" when theia.gameObject.activeInHierarchy => (theia.position, theia.lossyScale.x * 0.5f, "the red world"),
                "ICE_BELT" => (iceBelt.position, 120f, "the far ice"),
                "SUN" => (sun.position, sun.lossyScale.x * 0.5f, "the sun"),
                "VOLCANOES" => (earth.position + (player.transform.position - earth.position).normalized * earthRadius * 0.9f + Vector3.up * earthRadius * 0.3f, earthRadius * 0.25f, "the volcanoes"),
                "CORE" => (earth.position, earthRadius * 0.3f, "the core"),
                _ => (earth.position, earthRadius, "the earth"),
            };
            journal.Pointed(entry, what);
            yield return stream.Point(at, around);
        }

        IEnumerator Act(Journal.Entry entry, string action, string words)
        {
            var before = Array.ConvertAll(YoungWorld.Needs, world.Met);
            YoungWorld.Outcome outcome = default;
            void Finish(YoungWorld.Outcome o) => outcome = o;
            IEnumerator scene = action switch
            {
                "THEIA_GLANCE" when world.TheiaPresent => world.Impact(true, Finish),
                "THEIA_HEADON" when world.TheiaPresent => world.Impact(false, Finish),
                "PASS_TIME" => world.PassTime(Finish),
                "SEND_ASTEROIDS" => world.SendIce(false, Finish),
                "SEND_COMETS" => world.SendIce(true, Finish),
                "OUTGAS" => world.Outgas(Finish),
                "MOVE_CLOSER" => world.Move(true, Finish),
                "MOVE_FARTHER" => world.Move(false, Finish),
                "STIR_CORE" when !world.Shield => world.StirCore(Finish),
                _ => null,
            };

            if (scene == null)
            {
                // Not done: the law, the age, or a thing that cannot be done now.
                Sign sign = Sign.No;
                string margin = null;
                switch (action)
                {
                    case "LATER":
                        sign = Sign.Unknowable;
                        margin = "not yet";
                        break;
                    case "MAKE":
                        margin = "it will not make";
                        break;
                    case "STIR_CORE":
                        sign = Sign.Yes;
                        margin = "it already has";
                        break;
                    case "THEIA_GLANCE":
                    case "THEIA_HEADON":
                        margin = "the red world is gone";
                        break;
                }
                journal.Mark(entry, sign);
                if (sign == Sign.No)
                {
                    journal.Strike(entry, margin);
                    failed.Add((words, margin ?? "no"));
                    failures++;
                }
                else journal.Margin(entry, margin);
                yield return sky.Show(sign);
                if (action == "MAKE" && !lawTaught)
                {
                    lawTaught = true;
                    Thought("it will not make anything. it only moves what is already there.");
                }
                yield break;
            }

            if (journal.IsOpen) journal.Close();
            yield return scene;
            var after = Array.ConvertAll(YoungWorld.Needs, world.Met);
            bool progress = false;
            for (int i = 0; i < YoungWorld.Needs.Length; i++)
            {
                var need = YoungWorld.Needs[i];
                if (after[i] && !before[i])
                {
                    metBy[need] = words;
                    progress = true;
                }
                else if (!after[i]) metBy.Remove(need);
            }
            journal.Mark(entry, outcome.Worked ? Sign.Yes : Sign.No);
            if (progress) journal.Underline(entry, outcome.Margin);
            else if (!outcome.Worked)
            {
                journal.Strike(entry, outcome.Margin);
                failed.Add((words, outcome.Margin));
                failures++;
            }
            else journal.Margin(entry, outcome.Margin);
            if (progress)
            {
                hintLevel = 0;
                failures = 0;
                stuckSince = Time.time;
            }
            yield return sky.Show(outcome.Worked ? Sign.Yes : Sign.No);
            if (world.AllMet) StartCoroutine(Ending());
        }

        IEnumerator Hints_()
        {
            while (!arrived) yield return null;
            // The first thought, once they have looked around a little without asking anything.
            yield return WaitFor(25f, () => asked > 0);
            if (asked == 0) Thought("it is listening. ask it something.");
            while (!ended)
            {
                yield return new WaitForSeconds(1f);
                if (handling || world.Busy || ended) continue;
                bool stuck = Time.time - stuckSince > hintAfter || failures >= 2;
                var need = world.FirstUnmet;
                if (!stuck || need == null || hintLevel >= 3) continue;
                Thought(Hints[need.Value][hintLevel]);
                hintLevel++;
                failures = 0;
                stuckSince = Time.time;
            }
        }

        void Thought(string text)
        {
            journal.Thought(text);
            Hint(text);
        }

        void Hint(string text) => StartCoroutine(ShowHint(text));

        IEnumerator ShowHint(string text)
        {
            hud.ShowHint(text);
            yield return new WaitForSeconds(7f);
            hud.HideHint();
        }

        IEnumerator Ending()
        {
            ended = true;
            yield return new WaitForSeconds(2f);
            world.Bloom();
            player.Scripted = true;
            if (music) StartCoroutine(Tween(6f, k => music.volume = Mathf.Lerp(musicVolume, Mathf.Min(1f, musicVolume * 2f), k)));
            // Around to the day side, to see the blue world in sunlight.
            Vector3 from = (player.transform.position - earth.position).normalized;
            Vector3 toward = (sun.position - earth.position).normalized;
            Vector3 day = Vector3.Slerp(toward, Vector3.Cross(Vector3.up, toward).normalized, 0.35f).normalized;
            for (float t = 0f; t < 9f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 9f);
                player.Pull(earth.position + Vector3.Slerp(from, day, k) * 58f, 18f);
                player.TurnToward(earth.position, 2f);
                yield return null;
            }
            var met = new List<(string need, string words)>();
            foreach (var need in YoungWorld.Needs)
                if (metBy.TryGetValue(need, out string words)) met.Add((NeedNames[need], words));
            journal.Summary(met, failed);
            journal.Available = true;
            journal.Open();
            yield return new WaitForSeconds(7f);
            journal.Thought("it is alive?");
            yield return new WaitForSeconds(4f);
            journal.Close();
            yield return Tween(3f, k => hud.White = k);
            hud.ShowTitle("4.4 BILLION YEARS AGO");
            yield return new WaitForSeconds(4f);
            hud.ShowSubtitle("the story goes on from here   [R] begin again");
        }

        void Log(string words, bool spoken, Reading reading)
        {
            try
            {
                string line = "{\"time\":" + MetaApi.Quote(DateTime.Now.ToString("s")) + ",\"spoken\":" + (spoken ? "true" : "false") +
                    ",\"words\":" + MetaApi.Quote(words) + ",\"kind\":" + MetaApi.Quote(reading.Kind) + ",\"answer\":" + MetaApi.Quote(reading.Answer.ToString()) +
                    ",\"action\":" + MetaApi.Quote(reading.Action) + ",\"point_at\":" + MetaApi.Quote(reading.PointAt) + ",\"fact\":" + MetaApi.Quote(reading.Fact) +
                    ",\"source\":" + MetaApi.Quote(reading.Source) + ",\"seconds\":" + reading.Seconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "}";
                File.AppendAllText(logPath, line + "\n");
            }
            catch (IOException) { }
        }

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

    /// <summary>What the game remembers between its ages, for the end: the questions the sky could not answer.</summary>
    public static class GameMemory
    {
        public static readonly List<string> Unknowables = new List<string>();
    }
}
