using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The entity's voice in the sky: stars that light up as a giant 1 (yes) or 0 (no), drawn star by
    /// star and joined like a constellation, or a shooting star when it cannot say. The figure is put
    /// in front of the player, clear of the planets and the Sun, far behind everything, and the
    /// ordinary stars behind it dim while it shows.
    /// </summary>
    public class SkyAnswer : MonoBehaviour
    {
        static readonly int RevealId = Shader.PropertyToID("_Reveal");
        static readonly int FadeId = Shader.PropertyToID("_Fade");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int GlyphDirectionId = Shader.PropertyToID("_GlyphDirection");
        static readonly int GlyphDimId = Shader.PropertyToID("_GlyphDim");

        /// <summary>A sphere the figure must not hide behind, with extra room around it in degrees.</summary>
        [Serializable]
        public struct Blocker
        {
            public Transform body;
            public float margin;
        }

        [SerializeField] Camera view;
        [SerializeField, Tooltip("Draws the figure; its mesh is made for each answer.")] MeshRenderer figure;
        [SerializeField] LineRenderer meteor;
        [SerializeField] AudioSource sound;
        [SerializeField] AudioClip yesClip;
        [SerializeField] AudioClip noClip;
        [SerializeField] AudioClip meteorClip;
        [SerializeField] Blocker[] blockers;
        [SerializeField] float distance = 5000f;
        [SerializeField, Tooltip("How much of the sky the figure spans, in degrees.")] float height = 24f;
        [SerializeField] Color yesColour = new Color(1f, 0.85f, 0.6f);
        [SerializeField] Color noColour = new Color(0.6f, 0.74f, 1f);
        [SerializeField, Tooltip("Seconds the figure stays once drawn.")] float hold = 3.2f;

        // Strokes in a box one unit high, centred: the points a hand passes through, in order.
        static readonly Vector2[][] One =
        {
            new[] { new Vector2(-0.2f, 0.3f), new Vector2(0f, 0.5f), new Vector2(0f, 0.17f), new Vector2(0f, -0.17f), new Vector2(0f, -0.5f) },
            new[] { new Vector2(-0.21f, -0.5f), new Vector2(0f, -0.5f), new Vector2(0.21f, -0.5f) },
        };
        static readonly Vector2[][] Zero = { Ellipse(0.3f, 0.5f, 12) };

        static readonly int GatherId = Shader.PropertyToID("_StarGather");

        public bool Busy { get; private set; }

        Mesh mesh;
        Material material;
        readonly System.Random scatter = new System.Random(5);
        float gather, gathering;

        /// <summary>While the entity thinks, the stars shimmer, so the player sees it heard.</summary>
        public void Gather(bool on) => gathering = on ? 1f : 0f;

        void Update()
        {
            gather = Mathf.MoveTowards(gather, gathering, Time.deltaTime * 1.5f);
            Shader.SetGlobalFloat(GatherId, gather);
        }

        void Awake()
        {
            mesh = new Mesh { name = "Answer" };
            mesh.MarkDynamic();
            figure.GetComponent<MeshFilter>().sharedMesh = mesh;
            material = figure.material;
            figure.enabled = false;
            meteor.enabled = false;
            Shader.SetGlobalFloat(GlyphDimId, 0f);
        }

        void OnDestroy() => Shader.SetGlobalFloat(GlyphDimId, 0f);

        void LateUpdate()
        {
            // The figure keeps its place in the sky wherever the player drifts.
            figure.transform.position = view.transform.position;
        }

        /// <summary>Shows an answer; the coroutine ends once it has faded.</summary>
        public IEnumerator Show(Sign sign)
        {
            while (Busy) yield return null;
            Busy = true;
            if (sign == Sign.Unknowable) yield return Meteor();
            else yield return Figure(sign == Sign.Yes);
            Busy = false;
        }

        IEnumerator Figure(bool yes)
        {
            Vector3 direction = Clear();
            Vector3 up = Vector3.ProjectOnPlane(view.transform.up, direction).normalized;
            Vector3 right = Vector3.Cross(up, direction).normalized;
            Build(yes ? One : Zero, direction, right, up);
            material.SetColor(ColorId, yes ? yesColour : noColour);
            material.SetFloat(FadeId, 1f);
            material.SetFloat(RevealId, 0f);
            figure.enabled = true;
            Shader.SetGlobalVector(GlyphDirectionId, new Vector4(direction.x, direction.y, direction.z, Mathf.Cos(height * 0.62f * Mathf.Deg2Rad)));
            if (sound) sound.PlayOneShot(yes ? yesClip : noClip);

            const float draw = 1.1f, fade = 1.6f;
            for (float t = 0f; t < draw; t += Time.deltaTime)
            {
                material.SetFloat(RevealId, t / draw);
                Shader.SetGlobalFloat(GlyphDimId, Mathf.Clamp01(2f * t / draw));
                yield return null;
            }
            material.SetFloat(RevealId, 2f);
            Shader.SetGlobalFloat(GlyphDimId, 1f);
            yield return new WaitForSeconds(hold);
            for (float t = 0f; t < fade; t += Time.deltaTime)
            {
                float k = 1f - t / fade;
                material.SetFloat(FadeId, k * k);
                Shader.SetGlobalFloat(GlyphDimId, k);
                yield return null;
            }
            figure.enabled = false;
            Shader.SetGlobalFloat(GlyphDimId, 0f);
        }

        IEnumerator Meteor()
        {
            Vector3 centre = Clear();
            Vector3 up = Vector3.ProjectOnPlane(view.transform.up, centre).normalized;
            Vector3 right = Vector3.Cross(up, centre).normalized;
            float side = scatter.NextDouble() < 0.5 ? -1f : 1f;
            Vector3 from = (centre + right * (0.42f * side) + up * 0.22f).normalized;
            Vector3 to = (centre - right * (0.38f * side) - up * 0.12f).normalized;
            if (sound && meteorClip) sound.PlayOneShot(meteorClip);
            meteor.enabled = true;
            const int points = 16;
            const float flight = 0.95f, after = 0.35f;
            meteor.positionCount = points;
            for (float t = 0f; t < flight + after; t += Time.deltaTime)
            {
                float head = Mathf.Clamp01(t / flight);
                head = 1f - (1f - head) * (1f - head);
                float tail = Mathf.Clamp01((t - 0.3f) / flight);
                for (int i = 0; i < points; i++)
                {
                    float s = Mathf.Lerp(tail, head, i / (points - 1f));
                    meteor.SetPosition(i, view.transform.position + Vector3.Slerp(from, to, s) * (distance * 0.8f));
                }
                float alpha = t < flight ? 1f : 1f - (t - flight) / after;
                meteor.startColor = meteor.endColor = new Color(1f, 1f, 1f, alpha);
                yield return null;
            }
            meteor.enabled = false;
        }

        /// <summary>A direction in front of the player where the figure is not hidden by a planet or lost in the Sun.</summary>
        Vector3 Clear()
        {
            Transform eye = view.transform;
            float half = height * 0.55f;
            var tries = new[] { (8f, 0f), (20f, 0f), (8f, -26f), (8f, 26f), (26f, -22f), (26f, 22f), (34f, 0f), (-6f, -30f), (-6f, 30f) };
            foreach (var (pitch, yaw) in tries)
            {
                Vector3 direction = Quaternion.AngleAxis(yaw, eye.up) * Quaternion.AngleAxis(-pitch, eye.right) * eye.forward;
                if (IsClear(direction, half)) return direction;
            }
            return Quaternion.AngleAxis(-8f, eye.right) * eye.forward;
        }

        bool IsClear(Vector3 direction, float half)
        {
            foreach (var blocker in blockers)
            {
                if (!blocker.body || !blocker.body.gameObject.activeInHierarchy) continue;
                Vector3 to = blocker.body.position - view.transform.position;
                float radius = blocker.body.lossyScale.x * 0.5f;
                float angular = Mathf.Asin(Mathf.Clamp01(radius / Mathf.Max(to.magnitude, radius))) * Mathf.Rad2Deg;
                if (Vector3.Angle(direction, to) < angular + half + blocker.margin) return false;
            }
            return true;
        }

        /// <summary>
        /// The figure's mesh, in the plane facing the player: a quad for each star, slightly out of
        /// place as stars would be, and a thin quad for each line between them.
        /// </summary>
        void Build(Vector2[][] strokes, Vector3 direction, Vector3 right, Vector3 up)
        {
            float unit = 2f * distance * Mathf.Tan(height * 0.5f * Mathf.Deg2Rad);
            Vector3 centre = direction * distance;
            Vector3 At(Vector2 p) => centre + (right * p.x + up * p.y) * unit;

            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var timing = new List<Vector2>();
            var colours = new List<Color>();
            var triangles = new List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2[] corners, float order, float kind, float bright)
            {
                int first = vertices.Count;
                vertices.AddRange(new[] { a, b, c, d });
                uvs.AddRange(corners);
                for (int i = 0; i < 4; i++)
                {
                    timing.Add(new Vector2(order, kind));
                    colours.Add(new Color(1f, 1f, 1f, bright));
                }
                triangles.AddRange(new[] { first, first + 2, first + 1, first + 2, first + 3, first + 1 });
            }

            var placed = new List<(Vector2 at, Vector2 shown)>();
            int total = strokes.Sum(s => s.Length), index = 0;
            float star = 0.03f, line = 0.004f;
            foreach (var stroke in strokes)
            {
                Vector2 previous = default;
                for (int i = 0; i < stroke.Length; i++, index++)
                {
                    float order = 0.92f * index / total;
                    // A point two strokes share is one star.
                    Vector2 point = stroke[i];
                    int found = placed.FindIndex(p => (p.at - point).sqrMagnitude < 0.0004f);
                    Vector2 shown;
                    if (found >= 0) shown = placed[found].shown;
                    else
                    {
                        shown = stroke[i] + new Vector2(Jitter(), Jitter()) * 0.018f;
                        placed.Add((stroke[i], shown));
                        float size = star * (0.8f + 0.5f * (float)scatter.NextDouble());
                        Vector3 p = At(shown);
                        Vector3 r = right * (size * unit), u = up * (size * unit);
                        Quad(p - r - u, p + r - u, p - r + u, p + r + u,
                            new[] { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f) },
                            order, 0f, 0.8f + 0.4f * (float)scatter.NextDouble());
                    }
                    if (i > 0)
                    {
                        // A line from the last star to this one, stopping short of both.
                        Vector2 along = (shown - previous).normalized;
                        Vector3 a = At(previous + along * star * 0.9f), b = At(shown - along * star * 0.9f);
                        Vector3 across = Vector3.Cross(direction, (b - a).normalized) * (line * unit);
                        float lineOrder = 0.92f * (index - 1) / total;
                        Quad(a - across, a + across, b - across, b + across,
                            new[] { new Vector2(0f, -1f), new Vector2(0f, 1f), new Vector2(1f, -1f), new Vector2(1f, 1f) },
                            lineOrder, 1f, 1f);
                    }
                    previous = shown;
                }
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetUVs(1, timing);
            mesh.SetColors(colours);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }

        float Jitter() => (float)(scatter.NextDouble() * 2.0 - 1.0);

        static Vector2[] Ellipse(float rx, float ry, int points)
        {
            // From the top, around to the left first, as a hand writes a 0; back to the top to close it.
            var ring = new Vector2[points + 1];
            for (int i = 0; i <= points; i++)
            {
                float a = Mathf.PI * 0.5f + 2f * Mathf.PI * i / points;
                ring[i] = new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
            }
            ring[points] = ring[0];
            return ring;
        }
    }
}
