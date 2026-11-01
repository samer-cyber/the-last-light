using System;
using System.Collections.Generic;

namespace Game
{
    /// <summary>International Morse code, and a message turned into timed lit and dark steps.</summary>
    public static class MorseCode
    {
        public enum StepKind { Dot, Dash, SymbolGap, LetterGap, WordGap, Pause }

        public readonly struct Step
        {
            public readonly StepKind Kind;
            public readonly float Units;

            public Step(StepKind kind, float units)
            {
                Kind = kind;
                Units = units;
            }

            public bool IsLit => Kind == StepKind.Dot || Kind == StepKind.Dash;
        }

        public const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        static readonly Dictionary<char, string> Codes = new Dictionary<char, string>
        {
            { 'A', ".-" }, { 'B', "-..." }, { 'C', "-.-." }, { 'D', "-.." }, { 'E', "." },
            { 'F', "..-." }, { 'G', "--." }, { 'H', "...." }, { 'I', ".." }, { 'J', ".---" },
            { 'K', "-.-" }, { 'L', ".-.." }, { 'M', "--" }, { 'N', "-." }, { 'O', "---" },
            { 'P', ".--." }, { 'Q', "--.-" }, { 'R', ".-." }, { 'S', "..." }, { 'T', "-" },
            { 'U', "..-" }, { 'V', "...-" }, { 'W', ".--" }, { 'X', "-..-" }, { 'Y', "-.--" },
            { 'Z', "--.." },
            { '0', "-----" }, { '1', ".----" }, { '2', "..---" }, { '3', "...--" }, { '4', "....-" },
            { '5', "....." }, { '6', "-...." }, { '7', "--..." }, { '8', "---.." }, { '9', "----." },
        };

        /// <summary>Dots and dashes for one character, or null when Morse has none.</summary>
        public static string Encode(char c) => Codes.TryGetValue(char.ToUpperInvariant(c), out var code) ? code : null;

        /// <summary>
        /// The message as steps with the standard timing (a dash is three dots; gaps of one, three
        /// and seven between symbols, letters and words), then a pause before it repeats.
        /// </summary>
        public static List<Step> Sequence(string message, float pauseUnits)
        {
            var steps = new List<Step>();
            var words = message.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int w = 0; w < words.Length; w++)
            {
                if (w > 0) steps.Add(new Step(StepKind.WordGap, 7f));
                bool firstLetter = true;
                foreach (char c in words[w])
                {
                    string code = Encode(c);
                    if (code == null) continue;
                    if (!firstLetter) steps.Add(new Step(StepKind.LetterGap, 3f));
                    firstLetter = false;
                    for (int s = 0; s < code.Length; s++)
                    {
                        if (s > 0) steps.Add(new Step(StepKind.SymbolGap, 1f));
                        steps.Add(code[s] == '-' ? new Step(StepKind.Dash, 3f) : new Step(StepKind.Dot, 1f));
                    }
                }
            }
            steps.Add(new Step(StepKind.Pause, pauseUnits));
            return steps;
        }
    }
}
