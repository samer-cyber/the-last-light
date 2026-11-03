using UnityEngine;

namespace Game
{
    /// <summary>
    /// Lighter than air: it hangs a little above the ground and bobs. Held by the survivor, it lifts
    /// them, hanging upright from their hand; let go, it drifts where it was dropped.
    /// </summary>
    public class Floater : MonoBehaviour
    {
        [Tooltip("Metres a second it lifts whoever holds it.")] public float Lift = 1.6f;
        [Tooltip("Hangs this high over the ground when free.")] public float Hover = 2.2f;

        Vector3 anchor;
        bool anchored;
        Holdable holdable;

        void Awake() => holdable = GetComponent<Holdable>();

        void LateUpdate()
        {
            float t = Time.time + GetInstanceID() * 0.37f;
            if (holdable && holdable.Held)
            {
                anchored = false;
                // Upright above the hand that holds the cord, swaying a little.
                var hand = transform.parent;
                var sway = Quaternion.Euler(Mathf.Sin(t * 1.3f) * 4f, 0f, Mathf.Cos(t * 1.1f) * 4f);
                transform.rotation = sway;
                if (hand) transform.position = hand.position - sway * holdable.Grip;
                return;
            }
            if (!anchored)
            {
                anchor = transform.position;
                if (Physics.Raycast(anchor + Vector3.up * 0.5f, Vector3.down, out var hit, 60f, ~0, QueryTriggerInteraction.Ignore))
                    anchor.y = Mathf.Max(anchor.y, hit.point.y + Hover);
                anchored = true;
            }
            Vector3 bob = new Vector3(Mathf.Sin(t * 0.7f) * 0.15f, Mathf.Sin(t * 1.1f) * 0.2f, Mathf.Cos(t * 0.6f) * 0.15f);
            transform.position = Vector3.Lerp(transform.position, anchor + bob, 1f - Mathf.Exp(-2f * Time.deltaTime));
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(Mathf.Sin(t) * 5f, transform.eulerAngles.y, Mathf.Cos(t * 0.8f) * 5f), Time.deltaTime);
        }
    }
}
