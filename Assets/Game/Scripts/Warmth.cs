using System;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// How warm the survivor still is, which is how alive: the machines' fire takes warmth, and it
    /// comes back slowly, quickly by a fire. A shield held toward a shot stops it. At nothing, the
    /// survivor goes down (the director decides what happens then).
    /// </summary>
    public class Warmth : MonoBehaviour
    {
        [SerializeField] Traveller traveller;
        [SerializeField] FollowCamera follow;
        [SerializeField] Hud hud;
        [SerializeField] AudioSource sound;
        [SerializeField] AudioClip hitClip, blockClip;

        public float Value { get; private set; } = 1f;
        public bool Down { get; private set; }
        public event Action<float, Vector3> Hurt;
        public event Action Fell;

        float hurtAt = -10f;

        void Update()
        {
            if (Down) return;
            // Back slowly after a while, quickly near something hot.
            bool fire = Heat.Near(transform.position, 4.5f);
            float back = Time.time - hurtAt > 4f ? 0.04f : 0f;
            if (fire) back += 0.12f;
            Value = Mathf.MoveTowards(Value, 1f, back / Settings.Harm * Time.deltaTime);
            hud.Warmth(Value);
        }

        /// <summary>A shot reaches the survivor from a point.</summary>
        public void Hit(float amount, Vector3 from)
        {
            if (Down) return;
            var held = traveller.Holding ? traveller.Holding.GetComponent<Shield>() : null;
            if (held && held.Covers(transform.position, traveller.Facing, from))
            {
                Fx.Sparks(held.transform.position, 12, Shots.Cyan);
                if (sound && blockClip) sound.PlayOneShot(blockClip, 0.7f);
                return;
            }
            Value = Mathf.Max(0f, Value - amount * Settings.Harm);
            hurtAt = Time.time;
            follow.Shake = Mathf.Max(follow.Shake, 0.12f);
            hud.Flash(0.35f);
            if (sound && hitClip) sound.PlayOneShot(hitClip, 0.8f);
            Hurt?.Invoke(amount, from);
            if (Value <= 0f)
            {
                Down = true;
                Fell?.Invoke();
            }
        }

        /// <summary>Back on their feet, this warm.</summary>
        public void Restore(float value)
        {
            Value = value;
            Down = false;
            hurtAt = Time.time;
        }
    }
}
