using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// What Spark and the survivor have between them, kept by the game, never by the model: trust (a
    /// number and four tiers that decide what Spark will make), flags for what happened, a stream of
    /// memories each weighted by how much it mattered, and Spark's own log. Every line, the parts worth
    /// recalling are written into the moment: the trust and what it allows, what happened, the memories
    /// that are recent, weighty or about the same words, and the latest log entries.
    /// </summary>
    public class Bond
    {
        public enum Tier { Stranger, Companion, Partner, Trusted }

        /// <summary>A memory: when, what, how much it mattered (1 to 10).</summary>
        public class Memory
        {
            public float At;
            public string Text;
            public int Weight;
            public HashSet<string> Words;
        }

        public static readonly string[] TierNames = { "a stranger", "a companion", "a partner", "trusted" };
        static readonly int[] TierFrom = { 0, 25, 50, 75 };
        /// <summary>Words alone take trust this far, no further: the last tier needs deeds (saving it, fixing it).</summary>
        const int WordsCeiling = 70;

        public int Trust { get; private set; } = 8;
        public Tier Level { get; private set; } = Tier.Stranger;
        public readonly List<Memory> Stream = new List<Memory>();
        public readonly List<(float at, string entry)> Log = new List<(float, string)>();
        public readonly Dictionary<string, int> Flags = new Dictionary<string, int>();
        readonly float met;
        int sinceReflection;

        /// <summary>Trust crossed into a new tier: the tier, and whether it rose.</summary>
        public event Action<Tier, bool> TierChanged;

        public Bond(float now) => met = now;

        /// <summary>When they met (game time).</summary>
        public float Met => met;

        /// <summary>Lines and events since the last log entry: when enough have happened, it is time to write one.</summary>
        public bool WantsReflection => sinceReflection >= 6;

        /// <summary>The model's own reading of a line: -3 to 3, bounded, and never past what words can earn.</summary>
        public void Nudge(int change, string reason, float now)
        {
            if (change == 0) return;
            int points = change switch { 3 => 10, 2 => 6, 1 => 3, -1 => -5, -2 => -10, _ => -18 };
            if (points > 0 && Trust >= WordsCeiling) points = 0;
            else if (points > 0) points = Mathf.Min(points, WordsCeiling - Trust);
            Shift(points);
            Remember($"Trust {(change > 0 ? "rose" : "fell")}: {reason}", Mathf.Abs(change) * 2 + 2, now);
        }

        /// <summary>Something that happened, from the game: a flag counted, trust moved, a memory kept.</summary>
        public void Event(string flag, int points, string memory, int weight, float now)
        {
            if (!string.IsNullOrEmpty(flag)) Flags[flag] = Flags.TryGetValue(flag, out int n) ? n + 1 : 1;
            Shift(points);
            if (!string.IsNullOrEmpty(memory)) Remember(memory, weight, now);
        }

        public bool Has(string flag) => Flags.ContainsKey(flag);

        /// <summary>Sets trust outright (tests, debug keys).</summary>
        public void Set(int trust) => Shift(trust - Trust);

        void Shift(int points)
        {
            if (points == 0) return;
            Trust = Mathf.Clamp(Trust + points, 0, 100);
            var was = Level;
            Level = (Tier)Array.FindLastIndex(TierFrom, f => Trust >= f);
            if (Level != was) TierChanged?.Invoke(Level, Level > was);
        }

        public void Remember(string text, int weight, float now)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            Stream.Add(new Memory { At = now, Text = text.Trim(), Weight = Mathf.Clamp(weight, 1, 10), Words = Words(text) });
            sinceReflection++;
            // Keep the stream to a size the prompt can carry: the lightest old memories go first.
            if (Stream.Count > 160)
            {
                var lightest = Stream.Take(Stream.Count - 20).OrderBy(m => m.Weight).ThenBy(m => m.At).First();
                Stream.Remove(lightest);
            }
        }

        public void Write(string entry, float now)
        {
            if (string.IsNullOrWhiteSpace(entry)) return;
            Log.Add((now, entry.Trim()));
            sinceReflection = 0;
        }

        /// <summary>
        /// The few memories most worth recalling now: each scored by how recent it is (fading over about
        /// ten minutes), how much it mattered, and how many words it shares with what was just said.
        /// </summary>
        public List<Memory> Recall(string words, float now, int count = 6)
        {
            var asked = Words(words);
            return Stream
                .Select(m => (m, score: Mathf.Exp(-(now - m.At) / 600f) + m.Weight / 10f + (asked.Count == 0 ? 0f : 1.5f * m.Words.Count(asked.Contains) / Mathf.Max(3f, asked.Count))))
                .OrderByDescending(x => x.score)
                .Take(count)
                .Select(x => x.m)
                .OrderBy(m => m.At)
                .ToList();
        }

        /// <summary>What Spark will make for someone at a tier, and what it won't yet.</summary>
        public static string Allows(Tier tier) => tier switch
        {
            Tier.Stranger => "ways (bridges, stairs, ramps, lifts), plain shapes, lamps, fires, balloons, springs, a lantern in their hands. Not yet: tools in their hands, gliders, shields, decoys, things to throw, weapons",
            Tier.Companion => "all of the stranger's things, and tools in their hands (pick, torch, lantern) and gliders. Not yet: shields, mirror walls, decoys, things to throw, weapons",
            Tier.Partner => "all of the companion's things, and shields, mirror walls, decoys, flares and shock charges. Not yet: weapons",
            _ => "anything you can make, weapons against the machines too (never against people or living things)",
        };

        /// <summary>The tier a thing needs before Spark makes it.</summary>
        public static Tier Needs(ThingSpec k)
        {
            switch (k.form)
            {
                case "WEAPON": return Tier.Trusted;
                case "SHIELD":
                case "MIRROR":
                case "DECOY":
                case "THROWN": return Tier.Partner;
                case "GLIDER": return Tier.Companion;
                case "TOOL": return k.use == "LIGHT" ? Tier.Stranger : k.use is "BLADE" or "BEAM" or "BOLT" or "SPREAD" ? Tier.Trusted : Tier.Companion;
                default: return Tier.Stranger;
            }
        }

        /// <summary>The part of the moment about the two of them.</summary>
        public string Describe(string words, float now)
        {
            var text = new StringBuilder();
            float minutes = (now - met) / 60f;
            text.Append("You met the survivor ").Append(minutes < 1.5f ? "just now" : $"{minutes:F0} minutes ago").Append(". ");
            text.Append($"Your trust in them: {Trust} of 100, so they are {TierNames[(int)Level]} to you. You will make for them: {Allows(Level)}.\n");
            if (Flags.Count > 0)
                text.Append("What has happened between you: ").Append(string.Join("; ", Flags.Select(f => f.Value > 1 ? $"{Said(f.Key)} ({f.Value} times)" : Said(f.Key)))).Append(".\n");
            var recalled = Recall(words, now);
            if (recalled.Count > 0)
            {
                text.Append("Memories that come back to you now:\n");
                foreach (var m in recalled) text.Append("- ").Append(Ago(now - m.At)).Append(": ").Append(m.Text).Append('\n');
            }
            if (Log.Count > 0)
            {
                text.Append("Your own log, latest entries:\n");
                foreach (var (at, entry) in Log.Skip(Math.Max(0, Log.Count - 2))) text.Append("- ").Append(Ago(now - at)).Append(": ").Append(entry).Append('\n');
            }
            return text.ToString();
        }

        /// <summary>Lately, for the log: the memories since the last entry.</summary>
        public string Lately(float now)
        {
            float since = Log.Count > 0 ? Log[Log.Count - 1].at : met;
            var lately = Stream.Where(m => m.At >= since).Select(m => "- " + Ago(now - m.At) + ": " + m.Text);
            return string.Join("\n", lately);
        }

        static string Said(string flag) => flag switch
        {
            "named_you" => "they gave you a name",
            "told_name" => "they told you their name",
            "asked_weapon_early" => "they asked you for a weapon before you trusted them",
            "insulted" => "they insulted you",
            "kind" => "they were kind to you",
            "thanked" => "they thanked you",
            "saved_you" => "they saved you from a machine",
            "fixed_you" => "they repaired you, with hands that knew how",
            "you_saved_them" => "you saved them",
            "fell" => "they collapsed from the cold or the machines' fire",
            "architect" => "a Harvester called them ARCHITECT: they helped build the great minds",
            "seekers_beaten" => "you beat Seekers together",
            _ => flag.Replace('_', ' '),
        };

        static string Ago(float seconds) => seconds < 60f ? "just now" : seconds < 120f ? "a minute ago" : $"{seconds / 60f:F0} minutes ago";

        static readonly HashSet<string> Common = new HashSet<string>
        {
            "the", "a", "an", "and", "or", "to", "of", "in", "on", "it", "is", "you", "i", "me", "my", "your", "that", "this",
            "for", "with", "at", "be", "was", "are", "can", "do", "what", "they", "them", "we", "us", "so", "but", "not", "just",
        };

        static HashSet<string> Words(string text)
        {
            var words = new HashSet<string>();
            foreach (string w in (text ?? "").ToLowerInvariant().Split(new[] { ' ', ',', '.', '!', '?', ':', ';', '"', '\'', '(', ')' }, StringSplitOptions.RemoveEmptyEntries))
                if (w.Length > 2 && !Common.Contains(w)) words.Add(w.TrimEnd('s'));
            return words;
        }
    }
}
