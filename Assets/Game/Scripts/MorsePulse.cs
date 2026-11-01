using UnityEngine;

namespace Game
{
    /// <summary>
    /// The orb's voice: one recorded pulse for each dot and each dash, started with the light. Each
    /// carries its own long tail, so several sound at once; a small pool of sources lets them overlap.
    /// </summary>
    public class MorsePulse : MonoBehaviour
    {
        [SerializeField] MorseEmitter emitter;
        [SerializeField] AudioClip dot;
        [SerializeField] AudioClip dash;
        [SerializeField, Range(0f, 1f)] float volume = 0.85f;
        [SerializeField, Tooltip("Pulses that can ring at once.")] int voices = 6;

        /// <summary>Multiplies the volume; the director raises it near the orb.</summary>
        public float Loudness { get; set; } = 1f;
        public bool Muted { get; set; }
        public bool HasClips => dot && dash;

        AudioSource[] pool;
        int next;

        void Awake()
        {
            pool = new AudioSource[Mathf.Max(1, voices)];
            for (int i = 0; i < pool.Length; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                pool[i] = source;
            }
        }

        void OnEnable()
        {
            if (emitter) emitter.StepStarted += OnStep;
        }

        void OnDisable()
        {
            if (emitter) emitter.StepStarted -= OnStep;
        }

        void OnStep(MorseCode.StepKind kind)
        {
            var clip = kind == MorseCode.StepKind.Dot ? dot : kind == MorseCode.StepKind.Dash ? dash : null;
            if (Muted || clip == null) return;
            var source = pool[next];
            next = (next + 1) % pool.Length;
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume * Loudness);
            source.Play();
        }

        /// <summary>Lets every ringing pulse fade out quickly.</summary>
        public void Hush()
        {
            Muted = true;
            foreach (var source in pool) source.volume *= 0.3f;
        }
    }
}
