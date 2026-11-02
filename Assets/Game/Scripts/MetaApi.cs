using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Game
{
    /// <summary>
    /// Calls to Meta's Model API (https://api.meta.ai/v1): Muse Spark for reading what the player
    /// says, Muse Voice Transcribe for turning their voice into words. Both call back with a result,
    /// or with null and the reason; neither throws. The key comes from <see cref="Secrets"/>.
    /// </summary>
    public static class MetaApi
    {
        const string Base = "https://api.meta.ai/v1";
        const string KeyName = "META_API_KEY";

        public static bool HasKey => Secrets.Get(KeyName) != null;

        /// <summary>One chat completion; calls back with the reply's text.</summary>
        public static IEnumerator Chat(string body, float timeout, Action<string, string> done)
        {
            string key = Secrets.Get(KeyName);
            if (key == null)
            {
                done(null, "no " + KeyName);
                yield break;
            }
            using var request = new UnityWebRequest(Base + "/chat/completions", "POST")
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = Mathf.CeilToInt(timeout),
            };
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + key);
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                done(null, Failure(request));
                yield break;
            }
            ChatReply reply = null;
            try { reply = JsonUtility.FromJson<ChatReply>(request.downloadHandler.text); }
            catch (ArgumentException) { }
            string content = reply?.choices != null && reply.choices.Length > 0 ? reply.choices[0].message?.content : null;
            done(content, content == null ? "the reply had no text" : null);
        }

        /// <summary>Speech in a WAV file to words, with Muse Voice Transcribe in push-to-talk mode.</summary>
        public static IEnumerator Transcribe(byte[] wav, float timeout, Action<string, string> done)
        {
            string key = Secrets.Get(KeyName);
            if (key == null)
            {
                done(null, "no " + KeyName);
                yield break;
            }
            // The form is written by hand: Meta's server answers the body UnityWebRequest.Post makes
            // from form sections with "Malformed multipart body".
            const string settings = "{\"model\":\"muse-voice-transcribe-1.0\",\"audioEncoding\":\"WAV\",\"mode\":\"PUSH_TO_TALK\"}";
            string boundary = Guid.NewGuid().ToString("N");
            byte[] head = Encoding.UTF8.GetBytes(
                $"--{boundary}\r\nContent-Disposition: form-data; name=\"request\"\r\nContent-Type: application/json\r\n\r\n{settings}\r\n" +
                $"--{boundary}\r\nContent-Disposition: form-data; name=\"audio\"; filename=\"speech.wav\"\r\nContent-Type: audio/wav\r\n\r\n");
            byte[] tail = Encoding.UTF8.GetBytes($"\r\n--{boundary}--\r\n");
            var body = new byte[head.Length + wav.Length + tail.Length];
            Buffer.BlockCopy(head, 0, body, 0, head.Length);
            Buffer.BlockCopy(wav, 0, body, head.Length, wav.Length);
            Buffer.BlockCopy(tail, 0, body, head.Length + wav.Length, tail.Length);
            string type = "multipart/form-data; boundary=" + boundary;
            using var request = new UnityWebRequest(Base + "/asr/transcribe", "POST")
            {
                uploadHandler = new UploadHandlerRaw(body) { contentType = type },
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = Mathf.CeilToInt(timeout),
            };
            request.SetRequestHeader("Content-Type", type);
            request.SetRequestHeader("Authorization", "Bearer " + key);
            request.SetRequestHeader("Accept", "text/plain");
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                done(null, Failure(request));
                yield break;
            }
            // One turn per line; a speaker's label may lead a line ("A:hello", "unknown:hello").
            var words = new StringBuilder();
            foreach (string raw in request.downloadHandler.text.Split('\n'))
            {
                string line = raw.Trim();
                int colon = line.IndexOf(':');
                if (colon == 1 && char.IsUpper(line[0]) || line.StartsWith("unknown:")) line = line.Substring(colon + 1).Trim();
                if (line.Length == 0) continue;
                if (words.Length > 0) words.Append(' ');
                words.Append(line);
            }
            done(words.ToString(), null);
        }

        /// <summary>A string as a JSON literal, quotes included.</summary>
        public static string Quote(string text)
        {
            var json = new StringBuilder("\"");
            foreach (char c in text ?? "")
            {
                switch (c)
                {
                    case '"': json.Append("\\\""); break;
                    case '\\': json.Append("\\\\"); break;
                    case '\n': json.Append("\\n"); break;
                    case '\r': json.Append("\\r"); break;
                    case '\t': json.Append("\\t"); break;
                    default:
                        if (c < ' ') json.Append("\\u").Append(((int)c).ToString("x4"));
                        else json.Append(c);
                        break;
                }
            }
            return json.Append('"').ToString();
        }

        static string Failure(UnityWebRequest request)
        {
            string body = request.downloadHandler?.text ?? "";
            if (body.Length > 300) body = body.Substring(0, 300);
            return $"{request.responseCode} {request.error} {body}".Trim();
        }

        [Serializable] class ChatReply { public Choice[] choices; }
        [Serializable] class Choice { public Message message; }
        [Serializable] class Message { public string content; }
    }
}
