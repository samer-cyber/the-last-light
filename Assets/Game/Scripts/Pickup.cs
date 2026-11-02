using UnityEngine;

namespace Game
{
    /// <summary>
    /// A loose piece of stone, ice or metal lying in the world. Gathered with E, it lifts, flies to the
    /// mind and joins the ring of pieces turning around it (<see cref="Companion.Give"/>).
    /// </summary>
    public class Pickup : MonoBehaviour
    {
        [SerializeField] Matter matter = Matter.Stone;
        [SerializeField, Min(1)] int units = 2;

        static readonly int GlowId = Shader.PropertyToID("_Glow");

        public Matter Matter => matter;
        public int Units => units;
        public bool Taken { get; private set; }

        public void Set(Matter kind, int amount)
        {
            matter = kind;
            units = amount;
        }

        /// <summary>A slow warm glint while the mind wants this matter, so the player can find it.</summary>
        public bool Glinting { get; set; }

        Renderer look;
        MaterialPropertyBlock block;
        float glint;

        void Update()
        {
            glint = Mathf.MoveTowards(glint, Glinting && !Taken ? 1f : 0f, Time.deltaTime * 1.5f);
            if (glint <= 0f && block == null) return;
            if (!look) look = GetComponentInChildren<Renderer>();
            if (glint <= 0f)
            {
                // Back to the material's own look.
                look.SetPropertyBlock(null);
                block = null;
                return;
            }
            block ??= new MaterialPropertyBlock();
            look.GetPropertyBlock(block);
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * 2.6f + transform.position.x);
            block.SetColor(GlowId, new Color(1.6f, 1.05f, 0.6f) * glint * pulse);
            look.SetPropertyBlock(block);
        }

        /// <summary>Hands this piece to the mind; it flies there and is gone.</summary>
        public void Gather(Companion mind)
        {
            if (Taken) return;
            Taken = true;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            var body = GetComponent<Rigidbody>();
            if (body) Destroy(body);
            mind.Give(matter, units, transform);
        }
    }
}
