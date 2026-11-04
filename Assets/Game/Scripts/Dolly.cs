using UnityEngine;

namespace Game
{
    /// <summary>
    /// A moving camera for filming: it glides from one place to another (or round a point), looking from one point
    /// to another, eased, over a number of game seconds, then holds there until the shot ends or another begins.
    /// </summary>
    public class Dolly : MonoBehaviour
    {
        FollowCamera follow;
        Vector3 from0, from1, at0, at1, centre;
        float seconds, t, radius, high, angle0, angle1;
        bool orbit;
        static Dolly current;

        public static Dolly Move(FollowCamera follow, Vector3 from0, Vector3 from1, Vector3 at0, Vector3 at1, float seconds)
        {
            var d = Begin(follow);
            d.from0 = from0; d.from1 = from1; d.at0 = at0; d.at1 = at1; d.seconds = Mathf.Max(0.01f, seconds);
            d.Place(0f);
            return d;
        }

        /// <summary>Round a point at a radius and height, from one angle (degrees, 0 is north) to another, looking at it.</summary>
        public static Dolly Orbit(FollowCamera follow, Vector3 centre, float radius, float high, float angle0, float angle1, float seconds)
        {
            var d = Begin(follow);
            d.orbit = true; d.centre = centre; d.radius = radius; d.high = high; d.angle0 = angle0; d.angle1 = angle1; d.seconds = Mathf.Max(0.01f, seconds);
            d.Place(0f);
            return d;
        }

        static Dolly Begin(FollowCamera follow)
        {
            if (current) Destroy(current.gameObject);
            current = new GameObject("Dolly").AddComponent<Dolly>();
            current.follow = follow;
            return current;
        }

        public static void Stop()
        {
            if (current) Destroy(current.gameObject);
            current = null;
        }

        void Update()
        {
            t += Time.deltaTime;
            Place(Mathf.Clamp01(t / seconds));
        }

        void Place(float k)
        {
            float s = k * k * (3f - 2f * k);
            if (orbit)
            {
                float a = Mathf.Lerp(angle0, angle1, s) * Mathf.Deg2Rad;
                Vector3 at = centre + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius + Vector3.up * high;
                follow.Shot(at, centre, 80f);
            }
            else follow.Shot(Vector3.Lerp(from0, from1, s), Vector3.Lerp(at0, at1, s), 80f);
        }
    }
}
