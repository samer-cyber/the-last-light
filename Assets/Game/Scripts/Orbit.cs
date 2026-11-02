using UnityEngine;

namespace Game
{
    /// <summary>Carries a body around a centre in a circle, counter-clockwise seen from above, faster while time is hurried.</summary>
    public class Orbit : MonoBehaviour
    {
        [SerializeField] Transform centre;
        [SerializeField] float radius = 40f;
        [SerializeField, Tooltip("Seconds per turn.")] float period = 90f;
        [SerializeField] Vector3 axis = Vector3.up;
        [SerializeField, Tooltip("Degrees around the orbit, from the centre's forward.")] float angle;

        public float Hurry { get; set; } = 1f;
        public float Radius { get => radius; set => radius = value; }

        Vector3 Start0 => Vector3.ProjectOnPlane(Mathf.Abs(Vector3.Dot(axis.normalized, Vector3.forward)) > 0.9f ? Vector3.right : Vector3.forward, axis).normalized;

        /// <summary>Takes up the orbit from where the body is now.</summary>
        public void HoldFrom(Vector3 position)
        {
            Vector3 offset = Vector3.ProjectOnPlane(position - centre.position, axis);
            radius = offset.magnitude;
            angle = -Vector3.SignedAngle(Start0, offset, axis);
        }

        void LateUpdate()
        {
            angle += 360f / period * Hurry * Time.deltaTime;
            transform.position = centre.position + Quaternion.AngleAxis(-angle, axis) * Start0 * radius;
        }
    }
}
