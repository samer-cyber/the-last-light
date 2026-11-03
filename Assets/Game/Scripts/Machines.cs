using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Makes the great minds' machines when the story calls for them: packs of Seekers, built from
    /// simple shapes with the scene's materials (a black shell, three fins, one red eye).
    /// </summary>
    public class Machines : MonoBehaviour
    {
        [SerializeField] Material shell, eyeGlow, fin;
        [SerializeField] Material line, spark, charge;
        [SerializeField] AudioClip humClip, fireClip, chargeClip, hurtClip, breakClip;

        public Material Charge => charge;
        int made;

        void Awake()
        {
            Fx.Use(line, spark);
            // Shots pass through Spark and the ignore-raycast layer.
            Shots.Mask = ~(1 << LayerMask.NameToLayer("Drone")) & ~(1 << 2);
        }

        /// <summary>A pack of Seekers around a point, on patrol there.</summary>
        public List<Seeker> Seekers(Vector3 around, int count, float spread = 8f)
        {
            var pack = new List<Seeker>();
            for (int i = 0; i < count; i++)
            {
                Vector3 at = around + Quaternion.Euler(0f, 360f * i / count, 0f) * Vector3.forward * spread + Vector3.up * 3f;
                pack.Add(Seeker(at));
            }
            return pack;
        }

        public Seeker Seeker(Vector3 at)
        {
            var root = new GameObject("Seeker");
            root.transform.position = at;
            root.SetActive(false);
            var body = Part(root.transform, PrimitiveType.Sphere, shell, Vector3.zero, new Vector3(0.62f, 0.5f, 0.72f));
            body.name = "Shell";
            for (int i = 0; i < 3; i++)
            {
                var f = Part(root.transform, PrimitiveType.Cube, fin, Vector3.zero, new Vector3(0.05f, 0.5f, 0.3f));
                f.transform.localRotation = Quaternion.Euler(0f, 0f, 120f * i);
                f.transform.localPosition = f.transform.localRotation * Vector3.up * 0.3f + Vector3.back * 0.12f;
                Object.Destroy(f.GetComponent<Collider>());
            }
            var eye = Part(root.transform, PrimitiveType.Sphere, eyeGlow, new Vector3(0f, 0f, 0.33f), Vector3.one * 0.17f);
            eye.name = "Eye";
            Object.Destroy(eye.GetComponent<Collider>());
            var light = new GameObject("Eye Light").AddComponent<Light>();
            light.transform.SetParent(eye.transform, false);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.2f, 0.12f);
            light.range = 7f;
            light.intensity = 1.6f;
            var audio = root.AddComponent<AudioSource>();
            audio.spatialBlend = 1f;
            audio.minDistance = 3f;
            audio.maxDistance = 45f;
            var hum = root.AddComponent<AudioSource>();
            hum.clip = humClip;
            hum.loop = true;
            hum.spatialBlend = 1f;
            hum.minDistance = 2f;
            hum.maxDistance = 30f;
            hum.volume = 0.35f;
            var seeker = root.AddComponent<Seeker>();
            Set(seeker, "eye", eye.transform);
            Set(seeker, "eyeLight", light);
            Set(seeker, "eyeRenderer", eye.GetComponent<Renderer>());
            Set(seeker, "hum", hum);
            Set(seeker, "fireClip", fireClip);
            Set(seeker, "chargeClip", chargeClip);
            Set(seeker, "weakPoint", eye.transform);
            Set(seeker, "sound", audio);
            Set(seeker, "hurtClip", hurtClip);
            Set(seeker, "breakClip", breakClip);
            seeker.Id = "SEEKER_" + (++made);
            root.SetActive(true);
            if (humClip) hum.Play();
            return seeker;
        }

        static GameObject Part(Transform parent, PrimitiveType type, Material material, Vector3 at, Vector3 size)
        {
            var part = GameObject.CreatePrimitive(type);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = at;
            part.transform.localScale = size;
            if (material) part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }

        // Fields are serialized on the machine, private: set them by name, as the scene builder does.
        static void Set(Object target, string field, object value)
        {
            var info = target.GetType().GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                       ?? target.GetType().BaseType?.GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            info?.SetValue(target, value);
        }
    }
}
