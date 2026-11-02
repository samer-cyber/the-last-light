using System.Collections;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The entity's way of showing instead of saying: a river of tiny 1s and 0s that pours out of the
    /// sky in front of the player, crosses to one thing, and wraps around it (or sinks into it).
    /// The particles are placed along the curve by their age, so every digit follows the same river.
    /// </summary>
    public class PointingStream : MonoBehaviour
    {
        [SerializeField, Tooltip("Particles textured with a 1 and a 0; positions are set here.")] ParticleSystem digits;
        [SerializeField] Camera view;
        [SerializeField] AudioSource sound;
        [SerializeField] AudioClip clip;
        [SerializeField, Tooltip("Seconds a digit takes to cross.")] float travel = 2.4f;
        [SerializeField, Tooltip("Seconds it then circles the target.")] float circle = 1.8f;
        [SerializeField] float rate = 80f;
        [SerializeField] float pour = 2.6f;

        public bool Busy { get; private set; }

        ParticleSystem.Particle[] particles;
        Vector3 from, bend, to, right, up;
        float around;

        void Awake() => particles = new ParticleSystem.Particle[digits.main.maxParticles];

        /// <summary>Pours the stream to a point; "around" is the radius it circles at (small to sink in).</summary>
        public IEnumerator Point(Vector3 target, float aroundRadius)
        {
            while (Busy) yield return null;
            Busy = true;
            Transform eye = view.transform;
            to = target;
            around = aroundRadius;
            from = eye.position + eye.forward * 70f + eye.up * 28f;
            float span = Vector3.Distance(from, to);
            up = Vector3.up;
            right = Vector3.Cross(up, (to - from).normalized).normalized;
            if (right.sqrMagnitude < 0.5f) right = eye.right;
            bend = Vector3.Lerp(from, to, 0.45f) + Vector3.up * (span * 0.22f) + right * (span * 0.12f);
            if (sound && clip) sound.PlayOneShot(clip);

            float life = travel + circle;
            for (float t = 0f, owed = 0f; t < pour; t += Time.deltaTime)
            {
                owed += rate * Time.deltaTime;
                int count = Mathf.FloorToInt(owed);
                owed -= count;
                if (count > 0)
                    digits.Emit(new ParticleSystem.EmitParams { position = from, startLifetime = life, applyShapeToPosition = false, velocity = Vector3.zero }, count);
                yield return null;
            }
            yield return new WaitForSeconds(life);
            Busy = false;
        }

        void LateUpdate()
        {
            int count = digits.GetParticles(particles);
            if (count == 0) return;
            float time = Time.time;
            for (int i = 0; i < count; i++)
            {
                var p = particles[i];
                float age = p.startLifetime - p.remainingLifetime;
                uint seed = p.randomSeed;
                float a = (seed % 1000u) / 1000f, b = (seed / 1000u % 1000u) / 1000f;
                Vector3 position;
                if (age < travel)
                {
                    float s = Mathf.SmoothStep(0f, 1f, age / travel);
                    float spread = Mathf.Sin(Mathf.PI * s) * 7f;
                    position = Bezier(s) + right * ((a - 0.5f) * spread) + up * ((b - 0.5f) * spread);
                }
                else
                {
                    // Round and round the target, drawing in.
                    float k = (age - travel) / circle;
                    float angle = a * Mathf.PI * 2f + k * 5f + time * 0.3f;
                    float radius = around * (1.35f + 0.3f * b) * (1f - 0.35f * k);
                    Vector3 axis = (to - from).normalized;
                    Vector3 side = Vector3.Cross(axis, up).normalized;
                    position = to + (side * Mathf.Cos(angle) + Vector3.Cross(side, axis) * Mathf.Sin(angle)) * radius;
                }
                p.position = position;
                p.velocity = Vector3.zero;
                particles[i] = p;
            }
            digits.SetParticles(particles, count);
        }

        Vector3 Bezier(float t) => Vector3.Lerp(Vector3.Lerp(from, bend, t), Vector3.Lerp(bend, to, t), t);
    }
}
