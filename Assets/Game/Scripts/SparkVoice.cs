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
    /// Spark's spoken voice: each line read aloud by ElevenLabs v4 Turbo (Samer's designed voice "Game robot
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
            yield return Ask(line, mood, timeout, (bytes, e) => { pcm = bytes; error = e; });
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
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(Voice + "|" + Model + "|" + mood + "|" + line));
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
