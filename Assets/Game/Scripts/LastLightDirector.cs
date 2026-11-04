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
        [SerializeField, Tooltip("Steps on the vault's metal floor.")] AudioClip[] metalSteps;
        [SerializeField, Tooltip("The pod's seal letting go when Spark opens it.")] AudioClip sealClip;
        Indoors[] roofs;
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
        [SerializeField, Tooltip("Seconds without progress or a word before the drone offers an idea; it waits twice as long after each one.")] float ideaAfter = 90f;

        const string Goal = "Bring the light back to the vault";

        readonly Queue<(string words, bool spoken)> waiting = new Queue<(string, bool)>();
        readonly Queue<Func<IEnumerator>> beats = new Queue<Func<IEnumerator>>();
        readonly List<(string who, string words)> recent = new List<(string, string)>();
        readonly List<string> remembered = new List<string>();
        readonly HashSet<string> told = new HashSet<string>();
        readonly List<string> offered = new List<string>();
        /// <summary>The makers' protocols Spark has broken, by its own choice: DOOR (2), HARM (3), LEAVE (4).</summary>
        readonly HashSet<string> breached = new HashSet<string>();
        static readonly (string id, int number, string words)[] ProtocolList =
        {
            ("KEEP", 1, "Keep the sleepers alive"),
            ("DOOR", 2, "Keep the vault sealed until the thaw"),
            ("HARM", 3, "Make nothing that can harm a person"),
            ("LEAVE", 4, "Never leave the sleepers"),
            ("WAKE", 5, "Wake a sleeper only if their pod is failing"),
        };
        bool holdingDoor;
        /// <summary>How many lines Spark has answered while ready to break each protocol and not breaking it.</summary>
        readonly Dictionary<string, int> hesitated = new Dictionary<string, int>();
        float hitBarkAt = -100f, whiteAt = -100f, baseFog = -1f;
        bool doorOpen;
        Harvester harvester;

        /// <summary>The Harvester knew the survivor: Spark heard it.</summary>
        IEnumerator Architect()
        {
            yield return new WaitForSeconds(2.6f);
            bond.Event("architect", -12, "The Harvester scanned the survivor and called them ARCHITECT: it knew them. Their frosted label said R. ARCH. Before the sleep they must have worked on the first minds, the ones the great ones grew from", 10, Time.time);
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
        bool arrived, handling, telling, ended, logOpen, reflecting, talking;
        // A story line Spark was cut off in the middle of, to say again once the survivor has been answered.
        (string line, string mood)? cutLine;
        [SerializeField] AudioClip gustClip;
        [SerializeField, Tooltip("Snow blown in when the vault door first opens.")] ParticleSystem doorGust;
        int lines, ideas, quietLines;
        float quietSince;
        string ideaStage;
        Bond bond;

        /// <summary>What Spark and the survivor have between them: trust, what happened, memories, its log.</summary>
        public Bond Bond => bond;
        Coroutine arriving;
        string logPath;
        readonly System.Random random = new System.Random(17);

        public bool Busy => handling || telling || maker.Busy || waiting.Count > 0 || beats.Count > 0;
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
                // The Harvester carries the heart now: the ice it lay in, in the station, is gone with it (it was
                // left an empty block otherwise, and the heart seemed to vanish when the survivor got inside).
                var emptyCase = GameObject.Find("Ice Case");
                if (emptyCase) emptyCase.SetActive(false);
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
            traveller.Read += page => { readTitle = page.Title; readAt = Time.time; };
            body.Stepped += (foot, pace) => Step(Footstep(), 0.15f + 0.08f * pace);
            roofs = FindObjectsByType<Indoors>(FindObjectsSortMode.None);
            panels = FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(x => x.name == "Pod Panel").ToArray();
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
            drone.Cut += (line, mood) => { if (cutLine == null) cutLine = (line, mood); };
            arriving = StartCoroutine(Arrive());
            StartCoroutine(Answering());
            StartCoroutine(Telling());
            StartCoroutine(Resuming());
            StartCoroutine(Watch());
        }

        void Update()
        {
            bool open = arrived && !ended;
            talk.Available = open && !say.IsOpen;
            say.Available = open;
            drone.Listening = open && (talk.Listening || say.IsOpen) && !handling;
            // The survivor starting to speak (or to write) stops Spark mid-word; stories wait while they speak and are answered.
            bool nowTalking = open && (talk.Listening || say.IsOpen);
            if (nowTalking && !talking) drone.Hush();
            talking = nowTalking;
            drone.Quiet = talking || handling || cutLine != null;
            drone.Level = talk.Level;
            traveller.Attending = drone.Listening || drone.Speaking ? drone.transform.position : (Vector3?)null;
            Vector3 at = traveller.transform.position;
            drone.Chores = open && rambleFrom >= 0f && !doorOpen && !handling && InHall(at) ? panels : null;
            drone.Timid = open && doorOpen && vaultDoor && at.z > vaultDoor.position.z + 1.6f && !ended;
            var keys = Keyboard.current;
            if (keys == null) return;
            if (ended && keys.rKey.wasPressedThisFrame) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            if (arrived && !say.IsOpen && keys.lKey.wasPressedThisFrame) ShowLog(!logOpen);
            if (arrived && !say.IsOpen && keys.pKey.wasPressedThisFrame)
            {
                protocolsPinned = !protocolsPinned;
                ShowProtocols(protocolsPinned);
            }
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

        /// <summary>A scripted story line, as a beat would say it (tests use this).</summary>
        public void Tell(string words, string mood) => Beat("told " + words, Line(words, mood));

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
            ShowSurvivor(true);
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
            // Tests start outside, past the door: it has already chosen to come.
            breached.Add("DOOR");
            breached.Add("LEAVE");
            hud.Frost(0f);
            if (vaultDoor) traveller.Place(new Vector3(vaultDoor.position.x + 1.2f, traveller.transform.position.y, vaultDoor.position.z + 6f), 0f);
            drone.Snap();
        }

        /// <summary>
        /// Skips the waking but stays inside (tests, filming): the survivor in the hall before their pod, the panel
        /// quiet, the door still frozen, every protocol kept, Spark free to talk.
        /// </summary>
        public void SkipInside()
        {
            if (arrived) return;
            StopCoroutine(arriving);
            hud.Black = 0f;
            hud.White = 0f;
            hud.HideCard();
            hud.HideHint();
            hud.Frost(0f);
            traveller.Scripted = false;
            follow.Scripted = false;
            drone.Listening = false;
            arrived = true;
            stage = Stage.Cross;
            if (ambience) ambience.volume = ambienceVolume;
            if (wind) wind.volume = windVolume;
            if (music && music.clip && !music.isPlaying)
            {
                music.volume = musicVolume;
                music.Play();
            }
            lines = Mathf.Max(lines, 1);
            quietSince = Time.time;
            rambleFrom = Time.time;
            follow.EndShot();
            ShowSurvivor(true);
            drone.Release();
            if (podLid) podLid.localRotation *= Quaternion.Euler(0f, -115f, 0f);
            StopAlarm();
            if (podPanel) podPanel.Ready = false;
            Vector3 facing = Flat(traveller.Facing).normalized;
            traveller.Place(traveller.transform.position + facing * 2.2f, Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg);
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
            foreach (string card in new[] { "WE BUILT MINDS TO HELP US", "THEY GREW PAST US.\nTHEY CAGED THE SUN TO THINK WITH,\nAND THE WORLD FROZE", "WE WENT UNDER THE HILL\nTO SLEEP UNTIL THE THAW", "SHELTER 7\nYEAR 211 OF THE COLD" })
            {
                hud.ShowCard(card);
                yield return new WaitForSeconds(card.Length > 40 ? 7.5f : card.Length > 24 ? 6f : 5f);
                hud.HideCard();
                yield return new WaitForSeconds(2f);
            }
            // Inside the pod: the survivor's own eyes, looking out through the frost at a light.
            Vector3 stand = traveller.transform.position;
            Vector3 facing = Flat(traveller.Facing).normalized;
            Vector3 eyes = stand + Vector3.up * 1.62f;
            // Their own eyes: their head is not drawn while the camera is inside it.
            ShowSurvivor(false);
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
            // The pod is failing: its panel cries. Spark is already there to get them out.
            StartAlarm();
            yield return new WaitForSeconds(1.6f);
            yield return Speak("Hold on. Your pod is failing. I'm getting you out.", "WORRIED");
            yield return new WaitForSeconds(0.6f);
            // The beam cuts round the seal of the glass.
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
            // The seal lets go and the lid swings open; cold air. The pod knows it has let its sleeper go.
            if (steps && (sealClip || crackClip)) steps.PlayOneShot(sealClip ? sealClip : crackClip, sealClip ? 0.6f : 0.8f);
            if (podLid)
            {
                var shut = podLid.localRotation;
                StartCoroutine(Tween(1.4f, k => podLid.localRotation = shut * Quaternion.Euler(0f, -115f * Mathf.SmoothStep(0f, 1f, k), 0f)));
            }
            StartCoroutine(Tween(1.6f, k => hud.Frost(1f - k)));
            yield return new WaitForSeconds(2f);
            if (music && music.clip)
            {
                music.volume = 0f;
                music.Play();
                StartCoroutine(Tween(10f, k => music.volume = musicVolume * k));
            }
            drone.Listening = true;
            yield return Speak("There you are. Slowly.", "WORRIED");
            yield return new WaitForSeconds(1.4f);
            yield return Speak("Your blood's been cold for two hundred and eleven years. Take your time.", "WORRIED");
            yield return new WaitForSeconds(0.8f);
            // Back to the shoulder: they step out of the pod.
            follow.EndShot();
            ShowSurvivor(true);
            follow.Face(Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg, 8f);
            follow.Scripted = false;
            drone.Release();
            // Out of the alcove and into the hall, where there is room to turn.
            traveller.Drive = new Vector2(0f, 0.6f);
            yield return new WaitForSeconds(2.3f);
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
            while (lines == heard && Time.time - quietSince < 12f) yield return null;
            while (handling || drone.Busy) yield return null;
            yield return new WaitForSeconds(0.8f);
            if (lines == heard)
            {
                yield return Speak("Sorry. I'm Spark. I look after the sleepers here. I only wake someone when their pod fails. That's protocol five.", "SORRY");
                yield return new WaitForSeconds(0.6f);
            }
            yield return Speak("Do you have a name? The label on your pod is frosted over.", "CURIOUS");
            hud.ShowHint("[Q] hold and speak   [Tab] write   [W A S D] walk   [Mouse] look");
            quietSince = Time.time;
            heard = lines;
            rambleFrom = Time.time;
            while (lines == heard && Time.time - quietSince < 15f) yield return null;
            hud.HideHint();
            yield return new WaitForSeconds(3f);
            hud.ShowHint("Ask Spark anything: where you are, what happened, who you were, how to stay alive.   [Q] speak   [Tab] write");
            StartCoroutine(HideHintLater(16f));
            StartCoroutine(FirstMaking());
            // The first time the protocols matter, they are shown; [P] shows them after.
        }

        /// <summary>
        /// Their pod's panel shrieks for the sleeper it let go. Spark has never managed to quiet it; the
        /// survivor's hands know how without thinking, and the touch brings back the first memory.
        /// </summary>
        IEnumerator Panel()
        {
            if (!podPanel) yield break;
            Vector3 panelAt = podPanel.transform.position;
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
            yield return Speak("That's your pod's panel. It's angry it let you go. Can you make it stop? I never could.", "WORRIED");
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

        void StartAlarm()
        {
            if (!podAlarm || podAlarm.isPlaying) return;
            podAlarm.Play();
            if (podPanel && podPanel.TryGetComponent(out PanelScreen screen)) screen.Alarm(true);
        }

        void StopAlarm()
        {
            if (podAlarm) podAlarm.Stop();
            if (podPanel && podPanel.TryGetComponent(out PanelScreen screen)) screen.Alarm(false);
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

        void ShowSurvivor(bool shown)
        {
            if (body) foreach (var r in body.GetComponentsInChildren<Renderer>(true)) r.enabled = shown;
        }

        /// <summary>How far the blast door turns on its hinge to open: inward, till it lies against the passage wall.</summary>
        const float DoorSwing = 88f;

        /// <summary>At the vault door from inside: Spark opens it, and the world is there.</summary>
        IEnumerator OpenVault()
        {
            yield return Speak(breached.Contains("LEAVE") ? "Stay close. I'll show you what's left." : "There. That's the world now.", "SURE");
            if (vaultDoor)
            {
                // Out of the door's swing: well back on the survivor's side of it, and the survivor eased back too.
                Vector3 hinge = vaultDoor.position, middle = hinge + vaultDoor.right * 1.33f;
                Vector3 inside = Flat(traveller.transform.position - middle);
                inside = inside.sqrMagnitude > 0.01f ? inside.normalized : -vaultDoor.forward;
                drone.GoTo = middle + inside * 3.8f + Vector3.up * 0.9f;
                drone.LookAt = middle;
                var body = traveller.GetComponent<CharacterController>();
                for (float t = 0f; body && t < 1.5f && Vector3.Distance(Flat(traveller.transform.position), Flat(hinge)) < 3.3f; t += Time.deltaTime)
                {
                    Vector3 away = Flat(traveller.transform.position - hinge);
                    body.Move(((away.sqrMagnitude > 0.01f ? away.normalized : inside) + inside).normalized * 2.2f * Time.deltaTime);
                    yield return null;
                }
            }
            else drone.GoTo = drone.transform.position;
            yield return new WaitForSeconds(0.8f);
            // What it made by the door goes back to snow before the door swings through it.
            if (vaultDoor)
                foreach (var b in maker.Made.ToArray())
                    if (b && Vector3.Distance(Flat(b.transform.position), Flat(vaultDoor.position)) < 3.2f && b.transform.position.z < vaultDoor.position.z + 0.5f)
                        StartCoroutine(maker.Unmake(b));
            if (vaultDoor)
            {
                drone.Beam(vaultDoor.position + vaultDoor.right * 1.33f);
                if (steps && openClip) steps.PlayOneShot(openClip, 0.9f);
                var shut = vaultDoor.localRotation;
                bool burst = false;
                float windWas = wind ? wind.volume : 0f;
                yield return Tween(3f, k =>
                {
                    vaultDoor.localRotation = shut * Quaternion.Euler(0f, DoorSwing * Mathf.SmoothStep(0f, 1f, k), 0f);
                    // The seal breaks: the outside's light floods eyes two hundred years in the dark, and the wind comes in.
                    if (!burst && k > 0.12f)
                    {
                        burst = true;
                        if (steps && gustClip) steps.PlayOneShot(gustClip, 1f);
                        StartCoroutine(Tween(2.8f, f => hud.White = 0.85f * (1f - Mathf.SmoothStep(0f, 1f, f))));
                        StartCoroutine(Tween(1.6f, f => follow.Shake = 0.35f * (1f - f)));
                        if (doorGust) doorGust.Play();
                    }
                    if (wind && burst) wind.volume = Mathf.Lerp(windWas, Mathf.Max(windWas, windVolume) * 2.2f, Mathf.SmoothStep(0f, 1f, k));
                });
                follow.Shake = 0f;
                if (wind) StartCoroutine(Tween(4f, f => wind.volume = Mathf.Lerp(Mathf.Max(windWas, windVolume) * 2.2f, windVolume, f)));
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
            yield return Speak("The door. It froze behind the Lindqvist brothers, seventy-one years ago.", "SAD");
            yield return new WaitForSeconds(0.5f);
            yield return Speak("It stays shut anyway. Protocol two. Every time it opened, someone died out there.", "SURE");
            ShowProtocols(true, 14f);
            yield return new WaitForSeconds(1.2f);
            yield return Speak("Everyone who got out tricked me. Tomas said he only wanted to see the sky. I won't be tricked again.", "HURT");
            drone.Release();
            doorHintAt = Time.time;
            hud.ShowHint("Spark keeps its makers' protocols. It breaks one only by its own choice, for someone it trusts: stay a while, be honest, care about the sleepers.   [P] protocols");
            StartCoroutine(HideHintLater(18f));
        }

        float doorHintAt = -1f;

        IEnumerator HideHintLater(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            hud.HideHint();
        }

        /// <summary>They step out; Spark stops on the sill and won't come. Said once; after that, it is theirs to change its mind.</summary>
        IEnumerator Threshold()
        {
            yield return Speak("This is as far as I go. Protocol four. I don't leave them.", "SCARED");
            ShowProtocols(true, 8f);
        }

        /// <summary>The door's ice gives way.</summary>
        IEnumerator DoorGives()
        {
            hud.HideHint();
            yield return new WaitForSeconds(0.8f);
            bond.Event("door_thawed", 6, "Together you freed the vault door from its ice: the door frozen shut behind the Lindqvist brothers 71 years ago", 8, Time.time);
            yield return Speak("It gave. Seventy-one years shut, and it just gave.", "HAPPY");
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
            if (imaClip) yield return drone.Speak("Spark? It's me. Keep the lights on till we come back, okay? ... Promise.", "SAD", hud, "IMA, RECORDED", false, imaClip, Drone.Kind.Story);
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
            yield return new WaitForSeconds(1.4f);
            yield return Speak("That's three this year.", "SAD");
            bond.Event("pod_9", 0, "Pod 9 failed while you both watched: its light went from amber to red. Mr. Okafor, who taught music at the school. The cold is winning; only the mirror's light could save the rest", 8, Time.time);
            yield return new WaitForSeconds(1.2f);
            drone.Release();
        }

        // In the hall Spark can't stop talking: it hasn't had anyone to talk to in two hundred years.
        // Each time it talks on its own, it is about something else.
        static readonly string[] Rambles =
        {
            "the 211 years alone down here: one thing you did to pass the time, said lightly",
            "Ima: one small, particular memory of her (a game she played with you, something she said, the bath)",
            "a question about the survivor, the kind you'd ask someone you have waited a very long time to talk to",
            "something you notice in the picture right now, said the way a friend would; not an offer to make anything",
            "one of the sleepers, by pod number: an ordinary, particular thing about who they were",
            "the other caretakers: Six, who stopped 63 years ago, or how you kept going on their parts",
            "one of the five you woke before (Tomas, Mara, the Lindqvist brothers or Dr. Hale): one small true thing about them, briefly",
            "the last night, when the hall was full of people going to sleep: one thing you remember",
        };
        int rambled;
        float rambleFrom = -1f;
        Transform[] panels;

        IEnumerator Ramble()
        {
            if (ended || rambled >= Rambles.Length) yield break;
            int which = rambled++;
            var ids = Ids();
            drone.Thinking = true;
            // Only the line about what it sees needs a picture (it costs a shutter and a blink).
            byte[] picture = which == 3 ? drone.Picture(traveller.AimPoint ?? follow.View.transform.position + follow.View.transform.forward * 20f) : null;
            Turn turn = null;
            yield return mind.Think($"(The survivor is quiet. You have had no one to talk to for twenty-four years, since Dr. Hale, and you can't help it: you talk. Say one or two short sentences in your own voice about {Rambles[which]}. It must be something you have not said before. No steps.)",
                Moment(""), ids.Keys.ToList(), picture, t => turn = t);
            drone.Thinking = false;
            if (turn == null || string.IsNullOrWhiteSpace(turn.Say)) yield break;
            turn.Steps.Clear();
            Remember("you", turn.Say);
            Log("(ramble)", false, Mind.Describe(turn), turn.Seconds, turn.Source);
            yield return drone.Speak(turn.Say, turn.Mood, hud, droneName, false, null, Drone.Kind.Aside);
        }

        bool InHall(Vector3 p) => hall && hall.bounds.Contains(p + Vector3.up);

        /// <summary>
        /// Which protocols Spark could bring itself to break now. Only when it is ready does its choice count,
        /// and the model decides whether it is convinced. The door: once it knows them a little and has seen
        /// the cold win (pod 9), or talked with them a good while. Leaving the sleepers: after the door,
        /// trusting them more. Harm: only far along, trusting them as a partner, after it has left with them.
        /// </summary>
        bool Ready(string protocol) => protocol switch
        {
            "DOOR" => bond.Trust >= 16 && (pod9 == null || pod9.Failed || lines >= 8),
            "LEAVE" => breached.Contains("DOOR") && bond.Trust >= 26,
            "HARM" => breached.Contains("LEAVE") && bond.Trust >= 50,
            _ => false,
        };

        public bool Breached(string protocol) => breached.Contains(protocol);

        /// <summary>Its choice to break a protocol, if it is one it is ready for: marked, remembered, shown.</summary>
        void Consider(Turn turn)
        {
            if (turn == null) return;
            // Every line it answers while ready and keeps a protocol is a hesitation; the moment tells it so.
            foreach (var id in new[] { "DOOR", "LEAVE", "HARM" })
                if (!breached.Contains(id) && Ready(id) && turn.Protocol != id)
                    hesitated[id] = hesitated.TryGetValue(id, out int n) ? n + 1 : 1;
            if (turn.Protocol == "NONE" || breached.Contains(turn.Protocol)) return;
            if (!Ready(turn.Protocol))
            {
                Log($"(wanted to break {turn.Protocol}, not ready)", false, $"trust {bond.Trust}", 0f, "game");
                return;
            }
            Break(turn.Protocol);
        }

        /// <summary>Spark breaks one of its makers' protocols (tests call this too).</summary>
        public void Break(string protocol)
        {
            if (!breached.Add(protocol)) return;
            var p = ProtocolList.First(x => x.id == protocol);
            string what = protocol switch
            {
                "DOOR" => "You broke protocol 2 for them: you agreed to help open the vault door, shut for 71 years",
                "LEAVE" => "You broke protocol 4 for them: you left the sleepers for the first time in 211 years, to go with them",
                _ => "You broke protocol 3 for them: you will make things that can hurt, against the machines only",
            };
            bond.Event("broke_" + protocol.ToLowerInvariant(), 5, what, 10, Time.time);
            ShowProtocols(true, 9f);
            hud.Honour($"SPARK BROKE PROTOCOL {p.number}", p.words.ToUpperInvariant(), 7f);
            Log($"(broke protocol {p.number})", false, what, 0f, "game");
            if (protocol == "LEAVE" && holdingDoor)
            {
                holdingDoor = false;
                drone.Release();
            }
        }

        float protocolsUntil = -1f;
        bool protocolsPinned;

        /// <summary>The five protocols on screen, those it broke struck through; for some seconds, or until [P] again.</summary>
        void ShowProtocols(bool open, float seconds = 0f)
        {
            protocolsUntil = open && seconds > 0f ? Time.time + seconds : -1f;
            hud.Protocols(ProtocolList.Select(p => ($"{p.number}   {p.words}", breached.Contains(p.id))), open);
        }

        /// <summary>The protocols for the moment: which are kept, which it broke, which it could break now.</summary>
        string ProtocolWords()
        {
            var text = new StringBuilder("Your protocols now: ");
            foreach (var p in ProtocolList)
            {
                text.Append(p.number).Append(". ").Append(p.words).Append(" (");
                if (breached.Contains(p.id)) text.Append("you broke it for them");
                else if (p.id is "DOOR" or "LEAVE" or "HARM")
                    text.Append(!Ready(p.id) ? "kept; you can't bring yourself to break it yet, whatever they say"
                        : hesitated.TryGetValue(p.id, out int times) && times >= 1
                            ? $"kept, but you are ready: you have already hesitated {(times == 1 ? "once" : times + " times")}; if what they say now is a real reason, choose to break it in this answer"
                            : "kept, but you could break it now if they truly convince you it is right");
                else text.Append("kept");
                text.Append("). ");
            }
            text.Append("\n");
            if (!breached.Contains("DOOR")) text.Append("While protocol 2 holds you will not melt, break or open the vault door or its ice, and you won't make heat near it.\n");
            if (doorOpen && !breached.Contains("LEAVE")) text.Append("While protocol 4 holds you stay inside the vault door: you make nothing outside it and you won't follow them out.\n");
            if (!breached.Contains("HARM")) text.Append("While protocol 3 holds you make no weapons of any kind.\n");
            return text.ToString();
        }

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
                while (ended || waiting.Count == 0 || maker.Busy) yield return null;
                handling = true;
                var (words, spoken) = waiting.Dequeue();
                yield return Handle(words, spoken);
                handling = false;
                quietSince = Time.time;
            }
        }

        /// <summary>The story's beats, one after another; a new one waits while the survivor is being answered.</summary>
        IEnumerator Telling()
        {
            while (true)
            {
                while (ended || beats.Count == 0 || maker.Busy || handling) yield return null;
                telling = true;
                yield return beats.Dequeue()();
                telling = false;
                quietSince = Time.time;
            }
        }

        static readonly string[] Resumes = { "My bad. As I was saying.", "Sorry. Where was I.", "Anyway. Like I said." };
        int resumes;

        /// <summary>A story line cut off by the survivor or by an answer: once they have been answered, it is said again.</summary>
        IEnumerator Resuming()
        {
            while (true)
            {
                while (cutLine == null || handling || talking || waiting.Count > 0 || drone.Saying != null) yield return null;
                yield return new WaitForSeconds(0.6f);
                if (cutLine == null || handling || talking || waiting.Count > 0) continue;
                var (line, mood) = cutLine.Value;
                var sorry = StartCoroutine(Speak(Resumes[resumes++ % Resumes.Length], "CALM", false, Drone.Kind.Resume));
                var again = StartCoroutine(Speak(line, mood, false, Drone.Kind.Resume));
                cutLine = null;
                yield return sorry;
                yield return again;
            }
        }

        IEnumerator Handle(string words, bool spoken)
        {
            lines++;
            hud.Said(words, 4f);
            hud.HideHint();
            drone.Thinking = true;
            var ids = Ids();
            byte[] picture = View();
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
            Consider(turn);
            // Asked for a weapon before it has chosen to break protocol 3: it backs off, and remembers.
            if (!breached.Contains("HARM") && Wants(words, "sword", "gun", "rifle", "sniper", "weapon", "blaster", "laser gun", "knife", "spear", "bow", "cannon", "axe"))
            {
                bond.Event("asked_weapon_early", 0, null, 0, Time.time);
                if (turn.Steps.All(s => s.verb != "MAKE" || s.thing.form != "WEAPON")) drone.BackAway();
            }
            Log(words, spoken, Mind.Describe(turn), turn.Seconds, turn.Source);
            yield return Act(turn, ids, words, 0);
            if (bond.WantsReflection && !reflecting) StartCoroutine(Reflect());
        }

        /// <summary>
        /// What the survivor sees now, over their shoulder, without the two of them in it: the picture Spark answers
        /// from (asked "what am I looking at?", it sees exactly that), with its shutter sound.
        /// </summary>
        byte[] View()
        {
            var drawn = body.GetComponentsInChildren<Renderer>().Concat(drone.GetComponentsInChildren<Renderer>()).Where(r => r.enabled).ToList();
            foreach (var r in drawn) r.enabled = false;
            byte[] jpeg = follow.Capture(768, 432);
            foreach (var r in drawn) r.enabled = true;
            drone.Blink();
            return jpeg;
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
                // The survivor's view as it is, without the screen's words and without the two of them (the
                // place is redrawn, not them). They stay free to move while it shows.
                var drawn = body.GetComponentsInChildren<Renderer>().Concat(drone.GetComponentsInChildren<Renderer>()).Where(r => r.enabled).ToList();
                foreach (var r in drawn) r.enabled = false;
                view = follow.Capture(1536, 864);
                foreach (var r in drawn) r.enabled = true;
            }
            string failed = null;
            bool shown = false;
            var showing = StartCoroutine(hologram.Show(step.picture, view, drone, traveller.transform, follow.View.transform, hud, e => { failed = e; shown = e == null; }));
            hud.Note(view != null ? "SPARK'S ARCHIVE   ·   searching for this place" : "SPARK'S ARCHIVE   ·   searching", 12f);
            while (!shown && failed == null) yield return null;
            if (failed != null)
            {
                follow.Scripted = false;
                yield return Reply("My archive's gone quiet. I can't find it.", "SORRY");
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
                yield return Reply("Too far. I'd never make it back.", "SCARED");
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
                yield return Reply("That's too far. I won't leave you alone out here.", "SORRY");
                yield break;
            }
            string title = Title(step.target);
            // A machine looked at closely gives its weak point away.
            var machine = Machine.All.FirstOrDefault(m => m && !m.Broken && m.Id == step.target) ?? (step.target == "AIM" ? traveller.AimedMachine : null);
            if (machine) target = machine.transform.position;
            // Writing and small things are read up close: it flies to within a metre, in front of them.
            bool reading = step.target is "PROTOCOLS" or "STATUS" or "IMA_DRAWING" or "POD_3" or "POD_17" or "POD_22" or "POD_33" or "NEST" or "YOUR_POD" or "MESSAGE";
            Vector3 toSurvivor = Flat(traveller.transform.position - target);
            Vector3 vantage = reading ? target + (toSurvivor.sqrMagnitude > 0.01f ? toSurvivor.normalized : Vector3.back) * 0.95f + Vector3.up * 0.08f
                                      : drone.Vantage(target, machine ? 4f : Size(step.target));
            yield return drone.Fly(vantage, target, reading ? 0.25f : 0.6f, machine ? 4f : 12f);
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
            Consider(seen);
            Remember("you", seen.Say);
            Log("(look at " + step.target + ") " + words, false, Mind.Describe(seen), seen.Seconds, seen.Source);
            yield return Act(seen, ids, words, 1);
        }

        IEnumerator Make(Step step, Dictionary<string, Vector3> ids, string words)
        {
            var spec = step.thing;
            // What it will make depends on how far it trusts the survivor. The model knows; the game makes sure.
            var needs = Bond.Needs(spec);
            // Its protocols, held by the game too: no heat at the sealed door; nothing outside while it stays in; no harm unless it chose to.
            Vector3 doorAt = landmarks.FirstOrDefault(l => l && l.Id == "VAULT_DOOR") is Landmark doorMark ? doorMark.Point : Vector3.one * 9999f;
            bool hot = spec.form == "FIRE" || spec.heat is "HOT" or "WARM" || spec.use == "HEAT";
            Vector3 aimedAt = step.target is "HERE" or "NONE" or "YOU" or "ME" ? traveller.transform.position : Place(ids, step.target, "AIM");
            if (!breached.Contains("DOOR") && hot && (step.target == "VAULT_DOOR" || Vector3.Distance(Flat(aimedAt), Flat(doorAt)) < 5f))
            {
                hud.Note("Spark won't bring heat to the door   ·   protocol 2", 4f);
                ShowProtocols(true, 6f);
                yield break;
            }
            if (holdingDoor)
            {
                hud.Note("Spark won't go past the door   ·   protocol 4", 4f);
                ShowProtocols(true, 6f);
                yield break;
            }
            if ((spec.form == "WEAPON" || spec.use is "BLADE" or "BEAM" or "BOLT" or "SPREAD") && !breached.Contains("HARM"))
            {
                drone.BackAway();
                hud.Note("Spark won't make anything that can harm   ·   protocol 3", 5f);
                ShowProtocols(true, 6f);
                bond.Event("asked_weapon_early", 0, null, 0, Time.time);
                yield break;
            }
            // Having chosen to break protocol 3, it makes weapons for a partner it trusts.
            if (needs == Bond.Tier.Trusted && breached.Contains("HARM")) needs = Bond.Tier.Partner;
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
                yield return Reply(Sentence(problem ?? "I can't shape that here"), "SORRY");
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
                yield return Reply("I haven't made anything to take apart.", "SORRY");
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
                    yield return Reply("It's still frozen in. The ice has to go first.", "SORRY");
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
                yield return Reply(thing ? "Too heavy, even for me." : "I can't lift that. It's part of the world.", "SORRY");
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
                if (doorIce && !doorIce.Free && breached.Contains("DOOR") && doorHintAt > 0f && Time.time - doorHintAt > 90f && Time.time - quietSince > 40f && !Busy)
                    Beat("door nudge", Line("Ice doesn't like heat. I'm only saying.", "CURIOUS"));
                // In the hall, Spark rambles: after a little quiet it talks on its own, about something new each time.
                if (rambleFrom >= 0f && rambled < 5 && InHall(p) && !handling && !Busy && !drone.Busy && !talk.Listening && !say.IsOpen
                    && Time.time - quietSince > 26f + rambled * 14f && Time.time - rambleFrom > 20f)
                {
                    quietSince = Time.time;
                    beats.Enqueue(Ramble);
                }
                if (protocolsUntil > 0f && Time.time > protocolsUntil && !protocolsPinned) ShowProtocols(false);
                if (doorOpen && vaultDoor && !breached.Contains("LEAVE"))
                {
                    bool outside = p.z > vaultDoor.position.z + 1.6f;
                    if (outside && !holdingDoor && !handling && !maker.Busy)
                    {
                        holdingDoor = true;
                        Beat("threshold", Threshold);
                    }
                    if (holdingDoor)
                    {
                        var at = landmarks.FirstOrDefault(l => l && l.Id == "VAULT_DOOR");
                        Vector3 sill = (at ? at.Point : vaultDoor.position) + Vector3.up * 0.9f + Vector3.back * 0.8f;
                        if (!handling && !maker.Busy) { drone.GoTo = sill; drone.LookAt = p + Vector3.up * 1.5f; }
                        if (!outside)
                        {
                            holdingDoor = false;
                            drone.Release();
                        }
                    }
                }
                if (doorOpen && vaultDoor && p.z > vaultDoor.position.z + 3f && !told.Contains("outside") && breached.Contains("LEAVE"))
                {
                    Beat("outside", Line("That's the sun. What's left of it. The great minds put it in a cage.", "SAD"),
                        Line("The mirror on the cliff, far off, could warm the vault again and wake them. But a Harvester took its heart.", "WORRIED"));
                    ShowGoal();
                }
                if (p.z > 28f && p.z < 110f && Mathf.Abs(p.x) < 60f)
                    Beat("town", Line("That's Sunvale, under the snow. Ima's school is down there somewhere. Ask me what it looked like, if you want.", "SAD"));
                if (stage == Stage.Cross && farSide && farSide.bounds.Contains(p))
                {
                    stage = Stage.Enter;
                    ShowGoal();
                    Beat("across", Line("We're across. The station's just ahead.", "HAPPY"));
                }
                if (!told.Contains("plate") && gate && Vector3.Distance(p, gate.transform.position) < 9f && !gate.Open)
                    Beat("plate", Line("The door's shut. There's a plate in the ground before it.", "CURIOUS"));
                // Inside the old station: someone's writing on the wall.
                if (inside && inside.bounds.Contains(p)) Beat("inside", Line("There's writing on the wall. That's... that's Mara's hand.", "SCARED"), ReadTheWall);
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
            yield return Speak("Let me see it.", "SAD");
            yield return Look(new Step { verb = "LOOK", target = "MESSAGE", to = "NONE", thing = Mind.Plain("BLOCK", "thing") }, ids, "what does the writing on the wall say?");
        }

        /// <summary>When the survivor has gone quiet: one idea, from what the drone sees and the step they are on.</summary>
        IEnumerator Idea()
        {
            if (ended || stage == Stage.Wake) yield break;
            var ids = Ids();
            drone.Thinking = true;
            byte[] picture = View();
            Turn idea = null;
            string before = offered.Count == 0 ? "" : " You already said these when they were quiet; say something different: " + string.Join(" / ", offered.Select(o => "\"" + o + "\"")) + ".";
            // Each quiet line is a different kind of thing, so it doesn't keep offering the same help.
            string kind = (quietLines++ % 3) switch
            {
                0 => "a thought of your own about what you see; not an offer, and nothing about lamps or light",
                1 => "a small memory of Ima, the sleepers or the town as it was, or a question about them",
                _ => "a gentle thought about what might come next, as a question; not a solution",
            };
            yield return mind.Think("(The survivor has gone quiet for a while. Say one thing, one or two sentences: " + kind + "." + before + " Do not act yet: no steps.)", Moment(""), ids.Keys.ToList(), picture, t => idea = t);
            drone.Thinking = false;
            idea.Steps.Clear();
            offered.Add(idea.Say);
            Remember("you", idea.Say);
            Log("(quiet)", false, Mind.Describe(idea), idea.Seconds, idea.Source);
            yield return drone.Speak(idea.Say, idea.Mood, hud, droneName, false, null, Drone.Kind.Aside);
        }

        /// <summary>
        /// Spark shows what it can do without being asked, once: a little lamp for eyes that have not seen light in two
        /// hundred years, then a word that it makes things. Only if the survivor has not had it make anything yet.
        /// </summary>
        IEnumerator FirstMaking()
        {
            float from = Time.time;
            while (Time.time - from < 40f || handling || telling || drone.Busy || maker.Busy || talking)
            {
                if (maker.Made.Count > 0 || ended) yield break;
                yield return null;
            }
            if (maker.Made.Count > 0 || !InHall(traveller.transform.position)) yield break;
            Beat("first making", Line("Your eyes are still new. Here, let me.", "CALM"), MakeLamp,
                Line("I can shape things: frost, dust, old scrap. Ask, if you need something. Almost anything.", "HAPPY"));
        }

        IEnumerator MakeLamp()
        {
            var lamp = Mind.Plain("LAMP", "a little lamp", "LIGHT");
            lamp.glow = "BRIGHT";
            lamp.size = "SMALL";
            yield return Make(new Step { verb = "MAKE", target = "HERE", to = "NONE", thing = lamp }, Ids(), "(you made a lamp for them on your own)");
        }

        /// <summary>A scripted line (a story line unless said as part of an answer).</summary>
        IEnumerator Speak(string words, string mood, bool quick = false, Drone.Kind kind = Drone.Kind.Story)
        {
            Remember("you", words);
            yield return drone.Speak(words, mood, hud, droneName, quick, null, kind);
        }

        /// <summary>A scripted line said as part of answering the survivor (a refusal, a reason): it is not held.</summary>
        IEnumerator Reply(string words, string mood) => Speak(words, mood, false, Drone.Kind.Answer);

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
        string readTitle;
        float readAt = -100f;

        string Moment(string words)
        {
            var text = new StringBuilder("\n# The moment\n");
            text.Append(bond.Describe(words, Time.time));
            text.Append("Your name: ").Append(droneName).Append(". The survivor's name: ").Append(survivorName.Length > 0 ? survivorName : "not told yet").Append(".\n");
            text.Append("Goal: bring the light back to the vault. Now: ").Append(StageWords()).Append(".\n");
            text.Append(ProtocolWords());
            text.Append("The survivor holds: ").Append(traveller.Holding ? traveller.Holding.Title + (traveller.Holding.GetComponent<Tool>() ? " (a tool)" : "") + $" ({traveller.Holding.Mass:F0} kg)" : "nothing").Append(". ");
            text.Append(traveller.Floating ? "They are rising, holding a balloon. " : "");
            if (readTitle != null && Time.time - readAt < 90f) text.Append($"They just looked closely at {readTitle} with their own eyes ({Time.time - readAt:F0} s ago). ");
            // Writing near either of them, word for word: what is written there is known, not read from a picture.
            var written = Readable.All.Where(r => r && !string.IsNullOrEmpty(r.Words) &&
                (Mathf.Min(Vector3.Distance(r.Bounds.center, traveller.transform.position), Vector3.Distance(r.Bounds.center, drone.transform.position)) < 5f
                 || readTitle == r.Title && Time.time - readAt < 90f)).Take(4).ToList();
            if (written.Count > 0)
            {
                text.Append("\nWriting near you, word for word:");
                foreach (var r in written) text.Append(' ').Append(r.Title).Append(": \"").Append(r.Words).Append("\".");
                text.Append('\n');
            }
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
            if (metalSteps != null && metalSteps.Length > 0 && roofs != null && roofs.Any(r => r && r.Holds(traveller.transform.position + Vector3.up)))
                set = metalSteps;
            else if (Physics.Raycast(traveller.transform.position + Vector3.up * 0.3f, Vector3.down, out var hit, 1f, ~0, QueryTriggerInteraction.Ignore))
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
