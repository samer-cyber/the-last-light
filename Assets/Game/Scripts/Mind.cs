using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Game
{
    /// <summary>One thing the player asked the mind to make, as the model read it.</summary>
    public struct Wish
    {
        /// <summary>One of <see cref="Maker.Kinds"/>.</summary>
        public string Kind;
        /// <summary>STONE, ICE, METAL, LIGHT or ANY.</summary>
        public string Matter;
        /// <summary>Where it starts or stands, and where it reaches: a landmark's id, HERE, AIM, MIND or NONE.</summary>
        public string From, To;
        /// <summary>SMALL, MEDIUM or LARGE.</summary>
        public string Size;
        /// <summary>Metres asked for in so many words, or 0.</summary>
        public int Height, Length;
    }

    /// <summary>What the mind made of the player's words.</summary>
    public struct Thought
    {
        /// <summary>"build", "undo", "idea" (how could I...?), "question", "other".</summary>
        public string Kind;
        public Sign Answer;
        public List<Wish> Wishes;
        /// <summary>NONE, NOTHING_FROM_NOTHING (a thing that cannot be shaped from matter), UNKNOWN_THING.</summary>
        public string Refusal;
        public string Source;
        public float Seconds;
    }

    /// <summary>
    /// Reads the player's words with Muse Spark into a <see cref="Thought"/>: what kind of line it was,
    /// and for things to make, which of the kinds a person designed, of what, from where to where, how
    /// big. The form is strict and every value but none comes from a list, so the model can never
    /// answer in words of its own. The card says who the mind is and what it can make; the moment, from
    /// the director, says what is here now. Without a key or a connection a few word rules stand in.
    /// </summary>
    public class Mind : MonoBehaviour
    {
        [SerializeField] string model = "muse-spark-1.3";
        [SerializeField, Tooltip("minimal, low, medium or high")] string effort = "minimal";
        [SerializeField] float timeout = 15f;
        [SerializeField] TextAsset card;
        [SerializeField] bool offlineOnly;

        public string Model => !offlineOnly && MetaApi.HasKey ? model : "offline";

        static readonly string[] Matter = { "STONE", "ICE", "METAL", "LIGHT", "ANY" };
        static readonly string[] Sizes = { "SMALL", "MEDIUM", "LARGE" };
        static readonly string[] Refusals = { "NONE", "NOTHING_FROM_NOTHING", "UNKNOWN_THING" };

        /// <summary>Reads the words; calls back with the thought (never fails: word rules stand in).</summary>
        public IEnumerator Read(string words, string moment, IList<string> anchors, Action<Thought> done)
        {
            float began = Time.realtimeSinceStartup;
            Thought thought = default;
            bool read = false;
            if (!offlineOnly && MetaApi.HasKey)
            {
                string reply = null, error = null;
                yield return MetaApi.Chat(Body(words, moment, anchors), timeout, (r, e) => { reply = r; error = e; });
                read = reply != null && TryParse(reply, anchors, out thought);
                if (read) thought.Source = model;
                else Debug.LogWarning($"Mind: {model} gave no reading ({error ?? "unreadable: " + reply}); the word rules answer.");
            }
            if (!read)
            {
                thought = Offline(words);
                thought.Source = "offline";
            }
            thought.Seconds = Time.realtimeSinceStartup - began;
            Debug.Log($"Mind ({thought.Source}, {thought.Seconds:F1} s): \"{words}\" -> {Describe(thought)}");
            done(thought);
        }

        public static string Describe(Thought t)
        {
            var text = new StringBuilder(t.Kind);
            if (t.Kind == "question") text.Append(' ').Append(t.Answer);
            foreach (var w in t.Wishes ?? new List<Wish>())
                text.Append($"; {w.Kind} {w.Matter} {w.From}->{w.To} {w.Size}{(w.Height > 0 ? " h" + w.Height : "")}{(w.Length > 0 ? " l" + w.Length : "")}");
            if (!string.IsNullOrEmpty(t.Refusal) && t.Refusal != "NONE") text.Append(" (").Append(t.Refusal).Append(')');
            return text.ToString();
        }

        string Body(string words, string moment, IList<string> anchors)
        {
            string List(IEnumerable<string> items) => string.Join(",", items.Select(MetaApi.Quote));
            string schema =
                "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"kind\",\"answer\",\"builds\",\"refusal\"],\"properties\":{" +
                "\"kind\":{\"type\":\"string\",\"enum\":[\"build\",\"undo\",\"idea\",\"question\",\"other\"]}," +
                "\"answer\":{\"type\":\"string\",\"enum\":[\"yes\",\"no\",\"unknowable\"]}," +
                "\"builds\":{\"type\":\"array\",\"items\":{\"type\":\"object\",\"additionalProperties\":false," +
                "\"required\":[\"template\",\"matter\",\"from\",\"to\",\"size\",\"height\",\"length\"],\"properties\":{" +
                "\"template\":{\"type\":\"string\",\"enum\":[" + List(Maker.Kinds) + "]}," +
                "\"matter\":{\"type\":\"string\",\"enum\":[" + List(Matter) + "]}," +
                "\"from\":{\"type\":\"string\",\"enum\":[" + List(anchors) + "]}," +
                "\"to\":{\"type\":\"string\",\"enum\":[" + List(anchors) + "]}," +
                "\"size\":{\"type\":\"string\",\"enum\":[" + List(Sizes) + "]}," +
                "\"height\":{\"type\":\"integer\"}," +
                "\"length\":{\"type\":\"integer\"}}}}," +
                "\"refusal\":{\"type\":\"string\",\"enum\":[" + List(Refusals) + "]}}}";
            var body = new StringBuilder("{");
            body.Append("\"model\":").Append(MetaApi.Quote(model)).Append(',');
            body.Append("\"reasoning_effort\":").Append(MetaApi.Quote(effort)).Append(',');
            body.Append("\"max_completion_tokens\":4000,");
            body.Append("\"messages\":[");
            body.Append("{\"role\":\"system\",\"content\":").Append(MetaApi.Quote(card.text + "\n" + moment)).Append("},");
            body.Append("{\"role\":\"user\",\"content\":").Append(MetaApi.Quote(words)).Append("}],");
            body.Append("\"response_format\":{\"type\":\"json_schema\",\"json_schema\":{\"name\":\"thought\",\"strict\":true,\"schema\":").Append(schema).Append("}}");
            return body.Append('}').ToString();
        }

        static bool TryParse(string reply, IList<string> anchors, out Thought thought)
        {
            thought = default;
            int open = reply.IndexOf('{'), close = reply.LastIndexOf('}');
            if (open < 0 || close <= open) return false;
            Form form;
            try { form = JsonUtility.FromJson<Form>(reply.Substring(open, close - open + 1)); }
            catch (ArgumentException) { return false; }
            if (form == null || string.IsNullOrEmpty(form.kind)) return false;
            thought.Kind = form.kind;
            thought.Answer = form.answer == "yes" ? Sign.Yes : form.answer == "no" ? Sign.No : Sign.Unknowable;
            thought.Refusal = string.IsNullOrEmpty(form.refusal) ? "NONE" : form.refusal;
            thought.Wishes = new List<Wish>();
            // Checked again here: only kinds, matters and places from the lists get through.
            foreach (var b in form.builds ?? Array.Empty<FormBuild>())
            {
                if (!Maker.Kinds.Contains(b.template)) continue;
                thought.Wishes.Add(new Wish
                {
                    Kind = b.template,
                    Matter = Matter.Contains(b.matter) ? b.matter : "ANY",
                    From = anchors.Contains(b.from) ? b.from : "NONE",
                    To = anchors.Contains(b.to) ? b.to : "NONE",
                    Size = Sizes.Contains(b.size) ? b.size : "MEDIUM",
                    Height = Mathf.Clamp(b.height, 0, 30),
                    Length = Mathf.Clamp(b.length, 0, 40),
                });
                if (thought.Wishes.Count == 3) break;
            }
            return true;
        }

        /// <summary>Without the model: a kind of thing named outright, built from here to where the player looks.</summary>
        static Thought Offline(string words)
        {
            string text = " " + words.ToLowerInvariant() + " ";
            var thought = new Thought { Kind = "other", Answer = Sign.Unknowable, Wishes = new List<Wish>(), Refusal = "NONE" };
            string Has(params string[] keys) => keys.FirstOrDefault(k => text.Contains(k));
            string kind =
                Has("bridge", "cross") != null ? "BRIDGE" :
                Has("stair", "steps", "climb") != null ? "STAIRS" :
                Has("ramp", "slope") != null ? "RAMP" :
                Has("wall", "shield", "barrier") != null ? "WALL" :
                Has("pillar", "column", "tower") != null ? "PILLAR" :
                Has("platform", "floor") != null ? "PLATFORM" :
                Has("block", "cube", "box", "weight", "heavy") != null ? "BLOCK" :
                Has("ball", "sphere", "boulder") != null ? "BALL" :
                Has("lamp", "light up", "torch") != null ? "LAMP" : null;
            string matter = Has("ice", "frozen") != null ? "ICE" : Has("metal", "iron", "steel") != null ? "METAL" :
                Has(" light ") != null && kind != "LAMP" ? "LIGHT" : Has("stone", "rock") != null ? "STONE" : "ANY";
            if (Has("undo", "take it apart", "take that apart", "remove", "destroy", "unmake") != null) thought.Kind = "undo";
            else if (kind != null && Has("how ", "could i", "what should", "help") != null)
            {
                thought.Kind = "idea";
                thought.Wishes.Add(new Wish { Kind = kind, Matter = matter, From = "HERE", To = "AIM", Size = "MEDIUM" });
            }
            else if (kind != null)
            {
                thought.Kind = "build";
                thought.Wishes.Add(new Wish { Kind = kind, Matter = matter, From = "HERE", To = "AIM", Size = Has("big", "large", "huge", "tall") != null ? "LARGE" : Has("small", "little", "tiny") != null ? "SMALL" : "MEDIUM" });
            }
            else if (words.TrimEnd().EndsWith("?")) thought.Kind = "question";
            return thought;
        }

        [Serializable]
        class Form
        {
            public string kind;
            public string answer;
            public FormBuild[] builds;
            public string refusal;
        }

        [Serializable]
        class FormBuild
        {
            public string template;
            public string matter;
            public string from;
            public string to;
            public string size;
            public int height;
            public int length;
        }
    }
}
