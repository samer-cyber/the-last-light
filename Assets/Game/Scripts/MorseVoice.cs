using System;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The orb's voice: a low, glassy tone made on the fly that sounds while the light is on, so
    /// the message can be heard as well as seen. Drive holds the tone up for scripted moments.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class MorseVoice : MonoBehaviour
    {
        const double Turn = 2.0 * Math.PI;

        [SerializeField] MorseEmitter emitter;
        [SerializeField] float frequency = 110f;
        [SerializeField, Range(0f, 1f)] float volume = 0.2f;
        [SerializeField, Tooltip("Sound with the message; off when recorded pulses play it instead.")] bool followMessage = true;

        /// <summary>Holds the tone at this level (0 to 1) whatever the message is doing.</summary>
        public float Drive { get; set; }
        /// <summary>Multiplies the frequency.</summary>
        public float Pitch { get; set; } = 1f;
        public bool Muted { get; set; }

        // Written on the main thread, read on the audio thread.
        volatile float target;
        volatile float pitch = 1f;

        int sampleRate;
        double phase, shimmer;
        float level;

        void Awake()
        {
            sampleRate = AudioSettings.outputSampleRate;
            var source = GetComponent<AudioSource>();
            // A silent loop keeps the source playing, so that the filter below is called.
            source.clip = AudioClip.Create("Silence", sampleRate, 1, sampleRate, false);
            source.loop = true;
            source.spatialBlend = 0f;
            source.Play();
        }

        void Update()
        {
            float lit = followMessage && emitter != null && emitter.Lit ? 1f : 0f;
            target = Muted ? 0f : Mathf.Max(lit, Drive);
            pitch = Pitch;
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (sampleRate == 0) return;
            float goal = target;
            double step = Turn * frequency * pitch / sampleRate;
            float rise = 1f - (float)Math.Exp(-1.0 / (0.012 * sampleRate));
            float fall = 1f - (float)Math.Exp(-1.0 / (0.08 * sampleRate));
            for (int i = 0; i < data.Length; i += channels)
            {
                level += (goal - level) * (goal > level ? rise : fall);
                phase += step;
                shimmer += step * 8.02;
                double s = Math.Sin(phase) * 0.5 + Math.Sin(2.0 * phase + 0.3) * 0.22 + Math.Sin(3.0 * phase) * 0.1
                         + Math.Sin(4.0 * phase + 1.1) * 0.04 + Math.Sin(shimmer) * 0.035;
                float sample = (float)s * level * volume;
                for (int c = 0; c < channels; c++) data[i + c] += sample;
            }
            phase %= Turn;
            shimmer %= Turn;
        }
    }
}
