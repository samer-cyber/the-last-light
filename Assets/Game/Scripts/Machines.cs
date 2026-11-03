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
        [SerializeField] Material shieldGlow;
        [SerializeField] AudioClip stepClip, bigChargeClip, beamClip, ventClip, bigBreakClip, voiceClip;

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

        /// <summary>
        /// The Harvester: an angular hull on three long legs, one red eye in front, a vent on its back,
        /// a cage under its belly with the heart in it, and a red shield round it all.
        /// </summary>
        public Harvester Harvester(Vector3 at, Transform survivor, Holdable heart)
        {
            var root = new GameObject("Harvester");
            root.transform.position = at;
            root.SetActive(false);
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            body.localScale = Vector3.one * 1.45f;
            Part(body, PrimitiveType.Cube, shell, new Vector3(0f, 0f, 0f), new Vector3(4.6f, 2.2f, 6.2f));
            Part(body, PrimitiveType.Cube, shell, new Vector3(0f, 1.3f, -0.6f), new Vector3(3.4f, 1.1f, 4f));
            Part(body, PrimitiveType.Cube, shell, new Vector3(0f, -0.2f, 3.4f), new Vector3(2.6f, 1.6f, 1.4f));
            foreach (float side in new[] { -1f, 1f })
                Part(body, PrimitiveType.Cube, fin, new Vector3(side * 2.5f, 0.4f, 0f), new Vector3(0.3f, 1.4f, 5.4f)).transform.localRotation = Quaternion.Euler(0f, 0f, side * 12f);
            var eye = Part(body, PrimitiveType.Sphere, eyeGlow, new Vector3(0f, -0.1f, 4.15f), Vector3.one * 0.9f);
            eye.name = "Eye";
            Object.Destroy(eye.GetComponent<Collider>());
            var eyeLight = new GameObject("Eye Light").AddComponent<Light>();
            eyeLight.transform.SetParent(eye.transform, false);
            eyeLight.type = LightType.Point;
            eyeLight.color = new Color(1f, 0.2f, 0.12f);
            eyeLight.range = 22f;
            eyeLight.intensity = 3f;
            var vent = Part(body, PrimitiveType.Cube, eyeGlow, new Vector3(0f, 1.95f, -1.6f), new Vector3(1.8f, 0.35f, 1.6f));
            vent.name = "Vent";
            var ventLight = new GameObject("Vent Light").AddComponent<Light>();
            ventLight.transform.SetParent(vent.transform, false);
            ventLight.transform.localPosition = Vector3.up * 2f;
            ventLight.type = LightType.Point;
            ventLight.color = new Color(1f, 0.5f, 0.2f);
            ventLight.range = 14f;
            ventLight.intensity = 0f;
            // The cage under its belly, with the heart in it.
            var cage = new GameObject("Cage").transform;
            cage.SetParent(body, false);
            cage.localPosition = new Vector3(0f, -1.6f, 0.4f);
            for (int i = 0; i < 6; i++)
            {
                var bar = Part(cage, PrimitiveType.Cylinder, fin, Quaternion.Euler(0f, 60f * i, 0f) * Vector3.forward * 0.75f + Vector3.down * 0.5f, new Vector3(0.08f, 0.75f, 0.08f));
                Object.Destroy(bar.GetComponent<Collider>());
            }
            // The shield: a sphere of red shimmer, not solid.
            var shield = Part(root.transform, PrimitiveType.Sphere, null, Vector3.up * 0.2f, Vector3.one * 14f);
            shield.name = "Shield";
            Object.Destroy(shield.GetComponent<Collider>());
            var shieldRenderer = shield.GetComponent<Renderer>();
            if (shieldGlow)
            {
                var m = new Material(shieldGlow);
                m.SetTexture("_MainTex", Hexes());
                m.SetColor("_Tint", new Color(1f, 0.25f, 0.18f));
                m.SetFloat("_Colour", 0f);
                m.SetFloat("_Intensity", 0.35f);
                m.SetFloat("_Reveal", 0f);
                shieldRenderer.sharedMaterial = m;
            }
            shieldRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // Three legs: hips on the hull, thighs and shins placed each frame.
            var hips = new Transform[3];
            var thighs = new Transform[3];
            var shins = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                hips[i] = new GameObject("Hip").transform;
                hips[i].SetParent(body, false);
                hips[i].localPosition = Quaternion.Euler(0f, 120f * i + 60f, 0f) * Vector3.forward * 2f + Vector3.down * 0.5f;
                thighs[i] = Part(root.transform, PrimitiveType.Cylinder, shell, Vector3.zero, Vector3.one).transform;
                shins[i] = Part(root.transform, PrimitiveType.Cylinder, fin, Vector3.zero, Vector3.one).transform;
            }
            var beam = new GameObject("Beam").AddComponent<LineRenderer>();
            beam.transform.SetParent(root.transform, false);
            beam.sharedMaterial = line;
            beam.positionCount = 2;
            beam.useWorldSpace = true;
            beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beam.enabled = false;
            var audio = root.AddComponent<AudioSource>();
            audio.spatialBlend = 1f;
            audio.minDistance = 10f;
            audio.maxDistance = 140f;
            var loop = root.AddComponent<AudioSource>();
            loop.spatialBlend = 1f;
            loop.minDistance = 10f;
            loop.maxDistance = 120f;
            var harvester = root.AddComponent<Harvester>();
            Set(harvester, "body", body);
            Set(harvester, "eye", eye.transform);
            Set(harvester, "vent", vent.transform);
            Set(harvester, "cage", cage);
            Set(harvester, "eyeLight", eyeLight);
            Set(harvester, "ventLight", ventLight);
            Set(harvester, "eyeRenderer", eye.GetComponent<Renderer>());
            Set(harvester, "ventRenderer", vent.GetComponent<Renderer>());
            Set(harvester, "shieldRenderer", shieldRenderer);
            Set(harvester, "beam", beam);
            Set(harvester, "hips", hips);
            Set(harvester, "thighs", thighs);
            Set(harvester, "shins", shins);
            Set(harvester, "loop", loop);
            Set(harvester, "stepClip", stepClip);
            Set(harvester, "chargeClip", bigChargeClip);
            Set(harvester, "beamClip", beamClip);
            Set(harvester, "ventClip", ventClip);
            Set(harvester, "voiceClip", voiceClip);
            Set(harvester, "weakPoint", vent.transform);
            Set(harvester, "sound", audio);
            Set(harvester, "hurtClip", hurtClip);
            Set(harvester, "breakClip", bigBreakClip);
            harvester.Id = "HARVESTER";
            root.SetActive(true);
            harvester.Set(survivor, this, heart);
            return harvester;
        }

        /// <summary>A hex grid of light lines, for the shield.</summary>
        static Texture2D Hexes()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "Shield Hexes" };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x, y) / size * 6f;
                    Vector2 r = new Vector2(1f, 1.7320508f), h = r * 0.5f;
                    Vector2 a = new Vector2(Mod(p.x, r.x) - h.x, Mod(p.y, r.y) - h.y);
                    Vector2 b = new Vector2(Mod(p.x - h.x, r.x) - h.x, Mod(p.y - h.y, r.y) - h.y);
                    Vector2 g = a.sqrMagnitude < b.sqrMagnitude ? a : b;
                    Vector2 q = new Vector2(Mathf.Abs(g.x), Mathf.Abs(g.y));
                    float edge = 0.5f - Mathf.Max(Vector2.Dot(q, new Vector2(0.5f, 0.8660254f)), q.x);
                    float v = 1f - Mathf.Clamp01((edge - 0.02f) / 0.04f);
                    pixels[y * size + x] = new Color(v, v, v, 1f);
                }
            texture.SetPixels(pixels);
            texture.Apply(true);
            return texture;
        }

        static float Mod(float a, float b) => a - b * Mathf.Floor(a / b);

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
