using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game
{
    /// <summary>
    /// Spark's holograms, drawn by Muse Image. Asked to show something, it searches its archive (a
    /// flickering cone from its eye) and then projects the picture as light, floating in front of the
    /// survivor, built up from the bottom like a scan. Asked what a place looked like before, it takes
    /// the survivor's own view and has it redrawn in the old days, then lays that over the world itself
    /// for a few seconds, as if the cold had never come.
    /// </summary>
    public class Hologram : MonoBehaviour
    {
        [SerializeField] Material material;
        [SerializeField] Material lineMaterial;
        [SerializeField] AudioSource sound;
        [SerializeField] AudioClip onClip;
        [SerializeField] float hold = 11f;

        public bool Busy { get; private set; }
        public bool Showing { get; private set; }

        Renderer panel;
        Material panelMaterial;
        LineRenderer[] rays;
        Texture2D picture;
        float stopAt;

        static readonly int RevealId = Shader.PropertyToID("_Reveal");
        static readonly int FlickerId = Shader.PropertyToID("_Flicker");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        void Awake()
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Hologram Panel";
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(transform, false);
            panel = quad.GetComponent<Renderer>();
            panel.shadowCastingMode = ShadowCastingMode.Off;
            panel.receiveShadows = false;
            panelMaterial = new Material(material);
            panel.sharedMaterial = panelMaterial;
            panel.enabled = false;
            rays = new LineRenderer[4];
            for (int i = 0; i < 4; i++)
            {
                var ray = new GameObject("Ray").AddComponent<LineRenderer>();
                ray.transform.SetParent(transform, false);
                ray.sharedMaterial = lineMaterial;
                ray.positionCount = 2;
                ray.widthMultiplier = 0.012f;
                ray.shadowCastingMode = ShadowCastingMode.Off;
                ray.enabled = false;
                rays[i] = ray;
            }
        }

        /// <summary>
        /// Asks Muse Image for the picture and projects it from the eye at a point in front of the
        /// survivor. With a view (a JPEG), the view is redrawn as the words say and laid over the screen.
        /// Calls back with null when shown, or the reason it could not be.
        /// </summary>
        public IEnumerator Show(string words, byte[] view, Drone drone, Transform survivor, Transform viewer, Hud hud, Action<string> done)
        {
            Busy = true;
            string prompt = view != null
                ? "The same view, the same place, the same camera angle and framing, redrawn as the words say: " + words +
                  ". Keep every hill, cliff and building where it is in the picture. Photographic, natural light, no text, no interface, no people's faces close up."
                : "A hologram projected in the dark: " + words + ". Glowing translucent cyan and white light, fine scan lines, the subject alone and centred, " +
                  "on a pure black background; no robot, no projector, no frame, no text or labels.";
            byte[] bytes = null;
            string error = null;
            float began = Time.realtimeSinceStartup;
            // While it searches: a flicker of light from its eye, sweeping.
            var searching = StartCoroutine(Search(drone));
            yield return MetaApi.Image(prompt, view, view != null ? "1536x864" : "1024x1024", 60f, (b, e) => { bytes = b; error = e; });
            StopCoroutine(searching);
            drone.Beam(null);
            Debug.Log($"Hologram (muse-image-1.0, {Time.realtimeSinceStartup - began:F1} s{(view != null ? ", from the view" : "")}): \"{words}\" -> {(bytes != null ? bytes.Length / 1024 + " KB" : error)}");
            if (bytes == null)
            {
                Busy = false;
                done(error ?? "no picture");
                yield break;
            }
            if (!picture) picture = new Texture2D(2, 2, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Clamp, name = "Hologram" };
            if (!picture.LoadImage(bytes))
            {
                Busy = false;
                done("the picture was unreadable");
                yield break;
            }
            if (sound && onClip) sound.PlayOneShot(onClip, 0.6f);
            done(null);
            if (view != null) yield return Overlay(hud);
            else yield return Project(drone, survivor, viewer);
            Busy = false;
        }

        /// <summary>Takes it away early (the survivor walked off, or something happened).</summary>
        public void Stop() => stopAt = Mathf.Min(stopAt, Time.time);

        IEnumerator Search(Drone drone)
        {
            for (float t = 0f; ; t += Time.deltaTime)
            {
                Vector3 eye = drone.transform.position;
                Vector3 ahead = drone.transform.forward;
                Vector3 sweep = Quaternion.AngleAxis(Mathf.Sin(t * 3f) * 25f, Vector3.up) * ahead;
                drone.Beam(Mathf.Sin(t * 23f) > -0.2f ? eye + sweep * 2.5f + Vector3.down * 0.6f : (Vector3?)null, new Color(0.45f, 0.9f, 1f));
                yield return null;
            }
        }

        /// <summary>The picture floating in front of the survivor, at eye height, projected from the drone.</summary>
        IEnumerator Project(Drone drone, Transform survivor, Transform viewer)
        {
            Showing = true;
            Vector3 forward = Vector3.ProjectOnPlane(viewer.forward, Vector3.up).normalized;
            Vector3 centre = survivor.position + forward * 4.6f + Vector3.up * 1.9f;
            float size = 3.2f;
            panel.transform.position = centre;
            panel.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            panel.transform.localScale = new Vector3(size, size * picture.height / Mathf.Max(1f, picture.width), 1f);
            panelMaterial.SetTexture(MainTexId, picture);
            panel.enabled = true;
            foreach (var ray in rays) ray.enabled = true;
            stopAt = Time.time + hold;
            // Just ahead of the survivor's shoulder, so the light fans out from it toward the picture.
            drone.GoTo = survivor.position + forward * 1.1f + Vector3.Cross(Vector3.up, forward) * 0.9f + Vector3.up * 1.8f;
            drone.LookAt = centre;
            for (float t = 0f; Time.time < stopAt + 0.9f; t += Time.deltaTime)
            {
                float appear = Mathf.Clamp01(t / 1.4f);
                float gone = Mathf.Clamp01((Time.time - stopAt) / 0.9f);
                panelMaterial.SetFloat(RevealId, appear * (1f - gone));
                panelMaterial.SetFloat(FlickerId, Mathf.Max(1f - appear, gone, Mathf.PerlinNoise(t * 3f, 0f) > 0.82f ? 0.6f : 0f));
                Vector3 eye = drone.Eye ? drone.Eye.transform.position : drone.transform.position;
                var extent = panel.transform.localScale * 0.5f;
                Vector3 right = panel.transform.right * extent.x, up = panel.transform.up * extent.y;
                Vector3[] corners = { centre - right - up, centre + right - up, centre + right + up, centre - right + up };
                for (int i = 0; i < 4; i++)
                {
                    rays[i].SetPosition(0, eye);
                    rays[i].SetPosition(1, corners[i]);
                    var c = new Color(0.45f, 0.9f, 1f, 0.22f * appear * (1f - gone));
                    rays[i].startColor = c;
                    rays[i].endColor = new Color(c.r, c.g, c.b, c.a * 0.3f);
                }
                if (Vector3.Distance(survivor.position, centre) > 14f) Stop();
                yield return null;
            }
            panel.enabled = false;
            foreach (var ray in rays) ray.enabled = false;
            drone.Release();
            Showing = false;
        }

        /// <summary>The redrawn view laid over the world, flickering in and out.</summary>
        IEnumerator Overlay(Hud hud)
        {
            Showing = true;
            stopAt = Time.time + hold * 0.75f;
            hud.Hologram(picture, 0f);
            for (float t = 0f; Time.time < stopAt + 1.2f; t += Time.deltaTime)
            {
                float appear = Mathf.Clamp01(t / 1.6f);
                float gone = Mathf.Clamp01((Time.time - stopAt) / 1.2f);
                float flicker = appear < 1f || gone > 0f ? (Mathf.PerlinNoise(t * 18f, 1f) > 0.45f ? 1f : 0.55f) : 1f;
                hud.Hologram(picture, Mathf.SmoothStep(0f, 1f, appear) * (1f - gone) * flicker);
                yield return null;
            }
            hud.Hologram(null, 0f);
            Showing = false;
        }
    }
}
