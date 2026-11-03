using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A thing held in ice: the heart in its case, the mirror's joint. Heat nearby melts the ice from
    /// the top down (a fire, a stove, a torch held to it), dripping and then steaming as it goes; a pick
    /// breaks it in a few blows. Once free, it says so; what it held can be taken.
    /// </summary>
    public class Frozen : MonoBehaviour
    {
        [SerializeField, Tooltip("The ice itself.")] Renderer shell;
        [SerializeField, Tooltip("Melting this much frees it (a hot fire beside it melts 3 a second).")] float needs = 18f;
        [SerializeField, Tooltip("Blows of a pick that break it.")] int blows = 4;
        [SerializeField] ParticleSystem drips;
        [SerializeField] ParticleSystem steam;
        [SerializeField] AudioSource sound;
        [SerializeField] AudioClip crackClip, breakClip, meltClip;
        [SerializeField, Tooltip("Locked while frozen.")] List<Holdable> holds = new List<Holdable>();

        public event Action<Frozen> Freed;
        public bool Free { get; private set; }
        /// <summary>0 frozen solid, 1 melted.</summary>
        public float Progress => Mathf.Clamp01(melted / needs);
        /// <summary>Whether it is melting right now, and how fast (per second).</summary>
        public float Rate { get; private set; }
        public Renderer Shell => shell;

        float melted, warmth, shake;
        int hit;
        Vector3 shellRest;
        static readonly int BuildId = Shader.PropertyToID("_Build");
        static readonly int BuildRangeId = Shader.PropertyToID("_BuildRange");

        void Start()
        {
            foreach (var h in holds) if (h) h.Locked = true;
            if (shell)
            {
                shellRest = shell.transform.localPosition;
                var block = new MaterialPropertyBlock();
                shell.GetPropertyBlock(block);
                block.SetVector(BuildRangeId, new Vector4(shell.bounds.min.y, Mathf.Max(shell.bounds.size.y, 0.05f), 0f, 0f));
                block.SetFloat(BuildId, 1f);
                shell.SetPropertyBlock(block);
            }
        }

        /// <summary>Heat held to it this frame (a torch), in melt per second.</summary>
        public void Warm(float perSecond) => warmth += perSecond;

        /// <summary>A blow with a pick: it cracks, and after enough blows it breaks.</summary>
        public void Strike()
        {
            if (Free) return;
            hit++;
            shake = 1f;
            melted = Mathf.Max(melted, needs * hit / (blows + 1f));
            if (hit >= blows) Release(true);
            else if (sound && crackClip) sound.PlayOneShot(crackClip, 0.8f);
        }

        void Update()
        {
            if (Free) return;
            float dt = Time.deltaTime;
            Vector3 centre = shell ? shell.bounds.center : transform.position;
            Rate = Heat.Total(centre) + warmth;
            warmth = 0f;
            melted += Rate * dt;
            if (drips)
            {
                var emission = drips.emission;
                emission.rateOverTime = Rate > 0.05f ? 6f + Rate * 10f : 0f;
            }
            if (steam)
            {
                var emission = steam.emission;
                emission.rateOverTime = Rate > 1.5f ? Rate * 4f : 0f;
            }
            if (sound && meltClip)
            {
                if (Rate > 0.3f && !sound.isPlaying)
                {
                    sound.clip = meltClip;
                    sound.loop = true;
                    sound.Play();
                }
                else if (Rate <= 0.3f && sound.isPlaying && sound.clip == meltClip) sound.Stop();
                if (sound.clip == meltClip) sound.volume = Mathf.Clamp01(Rate * 0.25f);
            }
            if (shell)
            {
                shake = Mathf.MoveTowards(shake, 0f, dt * 4f);
                shell.transform.localPosition = shellRest + UnityEngine.Random.insideUnitSphere * 0.03f * shake;
                var block = new MaterialPropertyBlock();
                shell.GetPropertyBlock(block);
                block.SetFloat(BuildId, 1f - Progress * 0.85f);
                shell.SetPropertyBlock(block);
            }
            if (melted >= needs) Release(false);
        }

        void Release(bool broken)
        {
            if (Free) return;
            Free = true;
            if (sound)
            {
                sound.Stop();
                sound.loop = false;
                var clip = broken ? breakClip : breakClip ?? crackClip;
                if (clip) sound.PlayOneShot(clip, broken ? 1f : 0.6f);
            }
            if (steam)
            {
                var emission = steam.emission;
                emission.rateOverTime = 0f;
                steam.Emit(broken ? 10 : 30);
            }
            if (drips)
            {
                var emission = drips.emission;
                emission.rateOverTime = 0f;
                drips.Emit(broken ? 40 : 20);
            }
            if (shell)
            {
                foreach (var c in shell.GetComponentsInChildren<Collider>()) c.enabled = false;
                shell.enabled = false;
            }
            foreach (var c in GetComponents<Collider>()) if (!c.isTrigger) c.enabled = false;
            foreach (var h in holds) if (h) h.Locked = false;
            Freed?.Invoke(this);
        }
    }
}
