using System;
using UnityEngine;

namespace Game
{
    /// <summary>The mirror's empty socket: a heart set in it (by hand, or by the drone) fills it.</summary>
    public class Socket : MonoBehaviour
    {
        [SerializeField] Transform seat;
        [SerializeField] Renderer glow;
        public event Action Filled;
        public bool Full { get; private set; }
        public Vector3 Seat => seat ? seat.position : transform.position;

        public void Set(Transform s, Renderer g)
        {
            seat = s;
            glow = g;
        }

        public void Fill(Holdable heart)
        {
            if (Full || !heart) return;
            Full = true;
            heart.Locked = true;
            heart.transform.SetParent(seat ? seat : transform, true);
            heart.transform.localPosition = Vector3.zero;
            heart.transform.localRotation = Quaternion.identity;
            var body = heart.GetComponent<Rigidbody>();
            if (body) body.isKinematic = true;
            foreach (var c in heart.GetComponentsInChildren<Collider>()) c.enabled = false;
            if (glow) glow.enabled = true;
            Filled?.Invoke();
        }
    }
}
