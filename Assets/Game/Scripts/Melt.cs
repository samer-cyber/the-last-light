using UnityEngine;

namespace Game
{
    /// <summary>
    /// Snow or ice that Spark made melts near anything hot: it slumps and shrinks, faster the closer the heat, and
    /// when it is gone it goes back into the snow. A snowman beside a campfire does not last.
    /// </summary>
    public class Melt : MonoBehaviour
    {
        [Tooltip("How much of it heat takes away per second, for each unit of heat.")] public float Rate = 0.02f;

        Built built;
        Maker maker;
        Vector3 full;
        float melted;
        bool gone;

        /// <summary>How much of it has melted, 0 to 1.</summary>
        public float Melted => melted;

        void Start()
        {
            built = GetComponent<Built>();
            maker = FindFirstObjectByType<Maker>();
            full = transform.localScale;
        }

        void Update()
        {
            if (gone) return;
            var holdable = GetComponent<Holdable>();
            if (holdable && holdable.Held) return;
            float heat = Heat.Total(built ? built.Centre : transform.position);
            if (heat < 0.05f) return;
            float before = melted;
            melted = Mathf.Min(1f, melted + heat * Rate * Time.deltaTime);
            // It slumps more than it shrinks: lower and a little wider at first, then smaller all over.
            float k = melted;
            var scale = new Vector3(full.x * (1f + 0.15f * k - 0.75f * k * k), full.y * (1f - 0.85f * k), full.z * (1f + 0.15f * k - 0.75f * k * k));
            // Kept on the ground as it sinks.
            var body = GetComponent<Rigidbody>();
            if (body && !body.isKinematic) body.WakeUp();
            transform.localScale = Vector3.Max(scale, full * 0.05f);
            if (melted >= 1f && before < 1f)
            {
                gone = true;
                if (maker && built) StartCoroutine(maker.Unmake(built));
                else Destroy(gameObject);
            }
        }
    }
}
