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
        public PartSpec[] parts;
    }

    /// <summary>One thing the drone does: look, make, unmake, move, come back or wait.</summary>
    [Serializable]
    public class Step
    {
        public string verb;
        public string target;
        public string to;
        public ThingSpec thing;
    }

    /// <summary>What the drone makes of one line: what it says, how it feels, what it does.</summary>
    public class Turn
    {
        public string Say = "";
        public string Mood = "CALM";
        public List<Step> Steps = new List<Step>();
        public string Remember = "", DroneName = "", SurvivorName = "";
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

        public static readonly string[] Verbs = { "LOOK", "MAKE", "UNMAKE", "MOVE", "COME", "WAIT" };
        public static readonly string[] Forms = { "BRIDGE", "STAIRS", "RAMP", "LIFT", "PLATFORM", "WALL", "PILLAR", "BLOCK", "BALL", "PLANK", "LAMP", "FIRE", "BALLOON", "SPRING", "TOOL", "CUSTOM" };
        public static readonly string[] Looks = { "STONE", "METAL", "ICE", "GLASS", "WOOD", "LIGHT" };
        public static readonly string[] Sizes = { "TINY", "SMALL", "MEDIUM", "LARGE", "HUGE" };
        public static readonly string[] Weights = { "FEATHER", "LIGHT", "NORMAL", "HEAVY" };
        public static readonly string[] Heats = { "COLD", "NONE", "WARM", "HOT" };
        public static readonly string[] Glows = { "NONE", "DIM", "BRIGHT" };
        public static readonly string[] Uses = { "NONE", "BREAK", "HEAT", "LIGHT" };
        public static readonly string[] Shapes = { "BOX", "CYLINDER", "SPHERE", "CONE" };
        public static readonly string[] Moods = { "CALM", "HAPPY", "CURIOUS", "WORRIED", "SAD", "SURE", "SORRY" };

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
                    if (k.parts != null && k.parts.Length > 0)
                        text.Append(" [").Append(string.Join(" ", k.parts.Select(p => $"{p.shape.ToLowerInvariant()}@{p.x},{p.y},{p.z}:{p.w}x{p.h}x{p.d}"))).Append(']');
                    if (k.height > 0) text.Append(" h").Append(k.height);
                    if (k.length > 0) text.Append(" l").Append(k.length);
                }
            }
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
            string thing = "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"name\",\"form\",\"look\",\"size\",\"height\",\"length\",\"weight\",\"heat\",\"glow\",\"floats\",\"bouncy\",\"use\",\"parts\"],\"properties\":{" +
                           "\"name\":" + text + ",\"form\":" + Enum(Forms) + ",\"look\":" + Enum(Looks) + ",\"size\":" + Enum(Sizes) +
                           ",\"height\":" + integer + ",\"length\":" + integer + ",\"weight\":" + Enum(Weights) + ",\"heat\":" + Enum(Heats) +
                           ",\"glow\":" + Enum(Glows) + ",\"floats\":" + flag + ",\"bouncy\":" + flag + ",\"use\":" + Enum(Uses) +
                           ",\"parts\":{\"type\":\"array\",\"items\":" + part + "}}}";
            string step = "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"verb\",\"target\",\"to\",\"thing\"],\"properties\":{" +
                          "\"verb\":" + Enum(Verbs) + ",\"target\":" + Enum(ids) + ",\"to\":" + Enum(ids) + ",\"thing\":" + thing + "}}";
            return "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"say\",\"mood\",\"steps\",\"remember\",\"drone_name\",\"survivor_name\"],\"properties\":{" +
                   "\"say\":" + text + ",\"mood\":" + Enum(Moods) + ",\"steps\":{\"type\":\"array\",\"items\":" + step + "}" +
                   ",\"remember\":" + text + ",\"drone_name\":" + text + ",\"survivor_name\":" + text + "}}";
        }

        string Body(string words, string moment, IList<string> ids, byte[] picture)
        {
            var body = new StringBuilder("{");
            body.Append("\"model\":").Append(MetaApi.Quote(model)).Append(',');
            body.Append("\"reasoning_effort\":").Append(MetaApi.Quote(effort)).Append(',');
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

        static Turn Parse(string reply, IList<string> ids)
        {
            int open = reply.IndexOf('{'), close = reply.LastIndexOf('}');
            if (open < 0 || close <= open) return null;
            Form form;
            try { form = JsonUtility.FromJson<Form>(reply.Substring(open, close - open + 1)); }
            catch (ArgumentException) { return null; }
            if (form == null) return null;
            var turn = new Turn
            {
                Say = Clip(form.say, 220),
                Mood = Pick(form.mood, Moods, "CALM"),
                Remember = Clip(form.remember, 90),
                DroneName = Clip(form.drone_name, 20),
                SurvivorName = Clip(form.survivor_name, 20),
            };
            // Checked again here: only acts, places, forms and properties from the lists get through.
            foreach (var s in form.steps ?? Array.Empty<Step>())
            {
                if (s == null || !Verbs.Contains(s.verb)) continue;
                s.target = ids.Contains(s.target) ? s.target : "NONE";
                s.to = ids.Contains(s.to) ? s.to : "NONE";
                s.thing = Clean(s.thing);
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
            public string drone_name;
            public string survivor_name;
        }
    }
}
