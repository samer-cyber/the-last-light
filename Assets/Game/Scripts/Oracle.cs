using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Game
{
    /// <summary>The sky's three answers: a 1, a 0, or a shooting star.</summary>
    public enum Sign { Yes, No, Unknowable }

    /// <summary>What the entity made of the player's words.</summary>
    public struct Reading
    {
        /// <summary>"question", "request", "next" (what should I do?), "open" (a question with no yes or no) or "other".</summary>
        public string Kind;
        /// <summary>Its answer to a question.</summary>
        public Sign Answer;
        /// <summary>For a request: one of the phase's action ids; otherwise "NONE".</summary>
        public string Action;
        /// <summary>For an open question: the thing in the sky that holds its answer, or "NONE".</summary>
        public string PointAt;
        /// <summary>Which fact or rule it went by, for testing.</summary>
        public string Fact;
        /// <summary>The model's name, or "offline".</summary>
        public string Source;
        public float Seconds;

        public bool IsRequest => Kind == "request";
        public bool IsQuestion => Kind == "question";
    }

    /// <summary>
    /// Reads the player's words into a <see cref="Reading"/>. Muse Spark does it through Meta's API,
    /// held to a fixed form so it can never answer in words, with the phase's card as its
    /// instructions (who the entity is, what is true, what it can do) and the moment (what has
    /// happened so far) from the director. Without a key or a connection, a few rules that know only
    /// the phase's actions stand in. The same words in the same moment are read once.
    /// </summary>
    public class Oracle : MonoBehaviour
    {
        public enum Brain { Meta, Offline }

        /// <summary>An action the phase allows, and words that point at it (for the offline rules).</summary>
        [Serializable]
        public struct Act
        {
            public string id;
            public string[] words;
        }

        [SerializeField] Brain brain = Brain.Meta;
        [SerializeField] string model = "muse-spark-1.3";
        [SerializeField, Tooltip("minimal, low, medium or high")] string effort = "low";
        [SerializeField] float timeout = 15f;
        [SerializeField, Tooltip("The phase's card: the entity, the truth, the actions, the rules.")] TextAsset card;
        [SerializeField, Tooltip("Every action id the card names, with words for the offline rules.")] Act[] actions;
        [SerializeField, Tooltip("Things in the sky an open question can be pointed at.")] string[] targets;
        [SerializeField, Tooltip("The action an offline request with no matching words becomes.")] string otherAction = "OTHER";

        static readonly string[] QuestionStarts =
            { "is ", "are ", "am ", "do ", "does ", "did ", "can ", "could ", "will ", "would ", "should ", "was ", "were ", "has ", "have ", "had ", "shall ", "may ", "must " };
        static readonly string[] RequestWords =
            { "make", "send", "bring", "give", "let", "crash", "collide", "smash", "cool", "warm", "heat", "start", "create", "push", "throw",
              "fill", "protect", "shield", "add", "put", "turn", "spin", "speed", "wait", "please", "drop", "pull", "move", "thicken" };

        readonly Dictionary<string, Reading> remembered = new Dictionary<string, Reading>();

        public string Model => brain == Brain.Meta && MetaApi.HasKey ? model : "offline";

        /// <summary>Reads the words; calls back with the reading (never fails: offline rules stand in).</summary>
        public IEnumerator Read(string words, string moment, Action<Reading> done)
        {
            string key = Normalise(words) + "|" + moment;
            if (remembered.TryGetValue(key, out var known))
            {
                done(known);
                yield break;
            }
            float began = Time.realtimeSinceStartup;
            Reading reading = default;
            bool read = false;
            if (brain == Brain.Meta && MetaApi.HasKey)
            {
                string reply = null, error = null;
                yield return MetaApi.Chat(Body(words, moment), timeout, (r, e) => { reply = r; error = e; });
                read = reply != null && TryParse(reply, out reading);
                if (read) reading.Source = model;
                else Debug.LogWarning($"Oracle: {model} gave no reading ({error ?? "unreadable: " + reply}); the offline rules answer.");
            }
            if (!read)
            {
                reading = Offline(words);
                reading.Source = "offline";
            }
            reading.Seconds = Time.realtimeSinceStartup - began;
            remembered[key] = reading;
            Debug.Log($"Oracle ({reading.Source}, {reading.Seconds:F1} s): \"{words}\" -> {reading.Kind}, {reading.Answer}, {reading.Action} ({reading.Fact})");
            done(reading);
        }

        string Body(string words, string moment)
        {
            string List(IEnumerable<string> items) => string.Join(",", items.Append("NONE").Distinct().Select(MetaApi.Quote));
            string schema =
                "{\"type\":\"object\",\"properties\":{" +
                "\"kind\":{\"type\":\"string\",\"enum\":[\"question\",\"request\",\"next\",\"open\",\"other\"]}," +
                "\"answer\":{\"type\":\"string\",\"enum\":[\"yes\",\"no\",\"unknowable\"]}," +
                "\"action\":{\"type\":\"string\",\"enum\":[" + List(actions.Select(a => a.id)) + "]}," +
                "\"point_at\":{\"type\":\"string\",\"enum\":[" + List(targets ?? Array.Empty<string>()) + "]}," +
                "\"fact\":{\"type\":\"string\"}}," +
                "\"required\":[\"kind\",\"answer\",\"action\",\"point_at\",\"fact\"],\"additionalProperties\":false}";
            string instructions = card.text + "\n" + moment;
            var body = new StringBuilder("{");
            body.Append("\"model\":").Append(MetaApi.Quote(model)).Append(',');
            body.Append("\"reasoning_effort\":").Append(MetaApi.Quote(effort)).Append(',');
            body.Append("\"max_completion_tokens\":4000,");
            body.Append("\"messages\":[");
            body.Append("{\"role\":\"system\",\"content\":").Append(MetaApi.Quote(instructions)).Append("},");
            body.Append("{\"role\":\"user\",\"content\":").Append(MetaApi.Quote(words)).Append("}],");
            body.Append("\"response_format\":{\"type\":\"json_schema\",\"json_schema\":{\"name\":\"reading\",\"strict\":true,\"schema\":").Append(schema).Append("}}");
            return body.Append('}').ToString();
        }

        bool TryParse(string reply, out Reading reading)
        {
            reading = default;
            int open = reply.IndexOf('{'), close = reply.LastIndexOf('}');
            if (open < 0 || close <= open) return false;
            Form form;
            try { form = JsonUtility.FromJson<Form>(reply.Substring(open, close - open + 1)); }
            catch (ArgumentException) { return false; }
            if (form == null || string.IsNullOrEmpty(form.kind)) return false;
            reading.Kind = form.kind;
            reading.Answer = form.answer == "yes" ? Sign.Yes : form.answer == "no" ? Sign.No : Sign.Unknowable;
            reading.Action = string.IsNullOrEmpty(form.action) ? "NONE" : form.action;
            reading.PointAt = string.IsNullOrEmpty(form.point_at) ? "NONE" : form.point_at;
            reading.Fact = form.fact;
            return true;
        }

        /// <summary>
        /// Without the model: a request is anything with a doing word in it, matched to an action by
        /// the action's words; a question is anything shaped like one, and gets a shooting star.
        /// </summary>
        Reading Offline(string words)
        {
            string text = " " + Normalise(words) + " ";
            var reading = new Reading { Kind = "other", Answer = Sign.Unknowable, Action = "NONE", PointAt = "NONE", Fact = "offline_rules" };
            bool asks = words.TrimEnd().EndsWith("?") || QuestionStarts.Any(start => text.TrimStart().StartsWith(start));
            bool lost = text.Contains(" what should i ") || text.Contains(" what do you need") || text.Contains(" help ") || text.Contains(" what now ");
            bool wants = RequestWords.Any(word => text.Contains(" " + word));
            string act = actions.FirstOrDefault(a => a.words != null && a.words.Any(word => text.Contains(word))).id;
            if (lost) reading.Kind = "next";
            else if (wants)
            {
                reading.Kind = "request";
                reading.Action = act ?? otherAction;
            }
            else if (asks) reading.Kind = "question";
            return reading;
        }

        static string Normalise(string words)
        {
            var text = new StringBuilder();
            foreach (char c in (words ?? "").ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c)) text.Append(c);
                else if (text.Length > 0 && text[text.Length - 1] != ' ') text.Append(' ');
            }
            return text.ToString().Trim();
        }

        [Serializable]
        class Form
        {
            public string kind;
            public string answer;
            public string action;
            public string point_at;
            public string fact;
        }
    }
}
