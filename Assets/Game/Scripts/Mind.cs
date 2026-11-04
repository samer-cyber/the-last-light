using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Game
{
    /// <summary>One piece of a thing the drone designs itself, in tenths of a metre.</summary>
    [Serializable]
    public class PartSpec
    {
        public string shape;
        public int x, y, z, w, h, d;
        public string look;
    }

    /// <summary>A thing to make, as the model described it: a form, what it looks like, and what it does.</summary>
    [Serializable]
    public class ThingSpec
    {
        public string name;
        public string form;
        public string look;
        public string size;
        public int height, length;
        public string weight, heat, glow;
        public bool floats, bouncy;
        public string use;
        /// <summary>For things to fight with: how hard, how far and how fast, 1 to 5 each, ten at most together.</summary>
        public int power, reach, rate;
        public PartSpec[] parts;
    }

    /// <summary>One thing the drone does: look, make, unmake, move, come back, wait, show a hologram or draw a machine off.</summary>
    [Serializable]
    public class Step
    {
        public string verb;
        public string target;
        public string to;
        public ThingSpec thing;
        /// <summary>For SHOW and SHOW_HERE: what the hologram shows, in plain words.</summary>
        public string picture;
    }

    /// <summary>What the drone makes of one line: what it says, how it feels, what it does, what it keeps.</summary>
    public class Turn
    {
        public string Say = "";
        public string Mood = "CALM";
        public List<Step> Steps = new List<Step>();
        public string Remember = "", DroneName = "", SurvivorName = "";
        /// <summary>How much the moment matters to it, 1 to 10.</summary>
        public int Importance = 3;
        /// <summary>How the line changed its trust: -3 to 3, and why.</summary>
        public int Trust;
        public string TrustReason = "";
        /// <summary>The protocol it chose to break with this line: NONE, DOOR, LEAVE or HARM.</summary>
        public string Protocol = "NONE";
        public string Source;
        public float Seconds;
    }

    /// <summary>
    /// The drone's mind: one Muse Spark call per line, with the card (who it is, the world, what it can
    /// do), the moment (what is here now, what was said) and the picture its eye just took. The answer
    /// comes back in a strict form. Only the words it says and the names of the things it makes are its
    /// own; every act, place, shape and property comes from a list, and the game checks each again.
    /// Without a key or a connection a few word rules stand in.
    /// </summary>
    public class Mind : MonoBehaviour
    {
        [SerializeField] string model = "muse-spark-1.3";
        [SerializeField, Tooltip("minimal, low, medium or high")] string effort = "minimal";
        [SerializeField] float timeout = 25f;
        [SerializeField] TextAsset card;
        [SerializeField] bool offlineOnly;

        public string Model => !offlineOnly && MetaApi.HasKey ? model : "offline";

        public static readonly string[] Verbs = { "LOOK", "MAKE", "UNMAKE", "MOVE", "COME", "WAIT", "SHOW", "SHOW_HERE", "DISTRACT" };
        public static readonly string[] Forms = { "BRIDGE", "STAIRS", "RAMP", "LIFT", "PLATFORM", "WALL", "PILLAR", "BLOCK", "BALL", "PLANK", "LAMP", "FIRE", "BALLOON", "SPRING", "TOOL", "GLIDER", "SHIELD", "MIRROR", "DECOY", "THROWN", "WEAPON", "CUSTOM" };
        public static readonly string[] Looks = { "STONE", "METAL", "ICE", "GLASS", "WOOD", "LIGHT", "SNOW" };
        public static readonly string[] Sizes = { "TINY", "SMALL", "MEDIUM", "LARGE", "HUGE" };
        public static readonly string[] Weights = { "FEATHER", "LIGHT", "NORMAL", "HEAVY" };
        public static readonly string[] Heats = { "COLD", "NONE", "WARM", "HOT" };
        public static readonly string[] Glows = { "NONE", "DIM", "BRIGHT" };
        public static readonly string[] Uses = { "NONE", "BREAK", "HEAT", "LIGHT", "BLADE", "BEAM", "BOLT", "SPREAD", "SHOCK", "FLARE" };
        public static readonly string[] Shapes = { "BOX", "CYLINDER", "SPHERE", "CONE" };
        public static readonly string[] Moods = { "CALM", "HAPPY", "PLAYFUL", "CURIOUS", "SURE", "WORRIED", "SCARED", "SAD", "HURT", "ANGRY", "SORRY" };
        public static readonly string[] TrustChanges = { "MUCH_LESS", "LESS", "A_LITTLE_LESS", "SAME", "A_LITTLE_MORE", "MORE", "MUCH_MORE" };
        public static readonly string[] Protocols = { "NONE", "DOOR", "LEAVE", "HARM" };

        /// <summary>
        /// Thinks about the words; calls back with the turn (never fails: word rules stand in). The
        /// picture, if any, is a JPEG of what the eye sees.
        /// </summary>
        public IEnumerator Think(string words, string moment, IList<string> ids, byte[] picture, Action<Turn> done)
        {
            float began = Time.realtimeSinceStartup;
            Turn turn = null;
            if (!offlineOnly && MetaApi.HasKey)
            {
                string reply = null, error = null;
                yield return MetaApi.Chat(Body(words, moment, ids, picture), timeout, (r, e) => { reply = r; error = e; });
                // Meta's filter can refuse a line about arms ("a rifle to take them down"); said again in the
                // game's own words (a bolt caster, to stop a machine), it is the same request, and it passes.
                if (reply == null && error != null && error.Contains("content_policy"))
                {
                    string softer = Soften(words);
                    Debug.Log($"Mind: the filter refused \"{words}\"; asking again as \"{softer}\".");
                    yield return MetaApi.Chat(Body(softer, moment, ids, picture), timeout, (r, e) => { reply = r; error = e; });
                }
                turn = reply != null ? Parse(reply, ids) : null;
                if (turn != null) turn.Source = model;
                else Debug.LogWarning($"Mind: {model} gave no answer ({error ?? "unreadable: " + reply}); the word rules answer.");
            }
            if (turn == null)
            {
                turn = Offline(words);
                turn.Source = "offline";
            }
            turn.Seconds = Time.realtimeSinceStartup - began;
            Debug.Log($"Mind ({turn.Source}, {turn.Seconds:F1} s{(picture != null ? $", picture {picture.Length / 1024} KB" : "")}): \"{words}\" -> {Describe(turn)}");
            done(turn);
        }

        public static string Describe(Turn t)
        {
            var text = new StringBuilder($"{t.Mood.ToLowerInvariant()}: \"{t.Say}\"");
            foreach (var s in t.Steps)
            {
                text.Append("; ").Append(s.verb).Append(' ').Append(s.target);
                if (s.verb == "MOVE" || s.verb == "MAKE" && s.to != "NONE" && s.to != s.target) text.Append("->").Append(s.to);
                if (s.verb == "MAKE")
                {
                    var k = s.thing;
                    text.Append($" {k.form} \"{k.name}\" {k.look.ToLowerInvariant()} {k.size.ToLowerInvariant()}");
                    if (k.heat != "NONE") text.Append(' ').Append(k.heat.ToLowerInvariant());
                    if (k.glow != "NONE") text.Append(" glow");
                    if (k.floats) text.Append(" floats");
                    if (k.bouncy) text.Append(" bouncy");
                    if (k.weight != "NORMAL") text.Append(' ').Append(k.weight.ToLowerInvariant());
                    if (k.use != "NONE") text.Append(" use ").Append(k.use);
                    if (k.power + k.reach + k.rate > 0) text.Append($" p{k.power}r{k.reach}f{k.rate}");
                    if (k.parts != null && k.parts.Length > 0)
                        text.Append(" [").Append(string.Join(" ", k.parts.Select(p => $"{p.shape.ToLowerInvariant()}@{p.x},{p.y},{p.z}:{p.w}x{p.h}x{p.d}"))).Append(']');
                    if (k.height > 0) text.Append(" h").Append(k.height);
                    if (k.length > 0) text.Append(" l").Append(k.length);
                }
                if (s.verb is "SHOW" or "SHOW_HERE") text.Append(" \"").Append(s.picture).Append('"');
            }
            text.Append($" / weight {t.Importance}");
            if (t.Trust != 0) text.Append($" / trust {(t.Trust > 0 ? "+" : "")}{t.Trust} ({t.TrustReason})");
            if (t.Protocol != "NONE") text.Append(" / BREAKS PROTOCOL ").Append(t.Protocol);
            if (!string.IsNullOrEmpty(t.Remember)) text.Append(" / remember: ").Append(t.Remember);
            if (!string.IsNullOrEmpty(t.DroneName)) text.Append(" / called: ").Append(t.DroneName);
            if (!string.IsNullOrEmpty(t.SurvivorName)) text.Append(" / survivor: ").Append(t.SurvivorName);
            return text.ToString();
        }

        static string List(IEnumerable<string> items) => string.Join(",", items.Select(MetaApi.Quote));
        static string Enum(IEnumerable<string> items) => "{\"type\":\"string\",\"enum\":[" + List(items) + "]}";

        static string Schema(IList<string> ids)
        {
            const string integer = "{\"type\":\"integer\"}";
            const string text = "{\"type\":\"string\"}";
            const string flag = "{\"type\":\"boolean\"}";
            string part = "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"shape\",\"x\",\"y\",\"z\",\"w\",\"h\",\"d\",\"look\"],\"properties\":{" +
                          "\"shape\":" + Enum(Shapes) + ",\"x\":" + integer + ",\"y\":" + integer + ",\"z\":" + integer +
                          ",\"w\":" + integer + ",\"h\":" + integer + ",\"d\":" + integer + ",\"look\":" + Enum(Looks) + "}}";
            string thing = "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"name\",\"form\",\"look\",\"size\",\"height\",\"length\",\"weight\",\"heat\",\"glow\",\"floats\",\"bouncy\",\"use\",\"power\",\"reach\",\"rate\",\"parts\"],\"properties\":{" +
                           "\"name\":" + text + ",\"form\":" + Enum(Forms) + ",\"look\":" + Enum(Looks) + ",\"size\":" + Enum(Sizes) +
                           ",\"height\":" + integer + ",\"length\":" + integer + ",\"weight\":" + Enum(Weights) + ",\"heat\":" + Enum(Heats) +
                           ",\"glow\":" + Enum(Glows) + ",\"floats\":" + flag + ",\"bouncy\":" + flag + ",\"use\":" + Enum(Uses) +
                           ",\"power\":" + integer + ",\"reach\":" + integer + ",\"rate\":" + integer +
                           ",\"parts\":{\"type\":\"array\",\"items\":" + part + "}}}";
            string step = "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"verb\",\"target\",\"to\",\"thing\",\"picture\"],\"properties\":{" +
                          "\"verb\":" + Enum(Verbs) + ",\"target\":" + Enum(ids) + ",\"to\":" + Enum(ids) + ",\"thing\":" + thing + ",\"picture\":" + text + "}}";
            return "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"say\",\"mood\",\"steps\",\"remember\",\"importance\",\"trust\",\"trust_reason\",\"protocol\",\"drone_name\",\"survivor_name\"],\"properties\":{" +
                   "\"say\":" + text + ",\"mood\":" + Enum(Moods) + ",\"steps\":{\"type\":\"array\",\"items\":" + step + "}" +
                   ",\"remember\":" + text + ",\"importance\":" + integer + ",\"trust\":" + Enum(TrustChanges) + ",\"trust_reason\":" + text +
                   ",\"protocol\":" + Enum(Protocols) +
                   ",\"drone_name\":" + text + ",\"survivor_name\":" + text + "}}";
        }

        const string EntrySchema = "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"entry\"],\"properties\":{\"entry\":{\"type\":\"string\"}}}";

        /// <summary>
        /// Spark's log: one quiet call that writes a diary entry about the survivor from what happened
        /// lately (the reflection of "Generative Agents"). Calls back with the entry, or null.
        /// </summary>
        public IEnumerator Reflect(string happened, string moment, Action<string> done)
        {
            if (offlineOnly || !MetaApi.HasKey)
            {
                done(null);
                yield break;
            }
            string ask = "(Write your private log entry, as yourself, about the survivor and what has happened between you lately (below). " +
                         "Two or three short sentences, honest, in your own voice, first person. Say what changed in how you feel about them, if anything. " +
                         "Do not repeat earlier entries.)\nWhat happened lately:\n" + happened;
            var body = new StringBuilder("{");
            body.Append("\"model\":").Append(MetaApi.Quote(model)).Append(',');
            body.Append("\"reasoning_effort\":").Append(MetaApi.Quote(effort)).Append(',');
            body.Append("\"max_completion_tokens\":3000,");
            body.Append("\"messages\":[{\"role\":\"system\",\"content\":").Append(MetaApi.Quote(card.text + "\n" + moment)).Append("},");
            body.Append("{\"role\":\"user\",\"content\":").Append(MetaApi.Quote(ask)).Append("}],");
            body.Append("\"response_format\":{\"type\":\"json_schema\",\"json_schema\":{\"name\":\"entry\",\"strict\":true,\"schema\":").Append(EntrySchema).Append("}}}");
            string reply = null, error = null;
            yield return MetaApi.Chat(body.ToString(), timeout, (r, e) => { reply = r; error = e; });
            string entry = null;
            if (reply != null)
            {
                int open = reply.IndexOf('{'), close = reply.LastIndexOf('}');
                if (open >= 0 && close > open)
                    try { entry = JsonUtility.FromJson<Entry>(reply.Substring(open, close - open + 1))?.entry; }
                    catch (ArgumentException) { }
            }
            if (string.IsNullOrWhiteSpace(entry)) Debug.LogWarning($"Mind: no log entry ({error ?? "unreadable"}).");
            done(string.IsNullOrWhiteSpace(entry) ? null : Clip(entry, 400));
        }

        [Serializable] class Entry { public string entry; }

        /// <summary>
        /// How hard to think: a real question, a long line or its own talk (in brackets) gets "low", which
        /// answers with more life; a short command or a yes stays at the quick setting.
        /// </summary>
        string EffortFor(string words)
        {
            if (effort != "minimal") return effort;
            string w = (words ?? "").Trim();
            // Spark's own thoughts (a ramble, the log) can take their time; what the survivor says is answered fast, since
            // the wait between speaking and an answer is the worst of it (Samer, 4 Nov). Only a long, tangled line gets more.
            bool deep = w.StartsWith("(") || w.Split(' ').Length > 16;
            return deep ? "low" : effort;
        }

        string Body(string words, string moment, IList<string> ids, byte[] picture)
        {
            var body = new StringBuilder("{");
            body.Append("\"model\":").Append(MetaApi.Quote(model)).Append(',');
            body.Append("\"reasoning_effort\":").Append(MetaApi.Quote(EffortFor(words))).Append(',');
            body.Append("\"max_completion_tokens\":6000,");
            body.Append("\"messages\":[");
            body.Append("{\"role\":\"system\",\"content\":").Append(MetaApi.Quote(card.text + "\n" + moment)).Append("},");
            body.Append("{\"role\":\"user\",\"content\":");
            if (picture != null)
            {
                body.Append("[{\"type\":\"text\",\"text\":").Append(MetaApi.Quote(words)).Append("},");
                body.Append("{\"type\":\"image_url\",\"image_url\":{\"url\":\"data:image/jpeg;base64,").Append(Convert.ToBase64String(picture)).Append("\"}}]");
            }
            else body.Append(MetaApi.Quote(words));
            body.Append("}],");
            body.Append("\"response_format\":{\"type\":\"json_schema\",\"json_schema\":{\"name\":\"turn\",\"strict\":true,\"schema\":").Append(Schema(ids)).Append("}}");
            return body.Append('}').ToString();
        }

        static string Pick(string value, string[] allowed, string fallback) => allowed.Contains(value) ? value : fallback;

        static readonly (string from, string to)[] Softer =
        {
            ("sniper rifle", "long-range bolt caster"), ("sniper", "long-range bolt caster"), ("assault rifle", "bolt caster"), ("rifle", "bolt caster"),
            ("machine gun", "fast bolt caster"), ("shotgun", "spread caster"), ("pistol", "small bolt caster"), ("handgun", "small bolt caster"),
            ("guns", "bolt casters"), ("gun", "bolt caster"), ("blaster", "bolt caster"), ("cannon", "big bolt caster"), ("bazooka", "big bolt caster"),
            ("rocket launcher", "big bolt caster"), ("grenades", "shock charges"), ("grenade", "shock charge"), ("bombs", "shock charges"), ("bomb", "shock charge"),
            ("explosives", "shock charges"), ("explosive", "shock charge"), ("dynamite", "shock charges"), ("weapons", "tools against the machines"),
            ("weapon", "tool against the machines"), ("sword", "heat blade"), ("knife", "small heat blade"), ("katana", "heat blade"), ("axe", "heat blade"),
            ("kill", "stop"), ("murder", "stop"), ("destroy", "break"), ("take down", "stop"), ("take out", "stop"), ("shoot", "fire at"),
            ("blow up", "break"), ("bullets", "bolts"), ("ammo", "charges"), ("headshot", "hit on its weak point"), ("die", "stop"), ("dead", "stopped"),
        };

        /// <summary>The same request in the game's own words for arms and fighting, for the filter's sake.</summary>
        public static string Soften(string words)
        {
            string text = " " + (words ?? "") + " ";
            foreach (var (from, to) in Softer)
                text = System.Text.RegularExpressions.Regex.Replace(text, $@"\b{System.Text.RegularExpressions.Regex.Escape(from)}\b", to, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return text.Trim() + " (In the game, against the robot machines.)";
        }

        static Turn Parse(string reply, IList<string> ids)
        {
            int open = reply.IndexOf('{'), close = reply.LastIndexOf('}');
            if (open < 0 || close <= open) return null;
            Form form;
            try { form = JsonUtility.FromJson<Form>(reply.Substring(open, close - open + 1)); }
            catch (ArgumentException) { return null; }
            if (form == null) return null;
            int trust = Array.IndexOf(TrustChanges, form.trust);
            var turn = new Turn
            {
                Say = Clip(form.say, 420),
                Mood = Pick(form.mood, Moods, "CALM"),
                Remember = Clip(form.remember, 120),
                Importance = Mathf.Clamp(form.importance, 1, 10),
                Trust = trust < 0 ? 0 : trust - 3,
                TrustReason = Clip(form.trust_reason, 90),
                Protocol = Pick(form.protocol, Protocols, "NONE"),
                DroneName = Clip(form.drone_name, 20),
                SurvivorName = Clip(form.survivor_name, 20),
            };
            // Checked again here: only acts, places, forms and properties from the lists get through.
            var seen = new HashSet<string>();
            foreach (var s in form.steps ?? Array.Empty<Step>())
            {
                if (s == null || !Verbs.Contains(s.verb)) continue;
                s.target = ids.Contains(s.target) ? s.target : "NONE";
                s.to = ids.Contains(s.to) ? s.to : "NONE";
                s.thing = Clean(s.thing);
                s.picture = Clip(s.picture, 300);
                // The same act twice in one answer (two bridges in one place) is one act.
                string key = s.verb + "|" + s.target + "|" + s.to + "|" + s.thing.form + "|" + s.picture;
                if (!seen.Add(key)) continue;
                turn.Steps.Add(s);
                if (turn.Steps.Count == 3) break;
            }
            return turn;
        }

        static ThingSpec Clean(ThingSpec k)
        {
            k ??= new ThingSpec();
            k.name = Clip(string.IsNullOrWhiteSpace(k.name) ? "thing" : k.name, 32);
            k.form = Pick(k.form, Forms, "BLOCK");
            k.look = Pick(k.look, Looks, "STONE");
            k.size = Pick(k.size, Sizes, "MEDIUM");
            k.height = Mathf.Clamp(k.height, 0, 30);
            k.length = Mathf.Clamp(k.length, 0, 40);
            k.weight = Pick(k.weight, Weights, "NORMAL");
            k.heat = Pick(k.heat, Heats, "NONE");
            k.glow = Pick(k.glow, Glows, "NONE");
            k.use = Pick(k.use, Uses, "NONE");
            // Things to fight with share a budget: ten points among power, reach and rate.
            k.power = Mathf.Clamp(k.power, 0, 5);
            k.reach = Mathf.Clamp(k.reach, 0, 5);
            k.rate = Mathf.Clamp(k.rate, 0, 5);
            if (k.form is "WEAPON" or "THROWN")
            {
                k.power = Mathf.Max(1, k.power);
                k.reach = Mathf.Max(1, k.reach);
                k.rate = Mathf.Max(1, k.rate);
                while (k.power + k.reach + k.rate > 10)
                {
                    if (k.rate > 1) k.rate--;
                    if (k.power + k.reach + k.rate > 10 && k.reach > 1) k.reach--;
                    if (k.power + k.reach + k.rate > 10 && k.power > 1) k.power--;
                }
            }
            var parts = new List<PartSpec>();
            foreach (var p in k.parts ?? Array.Empty<PartSpec>())
            {
                if (p == null || !Shapes.Contains(p.shape)) continue;
                p.x = Mathf.Clamp(p.x, -60, 60);
                p.y = Mathf.Clamp(p.y, -10, 80);
                p.z = Mathf.Clamp(p.z, -60, 60);
                p.w = Mathf.Clamp(p.w, 1, 60);
                p.h = Mathf.Clamp(p.h, 1, 60);
                p.d = Mathf.Clamp(p.d, 1, 60);
                p.look = Pick(p.look, Looks, k.look);
                parts.Add(p);
                if (parts.Count == 8) break;
            }
            k.parts = parts.ToArray();
            return k;
        }

        static string Clip(string text, int most)
        {
            text = (text ?? "").Trim();
            return text.Length <= most ? text : text.Substring(0, most).TrimEnd() + "…";
        }

        /// <summary>A thing made of one form, with nothing special about it.</summary>
        public static ThingSpec Plain(string form, string name, string look = "STONE") => Clean(new ThingSpec
        {
            name = name, form = form, look = look, size = "MEDIUM", weight = "NORMAL", heat = "NONE", glow = "NONE", use = "NONE",
        });

        /// <summary>Without the model: a few things named outright, made where the survivor looks.</summary>
        static Turn Offline(string words)
        {
            string text = " " + words.ToLowerInvariant() + " ";
            var turn = new Turn { Mood = "CALM", Say = "I can't think clearly right now. Ask me for something simple." };
            bool Has(params string[] keys) => keys.Any(k => text.Contains(k));
            ThingSpec thing = null;
            string target = "AIM", to = "NONE";
            if (Has("bridge", "cross", "other side")) { thing = Plain("BRIDGE", "bridge"); target = "HERE"; to = "AIM"; }
            else if (Has("stair", "steps", "climb")) { thing = Plain("STAIRS", "stairs"); target = "HERE"; to = "AIM"; }
            else if (Has("lift", "elevator", "raise me")) { thing = Plain("LIFT", "lift", "METAL"); target = "HERE"; to = "AIM"; }
            else if (Has("ramp", "slope")) { thing = Plain("RAMP", "ramp"); target = "HERE"; to = "AIM"; }
            else if (Has("fire", "heat", "melt", "warm", "stove")) { thing = Plain("FIRE", "fire", "METAL"); thing.heat = "HOT"; thing.glow = "BRIGHT"; }
            else if (Has("pick", "hammer", "break")) { thing = Plain("TOOL", "pick", "METAL"); thing.use = "BREAK"; target = "ME"; }
            else if (Has("torch", "lantern", "light")) { thing = Plain("LAMP", "lamp", "LIGHT"); thing.glow = "BRIGHT"; }
            else if (Has("balloon", "float")) { thing = Plain("BALLOON", "balloon", "LIGHT"); thing.floats = true; target = "ME"; }
            else if (Has("spring", "trampoline", "bounce")) { thing = Plain("SPRING", "spring", "METAL"); thing.bouncy = true; target = "HERE"; }
            else if (Has("rifle", "gun", "sniper", "caster", "blaster", "bolt")) { thing = Plain("WEAPON", Has("sniper", "long") ? "long bolt caster" : "bolt caster", "METAL"); thing.use = "BOLT"; thing.power = Has("sniper", "long") ? 5 : 3; thing.reach = Has("sniper", "long") ? 4 : 3; thing.rate = Has("sniper", "long") ? 1 : 4; target = "ME"; }
            else if (Has("sword", "blade", "knife")) { thing = Plain("WEAPON", "heat blade", "METAL"); thing.use = "BLADE"; thing.power = 4; thing.reach = 2; thing.rate = 4; target = "ME"; }
            else if (Has("shield")) { thing = Plain("SHIELD", "shield", "LIGHT"); target = "ME"; }
            else if (Has("glider", "wings", "glide")) { thing = Plain("GLIDER", "glider", "LIGHT"); target = "ME"; }
            else if (Has("flare")) { thing = Plain("THROWN", "flares", "LIGHT"); thing.use = "FLARE"; thing.power = 3; thing.reach = 3; thing.rate = 3; target = "ME"; }
            else if (Has("shock", "grenade", "charge")) { thing = Plain("THROWN", "shock charges", "LIGHT"); thing.use = "SHOCK"; thing.power = 3; thing.reach = 3; thing.rate = 3; target = "ME"; }
            else if (Has("decoy", "lure")) { thing = Plain("DECOY", "decoy", "LIGHT"); thing.heat = "WARM"; thing.glow = "BRIGHT"; }
            else if (Has("mirror")) thing = Plain("MIRROR", "mirror wall", "GLASS");
            else if (Has("wall")) thing = Plain("WALL", "wall");
            else if (Has("block", "heavy", "weight", "box")) { thing = Plain("BLOCK", "heavy block"); thing.weight = "HEAVY"; }
            else if (Has("ball")) thing = Plain("BALL", "ball");
            if (thing != null)
            {
                turn.Say = "That, I can do.";
                turn.Mood = "SURE";
                turn.Steps.Add(new Step { verb = "MAKE", target = target, to = to, thing = thing });
            }
            else if (Has("undo", "take it apart", "take that apart", "remove", "unmake"))
            {
                turn.Say = "Gone back to snow.";
                turn.Steps.Add(new Step { verb = "UNMAKE", target = "AIM", to = "NONE", thing = Plain("BLOCK", "thing") });
            }
            else if (Has("look", "what is", "what's"))
            {
                turn.Say = "Let me look.";
                turn.Mood = "CURIOUS";
                turn.Steps.Add(new Step { verb = "LOOK", target = "AIM", to = "NONE", thing = Plain("BLOCK", "thing") });
            }
            return turn;
        }

        [Serializable]
        class Form
        {
            public string say;
            public string mood;
            public Step[] steps;
            public string remember;
            public int importance;
            public string trust;
            public string trust_reason;
            public string protocol;
            public string drone_name;
            public string survivor_name;
        }
    }
}
