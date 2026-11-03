using UnityEngine;

namespace Game
{
    /// <summary>A pad that throws whoever lands on it or steps onto it high into the air.</summary>
    public class Spring : MonoBehaviour
    {
        [Tooltip("Metres a second upward.")] public float Speed = 13f;
        [SerializeField] Transform pad;
        public AudioClip BoingClip;

        float squash;

        public void Thrown()
        {
            squash = 1f;
            if (BoingClip) AudioSource.PlayClipAtPoint(BoingClip, transform.position, 0.8f);
        }

        void Update()
        {
            if (!pad) return;
            squash = Mathf.MoveTowards(squash, 0f, Time.deltaTime * 3f);
            float s = 1f - 0.35f * Mathf.Sin(squash * Mathf.PI) * squash;
            pad.localScale = new Vector3(pad.localScale.x, Mathf.Max(0.05f, padHeight * s), pad.localScale.z);
        }

        float padHeight;
        public void SetPad(Transform t)
        {
            pad = t;
            padHeight = t.localScale.y;
        }
    }
}
