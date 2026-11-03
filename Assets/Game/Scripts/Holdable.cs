using UnityEngine;

namespace Game
{
    /// <summary>
    /// Something the survivor can pick up with [E] and carry: a heart, a lantern, a tool, a balloon, a
    /// small made thing. While held it rides in their hands and touches nothing; set down, it falls
    /// again. Heavy things slow them down. Something still frozen in can't be taken.
    /// </summary>
    public class Holdable : MonoBehaviour
    {
        [Tooltip("What it is called when the survivor looks at it.")] public string Title = "a thing";
        [Tooltip("Kilograms; above 60 it is too heavy to carry.")] public float Mass = 5f;
        [Tooltip("Where the hands hold it, in its own space.")] public Vector3 Grip;
        [Tooltip("Held in one hand at the side (a tool), or in both in front (a heart).")] public bool OneHand;
        public bool Locked { get; set; }
        public bool Held { get; private set; }

        Rigidbody body;
        Collider[] colliders;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            colliders = GetComponentsInChildren<Collider>();
        }

        public bool CanHold => !Locked && !Held && Mass <= 60f;

        public void Hold(Transform hand)
        {
            Held = true;
            if (!body) body = GetComponent<Rigidbody>();
            colliders = GetComponentsInChildren<Collider>();
            if (body) body.isKinematic = true;
            foreach (var c in colliders) if (!c.isTrigger) c.enabled = false;
            transform.SetParent(hand, true);
            transform.localRotation = Quaternion.identity;
            transform.localPosition = -Grip;
        }

        /// <summary>Lets go of it at a point, upright, facing a way.</summary>
        public void Drop(Vector3 at, Quaternion facing)
        {
            Held = false;
            transform.SetParent(null, true);
            transform.SetPositionAndRotation(at, facing);
            foreach (var c in colliders) if (c) c.enabled = true;
            if (body && !GetComponent<Floater>()) body.isKinematic = false;
        }
    }
}
