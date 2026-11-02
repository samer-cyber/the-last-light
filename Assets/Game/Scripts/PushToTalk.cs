using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>
    /// Speaking to the entity: while Q is held the microphone records, and when it is let go the
    /// recording goes out as a WAV file for the transcriber. Off while the notes are up, where Q is a
    /// letter. The HUD shows the voice's level while it listens.
    /// </summary>
    public class PushToTalk : MonoBehaviour
    {
        [SerializeField] Hud hud;
        [SerializeField] AudioSource sound;
        [SerializeField] AudioClip startClip;
        [SerializeField] AudioClip stopClip;
        [SerializeField, Tooltip("The longest a recording may run, in seconds.")] float longest = 12f;
        [SerializeField] int rate = 16000;

        /// <summary>The player let go of the key: what they said, as a WAV file.</summary>
        public event Action<byte[]> Spoken;
        /// <summary>Something the player should know, in a few words (no microphone, too short).</summary>
        public event Action<string> Trouble;

        public bool Available { get; set; } = true;
        public bool Listening => recording != null;
        public bool HasSpoken { get; private set; }
        public float Level { get; private set; }

        AudioClip recording;
        float startedAt;
        bool waitForRelease;
        readonly float[] window = new float[512];

        void Update()
        {
            var keys = Keyboard.current;
            bool held = keys != null && keys.qKey.isPressed;
            if (!held) waitForRelease = false;
            if (recording == null)
            {
                if (held && Available && !waitForRelease) Begin();
                return;
            }
            if (!held || !Available || Time.unscaledTime - startedAt > longest) End();
            else Meter();
        }

        void OnDisable()
        {
            if (recording == null) return;
            Microphone.End(null);
            recording = null;
        }

        void Begin()
        {
            waitForRelease = true;
            if (Microphone.devices.Length == 0)
            {
                Trouble?.Invoke("no microphone found   [Tab] write instead");
                return;
            }
            recording = Microphone.Start(null, false, Mathf.CeilToInt(longest) + 1, rate);
            startedAt = Time.unscaledTime;
            if (sound && startClip) sound.PlayOneShot(startClip, 0.6f);
        }

        void Meter()
        {
            int position = Microphone.GetPosition(null);
            if (position < window.Length) return;
            recording.GetData(window, position - window.Length);
            float sum = 0f;
            foreach (float s in window) sum += s * s;
            float decibels = 20f * Mathf.Log10(Mathf.Sqrt(sum / window.Length) + 1e-6f);
            Level = Mathf.Clamp01((decibels + 50f) / 40f);
            if (hud) hud.Listening(Level);
        }

        void End()
        {
            int samples = Microphone.GetPosition(null);
            Microphone.End(null);
            var clip = recording;
            recording = null;
            Level = 0f;
            if (hud) hud.StopListening();
            if (samples < rate * 0.35f)
            {
                Destroy(clip);
                Trouble?.Invoke("hold [Q] while you speak");
                return;
            }
            int channels = clip.channels, frequency = clip.frequency;
            var data = new float[samples * channels];
            clip.GetData(data, 0);
            Destroy(clip);
            if (sound && stopClip) sound.PlayOneShot(stopClip, 0.6f);
            HasSpoken = true;
            Spoken?.Invoke(Wav(data, channels, frequency));
        }

        /// <summary>16-bit PCM WAV.</summary>
        static byte[] Wav(float[] samples, int channels, int sampleRate)
        {
            using var stream = new MemoryStream(44 + samples.Length * 2);
            using var writer = new BinaryWriter(stream);
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + samples.Length * 2);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channels * 2);
            writer.Write((short)(channels * 2));
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(samples.Length * 2);
            foreach (float s in samples) writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(s, -1f, 1f) * short.MaxValue));
            writer.Flush();
            return stream.ToArray();
        }
    }
}
