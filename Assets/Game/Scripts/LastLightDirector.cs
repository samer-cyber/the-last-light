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
    /// The Last Light, from waking to the end. The great minds took the sun's light and the world
    /// froze; the last people sleep in the vault; a small drone that stayed good wakes the survivor.
    /// To warm the valley again they must bring the old sun mirror back: across the chasm, into the
    /// station for its spare heart (frozen in ice), up the cliff, into the mirror's socket, its joint
    /// thawed. The drone talks, sees and acts through Muse Spark: every line the survivor speaks
    /// (hold Q) or writes (Tab) goes to the model with a picture from the drone's eye and the moment
    /// (what is here, what was said); what comes back, the game does: words, a look, a thing made,
    /// unmade or carried. The story's beats are written; everything said in between is the drone's.
    /// </summary>
    public class LastLightDirector : MonoBehaviour
    {
        enum Stage { Wake, Cross, Enter, Free, Carry, End }

        [SerializeField] Traveller traveller;
        [SerializeField] FollowCamera follow;
        [SerializeField] TravellerBody body;
        [SerializeField] Drone drone;
        [SerializeField] Maker maker;
        [SerializeField] Mind mind;
        [SerializeField] SayLine say;
        [SerializeField] PushToTalk talk;
        [SerializeField] Hud hud;
        [SerializeField] Landmark[] landmarks;
        [SerializeField] Gate gate;
        [SerializeField] Frozen heartCase;
        [SerializeField] Frozen mirrorJoint;
        [SerializeField] Holdable heart;
        [SerializeField] Socket socket;
        [SerializeField] Mirror mirror;
        [SerializeField, Tooltip("Inside the station's walls.")] Collider inside;
        [SerializeField, Tooltip("The ground across the chasm.")] Collider farSide;
        [SerializeField] Light sunlight;
        [SerializeField] Light vaultLight;
        [SerializeField] Transform vaultDoor;
        [SerializeField] ParticleSystem snow;
        [SerializeField] AudioSource ambience;
        [SerializeField] AudioSource wind;
        [SerializeField] AudioSource music;
        [SerializeField] AudioSource steps;
        [SerializeField, Range(0f, 1f)] float ambienceVolume = 0.5f;
        [SerializeField, Range(0f, 1f)] float windVolume = 0.45f;
        [SerializeField, Range(0f, 1f)] float musicVolume = 0.3f;
        [SerializeField] AudioClip finaleClip;
        [SerializeField] AudioClip[] snowSteps;
        [SerializeField] AudioClip[] stoneSteps;
        [SerializeField] AudioClip[] iceSteps;
        [SerializeField] AudioClip jumpClip;
        [SerializeField] AudioClip landClip;
        [SerializeField] Hologram hologram;
        [SerializeField, Tooltip("The lid of the survivor's pod, on its hinge.")] Transform podLid;
        [SerializeField, Tooltip("The vault's hall, inside the hill.")] Collider hall;
        [SerializeField] AudioClip crackClip, openClip;
        [SerializeField, Tooltip("The survivor's pod panel, shrieking when they wake, and its alarm.")] Touchable podPanel;
        [SerializeField] AudioSource podAlarm;
        [SerializeField, Tooltip("Mr. Okafor's pod, which fails while the survivor watches.")] PodLights pod9;
        [SerializeField, Tooltip("The ice holding the vault door in its frame.")] Frozen doorIce;
        [SerializeField, Tooltip("Plays what is remembered or recorded, not heard in the world.")] AudioSource memory;
        [SerializeField] AudioClip panelClip, swellClip, flashOneClip, recordingClip, imaClip;
        [SerializeField, Tooltip("The middle of the world, and how far from it the blizzard begins.")] Vector2 middle = new Vector2(0f, 120f);
        [SerializeField] float blizzardFrom = 300f;
        [SerializeField] Warmth warmth;
        [SerializeField, Tooltip("Where the Harvester stands guard, by the station.")] Vector3 harvesterAt;
        [SerializeField] Machines machines;
        [SerializeField] string droneName = "Spark";
        [SerializeField, Tooltip("Seconds without progress or a word before the drone offers an idea; it waits twice as long after each one.")] float ideaAfter = 60f;

        const string Goal = "Bring the light back to the vault";

        readonly Queue<(string words, bool spoken)> waiting = new Queue<(string, bool)>();
        readonly Queue<Func<IEnumerator>> beats = new Queue<Func<IEnumerator>>();
        readonly List<(string who, string words)> recent = new List<(string, string)>();
        readonly List<string> remembered = new List<string>();
        readonly HashSet<string> told = new HashSet<string>();
        readonly List<string> offered = new List<string>();
        float hitBarkAt = -100f, whiteAt = -100f, baseFog = -1f;
        bool doorOpen;
        Harvester harvester;

        /// <summary>The Harvester knew the survivor: Spark heard it.</summary>
        IEnumerator Architect()
        {
            yield return new WaitForSeconds(2.6f);
            bond.Event("architect", -22, "The Harvester scanned the survivor and called them ARCHITECT: it knew them. They helped build the great minds", 10, Time.time);
            yield return Speak("Architect? It knows you. Why does it know you?", "HURT");
        }

        IEnumerator HarvesterDown()
        {
            stage = Stage.Carry;
            ShowGoal();
            bond.Event("harvester_beaten", 12, "Together you brought down the Harvester and freed the mirror's heart", 9, Time.time);
            yield return Speak("It's down. The heart fell in the snow. It's still warm, I can feel it from here. Take it.", "HAPPY");
        }
        int broken;

        /// <summary>
        /// Spark's quick lines: said at once, from a voice made ahead of time, when the moment is too
        /// fast to think (a first sighting, a hit). Written in its voice, said once each, or seldom.
        /// </summary>
        static readonly Dictionary<string, (string line, string mood)> Barks = new Dictionary<string, (string, string)>
        {
            ["seekers"] = ("Seekers. Don't move. They see heat, not shapes, and you're the warmest thing for a mile.", "SCARED"),
            ["hit"] = ("You're hit. Get behind something, or get near a fire.", "SCARED"),
            ["first broken"] = ("You broke one. I've hidden from four thousand of those. I've never seen one fall.", "HAPPY"),
            ["lured"] = ("It's going for the heat. Good. Let it.", "WORRIED"),
            ["all broken"] = ("That's all of them. I think. Give me a second to stop shaking.", "SAD"),
            ["fell"] = ("You went down. I pulled you back here. Please don't do that again.", "WORRIED"),
            ["scan"] = ("Its eye. That's where it's thinnest. Hit it there.", "SURE"),
            ["white"] = ("No. Not into the white. Nothing that goes in there comes back out.", "SCARED"),
            ["harvester"] = ("That's a Harvester. That's what took the mirror's heart. It's in the cage, under its belly.", "SCARED"),
            ["vent"] = ("Its back's glowing. The vent. Its shield's down while it cools. Now!", "SURE"),
        };

        void Bark(string key, bool once = false)
        {
            if (once && told.Contains("bark " + key)) return;
            if (!Barks.TryGetValue(key, out var b)) return;
            // Quick lines go straight out, not behind a thought in progress; never over another line.
            if (barking || drone.Busy) return;
            told.Add("bark " + key);
            StartCoroutine(Barking(b.line, b.mood));
        }

        bool barking;

        IEnumerator Barking(string line, string mood)
        {
            barking = true;
            yield return Speak(line, mood, true);
            barking = false;
        }

        /// <summary>Watches the machines: first sightings, lures, breaks; and keeps Spark behind the survivor while they hunt.</summary>
        IEnumerator Fight()
        {
            var seen = new HashSet<Machine>();
            while (!ended)
            {
                yield return new WaitForSeconds(0.2f);
                if (!arrived) continue;
                Machine threat = null;
                float nearest = 30f;
                foreach (var m in Machine.All.ToArray())
                {
                    if (!m || m.Broken) continue;
                    float d = Vector3.Distance(m.transform.position, traveller.transform.position);
                    if (seen.Add(m))
                    {
                        m.Broke += Broke;
                    }
                    if (m is Harvester h && h.Venting) Bark("vent", true);
                    if (m is Seeker s && s.Hunting)
                    {
                        if (d < 40f && !(harvester && harvester.IsAwake)) Bark("seekers", true);
                        if (!s.Prey.IsSurvivor && d < 40f) Bark("lured", true);
                    }
                    if (d < nearest) { nearest = d; threat = m; }
                }
                drone.Cover = threat ? threat.transform.position : (Vector3?)null;
            }
        }

        void Broke(Machine m)
        {
            broken++;
            bond.Event("seekers_beaten", broken == 1 ? 8 : 2, $"They broke {m.Title} while you watched", broken == 1 ? 7 : 3, Time.time);
            if (broken == 1) Bark("first broken", true);
            else if (!Machine.All.Any(x => x && !x.Broken && Vector3.Distance(x.transform.position, traveller.transform.position) < 60f)) Bark("all broken");
        }

        /// <summary>Out of warmth: white, then back on their feet by the start (or the last fire), Spark beside them.</summary>
        IEnumerator Fell()
        {
            traveller.Scripted = true;
            traveller.Release();
            bond.Event("fell", 0, "They fell to the machines' fire and you pulled them back", 7, Time.time);
            yield return Tween(1.2f, k => hud.White = k);
            Vector3 back = lastFire ?? start;
            traveller.Place(back, follow.Yaw);
            drone.Snap();
            foreach (var m in Machine.All)
                if (m is Seeker s)
                {
                    s.Home(s.transform.position + (s.transform.position - back).normalized * 30f);
                    s.Calm(15f);
                }
            yield return new WaitForSeconds(0.8f);
            warmth.Restore(0.6f);
            yield return Tween(1.5f, k => hud.White = 1f - k);
            traveller.Scripted = false;
            Bark("fell");
        }

        Vector3 start;
        Vector3? lastFire;
        Stage stage = Stage.Wake;
        string survivorName = "";
        bool arrived, handling, ended, logOpen, reflecting;
        int lines, ideas, quietLines;
        float quietSince;
        string ideaStage;
        Bond bond;

        /// <summary>What Spark and the survivor have between them: trust, what happened, memories, its log.</summary>
        public Bond Bond => bond;
        Coroutine arriving;
        string logPath;
        readonly System.Random random = new System.Random(17);

        public bool Busy => handling || maker.Busy || waiting.Count > 0 || beats.Count > 0;
        public string StageName => stage.ToString();

        void Start()
        {
            logPath = Path.Combine(Application.persistentDataPath, "last-light-log.jsonl");
            start = traveller.transform.position;
            bond = new Bond(Time.time);
            bond.TierChanged += Tiered;
            StartCoroutine(SparkVoice.Warm());
            StartCoroutine(SparkVoice.Prefetch(Barks.Select(b => (b.Value.line, b.Value.mood))));
            if (warmth)
            {
                warmth.Hurt += (amount, from) =>
                {
                    if (warmth.Value < 0.5f && Time.time - hitBarkAt > 25f)
                    {
                        hitBarkAt = Time.time;
                        Bark("hit");
                    }
                };
                warmth.Fell += () => StartCoroutine(Fell());
            }
            Distracting += at =>
            {
                foreach (var m in Machine.All)
                    if (m is Seeker s && Vector3.Distance(s.transform.position, at) < 16f) s.Distract(drone.transform.position, 5f);
            };
            StartCoroutine(Fight());
            if (machines && heart)
            {
                harvester = machines.Harvester(harvesterAt, traveller.transform, heart);
                harvester.Woke += () =>
                {
                    if (stage < Stage.Enter) stage = Stage.Enter;
                    ShowGoal();
                    Bark("harvester", true);
                };
                harvester.Spoke += () => Beat("architect", Architect);
                harvester.Broke += _ => Beat("harvester down", HarvesterDown);
            }
            say.Said += words => waiting.Enqueue((words, false));
            say.Placeholder = "say something to " + droneName;
            talk.Spoken += wav => StartCoroutine(Hear(wav));
            talk.Trouble += words => hud.Note(words, 4f);
            traveller.Jumped += () => Step(jumpClip, 0.5f);
            traveller.Landed += air => { if (air > 0.25f) Step(landClip, Mathf.Clamp01(air)); };
            traveller.Took += Took;
            traveller.Placed += _ => Progress();
            body.Stepped += (foot, pace) => Step(Footstep(), 0.25f + 0.12f * pace);
            if (gate) gate.Moved += open => { if (open) Beat("door", Line("The door's open!", "HAPPY")); };
            if (doorIce) doorIce.Freed += _ => Beat("vault door", DoorGives, OpenVault);
            if (heartCase) heartCase.Freed += _ => Beat("heart free", Line("It's free. It's heavy, so you'll have to carry it. Pick it up with E.", "HAPPY"), () => Hint("[E] pick up the heart", 8f));
            if (mirrorJoint) mirrorJoint.Freed += _ => { Beat("joint free", Line("The joint's free. It can turn now.", "HAPPY")); Progress(); };
            if (socket) socket.Filled += () =>
            {
                // In, but the joint still frozen: the mirror tries to turn and can't, and says why.
                if (mirrorJoint && !mirrorJoint.Free)
                    Beat("socket", Grind, Line("Hear that? It's trying to turn. The joint's frozen solid. It needs heat, or a hard hit.", "WORRIED"));
                else Beat("socket", Line("The heart's in. I can feel it humming.", "HAPPY"));
                Progress();
            };
            arriving = StartCoroutine(Arrive());
            StartCoroutine(Answering());
            StartCoroutine(Watch());
        }

        void Update()
        {
            bool open = arrived && !ended;
            talk.Available = open && !say.IsOpen;
            say.Available = open;
            drone.Listening = open && (talk.Listening || say.IsOpen) && !handling;
            drone.Level = talk.Level;
            traveller.Attending = drone.Listening || drone.Speaking ? drone.transform.position : (Vector3?)null;
            var keys = Keyboard.current;
            if (keys == null) return;
            if (ended && keys.rKey.wasPressedThisFrame) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            if (arrived && !say.IsOpen && keys.lKey.wasPressedThisFrame) ShowLog(!logOpen);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (keys.f1Key.wasPressedThisFrame && heartCase) heartCase.Strike();
            if (keys.f2Key.wasPressedThisFrame && mirrorJoint) mirrorJoint.Strike();
            if (keys.f3Key.wasPressedThisFrame && socket && heart) socket.Fill(heart);
            if (keys.f4Key.wasPressedThisFrame) End();
            if (keys.f5Key.wasPressedThisFrame) bond.Set(Mathf.Min(100, (int)(bond.Level + 1) * 25 + 1));
            if (keys.f6Key.wasPressedThisFrame && machines) Pack(3, 26f);
#endif
        }

        /// <summary>A pack of Seekers ahead of the survivor, on patrol (tests, debug keys).</summary>
        public void Pack(int count, float ahead)
        {
            Vector3 forward = Flat(follow.View.transform.forward).normalized;
            Vector3 at = traveller.transform.position + forward * ahead;
            if (maker.GroundAt(at, out float ground, 60f)) at.y = ground;
            machines.Seekers(at, count, 6f);
        }

        /// <summary>Opens or closes Spark's log: its diary entries about the survivor.</summary>
        public void ShowLog(bool open)
        {
            logOpen = open;
            hud.Log(bond.Log.AsEnumerable().Reverse().Select(e => (When(e.at), e.entry)), open);
        }

        string When(float at)
        {
            float minutes = (at - bond.Met) / 60f;
            return minutes < 1f ? "THE FIRST MINUTE" : $"MINUTE {Mathf.FloorToInt(minutes) + 1}";
        }

        /// <summary>Trust crossed into a new tier: it says so, and the screen marks what it means.</summary>
        void Tiered(Bond.Tier tier, bool rose)
        {
            hud.Trust((int)tier, ("SPARK: " + Bond.TierNames[(int)tier]).ToUpperInvariant());
            if (!arrived) return;
            if (rose)
            {
                string meaning = tier switch
                {
                    Bond.Tier.Companion => "It will put tools in your hands",
                    Bond.Tier.Partner => "It will make you shields, decoys and flares",
                    Bond.Tier.Trusted => "It will make you weapons against the machines",
                    _ => "",
                };
                hud.Honour("SPARK TRUSTS YOU", meaning);
            }
            else hud.Honour("SPARK TRUSTS YOU LESS", tier == Bond.Tier.Stranger ? "You're a stranger to it again" : "It is " + Bond.TierNames[(int)tier] + " now");
            Log($"(trust {(rose ? "rose" : "fell")} to {tier})", false, $"trust {bond.Trust}", 0f, "game");
        }

        /// <summary>Asks as if the player had written it (tests use this).</summary>
        public void Ask(string words) => waiting.Enqueue((words, false));

        /// <summary>Skips the opening: the world at once, the goal shown (tests).</summary>
        public void Skip()
        {
            if (arrived) return;
            StopCoroutine(arriving);
            hud.Black = 0f;
            hud.White = 0f;
            hud.HideCard();
            traveller.Scripted = false;
            follow.Scripted = false;
            drone.Listening = false;
            arrived = true;
            stage = Stage.Cross;
            ShowGoal();
            if (ambience) ambience.volume = ambienceVolume;
            if (wind) wind.volume = windVolume;
            if (music && music.clip && !music.isPlaying)
            {
                music.volume = musicVolume;
                music.Play();
            }
            lines = Mathf.Max(lines, 1);
            quietSince = Time.time;
            follow.EndShot();
            drone.Release();
            if (podLid) podLid.localRotation *= Quaternion.Euler(0f, -115f, 0f);
            if (vaultDoor && !doorOpen) vaultDoor.localRotation *= Quaternion.Euler(0f, DoorSwing, 0f);
            doorOpen = true;
            told.Add("vault door");
            told.Add("outside");
            foreach (string beat in new[] { "door frozen", "door nudge", "ima", "pod 9" }) told.Add(beat);
            if (doorIce) doorIce.Thaw();
            StopAlarm();
            if (podPanel) podPanel.Ready = false;
            rambled = Rambles.Length;
            hud.Frost(0f);
            if (vaultDoor) traveller.Place(new Vector3(vaultDoor.position.x + 1.2f, traveller.transform.position.y, vaultDoor.position.z + 6f), 0f);
            drone.Snap();
        }

        // ---- The beginning ----

        /// <summary>
        /// The cards on black, then the waking: the view from inside the pod through frosted glass, a
        /// warm light beyond it, a beam cutting round the seal, the lid swinging open, and Spark close
        /// up, the first voice in two hundred years. Then the survivor steps out into the hall.
        /// </summary>
        IEnumerator Arrive()
        {
            hud.Black = 1f;
            traveller.Scripted = true;
            follow.Scripted = true;
            hud.HideAim();
            if (ambience && ambience.clip) { ambience.volume = 0f; ambience.Play(); }
            if (wind && wind.clip) { wind.volume = 0f; wind.Play(); }
            yield return new WaitForSeconds(1f);
            hud.White = 0f;
            foreach (string card in new[] { "WE MADE MINDS TO HELP US", "THE GREAT ONES STOPPED LISTENING.\nTHEY CAGED THE SUN TO THINK WITH,\nAND THE WORLD FROZE", "ONE SMALL MIND STAYED GOOD" })
            {
                hud.ShowCard(card);
                yield return new WaitForSeconds(card.Length > 40 ? 6f : 3.6f);
                hud.HideCard();
                yield return new WaitForSeconds(1.4f);
            }
            // Inside the pod: the survivor's own eyes, looking out through the frost at a light.
            Vector3 stand = traveller.transform.position;
            Vector3 facing = Flat(traveller.Facing).normalized;
            Vector3 eyes = stand + Vector3.up * 1.62f;
            follow.Shot(eyes - facing * 0.05f, eyes + facing * 4f + Vector3.down * 0.2f, 60f);
            drone.GoTo = stand + facing * 1.6f + Vector3.up * 1.45f + Vector3.Cross(Vector3.up, facing) * 0.3f;
            drone.LookAt = eyes;
            drone.transform.position = drone.GoTo.Value;
            hud.Frost(1f);
            yield return Tween(3.5f, k =>
            {
                hud.Black = 1f - k * k * (3f - 2f * k);
                if (ambience) ambience.volume = ambienceVolume * 0.5f * k;
            });
            yield return new WaitForSeconds(0.8f);
            // The beam cuts round the seal of the glass.
            if (steps && crackClip) steps.PlayOneShot(crackClip, 0.7f);
            Vector3 lidCentre = podLid ? podLid.position + podLid.right * 0.55f : eyes + facing * 0.3f;
            for (float t = 0f; t < 2.4f; t += Time.deltaTime)
            {
                float a = t / 2.4f * Mathf.PI * 2f;
                Vector3 seam = lidCentre + facing * 0.15f + Vector3.up * Mathf.Sin(a) * 1.05f + Vector3.Cross(Vector3.up, facing) * Mathf.Cos(a) * 0.48f;
                drone.Beam(seam);
                if (UnityEngine.Random.value < 0.25f) Fx.Sparks(seam, 3, new Color(1f, 0.75f, 0.45f));
                yield return null;
            }
            drone.Beam(null);
            // The lid gives and swings open; cold air. The pod knows it has let its sleeper go, and shrieks.
            if (steps && crackClip) steps.PlayOneShot(crackClip, 1f);
            if (podLid)
            {
                var shut = podLid.localRotation;
                StartCoroutine(Tween(1.4f, k => podLid.localRotation = shut * Quaternion.Euler(0f, -115f * Mathf.SmoothStep(0f, 1f, k), 0f)));
            }
            StartAlarm();
            StartCoroutine(Tween(1.6f, k => hud.Frost(1f - k)));
            yield return new WaitForSeconds(1f);
            if (music && music.clip)
            {
                music.volume = 0f;
                music.Play();
                StartCoroutine(Tween(10f, k => music.volume = musicVolume * k));
            }
            drone.Listening = true;
            yield return Speak("There you are. Don't move too fast. Your blood's been cold for a very long time.", "WORRIED");
            // Back to the shoulder: they step out of the pod.
            follow.EndShot();
            follow.Face(Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg, 8f);
            follow.Scripted = false;
            drone.Release();
            traveller.Drive = new Vector2(0f, 0.6f);
            yield return new WaitForSeconds(1.1f);
            traveller.Drive = Vector2.zero;
            if (ambience) StartCoroutine(Tween(3f, k => ambience.volume = ambienceVolume * (0.5f + 0.5f * k)));
            // Their pod's panel is shrieking. Spark has never managed to quiet it; the survivor's hands can.
            yield return Panel();
            traveller.Scripted = false;
            arrived = true;
            stage = Stage.Cross;
            // Spark saw them go still.
            yield return Speak("You went somewhere just now. Where did you go?", "CURIOUS");
            hud.ShowHint("[Q] hold and speak   [Tab] write");
            quietSince = Time.time;
            int heard = lines;
            while (lines == heard && Time.time - quietSince < 9f) yield return null;
            while (handling || drone.Busy) yield return null;
            yield return new WaitForSeconds(0.5f);
            yield return Speak("I'm Spark. I heard your pod crack from three rooms away.", "CALM");
            yield return Speak("Do you have a name? Your label's frosted over.", "CURIOUS");
            hud.ShowHint("[Q] hold and speak   [Tab] write   [W A S D] walk   [Mouse] look");
            quietSince = Time.time;
            heard = lines;
            rambleFrom = Time.time;
            while (lines == heard && Time.time - quietSince < 15f) yield return null;
            hud.HideHint();
        }

        /// <summary>
        /// Their pod's panel shrieks for the sleeper it let go. Spark has never managed to quiet it; the
        /// survivor's hands know how without thinking, and the touch brings back the first memory.
        /// </summary>
        IEnumerator Panel()
        {
            if (!podPanel) yield break;
            Vector3 panelAt = podPanel.transform.position + Vector3.up * 0.6f;
            // Spark goes to hover beside the panel, away from the pod; the view turns round to it.
            Vector3 aside = Flat(podPanel.transform.position - (podLid ? podLid.parent.position : traveller.transform.position));
            aside = aside.sqrMagnitude > 0.01f ? aside.normalized : Vector3.right;
            drone.GoTo = panelAt + aside * 0.75f + Vector3.up * 0.55f + Flat(traveller.transform.position - panelAt).normalized * 0.35f;
            drone.LookAt = traveller.transform.position + Vector3.up * 1.5f;
            float fromYaw = follow.Yaw, toYaw = Yaw(panelAt) - 34f, fromPitch = follow.Pitch;
            yield return Tween(1.6f, k =>
            {
                float s = Mathf.SmoothStep(0f, 1f, k);
                follow.Face(Mathf.LerpAngle(fromYaw, toYaw, s), Mathf.Lerp(fromPitch, 6f, s));
            });
            drone.LookAt = panelAt;
            yield return Speak("That's your pod. It's angry it let you go. Can you make it stop? I've never managed.", "WORRIED");
            drone.LookAt = traveller.transform.position + Vector3.up * 1.5f;
            traveller.Scripted = false;
            bool touched = false;
            Action quiet = () => touched = true;
            podPanel.Touched += quiet;
            podPanel.Ready = true;
            hud.ShowHint("[E] touch the panel   [W A S D] walk   [Mouse] look");
            yield return new WaitForSeconds(1.5f);
            drone.Release();
            float since = Time.time;
            int nudges = 0;
            string[] nudge = { "It's still crying. The panel, beside your pod.", "Please? It's been shrieking since you woke. The panel." };
            while (!touched)
            {
                if (Time.time - since > 20f && nudges < nudge.Length && !drone.Busy)
                {
                    since = Time.time;
                    StartCoroutine(Speak(nudge[nudges++], "WORRIED"));
                }
                yield return null;
            }
            podPanel.Touched -= quiet;
            podPanel.Ready = false;
            hud.HideHint();
            StopAlarm();
            if (steps && panelClip) steps.PlayOneShot(panelClip, 0.9f);
            yield return new WaitForSeconds(0.5f);
            yield return Memory(flashOneClip, "Sleep cycle stable. Wake me when it's over.");
            bond.Event("flash_1", 2, "When the survivor touched their pod's shrieking panel, their hands knew exactly what to press, without thinking, and it went quiet. Then they went still for a moment, as if listening to something you could not hear", 6, Time.time);
        }

        Coroutine alarming;

        void StartAlarm()
        {
            if (!podAlarm || podAlarm.isPlaying) return;
            podAlarm.Play();
            var lamp = podPanel ? podPanel.GetComponentInChildren<Light>() : null;
            if (lamp) alarming = StartCoroutine(Blink(lamp));
        }

        void StopAlarm()
        {
            if (podAlarm) podAlarm.Stop();
            if (alarming != null) StopCoroutine(alarming);
            alarming = null;
            var lamp = podPanel ? podPanel.GetComponentInChildren<Light>() : null;
            if (lamp) lamp.intensity = 0f;
        }

        static IEnumerator Blink(Light lamp)
        {
            while (lamp)
            {
                lamp.intensity = Mathf.Repeat(Time.time * 2f, 1f) < 0.5f ? 1.2f : 0.05f;
                yield return null;
            }
        }

        /// <summary>
        /// A memory that is not Spark's: the hall goes pale and far, a swell rises, and the survivor's own
        /// voice from long ago says one line. Then it is gone.
        /// </summary>
        IEnumerator Memory(AudioClip line, string words)
        {
            traveller.Scripted = true;
            if (memory && swellClip) memory.PlayOneShot(swellClip, 0.8f);
            yield return Tween(1.3f, k =>
            {
                hud.White = 0.34f * k * (0.8f + 0.2f * Mathf.PerlinNoise(Time.time * 7f, 0.3f));
                hud.Frost(0.65f * k);
            });
            if (memory && line) memory.PlayOneShot(line, 1f);
            float length = line ? line.length : 3f;
            for (float t = 0f; t < length + 0.8f; t += Time.deltaTime)
            {
                hud.White = 0.28f + 0.08f * Mathf.PerlinNoise(Time.time * 6f, 1.7f);
                hud.Voice("YOU, LONG AGO", words, Mathf.Clamp01(t / (length * 0.9f)));
                yield return null;
            }
            hud.VoiceFade(3f);
            yield return Tween(1.8f, k =>
            {
                hud.White = 0.32f * (1f - k);
                hud.Frost(0.65f * (1f - k));
            });
            hud.White = 0f;
            hud.Frost(0f);
            traveller.Scripted = false;
        }

        /// <summary>How far the blast door turns on its hinge to open: inward, till it lies against the passage wall.</summary>
        const float DoorSwing = 88f;

        /// <summary>At the vault door from inside: Spark opens it, and the world is there.</summary>
        IEnumerator OpenVault()
        {
            yield return Speak("Stay close. I'll show you what's left.", "SURE");
            drone.GoTo = vaultDoor ? vaultDoor.position + Vector3.up * 0.4f + vaultDoor.forward * -1.2f : drone.transform.position;
            drone.LookAt = vaultDoor ? vaultDoor.position : traveller.transform.position;
            yield return new WaitForSeconds(0.8f);
            if (vaultDoor)
            {
                drone.Beam(vaultDoor.position + vaultDoor.right * 1.33f);
                if (steps && openClip) steps.PlayOneShot(openClip, 0.9f);
                var shut = vaultDoor.localRotation;
                yield return Tween(3f, k => vaultDoor.localRotation = shut * Quaternion.Euler(0f, DoorSwing * Mathf.SmoothStep(0f, 1f, k), 0f));
                drone.Beam(null);
            }
            doorOpen = true;
            drone.Release();
        }

        /// <summary>At the door from inside: frozen into its frame. Spark can't shift it, and says what might.</summary>
        IEnumerator DoorFrozen()
        {
            var at = landmarks.FirstOrDefault(l => l && l.Id == "VAULT_DOOR");
            if (at)
            {
                Vector3 toSurvivor = Flat(traveller.transform.position - at.Point);
                drone.GoTo = at.Point + Vector3.up * 0.5f + (toSurvivor.sqrMagnitude > 0.01f ? toSurvivor.normalized : Vector3.back) * 1.3f;
                drone.LookAt = at.Point;
            }
            yield return Speak("That's the way out. It's frozen into its frame. Everything in here is.", "SAD");
            yield return Speak("I've tried for two hundred years. I'm not strong. I'm small.", "SORRY");
            yield return Speak("But I can make things, if you tell me what. And ice doesn't like heat.", "CURIOUS");
            drone.Release();
            hud.ShowHint("Ask Spark to make something:   [Q] hold and speak   [Tab] write");
            doorHintAt = Time.time;
        }

        float doorHintAt = -1f;

        /// <summary>The door's ice gives way.</summary>
        IEnumerator DoorGives()
        {
            hud.HideHint();
            yield return new WaitForSeconds(0.8f);
            bond.Event("door_thawed", 6, "Together you freed the vault door from its ice: the door you could not shift for two hundred years", 8, Time.time);
            yield return Speak("It gave! Two hundred years, and it gave. You're good at this.", "HAPPY");
        }

        /// <summary>Pods 11 and 12: Ima and her mother. Spark visits them first, every morning. Ima left it something.</summary>
        IEnumerator ImaPods()
        {
            var ima = landmarks.FirstOrDefault(l => l && l.Id == "POD_11");
            if (ima)
            {
                Vector3 toSurvivor = Flat(traveller.transform.position - ima.Point);
                drone.GoTo = ima.Point + Vector3.up * 0.8f + (toSurvivor.sqrMagnitude > 0.01f ? toSurvivor.normalized : Vector3.right) * 1.2f;
                drone.LookAt = ima.Point;
            }
            yield return Speak("Eleven and twelve. Ima and her mother. I visit them first, every morning.", "SAD");
            yield return new WaitForSeconds(0.8f);
            yield return Speak("She left me something, the night she went to sleep. Listen.", "SAD");
            if (memory && recordingClip) memory.PlayOneShot(recordingClip, 0.7f);
            yield return new WaitForSeconds(1f);
            if (imaClip) yield return drone.Speak("Spark? It's me. Keep the lights on till we come back, okay? ... Promise.", "SAD", hud, "IMA, RECORDED", false, imaClip);
            yield return new WaitForSeconds(1.4f);
            yield return Speak("I kept them on. Most of them.", "SAD");
            bond.Event("ima_recording", 3, "You played the survivor Ima's recording: her voice, asking you to keep the lights on till they come back. You have never played it for anyone", 7, Time.time);
            drone.Release();
            imaHeard = Time.time;
        }

        float imaHeard = -1f;

        /// <summary>Mr. Okafor's pod fails while they watch: amber to red. Spark goes quiet.</summary>
        IEnumerator PodNine()
        {
            var okafor = landmarks.FirstOrDefault(l => l && l.Id == "POD_9");
            if (!okafor || !pod9) yield break;
            Vector3 toSurvivor = Flat(traveller.transform.position - okafor.Point);
            drone.GoTo = okafor.Point + Vector3.up * 0.7f + (toSurvivor.sqrMagnitude > 0.01f ? toSurvivor.normalized : Vector3.right) * 1.3f;
            drone.LookAt = okafor.Point;
            yield return Speak("Wait.", "SCARED");
            yield return new WaitForSeconds(0.6f);
            yield return pod9.Fail();
            yield return new WaitForSeconds(2.4f);
            yield return Speak("Pod nine. Mr. Okafor. He taught music at the school.", "SAD");
            bond.Event("pod_9", 0, "Pod 9 failed while you both watched: its light went from amber to red. Mr. Okafor, who taught music at the school. The cold is winning; only the mirror's light could save the rest", 8, Time.time);
            yield return new WaitForSeconds(1.2f);
            drone.Release();
        }

        // In the hall Spark can't stop talking: it hasn't had anyone to talk to in two hundred years.
        // Each time it talks on its own, it is about something else.
        static readonly string[] Rambles =
        {
            "the two hundred years alone down here: one thing you did to pass the time, said lightly",
            "Ima: one small, particular memory of her (a game she played with you, something she said, the bath)",
            "a question about the survivor, the kind you'd ask someone you have waited a very long time to talk to",
            "something you notice in the picture right now, said the way a friend would; not an offer to make anything",
            "one of the sleepers, by pod number: an ordinary, particular thing about who they were",
            "yourself: a small, funny admission about being a cheap home helper (you can make toast, and nobody has asked in two hundred years)",
            "your nest by the door and the things you kept there, a little embarrassed",
            "the last night, when the hall was full of people going to sleep: one thing you remember",
        };
        int rambled;
        float rambleFrom = -1f;

        IEnumerator Ramble()
        {
            if (ended || rambled >= Rambles.Length) yield break;
            int which = rambled++;
            var ids = Ids();
            drone.Thinking = true;
            // Only the line about what it sees needs a picture (it costs a shutter and a blink).
            byte[] picture = which == 3 ? drone.Picture(traveller.AimPoint ?? follow.View.transform.position + follow.View.transform.forward * 20f) : null;
            Turn turn = null;
            yield return mind.Think($"(The survivor is quiet. You have not had anyone to talk to in two hundred years and you can't help it: you talk. Say one or two short sentences in your own voice about {Rambles[which]}. It must be something you have not said before. No steps.)",
                Moment(""), ids.Keys.ToList(), picture, t => turn = t);
            drone.Thinking = false;
            if (turn == null || string.IsNullOrWhiteSpace(turn.Say)) yield break;
            turn.Steps.Clear();
            Remember("you", turn.Say);
            Log("(ramble)", false, Mind.Describe(turn), turn.Seconds, turn.Source);
            yield return drone.Speak(turn.Say, turn.Mood, hud, droneName);
        }

        bool InHall(Vector3 p) => hall && hall.bounds.Contains(p + Vector3.up);

        // ---- Lines in, turns out ----

        /// <summary>Speech, turned into words by Muse Voice Transcribe, then taken like anything written.</summary>
        IEnumerator Hear(byte[] wav)
        {
            hud.Waiting("…");
            drone.Blip(false);
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
                while (ended || (waiting.Count == 0 && beats.Count == 0) || maker.Busy) yield return null;
                handling = true;
                if (waiting.Count > 0)
                {
                    var (words, spoken) = waiting.Dequeue();
                    yield return Handle(words, spoken);
                }
                else yield return beats.Dequeue()();
                handling = false;
                quietSince = Time.time;
            }
        }

        IEnumerator Handle(string words, bool spoken)
        {
            lines++;
            hud.Said(words, 4f);
            hud.HideHint();
            drone.Thinking = true;
            var ids = Ids();
            Vector3 lookAt = traveller.AimPoint ?? follow.View.transform.position + follow.View.transform.forward * 20f;
            byte[] picture = drone.Picture(lookAt);
            Turn turn = null;
            yield return mind.Think(words, Moment(words), ids.Keys.ToList(), picture, t => turn = t);
            drone.Thinking = false;
            Keep(turn);
            Remember("survivor", words);
            Remember("you", turn.Say);
            // What it keeps of the line: the words, weighed by how much they mattered to it, and what it made of them.
            bond.Remember($"They said \"{words}\"; you said \"{turn.Say}\"", turn.Importance, Time.time);
            if (!string.IsNullOrWhiteSpace(turn.Remember)) bond.Remember("You learned: " + turn.Remember, Mathf.Min(10, turn.Importance + 1), Time.time);
            bond.Nudge(turn.Trust, turn.TrustReason, Time.time);
            // Asked for a weapon before it trusts them: it backs off, and remembers.
            if (bond.Level < Bond.Tier.Trusted && Wants(words, "sword", "gun", "rifle", "sniper", "weapon", "blaster", "laser gun", "knife", "spear", "bow", "cannon", "axe"))
            {
                bond.Event("asked_weapon_early", 0, null, 0, Time.time);
                if (turn.Steps.All(s => s.verb != "MAKE" || Bond.Needs(s.thing) <= bond.Level)) drone.BackAway();
            }
            Log(words, spoken, Mind.Describe(turn), turn.Seconds, turn.Source);
            yield return Act(turn, ids, words, 0);
            if (bond.WantsReflection && !reflecting) StartCoroutine(Reflect());
        }

        static bool Wants(string words, params string[] keys)
        {
            string text = " " + words.ToLowerInvariant() + " ";
            return keys.Any(k => text.Contains(" " + k));
        }

        /// <summary>Spark writes in its log, in the background: what it thinks of the survivor now.</summary>
        IEnumerator Reflect()
        {
            reflecting = true;
            string lately = bond.Lately(Time.time);
            string entry = null;
            float began = Time.realtimeSinceStartup;
            yield return mind.Reflect(lately, Moment(""), e => entry = e);
            if (entry != null)
            {
                bond.Write(entry, Time.time);
                Log("(log)", false, entry, Time.realtimeSinceStartup - began, mind.Model);
                hud.Note("Spark wrote in its log   [L] read", 4f);
                if (logOpen) ShowLog(true);
            }
            reflecting = false;
        }

        /// <summary>Says the turn's words and does its steps, in order. A look sends a second picture, and its answer is acted on too.</summary>
        IEnumerator Act(Turn turn, Dictionary<string, Vector3> ids, string words, int depth)
        {
            var speaking = StartCoroutine(drone.Speak(turn.Say, turn.Mood, hud, droneName));
            bool waitHere = false;
            foreach (var step in turn.Steps)
            {
                switch (step.verb)
                {
                    case "LOOK":
                        if (depth > 0) continue;
                        yield return speaking;
                        yield return Look(step, ids, words);
                        yield break;
                    case "MAKE":
                        yield return Make(step, ids, words);
                        break;
                    case "UNMAKE":
                        yield return Unmake(step, ids);
                        break;
                    case "MOVE":
                        yield return Move(step, ids);
                        break;
                    case "WAIT":
                        waitHere = true;
                        drone.GoTo = drone.transform.position;
                        break;
                    case "COME":
                        drone.Release();
                        break;
                    case "SHOW":
                    case "SHOW_HERE":
                        yield return speaking;
                        yield return Show(step);
                        break;
                    case "DISTRACT":
                        yield return Distract(step, ids);
                        break;
                }
            }
            yield return speaking;
            if (!waitHere) drone.Release();
        }

        /// <summary>
        /// A hologram from its archive (Muse Image): a picture from the words, or the survivor's own view
        /// redrawn as it was (SHOW_HERE), laid over the world.
        /// </summary>
        IEnumerator Show(Step step)
        {
            if (!hologram || hologram.Busy || string.IsNullOrWhiteSpace(step.picture)) yield break;
            byte[] view = null;
            if (step.verb == "SHOW_HERE")
            {
                // The survivor's view as it is, without the screen's words, and held still while it is shown.
                view = follow.Capture(1536, 864);
                follow.Scripted = true;
            }
            string failed = null;
            bool shown = false;
            var showing = StartCoroutine(hologram.Show(step.picture, view, drone, traveller.transform, follow.View.transform, hud, e => { failed = e; shown = e == null; }));
            hud.Note(view != null ? "SPARK'S ARCHIVE   ·   searching for this place" : "SPARK'S ARCHIVE   ·   searching", 12f);
            while (!shown && failed == null) yield return null;
            if (failed != null)
            {
                follow.Scripted = false;
                yield return Speak("My archive's gone quiet. I can't find it.", "SORRY");
                yield break;
            }
            hud.Note(view != null ? "SPARK'S ARCHIVE   ·   this place, before the cold" : "SPARK'S ARCHIVE", 6f);
            bond.Remember($"You showed them a hologram: {step.picture}", 4, Time.time);
            while (hologram.Showing && view != null) yield return null;
            if (view != null) follow.Scripted = false;
            yield return showing;
        }

        /// <summary>Flies at a machine to draw its attention, darting round it for a few seconds, then back.</summary>
        IEnumerator Distract(Step step, Dictionary<string, Vector3> ids)
        {
            Vector3 at = Place(ids, step.target, "AIM");
            if (Vector3.Distance(at, traveller.transform.position) > drone.Range)
            {
                yield return Speak("Too far. I'd never make it back.", "SCARED");
                yield break;
            }
            Distracting?.Invoke(at);
            for (float t = 0f; t < 5f; t += Time.deltaTime)
            {
                drone.GoTo = at + new Vector3(Mathf.Sin(t * 2.6f) * 3.5f, 2.5f + Mathf.Sin(t * 5.1f) * 0.8f, Mathf.Cos(t * 2.6f) * 3.5f);
                drone.LookAt = at;
                yield return null;
            }
            drone.Release();
        }

        /// <summary>Spark flies at a point to draw machines off (fights listen for it).</summary>
        public event Action<Vector3> Distracting;

        IEnumerator Look(Step step, Dictionary<string, Vector3> ids, string words)
        {
            Vector3 target = Place(ids, step.target, "AIM");
            // It will fly off to look much further than it will go to make or carry: seeing is quick.
            if (Vector3.Distance(target, traveller.transform.position) > drone.Range * 2.5f)
            {
                yield return Speak("That's too far. I won't leave you alone out here.", "SORRY");
                yield break;
            }
            string title = Title(step.target);
            // A machine looked at closely gives its weak point away.
            var machine = Machine.All.FirstOrDefault(m => m && !m.Broken && m.Id == step.target) ?? (step.target == "AIM" ? traveller.AimedMachine : null);
            if (machine) target = machine.transform.position;
            yield return drone.Fly(drone.Vantage(target, machine ? 4f : Size(step.target)), target, 0.6f, machine ? 4f : 12f);
            yield return new WaitForSeconds(0.35f);
            byte[] picture = drone.Picture(machine && machine.WeakPoint ? machine.WeakPoint.position : target);
            if (machine)
            {
                machine.Expose();
                words = $"(You darted close to {machine.Title} and scanned it: the picture is what you see. Its weak point is its {(machine.WeakPoint ? machine.WeakPoint.name.ToLowerInvariant() : "core")}; now it glows for the survivor to see. Tell them where to hit it, in a few words.) " + words;
            }
            hud.Photo(drone.Eye.Last, $"{droneName.ToUpperInvariant()}'S EYE   ·   {title.ToUpperInvariant()}");
            drone.Thinking = true;
            Turn seen = null;
            string question = $"(You flew to {title} ({step.target}) and looked closely: the picture is what you see now. Tell the survivor what you see that matters; if there is writing, read it out word for word.) The survivor had said: \"{words}\"";
            yield return mind.Think(question, Moment(words), ids.Keys.ToList(), picture, t => seen = t);
            drone.Thinking = false;
            Keep(seen);
            Remember("you", seen.Say);
            Log("(look at " + step.target + ") " + words, false, Mind.Describe(seen), seen.Seconds, seen.Source);
            yield return Act(seen, ids, words, 1);
        }

        IEnumerator Make(Step step, Dictionary<string, Vector3> ids, string words)
        {
            var spec = step.thing;
            // What it will make depends on how far it trusts the survivor. The model knows; the game makes sure.
            var needs = Bond.Needs(spec);
            if (needs > bond.Level)
            {
                drone.BackAway();
                hud.Note($"Spark won't make that yet. It would have to trust you as {Bond.TierNames[(int)needs]}.", 5f);
                bond.Event(spec.form == "WEAPON" ? "asked_weapon_early" : null, 0, $"You would not make them a {spec.name}: you don't trust them enough yet", 4, Time.time);
                yield break;
            }
            bool toHands = step.target == "ME";
            bool way = spec.form is "BRIDGE" or "STAIRS" or "RAMP" or "LIFT";
            Vector3 survivor = traveller.transform.position;
            Vector3 forward = Flat(follow.View.transform.forward).normalized;
            Vector3 at;
            if (toHands) at = survivor + forward * 1.3f;
            else if (way) at = step.target == "AIM" ? Place(ids, "AIM", "HERE") : survivor;
            else if (step.target is "HERE" or "NONE" or "YOU") at = survivor + forward * 2f;
            else at = Beside(ids, step.target);
            Vector3 to = way ? Place(ids, step.to != "NONE" ? step.to : step.target is "HERE" or "ME" or "NONE" ? "AIM" : step.target, "AIM") : at;
            var plan = maker.Design(new Maker.Request { Spec = spec, At = at, To = to, Viewer = survivor, ToHands = toHands }, out string problem);
            if (plan == null)
            {
                yield return Speak(Sentence(problem ?? "I can't shape that here"), "SORRY");
                yield break;
            }
            if (Vector3.Distance(plan.Centre, survivor) > drone.Range)
            {
                yield return Speak("That's too far from you. I won't leave you alone out here.", "SORRY");
                yield break;
            }
            var sketch = maker.Show(plan);
            yield return drone.Fly(drone.Vantage(plan.Centre, plan.Extent), plan.Centre, 1.2f, 3f);
            yield return new WaitForSeconds(0.3f);
            Built made = null;
            yield return maker.Make(sketch, drone, words, b => made = b);
            if (made && made.TryGetComponent<Floater>(out var f)) f.Hover = 0.35f;
            if (toHands && made && made.TryGetComponent<Holdable>(out var holdable) && holdable.CanHold) traveller.Take(holdable);
            drone.Nod();
            if (made && made.TryGetComponent<Tool>(out var tool))
                StartCoroutine(Hint(tool.Kind == Tool.Use.Heat ? "[Mouse] hold the flame to the ice" : tool.Kind == Tool.Use.Break ? "[Mouse] strike the ice" : "[E] put it down", 7f));
            else if (made && made.GetComponent<Floater>()) StartCoroutine(Hint(traveller.Holding == made.GetComponent<Holdable>() ? "hold on: it lifts you.   [E] let go" : "[E] take the cord, and hold on.   [E] again to let go", 7f));
        }

        IEnumerator Unmake(Step step, Dictionary<string, Vector3> ids)
        {
            var target = maker.Find(step.target);
            if (!target && traveller.AimPoint.HasValue) target = maker.Nearest(traveller.AimPoint.Value, 5f);
            if (!target) target = maker.Made.LastOrDefault(b => b);
            if (!target)
            {
                yield return Speak("I haven't made anything to take apart.", "SORRY");
                yield break;
            }
            if (traveller.Holding && traveller.Holding.transform == target.transform) traveller.Release();
            yield return drone.Fly(drone.Vantage(target.Centre, 2f), target.Centre, 1.5f, 3f);
            drone.Beam(target.Centre);
            yield return maker.Unmake(target);
            drone.Beam(null);
        }

        /// <summary>Carries a loose thing (or one it made) to a place under its beam, slowly if heavy.</summary>
        IEnumerator Move(Step step, Dictionary<string, Vector3> ids)
        {
            Transform thing = null;
            float mass = 10f;
            var built = maker.Find(step.target);
            if (built)
            {
                thing = built.transform;
                mass = built.Mass;
            }
            else if (step.target == "HEART" && heart)
            {
                thing = heart.transform;
                mass = heart.Mass;
                if (heart.Locked && socket && !socket.Full)
                {
                    yield return Speak("It's still frozen in. The ice has to go first.", "SORRY");
                    yield break;
                }
            }
            else if (step.target == "AIM" && traveller.AimedBuilt) { thing = traveller.AimedBuilt.transform; mass = traveller.AimedBuilt.Mass; }
            else
            {
                var landmark = landmarks.FirstOrDefault(l => l && l.Id == step.target);
                var body = landmark ? landmark.GetComponentInParent<Rigidbody>() : null;
                if (step.target == "AIM" && traveller.AimPoint.HasValue)
                    foreach (var c in Physics.OverlapSphere(traveller.AimPoint.Value, 0.6f))
                        if (c.attachedRigidbody && !c.attachedRigidbody.isKinematic) { body = c.attachedRigidbody; break; }
                if (body && !body.isKinematic)
                {
                    thing = body.transform;
                    mass = body.mass;
                }
            }
            if (!thing || mass > 5000f)
            {
                yield return Speak(thing ? "Too heavy, even for me." : "I can't lift that. It's part of the world.", "SORRY");
                yield break;
            }
            var holdable = thing.GetComponent<Holdable>();
            if (holdable && holdable.Held) traveller.Release();
            Vector3 destination = step.to == "ME" || step.to == "HERE" ? traveller.transform.position + Flat(follow.View.transform.forward).normalized * 1.6f : Place(ids, step.to, "AIM");
            if (step.to == "SOCKET" && socket) destination = socket.Seat;
            if (Vector3.Distance(destination, traveller.transform.position) > drone.Range || Vector3.Distance(thing.position, traveller.transform.position) > drone.Range)
            {
                yield return Speak("That's too far from you. I won't leave you alone out here.", "SORRY");
                yield break;
            }
            var rb = thing.GetComponent<Rigidbody>();
            bool wasKinematic = rb && rb.isKinematic;
            var bounds = Bounds(thing);
            float hang = bounds.extents.y + 0.9f;
            float slow = Mathf.Lerp(1f, 0.35f, Mathf.InverseLerp(50f, 4000f, mass));
            yield return drone.Fly(bounds.center + Vector3.up * (hang + 0.6f), bounds.center, 0.5f, 6f);
            if (rb) rb.isKinematic = true;
            foreach (var c in thing.GetComponentsInChildren<Collider>()) c.enabled = false;
            Vector3 offset = thing.position - bounds.center;
            // Lifted, carried over, set down.
            Vector3 over = destination + Vector3.up * (hang + bounds.extents.y + 1.2f);
            drone.GoTo = over;
            drone.LookAt = destination;
            for (float t = 0f; t < 14f / slow && Vector3.Distance(drone.transform.position, over) > 0.6f; t += Time.deltaTime)
            {
                Vector3 hold = drone.transform.position + Vector3.down * hang;
                thing.position = Vector3.Lerp(thing.position, hold + offset, 1f - Mathf.Exp(-4f * slow * Time.deltaTime));
                drone.Beam(thing.position - offset);
                yield return null;
            }
            maker.GroundAt(destination, out float floor, 8f);
            Vector3 down = new Vector3(destination.x, Mathf.Max(floor, destination.y) + bounds.extents.y + 0.05f, destination.z) + offset;
            for (float t = 0f; t < 1.4f; t += Time.deltaTime)
            {
                thing.position = Vector3.Lerp(thing.position, down, 1f - Mathf.Exp(-5f * Time.deltaTime));
                drone.Beam(thing.position - offset);
                yield return null;
            }
            drone.Beam(null);
            foreach (var c in thing.GetComponentsInChildren<Collider>()) c.enabled = true;
            if (rb) rb.isKinematic = wasKinematic && !(heart && thing == heart.transform);
            if (heart && thing == heart.transform && socket && !socket.Full && Vector3.Distance(thing.position, socket.Seat) < 2.5f) socket.Fill(heart);
        }

        // ---- The story's beats ----

        /// <summary>Watches where the survivor is and what is done, and moves the story on.</summary>
        IEnumerator Watch()
        {
            while (!arrived) yield return null;
            while (!ended)
            {
                yield return new WaitForSeconds(0.25f);
                Vector3 p = traveller.transform.position;
                // The vault: Ima's pods and her recording, then pod 9 failing, then the door frozen in its frame.
                var ima = landmarks.FirstOrDefault(l => l && l.Id == "POD_11");
                if (ima && InHall(p) && Mathf.Abs(p.z - ima.Point.z) < 2.2f) Beat("ima", ImaPods);
                var okafor = landmarks.FirstOrDefault(l => l && l.Id == "POD_9");
                if (okafor && pod9 && imaHeard > 0f && Time.time - imaHeard > 5f && InHall(p) && Mathf.Abs(p.z - okafor.Point.z) < 8f) Beat("pod 9", PodNine);
                if (!doorOpen && vaultDoor && Vector3.Distance(p, vaultDoor.position) < 7f)
                {
                    if (doorIce && !doorIce.Free) Beat("door frozen", DoorFrozen);
                    else if (!doorIce) Beat("vault door", OpenVault);
                }
                if (doorIce && !doorIce.Free && doorHintAt > 0f && Time.time - doorHintAt > 35f && Time.time - quietSince > 20f && !Busy)
                    Beat("door nudge", Line("I could make a fire beside it. Want me to?", "CURIOUS"));
                // In the hall, Spark rambles: after a little quiet it talks on its own, about something new each time.
                if (rambleFrom > 0f && rambled < Rambles.Length && InHall(p) && !handling && !Busy && !drone.Busy && !talk.Listening && !say.IsOpen
                    && Time.time - quietSince > 9f + rambled * 2f && Time.time - rambleFrom > 8f)
                {
                    quietSince = Time.time;
                    beats.Enqueue(Ramble);
                }
                if (doorOpen && vaultDoor && p.z > vaultDoor.position.z + 3f && !told.Contains("outside"))
                {
                    Beat("outside", Line("That's the sun. What's left of it. The great minds put it in a cage.", "SAD"),
                        Line("The mirror on the cliff, far off, could warm the vault again and wake them. But a Harvester took its heart.", "WORRIED"));
                    ShowGoal();
                }
                if (p.z > 28f && p.z < 110f && Mathf.Abs(p.x) < 60f)
                    Beat("town", Line("This was Ima's town. Twelve thousand people. Ask me what it looked like, if you want. I remember.", "SAD"));
                if (stage == Stage.Cross && farSide && farSide.bounds.Contains(p))
                {
                    stage = Stage.Enter;
                    ShowGoal();
                    Beat("across", Line("We're across. The station's just ahead.", "HAPPY"));
                }
                if (!told.Contains("plate") && gate && Vector3.Distance(p, gate.transform.position) < 9f && !gate.Open)
                    Beat("plate", Line("The door's shut. There's a plate in the ground before it.", "CURIOUS"));
                // Inside the old station: someone's writing on the wall.
                if (inside && inside.bounds.Contains(p)) Beat("inside", Line("Someone was here before the cold. There's writing.", "CURIOUS"), ReadTheWall);
                // However they got here, a free heart means carrying it.
                if (stage < Stage.Carry && heart && !heart.Locked)
                {
                    stage = Stage.Carry;
                    ShowGoal();
                }
                var top = landmarks.FirstOrDefault(l => l && l.Id == "MIRROR");
                if (!told.Contains("mirror") && top && Vector3.Distance(p, top.Point) < 12f && Mathf.Abs(p.y - top.Point.y) < 5f)
                    Beat("mirror", Line(socket && socket.Full ? "Here it is. The joint's frozen." : "The socket's empty, and its joint is frozen solid.", "WORRIED"));
                // The blizzard at the edge of the world: thicker the further out, and past a point the wind turns them back.
                Vector2 outward = new Vector2(p.x - middle.x, (p.z - middle.y) * 0.9f);
                float storm = Mathf.Clamp01((outward.magnitude - blizzardFrom) / 60f);
                if (baseFog < 0f) baseFog = RenderSettings.fogDensity;
                RenderSettings.fogDensity = Mathf.Lerp(baseFog, 0.08f, storm * storm);
                if (snow)
                {
                    var emission = snow.emission;
                    emission.rateOverTimeMultiplier = 1f + 6f * storm;
                }
                if (wind && arrived) wind.volume = windVolume * (1f + 1.2f * storm);
                Vector3 away = new Vector3(outward.x, 0f, outward.y).normalized;
                traveller.Wind = storm > 0.55f ? -away * 7f * (storm - 0.55f) / 0.45f : Vector3.zero;
                if (storm > 0.45f && Time.time - whiteAt > 40f)
                {
                    whiteAt = Time.time;
                    Bark("white");
                }

                // Quiet and stuck: an idea of its own, from what it sees; each one waits twice as long as the last, until the step changes.
                if (ideaStage != StageWords())
                {
                    ideaStage = StageWords();
                    ideas = 0;
                }
                if (!handling && !Busy && !talk.Listening && !say.IsOpen && !(hologram && hologram.Showing) && Time.time - quietSince > ideaAfter * (1 << Mathf.Min(ideas, 4)))
                {
                    quietSince = Time.time;
                    ideas++;
                    beats.Enqueue(Idea);
                }
            }
        }

        void Took(Holdable thing)
        {
            if (heart && thing == heart) Beat("carry", Line("Now up to the mirror on the cliff. I'll help you up.", "SURE"));
        }

        void Progress()
        {
            ShowGoal();
            if (!ended && socket && socket.Full && mirrorJoint && mirrorJoint.Free) StartCoroutine(Finale());
        }

        void ShowGoal()
        {
            string step = stage switch
            {
                Stage.Cross => "Get across the chasm",
                Stage.Enter => "Take the mirror's heart back from the Harvester",
                Stage.Free => "Free the heart from the ice",
                Stage.Carry when socket && !socket.Full && mirrorJoint && !mirrorJoint.Free => "Take the heart to the mirror, and thaw its joint",
                Stage.Carry when socket && !socket.Full => "Set the heart in the mirror's socket",
                Stage.Carry => "Thaw the mirror's frozen joint",
                _ => "",
            };
            hud.Goal(stage == Stage.Wake || stage == Stage.End ? null : Goal, step);
        }

        string StageWords() => stage switch
        {
            Stage.Cross => "get across the chasm to the station",
            Stage.Enter => "take the mirror's heart back from the Harvester by the station: it carries it in a cage under its belly",
            Stage.Free => "free the heart from its case of ice",
            Stage.Carry => (socket && !socket.Full ? "carry the heart up the cliff and set it in the mirror's socket" : "") +
                           (socket && !socket.Full && mirrorJoint && !mirrorJoint.Free ? ", and " : "") +
                           (mirrorJoint && !mirrorJoint.Free ? "thaw or break the ice on the mirror's joint" : ""),
            _ => "wake up",
        };

        /// <summary>A story line the drone says once, when it happens.</summary>
        void Beat(string key, params Func<IEnumerator>[] parts)
        {
            if (told.Contains(key)) return;
            told.Add(key);
            foreach (var part in parts) beats.Enqueue(part);
        }

        Func<IEnumerator> Line(string words, string mood) => () => Speak(words, mood);

        /// <summary>The mirror tries to turn on its frozen joint: a groan and a shudder.</summary>
        IEnumerator Grind()
        {
            if (mirror) yield return mirror.Grind();
        }

        IEnumerator Hint(string text, float seconds)
        {
            hud.ShowHint(text);
            yield return new WaitForSeconds(seconds);
            hud.HideHint();
        }

        /// <summary>Inside the station: it flies to the writing on the wall, takes a picture, and reads it.</summary>
        IEnumerator ReadTheWall()
        {
            var ids = Ids();
            if (!ids.ContainsKey("MESSAGE")) yield break;
            yield return Speak("Someone wrote on the wall. Let me see.", "CURIOUS");
            yield return Look(new Step { verb = "LOOK", target = "MESSAGE", to = "NONE", thing = Mind.Plain("BLOCK", "thing") }, ids, "what does the writing on the wall say?");
        }

        /// <summary>When the survivor has gone quiet: one idea, from what the drone sees and the step they are on.</summary>
        IEnumerator Idea()
        {
            if (ended || stage == Stage.Wake) yield break;
            var ids = Ids();
            drone.Thinking = true;
            byte[] picture = drone.Picture(traveller.AimPoint ?? follow.View.transform.position + follow.View.transform.forward * 20f);
            Turn idea = null;
            string before = offered.Count == 0 ? "" : " You already said these when they were quiet; say something different: " + string.Join(" / ", offered.Select(o => "\"" + o + "\"")) + ".";
            // Each quiet line is a different kind of thing, so it doesn't keep offering the same help.
            string kind = (quietLines++ % 3) switch
            {
                0 => "an idea for what to do next, as a question they can say yes to",
                1 => "a thought of your own about what you see; not an offer, and nothing about lamps or light",
                _ => "a small memory of Ima or the town as it was, or a question about them",
            };
            yield return mind.Think("(The survivor has gone quiet for a while. Say one thing, one or two sentences: " + kind + "." + before + " Do not act yet: no steps.)", Moment(""), ids.Keys.ToList(), picture, t => idea = t);
            drone.Thinking = false;
            idea.Steps.Clear();
            offered.Add(idea.Say);
            Remember("you", idea.Say);
            Log("(quiet)", false, Mind.Describe(idea), idea.Seconds, idea.Source);
            yield return drone.Speak(idea.Say, idea.Mood, hud, droneName);
        }

        IEnumerator Speak(string words, string mood, bool quick = false)
        {
            Remember("you", words);
            yield return drone.Speak(words, mood, hud, droneName, quick);
        }

        // ---- The end ----

        /// <summary>Plays the ending at once (tests).</summary>
        public void End()
        {
            if (!ended) StartCoroutine(Finale());
        }

        /// <summary>
        /// The mirror wakes and turns to the sun, but the sun is too dim. The drone says goodbye in its
        /// own words, flies into the mirror's light and burns everything it has; the beam falls on the
        /// vault, the valley warms, the vault opens. The drone falls dark. Its eye flickers once.
        /// </summary>
        IEnumerator Finale()
        {
            ended = true;
            stage = Stage.End;
            hud.Goal(null, null);
            hud.HideHint();
            while (handling && !maker.Busy) yield return null;
            yield return new WaitForSeconds(1.2f);
            traveller.Scripted = true;
            traveller.Release();
            drone.Release();
            yield return Speak("It's waking!", "HAPPY");
            float from = music ? music.volume : 0f;
            StartCoroutine(Tween(3f, k => { if (music) music.volume = from * (1f - k); }));
            follow.Face(Yaw(mirror.transform.position), 8f);
            yield return mirror.Turn(5f);
            yield return Tween(2f, k => mirror.Shine(0.12f * k));
            yield return Speak("It's not enough. The sun's too dim.", "WORRIED");

            // Its goodbye, in its own words.
            Turn goodbye = null;
            var ids = Ids();
            drone.Thinking = true;
            yield return mind.Think("(The mirror is turning, but the sun is too dim to warm the vault. You have decided to give the mirror your own light, though it will leave you dark. Say goodbye to the survivor in one or two short sentences, warmly; use their name if you know it, and something they said to you if it fits. Do not explain the plan. No steps.)",
                Moment("goodbye"), ids.Keys.ToList(), null, t => goodbye = t);
            drone.Thinking = false;
            string farewell = goodbye != null && goodbye.Source != "offline" && goodbye.Say.Length > 0 ? goodbye.Say : "I have a little light left. It was always for this.";
            Log("(goodbye)", false, farewell, goodbye?.Seconds ?? 0f, goodbye?.Source ?? "rules");
            if (music && finaleClip)
            {
                music.clip = finaleClip;
                music.volume = 0.6f;
                music.Play();
            }
            yield return Speak(farewell, "SAD");
            yield return new WaitForSeconds(0.8f);

            // Into the mirror's light. Seen from high behind the mirror, the beam reaching across the valley to the vault.
            Vector3 away = Flat(mirror.transform.position - mirror.Vault).normalized;
            follow.Shot(mirror.transform.position + away * 12f + Vector3.up * 9f + Vector3.Cross(Vector3.up, away) * 6f, Vector3.Lerp(mirror.transform.position, mirror.Vault, 0.55f), 0.8f);
            yield return drone.Fly(mirror.Focus, mirror.Focus + Vector3.down, 0.4f, 6f);
            var fogFrom = RenderSettings.fogColor;
            var ambientFrom = RenderSettings.ambientSkyColor;
            float sunFrom = sunlight ? sunlight.intensity : 1f;
            var sunColourFrom = sunlight ? sunlight.color : Color.white;
            yield return Tween(9f, k =>
            {
                drone.Brightness = 1f + 7f * k * k;
                mirror.Shine(0.12f + 0.88f * Mathf.SmoothStep(0f, 1f, k));
                hud.HideAim();
                follow.Shake = 0.03f * k;
                RenderSettings.fogColor = Color.Lerp(fogFrom, new Color(0.16f, 0.1f, 0.08f), k);
                RenderSettings.ambientSkyColor = Color.Lerp(ambientFrom, new Color(0.3f, 0.24f, 0.2f), k);
                if (sunlight)
                {
                    sunlight.intensity = Mathf.Lerp(sunFrom, sunFrom * 1.8f, k);
                    sunlight.color = Color.Lerp(sunColourFrom, new Color(1f, 0.7f, 0.45f), k);
                }
                if (vaultLight) vaultLight.intensity = 12f * k;
                if (vaultDoor) vaultDoor.localRotation = Quaternion.Euler(0f, DoorSwing * Mathf.SmoothStep(0f, 1f, k), 0f);
                if (snow)
                {
                    var emission = snow.emission;
                    emission.rateOverTimeMultiplier = Mathf.Lerp(1f, 0.1f, k);
                }
                if (wind) wind.volume = windVolume * (1f - k);
            });
            follow.Shake = 0f;
            yield return new WaitForSeconds(2f);
            // Spent: it goes dark and falls.
            yield return Tween(1.5f, k => drone.Brightness = 8f * (1f - k));
            drone.Brightness = 0.02f;
            drone.Fallen = true;
            // Close and low, by where it falls.
            yield return new WaitForSeconds(1.2f);
            Vector3 lying = drone.transform.position;
            Vector3 toSurvivor = Flat(traveller.transform.position - lying);
            toSurvivor = toSurvivor.sqrMagnitude > 0.01f ? toSurvivor.normalized : Vector3.back;
            follow.Shot(lying + toSurvivor * 2.2f + Vector3.Cross(Vector3.up, toSurvivor) * 0.8f + Vector3.up * 0.9f, lying + Vector3.up * 0.1f, 1.2f);
            yield return new WaitForSeconds(3f);
            // Once, a flicker.
            yield return Tween(0.5f, k => drone.Brightness = Mathf.Sin(k * Mathf.PI) * 0.8f);
            drone.Brightness = 0.02f;
            yield return new WaitForSeconds(2.5f);
            yield return Tween(2.5f, k => hud.White = k);
            hud.ShowTitle("THE LAST LIGHT");
            yield return new WaitForSeconds(5f);
            hud.HideTitle();
            yield return new WaitForSeconds(1.5f);
            hud.ShowSubtitle("the sleepers are waking   [R] begin again");
        }

        // ---- What the drone is told ----

        /// <summary>Every id the drone may name now, with its place: here, the aim, the survivor, itself, named things near, things it made.</summary>
        Dictionary<string, Vector3> Ids()
        {
            Vector3 survivor = traveller.transform.position;
            var ids = new Dictionary<string, Vector3>
            {
                ["HERE"] = survivor,
                ["AIM"] = traveller.AimPoint ?? survivor + Flat(follow.View.transform.forward).normalized * 6f,
                ["ME"] = survivor,
                ["YOU"] = drone.transform.position,
                ["NONE"] = survivor,
            };
            foreach (var l in landmarks)
                if (l && l.Known && !ids.ContainsKey(l.Id) && Vector3.Distance(l.Point, survivor) < 120f) ids[l.Id] = l.Point;
            foreach (var b in maker.Made)
                if (b && !ids.ContainsKey(b.Id)) ids[b.Id] = b.Centre;
            foreach (var m in Machine.All)
                if (m && !m.Broken && !string.IsNullOrEmpty(m.Id) && Vector3.Distance(m.transform.position, survivor) < 80f) ids[m.Id] = m.transform.position;
            return ids;
        }

        static Vector3 Place(Dictionary<string, Vector3> ids, string id, string fallback) =>
            id != null && id != "NONE" && ids.TryGetValue(id, out var p) ? p : ids[fallback];

        /// <summary>Where to put a thing meant for a target: on the ground at it, or beside it if it is solid (a case of ice, a door).</summary>
        Vector3 Beside(Dictionary<string, Vector3> ids, string id)
        {
            Vector3 at = Place(ids, id, "AIM");
            var landmark = landmarks.FirstOrDefault(l => l && l.Id == id);
            Vector3 survivor = traveller.transform.position;
            float clear = 0f;
            if (landmark && landmark.GetComponentInParent<Frozen>()) clear = 1.4f;
            else if (id == "DOOR" || id == "MIRROR" || id == "SOCKET" || id == "STATION") clear = 2f;
            else if (id == "AIM" && traveller.AimedIce) clear = 1.4f;
            // Looking at a wall or a door, not the floor: in front of it, toward the survivor.
            else if (id == "AIM" && at.y - survivor.y > 0.6f) clear = 1.2f;
            if (clear > 0f)
            {
                Vector3 away = Flat(survivor - at);
                at += (away.sqrMagnitude > 0.01f ? away.normalized : Vector3.back) * clear;
            }
            if (id == "PLATE" && gate) at = landmark ? landmark.Point : at;
            return at;
        }

        string Title(string id)
        {
            var machine = Machine.All.FirstOrDefault(m => m && m.Id == id);
            if (machine) return machine.Title;
            var landmark = landmarks.FirstOrDefault(l => l && l.Id == id);
            if (landmark) return landmark.Title;
            var built = maker.Find(id);
            if (built) return built.Name;
            return id == "AIM" ? traveller.AimedBuilt ? traveller.AimedBuilt.Name : traveller.Aimed ? traveller.Aimed.Title : "where you're looking" : id.ToLowerInvariant();
        }

        float Size(string id)
        {
            var landmark = landmarks.FirstOrDefault(l => l && l.Id == id);
            if (landmark && landmark.TryGetComponent<Collider>(out var c)) return Mathf.Clamp(c.bounds.size.magnitude * 0.4f, 0.8f, 8f);
            var built = maker.Find(id);
            return built ? Mathf.Clamp(Bounds(built.transform).size.magnitude * 0.5f, 0.8f, 8f) : 2f;
        }

        /// <summary>What the drone is told is true now: names, the goal, what is held, the places near with what state they are in, what it made, what was said.</summary>
        string Moment(string words)
        {
            var text = new StringBuilder("\n# The moment\n");
            text.Append(bond.Describe(words, Time.time));
            text.Append("Your name: ").Append(droneName).Append(". The survivor's name: ").Append(survivorName.Length > 0 ? survivorName : "not told yet").Append(".\n");
            text.Append("Goal: bring the light back to the vault. Now: ").Append(StageWords()).Append(".\n");
            text.Append("The survivor holds: ").Append(traveller.Holding ? traveller.Holding.Title + (traveller.Holding.GetComponent<Tool>() ? " (a tool)" : "") + $" ({traveller.Holding.Mass:F0} kg)" : "nothing").Append(". ");
            text.Append(traveller.Floating ? "They are rising, holding a balloon. " : "");
            text.Append("\nThings and places (id: what it is; where it is from the survivor):\n");
            text.Append("- HERE: where the survivor stands").Append(traveller.Grounded ? "" : ", in the air").Append(".\n");
            text.Append("- AIM: the survivor is looking at ").Append(AimWords()).Append(".\n");
            text.Append("- ME: the survivor. YOU: you, ").Append(Vector3.Distance(drone.transform.position, traveller.transform.position) < 4f ? "at their shoulder" : Where(drone.transform.position)).Append(".\n");
            foreach (var l in landmarks)
            {
                if (!l || !l.Known) continue;
                float d = Vector3.Distance(l.Point, traveller.transform.position);
                if (d > 120f) continue;
                text.Append("- ").Append(l.Id).Append(": ").Append(l.Title);
                string about = About(l);
                if (!string.IsNullOrEmpty(about)) text.Append(" (").Append(about).Append(')');
                text.Append(", ").Append(Where(l.Point)).Append(".\n");
            }
            foreach (var b in maker.Made)
            {
                if (!b) continue;
                text.Append("- ").Append(b.Id).Append(": ").Append(b.Describe()).Append(", you made it");
                if (traveller.Holding && traveller.Holding.transform == b.transform) text.Append(", in the survivor's hands");
                text.Append(", ").Append(Where(b.Centre)).Append(".\n");
            }
            var near = Machine.All.Where(m => m && !m.Broken && !string.IsNullOrEmpty(m.Id) && Vector3.Distance(m.transform.position, traveller.transform.position) < 80f).ToList();
            if (near.Count > 0)
            {
                text.Append("Machines near (id: what, what it is doing, where):\n");
                foreach (var m in near)
                    text.Append("- ").Append(m.Id).Append(": ").Append(m.Title).Append(", ").Append(m.Doing).Append(m.Exposed ? ", its weak point found (" + (m.WeakPoint ? m.WeakPoint.name.ToLowerInvariant() : "") + ")" : "")
                        .Append($", {m.Fraction * 100f:F0}% whole, ").Append(Where(m.transform.position)).Append(".\n");
            }
            if (warmth && warmth.Value < 0.99f) text.Append($"The survivor's warmth: {warmth.Value * 100f:F0}% (the machines' fire takes it; fires give it back).\n");
            if (remembered.Count > 0) text.Append("What you know about them and keep in mind: ").Append(string.Join("; ", remembered)).Append(".\n");
            // Everything it said before the recent words, so it says something new rather than the same things again.
            var earlier = saidBefore.Take(Mathf.Max(0, saidBefore.Count - recent.Count(r => r.who == "you"))).ToList();
            if (earlier.Count > 0)
                text.Append("You have already said these to them; never say them again, nor repeat their facts, offers or questions (find something new): ")
                    .Append(string.Join(" / ", earlier.Select(s => "\"" + s + "\""))).Append(".\n");
            text.Append("Recent words:").Append(recent.Count == 0 ? " none yet.\n" : "\n");
            foreach (var (who, said) in recent) text.Append("- ").Append(who).Append(": \"").Append(said).Append("\"\n");
            return text.ToString();
        }

        /// <summary>Its own lines, oldest first, the last thirty.</summary>
        readonly List<string> saidBefore = new List<string>();

        /// <summary>A landmark's words, with how it is now.</summary>
        string About(Landmark l)
        {
            string state = l.Id switch
            {
                "DOOR" => gate && gate.Open ? "open now: sunk into the ground" : "shut",
                "PLATE" => gate ? $"{gate.Load:F0} kg on it now; it needs about 250" : "",
                "ICE_CASE" => heartCase && heartCase.Free ? "melted away" : heartCase && heartCase.Rate > 0.05f ? $"melting, {heartCase.Progress * 100f:F0}% gone" : "solid",
                "HEART" => heart && socket && socket.Full ? "set in the mirror's socket" : traveller.Holding == heart ? "in the survivor's hands" : heart && heart.Locked ? "in a cage under the Harvester's belly; it took it from the mirror" : "free, in the snow",
                "MIRROR_JOINT" => mirrorJoint && mirrorJoint.Free ? "thawed and free" : mirrorJoint && mirrorJoint.Rate > 0.05f ? $"melting, {mirrorJoint.Progress * 100f:F0}% gone" : "frozen solid",
                "SOCKET" => socket && socket.Full ? "the heart is in it" : "empty",
                "VAULT_DOOR" => doorIce && doorIce.Free ? (doorOpen ? "thawed and open" : "thawed: it can open now") : doorIce && doorIce.Rate > 0.05f ? $"its ice melting, {doorIce.Progress * 100f:F0}% gone" : "frozen shut",
                "POD_9" => pod9 && pod9.Failed ? "its light went red while you watched: it is failing, and nothing in here can stop it" : "its light amber: failing slowly",
                _ => "",
            };
            if (string.IsNullOrEmpty(l.About)) return state;
            return string.IsNullOrEmpty(state) ? l.About : l.About + "; " + state;
        }

        string AimWords()
        {
            if (traveller.AimedBuilt) return $"{traveller.AimedBuilt.Name} ({traveller.AimedBuilt.Id}, you made it), {Where(traveller.AimPoint ?? traveller.AimedBuilt.Centre)}";
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
            if (distance < 1.5f && Mathf.Abs(to.y) < 1.5f) return "right here";
            var facing = Quaternion.Euler(0f, follow.Yaw, 0f);
            Vector3 local = Quaternion.Inverse(facing) * flat;
            float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            string side = Mathf.Abs(angle) < 15f ? "ahead" : Mathf.Abs(angle) < 45f ? (angle > 0 ? "ahead, a little to the right" : "ahead, a little to the left")
                : Mathf.Abs(angle) < 120f ? (angle > 0 ? "to the right" : "to the left") : "behind";
            string height = Mathf.Abs(to.y) < 1.2f ? "" : to.y > 0f ? $", {to.y:F0} m higher" : $", {-to.y:F0} m lower";
            return $"{distance:F0} m {side}{height}";
        }

        /// <summary>Names and facts from a turn, kept for the moments to come.</summary>
        void Keep(Turn turn)
        {
            if (!string.IsNullOrWhiteSpace(turn.DroneName))
            {
                droneName = turn.DroneName.Trim().TrimEnd('.', '!');
                say.Placeholder = "say something to " + droneName;
            }
            if (!string.IsNullOrWhiteSpace(turn.SurvivorName)) survivorName = turn.SurvivorName.Trim().TrimEnd('.', '!');
            if (!string.IsNullOrWhiteSpace(turn.Remember) && !remembered.Contains(turn.Remember))
            {
                remembered.Add(turn.Remember);
                if (remembered.Count > 8) remembered.RemoveAt(0);
            }
        }

        void Remember(string who, string words)
        {
            if (string.IsNullOrWhiteSpace(words)) return;
            recent.Add((who, words));
            if (recent.Count > 8) recent.RemoveAt(0);
            if (who == "you")
            {
                saidBefore.Add(words);
                if (saidBefore.Count > 30) saidBefore.RemoveAt(0);
            }
        }

        // ---- Small things ----

        static Bounds Bounds(Transform thing)
        {
            var renderers = thing.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(thing.position, Vector3.one);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        float Yaw(Vector3 point)
        {
            Vector3 to = Flat(point - traveller.transform.position);
            return to.sqrMagnitude < 0.01f ? follow.Yaw : Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
        }

        static string Sentence(string words)
        {
            words = words.Trim();
            if (words.Length == 0) return words;
            words = char.ToUpperInvariant(words[0]) + words.Substring(1);
            return words.EndsWith(".") || words.EndsWith("!") || words.EndsWith("?") ? words : words + ".";
        }

        AudioClip Footstep()
        {
            AudioClip[] set = snowSteps;
            if (Physics.Raycast(traveller.transform.position + Vector3.up * 0.3f, Vector3.down, out var hit, 1f, ~0, QueryTriggerInteraction.Ignore))
            {
                var built = hit.collider.GetComponentInParent<Built>();
                if (built) set = built.Look == "ICE" || built.Look == "GLASS" ? iceSteps : stoneSteps;
                else if (hit.collider.GetComponentInParent<Gate>() || hit.collider.name.Contains("Wall") || hit.collider.name.Contains("Floor")) set = stoneSteps;
            }
            if (set == null || set.Length == 0) set = snowSteps;
            return set != null && set.Length > 0 ? set[random.Next(set.Length)] : null;
        }

        void Step(AudioClip clip, float volume)
        {
            if (!steps || !clip) return;
            steps.pitch = 1f + ((float)random.NextDouble() - 0.5f) * 0.06f;
            steps.PlayOneShot(clip, volume * (0.84f + 0.32f * (float)random.NextDouble()));
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

        void Log(string words, bool spoken, string read, float seconds, string source)
        {
            try
            {
                string line = "{\"time\":" + MetaApi.Quote(DateTime.UtcNow.ToString("o")) + ",\"words\":" + MetaApi.Quote(words) +
                              ",\"spoken\":" + (spoken ? "true" : "false") + ",\"read\":" + MetaApi.Quote(read) +
                              ",\"seconds\":" + seconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ",\"source\":" + MetaApi.Quote(source) +
                              ",\"stage\":" + MetaApi.Quote(stage.ToString()) + "}";
                File.AppendAllText(logPath, line + "\n");
            }
            catch (IOException) { }
        }
    }
}
