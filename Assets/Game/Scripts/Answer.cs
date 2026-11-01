using System;
using System.Text;

namespace Game
{
    /// <summary>Judges what the player wrote against the message, ignoring case, spaces and punctuation.</summary>
    public static class Answer
    {
        public enum Verdict { Wrong, Close, Right }

        public static Verdict Judge(string guess, string message)
        {
            string written = Letters(guess), meant = Letters(message);
            if (written.Length == 0) return Verdict.Wrong;
            int distance = Distance(written, meant);
            if (distance == 0) return Verdict.Right;
            return distance <= Math.Max(1, meant.Length / 3) ? Verdict.Close : Verdict.Wrong;
        }

        static string Letters(string text)
        {
            var kept = new StringBuilder(text.Length);
            foreach (char c in text.ToUpperInvariant())
                if (c >= 'A' && c <= 'Z' || c >= '0' && c <= '9') kept.Append(c);
            return kept.ToString();
        }

        /// <summary>Edits (insertions, deletions, substitutions) between two strings.</summary>
        static int Distance(string a, string b)
        {
            var previous = new int[b.Length + 1];
            var current = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) previous[j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                current[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    int substitution = previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1);
                    current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), substitution);
                }
                (previous, current) = (current, previous);
            }
            return previous[b.Length];
        }
    }
}
