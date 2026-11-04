using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Game
{
    /// <summary>
    /// Spark's spoken voice. A line is spoken sentence by sentence, all asked for at once (so it comes sooner than
    /// the whole line would), each sentence told what comes before and after it so the voice carries across, and
    /// joined with a breath between: longer after a question or in a sad line, shorter when it is excited.
    /// Each line read aloud by ElevenLabs v4 Turbo (Samer's designed voice "Game robot
    /// voice", a robotic, conversational cyborg, a little joyful and silly), the line's mood given to the model
    /// as an audio tag ([sad], [happy], [scared] ...) so the feeling follows the words, at a steady setting. Lines are kept in memory and on disk, so a line said before (the story's own lines, warnings)
    /// plays at once. Without a key or a connection the caller falls back to chirps. The key comes from
    /// <see cref="Secrets"/>; it is never logged.
    /// </summary>
    public static class SparkVoice
    {
        const string KeyName = "ELEVENLABS_API_KEY";
        const string Voice = "1AVDZUKrjr2dXegY7zF8";
        const string Model = "eleven_v4_turbo";
        public const int Rate = 24000;

        static readonly Dictionary<string, AudioClip> kept = new Dictionary<string, AudioClip>();
        static readonly HashSet<string> asking = new HashSet<string>();
        static string folder;

        public static bool HasKey => Secrets.Get(KeyName) != null;

        /// <summary>The line spoken in the mood: from memory, from disk, or asked for. Calls back with null if it cannot.</summary>
        public static IEnumerator Clip(string line, string mood, float timeout, Action<AudioClip> done)
        {
            line = (line ?? "").Trim();
            if (line.Length == 0 || !HasKey)
            {
                done(null);
                yield break;
            }
            string name = Name(line, mood);
            // Another call is already asking for this line (a prefetch): wait for it.
            for (float t = 0f; asking.Contains(name) && t < timeout; t += Time.unscaledDeltaTime) yield return null;
            if (kept.TryGetValue(name, out var clip) && clip)
            {
                done(clip);
                yield break;
            }
            string path = Path.Combine(Folder(), name + ".pcm");
            if (File.Exists(path))
            {
                try
                {
                    clip = Make(name, File.ReadAllBytes(path));
                    kept[name] = clip;
                    done(clip);
                    yield break;
                }
                catch (IOException) { }
            }
            asking.Add(name);
            byte[] pcm = null;
            string error = null;
            var sentences = Sentences(line);
            if (sentences.Count <= 1) yield return Ask(line, mood, timeout, (bytes, e) => { pcm = bytes; error = e; });
            else yield return AskAll(sentences, mood, timeout, (bytes, e) => { pcm = bytes; error = e; });
            asking.Remove(name);
            if (pcm == null || pcm.Length < 200)
            {
                Debug.LogWarning($"Spark's voice: no audio for \"{line}\" ({error ?? "too short"}); chirps instead.");
                done(null);
                yield break;
            }
            try { File.WriteAllBytes(path, pcm); }
            catch (IOException) { }
            clip = Make(name, pcm);
            kept[name] = clip;
            done(clip);
        }

        /// <summary>Asks for lines ahead of time, one after another, so they play at once when needed.</summary>
        public static IEnumerator Prefetch(IEnumerable<(string line, string mood)> lines)
        {
            foreach (var (line, mood) in lines) yield return Clip(line, mood, 20f, _ => { });
        }

        /// <summary>The first request to a voice can take many seconds; one short word at the start takes that wait out of play.</summary>
        public static IEnumerator Warm() => Clip("Hm.", "CALM", 30f, _ => { });

        /// <summary>How loud the line is, sixty times a second (0 to 1): the eye pulses with it.</summary>
        public static float[] Envelope(AudioClip clip)
        {
            if (!clip) return Array.Empty<float>();
            var data = new float[clip.samples * clip.channels];
            clip.GetData(data, 0);
            int step = Mathf.Max(1, clip.frequency / 60);
            var envelope = new float[data.Length / step + 1];
            float peak = 0.0001f;
            for (int i = 0; i < envelope.Length; i++)
            {
                float sum = 0f;
                int from = i * step, to = Mathf.Min(data.Length, from + step);
                for (int j = from; j < to; j++) sum += data[j] * data[j];
                envelope[i] = to > from ? Mathf.Sqrt(sum / (to - from)) : 0f;
                peak = Mathf.Max(peak, envelope[i]);
            }
            for (int i = 0; i < envelope.Length; i++) envelope[i] = Mathf.Clamp01(envelope[i] / peak);
            return envelope;
        }

        /// <summary>The line in sentences (not split after Mr., Dr. and the like), at most six pieces.</summary>
        public static List<string> Sentences(string line)
        {
            var pieces = new List<string>();
            var current = new StringBuilder();
            string[] titles = { "Mr.", "Mrs.", "Ms.", "Dr.", "St.", "No." };
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                current.Append(c);
                bool end = c == '.' || c == '!' || c == '?' || c == '\u2026';
                if (!end) continue;
                // Keep runs like "?!" and "..." together, and closing quotes with their sentence.
                while (i + 1 < line.Length && (line[i + 1] == '.' || line[i + 1] == '!' || line[i + 1] == '?' || line[i + 1] == '"' || line[i + 1] == '\u201d'))
                    current.Append(line[++i]);
                string sofar = current.ToString().TrimEnd();
                bool title = Array.Exists(titles, t => sofar.EndsWith(" " + t) || sofar == t);
                bool next = i + 1 >= line.Length || char.IsWhiteSpace(line[i + 1]);
                if (title || !next) continue;
                if (sofar.Trim().Length > 0) pieces.Add(sofar.Trim());
                current.Clear();
            }
            if (current.ToString().Trim().Length > 0) pieces.Add(current.ToString().Trim());
            // Too many pieces: the shortest neighbours go together.
            while (pieces.Count > 6)
            {
                int best = 0;
                for (int i = 1; i < pieces.Count - 1; i++)
                    if (pieces[i].Length + pieces[i + 1].Length < pieces[best].Length + pieces[best + 1].Length) best = i;
                pieces[best] = pieces[best] + " " + pieces[best + 1];
                pieces.RemoveAt(best + 1);
            }
            return pieces;
        }

        /// <summary>The breath after a sentence, in seconds: by how it ends and how Spark feels.</summary>
        static float Pause(string sentence, string mood)
        {
            string end = sentence.TrimEnd('"', '\u201d', ' ');
            float gap = end.EndsWith("?") ? 0.6f : end.EndsWith("!") ? 0.38f : end.EndsWith("...") || end.EndsWith("\u2026") ? 0.7f : 0.48f;
            if (sentence.Split(' ').Length <= 2) gap *= 1.25f;
            gap *= mood switch
            {
                "SAD" or "HURT" or "SORRY" => 1.4f,
                "WORRIED" or "SCARED" => 1.1f,
                "HAPPY" or "PLAYFUL" => 0.7f,
                "CURIOUS" => 0.9f,
                _ => 1f,
            };
            return Mathf.Min(gap, 0.85f);
        }

        /// <summary>Every sentence asked for at once, each told its neighbours; then trimmed and joined with breaths.</summary>
        static IEnumerator AskAll(List<string> sentences, string mood, float timeout, Action<byte[], string> done)
        {
            var requests = new List<UnityWebRequest>();
            for (int i = 0; i < sentences.Count; i++)
            {
                string before = i > 0 ? string.Join(" ", sentences.GetRange(0, i)) : null;
                string after = i + 1 < sentences.Count ? string.Join(" ", sentences.GetRange(i + 1, sentences.Count - i - 1)) : null;
                var request = Request(sentences[i], mood, before, after, timeout);
                request.SendWebRequest();
                requests.Add(request);
            }
            for (float t = 0f; t < timeout && requests.Exists(r => !r.isDone); t += Time.unscaledDeltaTime) yield return null;
            var joined = new List<byte>();
            string error = null;
            for (int i = 0; i < requests.Count; i++)
            {
                var r = requests[i];
                if (!r.isDone || r.result != UnityWebRequest.Result.Success)
                {
                    error = r.isDone ? $"{r.responseCode} {r.error}" : "timed out";
                    break;
                }
                byte[] part = Trim(r.downloadHandler.data);
                joined.AddRange(part);
                if (i + 1 < requests.Count) joined.AddRange(new byte[2 * Mathf.RoundToInt(Pause(sentences[i], mood) * Rate)]);
            }
            foreach (var r in requests) r.Dispose();
            done(error == null ? joined.ToArray() : null, error);
        }

        /// <summary>16-bit PCM without the silence at its ends (a little kept, so a word is never clipped).</summary>
        static byte[] Trim(byte[] pcm)
        {
            if (pcm == null || pcm.Length < 4) return pcm ?? Array.Empty<byte>();
            int n = pcm.Length / 2;
            short Sample(int i) => (short)(pcm[2 * i] | pcm[2 * i + 1] << 8);
            const int Threshold = 330;   // about -40 dB
            int first = 0, last = n - 1;
            while (first < n && Math.Abs((int)Sample(first)) < Threshold) first++;
            while (last > first && Math.Abs((int)Sample(last)) < Threshold) last--;
            first = Math.Max(0, first - Rate / 25);
            last = Math.Min(n - 1, last + Rate / 12);
            var trimmed = new byte[(last - first + 1) * 2];
            Buffer.BlockCopy(pcm, first * 2, trimmed, 0, trimmed.Length);
            return trimmed;
        }

        /// <summary>The feeling as a direction the model performs, not loose settings that swing at random.</summary>
        static string Tag(string mood) => mood switch
        {
            "SAD" => "[softly] ",
            "HURT" => "[quietly] ",
            "HAPPY" => "[happy] ",
            "PLAYFUL" => "[playfully] ",
            "CURIOUS" => "[curious] ",
            "SCARED" => "[tense] ",
            "WORRIED" => "[thoughtful] ",
            "ANGRY" => "[firmly] ",
            "SORRY" => "[gently] ",
            "SURE" => "[warmly] ",
            _ => "",
        };

        static UnityWebRequest Request(string text, string mood, string before, string after, float timeout)
        {
            string body = "{\"text\":" + MetaApi.Quote(Tag(mood) + text) + ",\"model_id\":\"" + Model + "\"" +
                          (string.IsNullOrEmpty(before) ? "" : ",\"previous_text\":" + MetaApi.Quote(before)) +
                          (string.IsNullOrEmpty(after) ? "" : ",\"next_text\":" + MetaApi.Quote(after)) +
                          ",\"voice_settings\":{\"stability\":0.5,\"similarity_boost\":0.8,\"use_speaker_boost\":true}}";
            var request = new UnityWebRequest($"https://api.elevenlabs.io/v1/text-to-speech/{Voice}?output_format=pcm_{Rate}", "POST")
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = Mathf.CeilToInt(timeout),
            };
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("xi-api-key", Secrets.Get(KeyName));
            return request;
        }

        static IEnumerator Ask(string line, string mood, float timeout, Action<byte[], string> done)
        {
            string key = Secrets.Get(KeyName);
            // The feeling as a direction the model performs, not loose settings that swing at random.
            string tag = mood switch
            {
                "SAD" => "[sad] ",
                "HURT" => "[hurt] ",
                "HAPPY" => "[happy] ",
                "PLAYFUL" => "[playfully] ",
                "CURIOUS" => "[curious] ",
                "SCARED" => "[scared] ",
                "WORRIED" => "[nervous] ",
                "ANGRY" => "[frustrated] ",
                "SORRY" => "[apologetic] ",
                "SURE" => "[confident] ",
                _ => "",
            };
            string body = "{\"text\":" + MetaApi.Quote(tag + line) + ",\"model_id\":\"" + Model + "\",\"voice_settings\":{" +
                          "\"stability\":0.5,\"similarity_boost\":0.8,\"use_speaker_boost\":true" + "}}";
            using var request = new UnityWebRequest($"https://api.elevenlabs.io/v1/text-to-speech/{Voice}?output_format=pcm_{Rate}", "POST")
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = Mathf.CeilToInt(timeout),
            };
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("xi-api-key", key);
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                string text = request.downloadHandler?.text ?? "";
                done(null, $"{request.responseCode} {request.error} {(text.Length > 200 ? text.Substring(0, 200) : text)}".Trim());
                yield break;
            }
            done(request.downloadHandler.data, null);
        }

        static AudioClip Make(string name, byte[] pcm)
        {
            int n = pcm.Length / 2;
            var samples = new float[n];
            for (int i = 0; i < n; i++) samples[i] = (short)(pcm[2 * i] | pcm[2 * i + 1] << 8) / 32768f;
            var clip = AudioClip.Create("spark " + name.Substring(0, 8), n, 1, Rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        static string Name(string line, string mood)
        {
            using var sha = SHA1.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(Voice + "|" + Model + "|" + mood + "|" + line + "|sentences"));
            var text = new StringBuilder();
            for (int i = 0; i < 10; i++) text.Append(hash[i].ToString("x2"));
            return text.ToString();
        }

        static string Folder()
        {
            if (folder != null) return folder;
            folder = Path.Combine(Application.persistentDataPath, "spark-voice");
            try { Directory.CreateDirectory(folder); }
            catch (IOException) { }
            return folder;
        }

        static string F(float value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }
}
