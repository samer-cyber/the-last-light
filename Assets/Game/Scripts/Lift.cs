using UnityEngine;

namespace Game
{
    /// <summary>
    /// A platform that carries whoever stands on it up to the top, waits, and comes back down once
    /// they have stepped off. <see cref="Delta"/> is how far it moved this frame, so riders move with it.
    /// </summary>
    public class Lift : MonoBehaviour
    {
        [SerializeField] Transform platform;
        public float Bottom, Top;
        [SerializeField] float speed = 1.6f;
        public AudioSource Sound;

        public Vector3 Delta { get; private set; }
        /// <summary>Someone stands on it (the survivor tells it each frame).</summary>
        public bool Ridden { get; set; }

        float idle;
        Vector3 last;

        public Transform Platform => platform;

        public void Set(Transform p, float bottom, float top)
        {
            platform = p;
            Bottom = bottom;
            Top = top;
            last = p.position;
        }

        void LateUpdate()
        {
            if (!platform) return;
            float dt = Time.deltaTime;
            idle = Ridden ? 0f : idle + dt;
            float goal = Ridden ? Top : idle > 2.5f ? Bottom : platform.position.y;
            Vector3 p = platform.position;
            float before = p.y;
            p.y = Mathf.MoveTowards(p.y, goal, speed * dt);
            platform.position = p;
            if (Sound)
            {
                bool moving = Mathf.Abs(p.y - before) > 0.0001f;
                if (moving && !Sound.isPlaying) Sound.Play();
                else if (!moving && Sound.isPlaying) Sound.Stop();
            }
            Delta = platform.position - last;
            last = platform.position;
            Ridden = false;
        }
    }
}
