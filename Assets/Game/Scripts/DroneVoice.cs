using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The drone's voice: not words but a run of chirps made from them, one or two per word, their
    /// pitch from the letters and the shape of the line from the mood: higher and lifting when happy,
    /// rising at the end of a question, falling and low when sad. The same words always chirp the same.
    /// </summary>
    public static class DroneVoice
    {
        const int Rate = 44100;

        /// <summary>The chirps for a line; their length is how long the line takes to say.</summary>
        public static AudioClip Chirps(string line, string mood)
        {
            var samples = new List<float>(Rate * 4);
            float baseHz = mood switch
            {
                "HAPPY" => 1180f,
                "CURIOUS" => 1040f,
                "WORRIED" => 980f,
                "SAD" => 640f,
                "SORRY" => 720f,
                "SURE" => 860f,
                _ => 900f,
            };
            float wobble = mood == "WORRIED" ? 1f : 0f;
            string[] words = (line ?? "").Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            bool question = line != null && line.TrimEnd().EndsWith("?");
            for (int w = 0; w < words.Length; w++)
            {
                string word = words[w];
                int hash = 17;
                foreach (char c in word.ToLowerInvariant()) hash = hash * 31 + c;
                int syllables = word.Length > 6 ? 2 : 1;
                float through = words.Length <= 1 ? 1f : w / (float)(words.Length - 1);
                for (int s = 0; s < syllables; s++)
                {
                    int h = hash + s * 7919;
                    float offset = 0.78f + 0.44f * ((h & 0xff) / 255f);
                    // The line's shape: sad and sorry sink, questions rise at the end, the rest falls a little.
                    float contour = mood == "SAD" || mood == "SORRY" ? 1f - 0.25f * through
                        : question ? 1f + 0.35f * through * through
                        : mood == "HAPPY" ? 1f + 0.12f * Mathf.Sin(through * Mathf.PI)
                        : 1f - 0.12f * through;
                    float from = baseHz * offset * contour;
                    float glide = ((h >> 8) & 3) switch { 0 => 1.25f, 1 => 0.8f, 2 => 1.08f, _ => 0.95f };
                    if (question && w == words.Length - 1 && s == syllables - 1) glide = 1.5f;
                    float seconds = Mathf.Clamp(0.05f + 0.012f * word.Length / syllables, 0.055f, 0.12f);
                    Syllable(samples, from, from * glide, seconds, wobble);
                    Silence(samples, 0.022f);
                }
                char last = word[word.Length - 1];
                Silence(samples, last == '.' || last == '!' || last == '?' ? 0.16f : last == ',' ? 0.09f : 0.035f);
            }
            if (samples.Count == 0) Silence(samples, 0.05f);
            var clip = AudioClip.Create("chirps", samples.Count, 1, Rate, false);
            clip.SetData(samples.ToArray(), 0);
            return clip;
        }

        /// <summary>Two quick notes: rising (it is listening), or falling (it heard).</summary>
        public static AudioClip Blip(bool rising)
        {
            var samples = new List<float>();
            Syllable(samples, rising ? 900f : 1300f, rising ? 1100f : 1100f, 0.06f, 0f);
            Silence(samples, 0.03f);
            Syllable(samples, rising ? 1250f : 950f, rising ? 1500f : 800f, 0.07f, 0f);
            var clip = AudioClip.Create(rising ? "listen" : "heard", samples.Count, 1, Rate, false);
            clip.SetData(samples.ToArray(), 0);
            return clip;
        }

        /// <summary>The eye's shutter: a soft tick and a short whirr.</summary>
        public static AudioClip Shutter()
        {
            int n = (int)(Rate * 0.22f);
            var data = new float[n];
            var random = new System.Random(4);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float tick = t < 0.012f ? (float)(random.NextDouble() * 2 - 1) * (1f - t / 0.012f) : 0f;
                float whirr = Mathf.Sin(2f * Mathf.PI * (2400f - 3000f * t) * t) * Mathf.Exp(-t * 18f) * 0.25f;
                data[i] = (tick * 0.6f + whirr) * 0.7f;
            }
            var clip = AudioClip.Create("shutter", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static void Syllable(List<float> samples, float fromHz, float toHz, float seconds, float wobble)
        {
            int n = Mathf.Max(1, (int)(seconds * Rate));
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float hz = Mathf.Lerp(fromHz, toHz, t * t * (3f - 2f * t)) * (1f + wobble * 0.06f * Mathf.Sin(t * 40f));
                phase += 2.0 * Mathf.PI * hz / Rate;
                float p = (float)phase;
                // A sine with a little of its octave and a soft buzz: small, bright, not a whistle.
                float tone = Mathf.Sin(p) + 0.28f * Mathf.Sin(2f * p) + 0.1f * Mathf.Sign(Mathf.Sin(p)) * 0.6f;
                float attack = Mathf.Clamp01(i / (Rate * 0.006f));
                float release = Mathf.Clamp01((n - i) / (Rate * 0.02f));
                samples.Add(tone * attack * release * 0.32f);
            }
        }

        static void Silence(List<float> samples, float seconds)
        {
            int n = (int)(seconds * Rate);
            for (int i = 0; i < n; i++) samples.Add(0f);
        }
    }
}
