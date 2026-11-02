using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Game
{
    /// <summary>
    /// The end of time, from the traveller's waking. Every line the player speaks (hold Q) or writes
    /// (Tab) goes to the mind; what the model reads it as, the game carries out: a plan drawn in
    /// light and then made from what the mind holds, an idea drawn fainter for the player to accept,
    /// a thing taken apart, a stroke or a ring for yes and no. The game, not the model, decides
    /// what can be made and what it costs. The mind shows an idea of its own when the player is stuck.
    /// </summary>
    public class LastLightDirector : MonoBehaviour
    {
        [SerializeField] Traveller traveller;
        [SerializeField] FollowCamera follow;
        [SerializeField] TravellerBody body;
        [SerializeField] Companion mind;
        [SerializeField] Maker maker;
        [SerializeField] Mind reader;
        [SerializeField] SayLine say;
        [SerializeField] PushToTalk talk;
        [SerializeField] Hud hud;
        [SerializeField] Ember[] embers;
        [SerializeField] Gate gate;
        [SerializeField, Tooltip("Bends the sky round the black hole; at the end the mind bends it instead.")] SpaceWarp holeWarp;
        [SerializeField] SpaceWarp mindWarp;
        [SerializeField] AudioClip finaleClip;
        [SerializeField] Landmark[] landmarks;
        [SerializeField, Tooltip("The first idea the mind shows if the player is stuck at the start: a bridge from HERE to this place.")] Landmark firstIdeaTo;
        [SerializeField] AudioSource ambience;
        [SerializeField] AudioSource wind;
        [SerializeField] AudioSource music;
        [SerializeField] AudioSource steps;
        [SerializeField, Range(0f, 1f)] float ambienceVolume = 0.5f;
        [SerializeField, Range(0f, 1f)] float windVolume = 0.45f;
        [SerializeField, Range(0f, 1f)] float musicVolume = 0.32f;
        [SerializeField] AudioClip[] snowSteps;
        [SerializeField] AudioClip[] stoneSteps;
        [SerializeField] AudioClip[] iceSteps;
        [SerializeField] AudioClip jumpClip;
        [SerializeField] AudioClip landClip;
        [SerializeField, Tooltip("Seconds without a word before the mind shows an idea of its own.")] float ideaAfter = 35f;

        static readonly string[] Yes = { "yes", "yeah", "yep", "do it", "ok", "okay", "sure", "go", "go ahead", "make it", "build it", "please", "that", "that one", "y" };

        readonly Queue<(string words, bool spoken)> waiting = new Queue<(string, bool)>();
        Maker.Sketch pending, idea;
        string pendingWords, ideaWords;
        bool arrived, handling, ended;
        Pickup[] pickups;
        int lines, gathered;
        float quietSince;
        string logPath;
        readonly System.Random random = new System.Random(17);

        void Start()
        {
            // The opening's lens belongs to the black hole now; SpaceWarp sets it every frame.
            logPath = Path.Combine(Application.persistentDataPath, "last-light-log.jsonl");
            say.Said += words => waiting.Enqueue((words, false));
            talk.Spoken += wav => StartCoroutine(Hear(wav));
            talk.Trouble += words => hud.Note(words, 4f);
            mind.Changed += ShowCarry;
            traveller.Gathered += _ => gathered++;
            traveller.Jumped += () => Step(jumpClip, 0.5f);
            traveller.Landed += air => { if (air > 0.25f) Step(landClip, Mathf.Clamp01(air)); };
            body.Stepped += (foot, pace) => Step(Footstep(), 0.25f + 0.12f * pace);
            foreach (var e in embers) e.Watch(traveller.transform, mind);
            foreach (var e in embers) e.Taken += Taken;
            pickups = FindObjectsByType<Pickup>(FindObjectsSortMode.None);
            if (gate) gate.Moved += open => { if (open) hud.Note("the door sinks into the ground", 4f); };
            if (mindWarp) mindWarp.enabled = false;
            StartCoroutine(Arrive());
            StartCoroutine(Answering());
            StartCoroutine(Ideas());
        }

        void Update()
        {
            talk.Available = arrived && !ended && !say.IsOpen;
            say.Available = arrived;
            // While a plan waits for matter, loose pieces of that matter glint.
            Matter? wanted = pending != null ? pending.Plan.Matter : (Matter?)null;
            foreach (var p in pickups)
                if (p) p.Glinting = wanted.HasValue && p.Matter == wanted.Value && Vector3.Distance(p.transform.position, traveller.transform.position) < 70f;
            // A plan waiting for matter is made as soon as the mind holds enough.
            if (pending != null && !handling && !maker.Busy && mind.Count(pending.Plan.Matter) >= pending.Plan.Cost)
                StartCoroutine(MakePending());
            var keys = Keyboard.current;
            if (keys == null) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (keys.f1Key.wasPressedThisFrame) mind.Add(Matter.Stone, 10);
            if (keys.f2Key.wasPressedThisFrame) mind.Add(Matter.Ice, 10);
            if (keys.f3Key.wasPressedThisFrame) mind.Add(Matter.Metal, 10);
            if (keys.f4Key.wasPressedThisFrame) mind.Add(Matter.Light, 10);
#endif
        }

        /// <summary>Asks as if the player had written it (tests use this).</summary>
        public void Ask(string words) => waiting.Enqueue((words, false));

        public bool Busy => handling || maker.Busy || waiting.Count > 0;

        IEnumerator Arrive()
        {
            hud.White = 1f;
            hud.Black = 0f;
            traveller.Scripted = true;
            follow.Scripted = true;
            hud.HideAim();
            if (ambience && ambience.clip) { ambience.volume = 0f; ambience.Play(); }
            if (wind && wind.clip) { wind.volume = 0f; wind.Play(); }
            yield return new WaitForSeconds(0.8f);
            hud.ShowTitle("A HUNDRED TRILLION YEARS LATER");
            yield return new WaitForSeconds(3.4f);
            hud.HideTitle();
            // The mind comes down to the traveller from high above.
            mind.transform.position = traveller.transform.position + Vector3.up * 14f + traveller.Body.forward * 6f;
            for (float t = 0f; t < 5f; t += Time.deltaTime)
            {
                float k = t / 5f;
                hud.White = 1f - k * k * (3f - 2f * k);
                if (ambience) ambience.volume = ambienceVolume * k;
                if (wind) wind.volume = windVolume * k;
                yield return null;
            }
            hud.White = 0f;
            traveller.Scripted = false;
            follow.Scripted = false;
            arrived = true;
            quietSince = Time.time;
            ShowCarry();
            if (music && music.clip)
            {
                music.volume = 0f;
                music.Play();
                StartCoroutine(Tween(10f, k => music.volume = musicVolume * k));
            }
            yield return new WaitForSeconds(1.5f);
            hud.ShowHint("[W A S D] walk   [Shift] run   [Space] jump   [Mouse] look");
            yield return new WaitForSeconds(7f);
            hud.ShowHint("the last embers of light lie across the ice.   bring them to it");
            yield return new WaitForSeconds(6f);
            hud.ShowHint("it makes things from what you bring it.   [Q] hold and speak to it   [Tab] write to it");
            while (lines == 0) yield return null;
            hud.HideHint();
        }

        /// <summary>Speech, turned into words by Muse Voice Transcribe, then read like anything written.</summary>
        IEnumerator Hear(byte[] wav)
        {
            hud.Waiting("…");
            string words = null, error = null;
            yield return MetaApi.Transcribe(wav, 15f, (w, e) => { words = w; error = e; });
            hud.StopListening();
            if (string.IsNullOrWhiteSpace(words))
            {
                if (error != null) Debug.LogWarning("Transcription failed: " + error);
                hud.Note(error != null ? "it could not hear you.   [Tab] write instead" : "it heard nothing.   hold [Q] and speak", 4f);
                yield break;
            }
            waiting.Enqueue((words.Trim(), true));
        }

        IEnumerator Answering()
        {
            while (true)
            {
                while (waiting.Count == 0 || maker.Busy || ended) yield return null;
                var (words, spoken) = waiting.Dequeue();
                handling = true;
                yield return Handle(words, spoken);
                handling = false;
                quietSince = Time.time;
            }
        }

        IEnumerator Handle(string words, bool spoken)
        {
            lines++;
            hud.Said(words, 4f);
            // Accepting the idea on show needs no reading.
            if (idea != null && Yes.Contains(Clean(words)))
            {
                var accepted = idea;
                idea = null;
                maker.Hide(accepted);
                yield return Build(accepted.Plan, ideaWords, null);
                Log(words, spoken, "accept idea", 0f, "rules");
                yield break;
            }

            mind.Thinking = true;
            var anchors = Anchors();
            Thought thought = default;
            yield return reader.Read(words, Moment(), anchors.Keys.ToList(), t => thought = t);
            mind.Thinking = false;
            Log(words, spoken, Mind.Describe(thought), thought.Seconds, thought.Source);

            switch (thought.Kind)
            {
                case "build" when thought.Wishes.Count > 0:
                    ClearIdea();
                    foreach (var wish in thought.Wishes)
                    {
                        var plan = Design(wish, anchors, out string problem);
                        if (plan == null)
                        {
                            mind.Cannot();
                            hud.Note(problem, 4f);
                            break;
                        }
                        yield return Build(plan, words, null);
                        while (maker.Busy) yield return null;
                        if (pending != null) break;
                    }
                    break;
                case "idea" when thought.Wishes.Count > 0:
                    ClearIdea();
                    var ideaPlan = Design(thought.Wishes[0], anchors, out string why);
                    if (ideaPlan == null)
                    {
                        mind.Unsure();
                        hud.Note(why, 4f);
                        break;
                    }
                    ShowIdea(ideaPlan, words);
                    break;
                case "undo":
                    var target = traveller.AimPoint.HasValue ? maker.Nearest(traveller.AimPoint.Value, 5f) : null;
                    if (!target) target = maker.Made.LastOrDefault(b => b);
                    if (!target)
                    {
                        mind.Unsure();
                        hud.Note("it has made nothing to take apart", 3f);
                        break;
                    }
                    yield return maker.Unmake(target, mind);
                    mind.Mark(true);
                    break;
                case "question" when thought.Answer != Sign.Unknowable:
                    mind.Mark(thought.Answer == Sign.Yes);
                    break;
                default:
                    if (thought.Refusal == "NOTHING_FROM_NOTHING")
                    {
                        mind.Cannot();
                        hud.Note("it cannot shape that from stone, ice, metal or light", 4f);
                    }
                    else mind.Unsure();
                    break;
            }
        }

        /// <summary>Draws the plan, then makes it if the mind holds enough, or leaves it waiting.</summary>
        IEnumerator Build(Maker.Plan plan, string words, Action done)
        {
            if (pending != null)
            {
                maker.Hide(pending);
                pending = null;
            }
            var sketch = maker.Show(plan, false);
            mind.Attending = plan.Centre;
            yield return new WaitForSeconds(1.1f);
            int have = mind.Count(plan.Matter);
            if (have >= plan.Cost)
            {
                yield return maker.Make(sketch, mind, words, _ => { });
                mind.Attending = null;
                mind.Mark(true);
            }
            else
            {
                maker.Wanting(sketch, true);
                pending = sketch;
                pendingWords = words;
                mind.Attending = null;
                string matter = Matters.Word(plan.Matter);
                hud.Note($"it needs {plan.Cost - have} more {matter}", 5f);
                if (plan.Matter == Matter.Light) hud.ShowHint("light comes from embers");
                else hud.ShowHint($"[E] gather {matter} for it");
                StartCoroutine(HideHintLater(7f));
            }
            done?.Invoke();
        }

        IEnumerator MakePending()
        {
            handling = true;
            var sketch = pending;
            pending = null;
            maker.Wanting(sketch, false);
            mind.Attending = sketch.Plan.Centre;
            yield return new WaitForSeconds(0.4f);
            yield return maker.Make(sketch, mind, pendingWords, _ => { });
            mind.Attending = null;
            mind.Mark(true);
            handling = false;
        }

        void ShowIdea(Maker.Plan plan, string words)
        {
            idea = maker.Show(plan, true);
            ideaWords = words;
            mind.Attending = plan.Centre;
            hud.Note($"an idea: a {plan.Summary}.   say \"do it\" to make it", 6f);
            StartCoroutine(StopAttending(3f));
        }

        void ClearIdea()
        {
            if (idea == null) return;
            maker.Hide(idea);
            idea = null;
        }

        /// <summary>Turns a wish into a plan: places from the anchors, matter from what the mind holds.</summary>
        Maker.Plan Design(Wish wish, Dictionary<string, Vector3> anchors, out string problem)
        {
            Vector3 Place(string id, string fallback) =>
                anchors.TryGetValue(id, out var p) && id != "NONE" ? p : anchors[fallback];
            Matter matter;
            if (wish.Kind == "LAMP") matter = Matter.Light;
            else if (!Matters.TryParse(wish.Matter, out matter))
            {
                // Unsaid: what it holds most of, keeping its own light for last.
                matter = Matters.All.Where(m => m != Matter.Light).OrderByDescending(mind.Count).First();
                if (mind.Count(matter) == 0) matter = Matter.Stone;
            }
            var request = new Maker.Request
            {
                Kind = wish.Kind,
                Matter = matter,
                From = Place(wish.From, "HERE"),
                To = Place(wish.To, "AIM"),
                Size = wish.Size == "SMALL" ? 0.6f : wish.Size == "LARGE" ? 1.6f : 1f,
                Height = wish.Height,
                Length = wish.Length,
            };
            if (Vector3.Distance(request.From, request.To) < 0.5f && wish.Kind == "BRIDGE") request.To = anchors["AIM"];
            return maker.Design(request, out problem);
        }

        /// <summary>Every place the mind may name now, by id: here, where the player looks, itself, and the named places it knows.</summary>
        Dictionary<string, Vector3> Anchors()
        {
            var anchors = new Dictionary<string, Vector3>
            {
                ["HERE"] = traveller.transform.position,
                ["AIM"] = traveller.AimPoint ?? traveller.transform.position + Flat(follow.transform.forward) * 6f,
                ["MIND"] = mind.transform.position,
                ["NONE"] = traveller.transform.position,
            };
            foreach (var l in landmarks)
                if (l && l.Known && !anchors.ContainsKey(l.Id) && Vector3.Distance(l.Point, traveller.transform.position) < 120f) anchors[l.Id] = l.Point;
            return anchors;
        }

        /// <summary>What the mind is told is true now, for the model.</summary>
        string Moment()
        {
            var text = new StringBuilder("\n# The moment\n");
            text.Append("You hold: ").Append(mind.Holding()).Append(".\n");
            text.Append("Places (id: what it is, where it is from the traveller):\n");
            text.Append("- HERE: where the traveller stands.\n");
            text.Append("- AIM: the point the traveller is looking at: ").Append(AimWords()).Append(".\n");
            text.Append("- MIND: you, at the traveller's shoulder.\n");
            foreach (var l in landmarks)
            {
                if (!l || !l.Known) continue;
                float d = Vector3.Distance(l.Point, traveller.transform.position);
                if (d > 120f) continue;
                text.Append("- ").Append(l.Id).Append(": ").Append(l.Title);
                if (!string.IsNullOrEmpty(l.About)) text.Append(" (").Append(l.About).Append(')');
                text.Append(", ").Append(Where(l.Point)).Append(".\n");
            }
            var made = maker.Made.Where(b => b).Select(b => $"{b.Kind.ToLowerInvariant()} of {Matters.Word(b.Matter)}").ToList();
            text.Append("Made so far: ").Append(made.Count == 0 ? "nothing" : string.Join(", ", made)).Append(".\n");
            if (idea != null) text.Append("On show now, as your idea: a ").Append(idea.Plan.Summary).Append(".\n");
            if (pending != null) text.Append("Waiting for matter: a ").Append(pending.Plan.Summary).Append(".\n");
            return text.ToString();
        }

        string AimWords()
        {
            if (traveller.Aimed) return $"{traveller.Aimed.Title} ({traveller.Aimed.Id}), {Where(traveller.AimPoint ?? traveller.Aimed.Point)}";
            if (traveller.AimPoint.HasValue) return "the ground, " + Where(traveller.AimPoint.Value);
            return "the sky";
        }

        /// <summary>"14 m ahead, a little to the right, 6 m higher".</summary>
        string Where(Vector3 point)
        {
            Vector3 to = point - traveller.transform.position;
            Vector3 flat = Flat(to);
            float distance = flat.magnitude;
            var facing = Quaternion.Euler(0f, follow.Yaw, 0f);
            Vector3 local = Quaternion.Inverse(facing) * flat;
            float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            string side = Mathf.Abs(angle) < 15f ? "ahead" : Mathf.Abs(angle) < 45f ? (angle > 0 ? "ahead, a little to the right" : "ahead, a little to the left")
                : Mathf.Abs(angle) < 120f ? (angle > 0 ? "to the right" : "to the left") : "behind";
            string height = Mathf.Abs(to.y) < 1.2f ? "" : to.y > 0f ? $", {to.y:F0} m higher" : $", {-to.y:F0} m lower";
            return $"{distance:F0} m {side}{height}";
        }

        void Taken(Ember ember)
        {
            mind.Mark(true);
            int left = embers.Count(e => e && !e.Gone);
            hud.Note(left == 0 ? "the last ember" : left == 1 ? "one ember left" : $"{left} embers left", 4f);
            ShowCarry();
            if (left == 0 && !ended) StartCoroutine(Finale());
        }

        /// <summary>Plays the ending at once (tests).</summary>
        public void End()
        {
            if (!ended) StartCoroutine(Finale());
        }

        /// <summary>
        /// With the last ember the mind has enough light: it rises over the traveller and grows, space
        /// bending round it, its warm light turning to the colours of the orb at the beginning. White.
        /// It asks for its first words; whatever the player writes is kept, and becomes the message
        /// the orb flashes at the next beginning.
        /// </summary>
        IEnumerator Finale()
        {
            ended = true;
            ClearIdea();
            if (pending != null)
            {
                maker.Hide(pending);
                pending = null;
            }
            yield return new WaitForSeconds(2f);
            traveller.Scripted = true;
            hud.HideHint();
            hud.HideAim();
            float from = music ? music.volume : 0f;
            yield return Tween(3f, k => { if (music) music.volume = from * (1f - k); });
            if (music && finaleClip)
            {
                music.clip = finaleClip;
                music.volume = 0.55f;
                music.Play();
            }
            if (holeWarp) holeWarp.enabled = false;
            if (mindWarp) mindWarp.enabled = true;
            follow.Face(traveller.Body.eulerAngles.y, -14f);
            for (float t = 0f; t < 16f; t += Time.deltaTime)
            {
                float k = t / 16f;
                mind.Ascent = k * k * (3f - 2f * k);
                if (mindWarp)
                {
                    mindWarp.CoreRadius = Mathf.Lerp(0.1f, 0.9f, k);
                    mindWarp.Strength = Mathf.Lerp(0.3f, 1.8f, k);
                    mindWarp.Twist = 1f + 2f * k;
                }
                follow.Shake = k * k * 0.05f;
                if (ambience) ambience.volume = ambienceVolume * (1f - k);
                if (wind) wind.volume = windVolume * (1f - k);
                yield return null;
            }
            yield return Tween(1.2f, k => hud.White = k);
            if (music) music.Stop();
            follow.Shake = 0f;
            yield return new WaitForSeconds(1.5f);
            hud.ShowTitle("IT ASKS FOR ITS FIRST WORDS");
            yield return new WaitForSeconds(2.5f);
            string given = null;
            void Got(string w) => given = Letters(w).Length > 0 ? w : null;
            say.Said += Got;
            say.Placeholder = "give it its first words";
            while (given == null)
            {
                if (!say.IsOpen) say.Open();
                yield return null;
            }
            say.Said -= Got;
            hud.HideTitle();
            string message = Letters(given);
            PlayerPrefs.SetString(MorseEmitter.FirstWordsKey, message);
            PlayerPrefs.Save();
            Log(given, false, "first words: " + message, 0f, "rules");
            yield return new WaitForSeconds(1.5f);
            hud.ShowTitle(message);
            yield return new WaitForSeconds(4f);
            hud.HideTitle();
            yield return new WaitForSeconds(1.5f);
            yield return Tween(2f, k => hud.Black = k);
            SceneManager.LoadScene(0);
        }

        /// <summary>The words as the orb can flash them: capital letters, at most four words and 24 letters.</summary>
        static string Letters(string words)
        {
            var kept = new List<string>();
            int letters = 0;
            foreach (string word in words.ToUpperInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string w = new string(word.Where(c => c >= 'A' && c <= 'Z').ToArray());
                if (w.Length == 0) continue;
                if (kept.Count == 4 || letters + w.Length > 24) break;
                kept.Add(w);
                letters += w.Length;
            }
            return string.Join(" ", kept);
        }

        /// <summary>When the player has said nothing for a while, the mind shows an idea of its own.</summary>
        IEnumerator Ideas()
        {
            while (!arrived) yield return null;
            while (true)
            {
                yield return null;
                if (handling || maker.Busy || idea != null || pending != null || say.IsOpen || talk.Listening) { quietSince = Time.time; continue; }
                if (Time.time - quietSince < ideaAfter) continue;
                quietSince = Time.time;
                if (!firstIdeaTo || !firstIdeaTo.Known || maker.Made.Any(b => b && b.Kind == "BRIDGE")) continue;
                var plan = maker.Design(new Maker.Request
                {
                    Kind = "BRIDGE",
                    Matter = Matter.Stone,
                    From = traveller.transform.position,
                    To = firstIdeaTo.Point,
                    Size = 1f,
                }, out _);
                if (plan == null) continue;
                ShowIdea(plan, "(its own idea)");
                hud.ShowHint("[Q] or [Tab]   tell it \"do it\", or ask for something else");
                StartCoroutine(HideHintLater(8f));
            }
        }

        void ShowCarry()
        {
            var parts = Matters.All.Where(m => mind.Count(m) > 0 || m == Matter.Light).Select(m => $"{Matters.Word(m)} {mind.Count(m)}");
            hud.Carry(string.Join("     ", parts));
        }

        AudioClip Footstep()
        {
            AudioClip[] set = snowSteps;
            if (Physics.Raycast(traveller.transform.position + Vector3.up * 0.3f, Vector3.down, out var hit, 1f, ~0, QueryTriggerInteraction.Ignore))
            {
                var built = hit.collider.GetComponentInParent<Built>();
                if (built) set = built.Matter == Matter.Ice ? iceSteps : stoneSteps;
            }
            if (set == null || set.Length == 0) set = snowSteps;
            return set != null && set.Length > 0 ? set[random.Next(set.Length)] : null;
        }

        void Step(AudioClip clip, float volume)
        {
            if (!steps || !clip) return;
            // A little different every time: pitch by 3 per cent, volume by 1.5 dB.
            steps.pitch = 1f + ((float)random.NextDouble() - 0.5f) * 0.06f;
            steps.PlayOneShot(clip, volume * (0.84f + 0.32f * (float)random.NextDouble()));
        }

        IEnumerator StopAttending(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            mind.Attending = null;
        }

        IEnumerator HideHintLater(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            hud.HideHint();
        }

        static IEnumerator Tween(float seconds, Action<float> set)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                set(t / seconds);
                yield return null;
            }
            set(1f);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        static string Clean(string words)
        {
            var text = new StringBuilder();
            foreach (char c in words.ToLowerInvariant())
                if (char.IsLetter(c) || c == ' ') text.Append(c);
            return text.ToString().Trim();
        }

        void Log(string words, bool spoken, string read, float seconds, string source)
        {
            try
            {
                string line = "{\"time\":" + MetaApi.Quote(DateTime.UtcNow.ToString("o")) + ",\"words\":" + MetaApi.Quote(words) +
                              ",\"spoken\":" + (spoken ? "true" : "false") + ",\"read\":" + MetaApi.Quote(read) +
                              ",\"seconds\":" + seconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ",\"source\":" + MetaApi.Quote(source) +
                              ",\"holding\":" + MetaApi.Quote(mind.Holding()) + "}";
                File.AppendAllText(logPath, line + "\n");
            }
            catch (IOException) { }
        }
    }
}
