using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// How the mind makes things. A request becomes a <see cref="Plan"/>: one of a few kinds of thing a
    /// person designed (a bridge, stairs, a ramp, a wall, a pillar, a platform, a block, a ball, a
    /// lamp), fitted to the places named and to the ground between them, as pieces of one matter.
    /// The plan is first drawn in light; then pieces fly out of the mind's ring to their places and
    /// form there. Every piece costs what it is made of; nothing comes from nothing, and taking a thing
    /// apart gives its matter back.
    /// </summary>
    public class Maker : MonoBehaviour
    {
        public enum Shape { Box, Cylinder, Sphere }

        public struct Part
        {
            public Shape Shape;
            public Vector3 Position;
            public Quaternion Rotation;
            /// <summary>Box: its sides; cylinder: (diameter, height, diameter); sphere: (d, d, d).</summary>
            public Vector3 Size;
            /// <summary>Falls and rolls once made.</summary>
            public bool Loose;

            public float Volume => Shape switch
            {
                Shape.Box => Size.x * Size.y * Size.z,
                Shape.Cylinder => Mathf.PI * Size.x * Size.x * 0.25f * Size.y,
                _ => Mathf.PI / 6f * Size.x * Size.x * Size.x,
            };
        }

        public class Plan
        {
            public string Kind;
            public Matter Matter;
            public readonly List<Part> Parts = new List<Part>();
            public int Cost;
            public string Words;
            public Vector3 Centre => Parts.Count == 0 ? Vector3.zero : Parts.Aggregate(Vector3.zero, (sum, p) => sum + p.Position) / Parts.Count;
            /// <summary>A short description for the mind's instructions and the log.</summary>
            public string Summary => $"{Kind.ToLowerInvariant()} of {Matters.Word(Matter)}";
        }

        /// <summary>What a request names, already turned into places in the world.</summary>
        public struct Request
        {
            public string Kind;
            public Matter Matter;
            public Vector3 From;
            public Vector3 To;
            /// <summary>0.6 small, 1 middling, 1.6 large.</summary>
            public float Size;
            /// <summary>Metres asked for, or 0 for the kind's own.</summary>
            public float Height, Length;
        }

        [SerializeField] Mesh cube, cylinder, sphere, shard;
        [SerializeField, Tooltip("Stone, ice, metal, light.")] Material[] matter = new Material[4];
        [SerializeField] Material ghost;
        [SerializeField] Material ideaGhost;
        [SerializeField] PhysicsMaterial slippery;
        [SerializeField] AudioSource sound;
        [SerializeField] AudioClip[] flyClips;
        [SerializeField, Tooltip("Stone, ice, metal, light: a piece locking into place.")] AudioClip[] lockClips;
        [SerializeField] AudioClip doneClip, apartClip, planClip;
        [SerializeField, Tooltip("What the ground is, for finding gaps and slopes.")] LayerMask ground = ~0;
        [SerializeField] float longest = 34f;
        [SerializeField] float tallest = 16f;

        public static readonly string[] Kinds = { "BRIDGE", "STAIRS", "RAMP", "WALL", "PILLAR", "PLATFORM", "BLOCK", "BALL", "LAMP" };

        public IReadOnlyList<Built> Made => made;
        public bool Busy { get; private set; }

        readonly List<Built> made = new List<Built>();
        readonly System.Random random = new System.Random(3);
        static readonly int BuildId = Shader.PropertyToID("_Build");
        static readonly int BuildRangeId = Shader.PropertyToID("_BuildRange");
        static readonly int EdgeWearId = Shader.PropertyToID("_EdgeWear");
        static readonly int SeamId = Shader.PropertyToID("_Seam");
        static readonly int RevealId = Shader.PropertyToID("_Reveal");
        static readonly int RevealRangeId = Shader.PropertyToID("_RevealRange");
        static readonly int WantingId = Shader.PropertyToID("_Wanting");

        // ---- Designing ----

        /// <summary>Fits a kind of thing to the places asked for; null and the reason if it cannot.</summary>
        public Plan Design(Request r, out string problem)
        {
            problem = null;
            var plan = new Plan { Kind = r.Kind, Matter = r.Matter };
            float size = r.Size <= 0f ? 1f : r.Size;
            switch (r.Kind)
            {
                case "BRIDGE": problem = Bridge(plan, r, size); break;
                case "STAIRS": problem = Stairs(plan, r, size); break;
                case "RAMP": problem = Ramp(plan, r, size); break;
                case "WALL": problem = Wall(plan, r, size); break;
                case "PILLAR": problem = Pillar(plan, r, size); break;
                case "PLATFORM": problem = Platform(plan, r, size); break;
                case "BLOCK": problem = Block(plan, r, size); break;
                case "BALL": problem = Ball(plan, r, size); break;
                case "LAMP": problem = Lamp(plan, r, size); break;
                default: problem = "it does not know how to make that"; break;
            }
            if (problem != null || plan.Parts.Count == 0) return null;
            float volume = plan.Parts.Sum(p => p.Volume);
            plan.Cost = Matters.Units(plan.Matter, volume);
            return plan;
        }

        /// <summary>The height of the ground under a point, looking down from above it.</summary>
        public bool GroundAt(Vector3 p, out float height, float from = 30f)
        {
            if (Physics.Raycast(new Vector3(p.x, p.y + from, p.z), Vector3.down, out var hit, from + 60f, ground, QueryTriggerInteraction.Ignore))
            {
                height = hit.point.y;
                return true;
            }
            height = -100f;
            return false;
        }

        string Bridge(Plan plan, Request r, float size)
        {
            Vector3 flat = Flat(r.To - r.From);
            if (flat.magnitude < 1.5f) return "show it where the bridge should go";
            Vector3 dir = flat.normalized;
            GroundAt(r.From, out float startY, 3f);
            float reachLimit = Mathf.Min(flat.magnitude + 6f, longest + 10f);
            // Walk toward the far place until the ground drops away, then until it comes back; once
            // over a gap, keep looking for its far edge even past the place named.
            float nearEdge = -1f, farEdge = -1f, farY = startY;
            for (float d = 0f; d <= (nearEdge < 0f ? reachLimit : nearEdge + longest); d += 0.25f)
            {
                Vector3 p = r.From + dir * d;
                bool any = GroundAt(new Vector3(p.x, startY, p.z), out float y, 6f);
                bool low = !any || y < startY - 1.5f;
                if (nearEdge < 0f)
                {
                    if (low) nearEdge = d;
                }
                else if (!low && y > startY - 3f)
                {
                    farEdge = d;
                    farY = y;
                    break;
                }
            }
            float a, b, ya, yb;
            if (nearEdge < 0f)
            {
                // No gap: a walkway straight to the place.
                a = 0.5f;
                b = flat.magnitude;
                ya = startY;
                GroundAt(r.To, out yb, 4f);
            }
            else
            {
                if (farEdge < 0f) return "there is nothing on the other side to reach";
                a = Mathf.Max(0f, nearEdge - 1.2f);
                b = farEdge + 1.2f;
                GroundAt(r.From + dir * a, out ya, 4f);
                GroundAt(r.From + dir * b, out yb, 4f);
                if (yb < farY - 0.5f) yb = farY;
            }
            float length = b - a;
            if (r.Length > 0f && nearEdge < 0f) length = Mathf.Min(length, r.Length);
            if (length > longest) return "the gap is too wide for one bridge";
            float rise = yb - ya;
            if (Mathf.Abs(rise) / Mathf.Max(length, 0.1f) > 0.5f) return "too steep for a bridge: stairs, maybe";

            float width = 1.6f * Mathf.Clamp(size, 0.7f, 1.6f);
            Vector3 start = r.From + dir * a;
            start.y = ya + 0.05f;
            Vector3 end = r.From + dir * (a + length);
            end.y = yb + 0.05f;
            Vector3 along = end - start;
            var turn = Quaternion.LookRotation(along.normalized, Vector3.up);
            // Planks across it, a little uneven, on two beams.
            int planks = Mathf.Max(2, Mathf.CeilToInt(along.magnitude / 0.5f));
            for (int i = 0; i < planks; i++)
            {
                float t = (i + 0.5f) / planks;
                float jitter = ((float)random.NextDouble() - 0.5f);
                plan.Parts.Add(Box(start + along * t + Vector3.up * jitter * 0.03f,
                    turn * Quaternion.Euler(0f, jitter * 3f, 0f), new Vector3(width + jitter * 0.12f, 0.12f, along.magnitude / planks - 0.04f)));
            }
            foreach (float side in new[] { -1f, 1f })
                plan.Parts.Add(Box(start + along * 0.5f + turn * new Vector3(side * (width * 0.5f - 0.08f), -0.14f, 0f), turn, new Vector3(0.16f, 0.2f, along.magnitude + 0.3f)));
            return null;
        }

        /// <summary>
        /// Where a way up should arrive: walking from the start toward the place named (and a little
        /// past it), the lip where the ground stops rising after it began to, so "the cliff" means its
        /// top edge; its height and its distance along the way.
        /// </summary>
        void Summit(Request r, Vector3 dir, float reachTo, out float topY, out float edge)
        {
            GroundAt(r.From, out float baseY, 3f);
            topY = baseY;
            edge = reachTo;
            if (r.Height > 0f)
            {
                topY = baseY + Mathf.Min(r.Height, tallest);
                return;
            }
            float previous = baseY;
            bool climbing = false;
            for (float d = 0.25f; d <= reachTo + 6f; d += 0.25f)
            {
                if (!GroundAt(r.From + dir * d, out float g, 40f)) continue;
                if (!climbing && g > baseY + 0.6f) climbing = true;
                if (climbing && g - previous < 0.08f)
                {
                    edge = d;
                    topY = g;
                    return;
                }
                previous = g;
            }
            // Never levelled out: the highest point reached.
            if (climbing) topY = previous;
            else if (GroundAt(r.To, out float there, 6f)) topY = Mathf.Max(there, r.To.y);
        }

        string Stairs(Plan plan, Request r, float size)
        {
            Vector3 flat = Flat(r.To - r.From);
            Vector3 dir = flat.magnitude > 0.5f ? flat.normalized : Flat(transform.forward).normalized;
            GroundAt(r.From, out float baseY, 3f);
            Summit(r, dir, Mathf.Max(flat.magnitude, 1f), out float topY, out float edge);
            float height = topY - baseY;
            if (height < 0.6f) return "it is already level there";
            if (height > tallest) return "too high for one flight of stairs";
            // Easy steps if there is room before the top; steep ones if not; never starting behind the traveller.
            int steps = Mathf.CeilToInt(height / 0.32f);
            float tread = 0.46f;
            float room = edge - 1.2f;
            if (steps * tread > room)
            {
                steps = Mathf.CeilToInt(height / 0.4f);
                tread = Mathf.Clamp(room / steps, 0.3f, 0.46f);
            }
            float run = steps * tread;
            float width = 1.4f * Mathf.Clamp(size, 0.7f, 1.6f);
            Vector3 top = r.From + dir * (edge - 0.1f);
            top.y = topY;
            Vector3 start = top - dir * run;
            if (!GroundAt(start, out float startGround, 4f) || startGround > baseY + 0.8f) startGround = baseY;
            var turn = Quaternion.LookRotation(dir, Vector3.up);
            float stepRise = (topY - startGround) / steps;
            for (int i = 0; i < steps; i++)
            {
                Vector3 p = start + dir * (tread * (i + 0.5f));
                p.y = startGround + stepRise * (i + 1) - 0.09f;
                plan.Parts.Add(Box(p, turn, new Vector3(width, 0.18f, tread + 0.02f)));
            }
            // A beam under the middle, from the ground to the top.
            Vector3 low = new Vector3(start.x, startGround, start.z);
            Vector3 high = top + Vector3.down * 0.25f;
            Vector3 beam = high - low;
            plan.Parts.Add(Box(low + beam * 0.5f + Vector3.down * 0.15f, Quaternion.LookRotation(beam.normalized, Vector3.up), new Vector3(0.3f, 0.26f, beam.magnitude)));
            return null;
        }

        string Ramp(Plan plan, Request r, float size)
        {
            Vector3 flat = Flat(r.To - r.From);
            if (flat.magnitude < 1f) return "show it where the ramp should lead";
            Vector3 dir = flat.normalized;
            GroundAt(r.From, out float baseY, 3f);
            Summit(r, dir, flat.magnitude, out float topY, out float edge);
            float height = Mathf.Max(topY - baseY, 0.4f);
            float length = r.Length > 0f ? r.Length : Mathf.Max(height / Mathf.Tan(28f * Mathf.Deg2Rad), 2.5f);
            // It ends at the top; where there is not room for an easy slope, it gets steeper.
            length = Mathf.Clamp(length, Mathf.Min(2.5f, edge), Mathf.Min(longest, Mathf.Max(edge - 1.2f, height / Mathf.Tan(50f * Mathf.Deg2Rad))));
            float width = 1.8f * Mathf.Clamp(size, 0.7f, 1.6f);
            Vector3 end = r.From + dir * (edge - 0.1f);
            end.y = topY;
            Vector3 start = end - dir * length;
            if (!GroundAt(start, out float startY, 4f) || startY > topY - 0.3f) startY = baseY;
            start.y = startY;
            Vector3 along = end - start;
            var turn = Quaternion.LookRotation(along.normalized, Vector3.up);
            plan.Parts.Add(Box(start + along * 0.5f - turn * Vector3.up * 0.12f, turn, new Vector3(width, 0.24f, along.magnitude)));
            // Legs under its high end.
            if (topY - startY > 1f)
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 leg = end - dir * 0.3f + turn * Vector3.right * side * (width * 0.5f - 0.15f);
                    float legHeight = Mathf.Max(0.3f, leg.y - startY);
                    plan.Parts.Add(Box(new Vector3(leg.x, startY + legHeight * 0.5f, leg.z), Quaternion.LookRotation(dir), new Vector3(0.22f, legHeight, 0.22f)));
                }
            return null;
        }

        string Wall(Plan plan, Request r, float size)
        {
            Vector3 at = r.To;
            GroundAt(at, out float baseY, 4f);
            Vector3 facing = Flat(at - r.From);
            if (facing.magnitude < 0.1f) facing = Vector3.forward;
            var turn = Quaternion.LookRotation(facing.normalized, Vector3.up);
            float length = r.Length > 0f ? Mathf.Min(r.Length, longest) : 4f * Mathf.Clamp(size, 0.6f, 2f);
            float height = r.Height > 0f ? Mathf.Min(r.Height, tallest) : 2.4f * Mathf.Clamp(size, 0.6f, 2f);
            // Courses of blocks, every other course offset by half a block.
            const float blockLength = 0.8f, blockHeight = 0.4f;
            int courses = Mathf.Max(1, Mathf.RoundToInt(height / blockHeight));
            int perCourse = Mathf.Max(1, Mathf.RoundToInt(length / blockLength));
            for (int c = 0; c < courses; c++)
            {
                float shift = (c & 1) == 0 ? 0f : blockLength * 0.5f;
                for (int i = 0; i < perCourse; i++)
                {
                    float x = -length * 0.5f + blockLength * (i + 0.5f) + shift;
                    float w = blockLength - 0.03f;
                    if (x + w * 0.5f > length * 0.5f) w = length * 0.5f - (x - w * 0.5f);
                    if (w < 0.15f) continue;
                    Vector3 p = new Vector3(at.x, baseY, at.z) + turn * new Vector3(x - (blockLength - 0.03f - w) * 0.5f, blockHeight * (c + 0.5f), 0f);
                    plan.Parts.Add(Box(p, turn, new Vector3(w, blockHeight - 0.03f, 0.45f)));
                }
            }
            return null;
        }

        string Pillar(Plan plan, Request r, float size)
        {
            Vector3 at = r.To;
            GroundAt(at, out float baseY, 4f);
            float height = r.Height > 0f ? Mathf.Min(r.Height, tallest) : 3.5f * Mathf.Clamp(size, 0.6f, 2.2f);
            float diameter = 0.9f * Mathf.Clamp(size, 0.7f, 1.8f);
            int drums = Mathf.Max(1, Mathf.RoundToInt(height / 0.7f));
            for (int i = 0; i < drums; i++)
            {
                float h = height / drums;
                plan.Parts.Add(new Part
                {
                    Shape = Shape.Cylinder,
                    Position = new Vector3(at.x, baseY + h * (i + 0.5f), at.z),
                    Rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f),
                    Size = new Vector3(diameter * (1f - 0.04f * (i & 1)), h - 0.02f, diameter * (1f - 0.04f * (i & 1))),
                });
            }
            return null;
        }

        string Platform(Plan plan, Request r, float size)
        {
            Vector3 at = r.To;
            GroundAt(at, out float baseY, 4f);
            float height = r.Height > 0f ? Mathf.Min(r.Height, tallest) : 1.5f;
            float side = 2.6f * Mathf.Clamp(size, 0.6f, 2f);
            var turn = Quaternion.LookRotation(Flat(at - r.From).sqrMagnitude > 0.01f ? Flat(at - r.From).normalized : Vector3.forward);
            plan.Parts.Add(Box(new Vector3(at.x, baseY + height - 0.12f, at.z), turn, new Vector3(side, 0.24f, side)));
            foreach (var corner in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
            {
                Vector3 p = new Vector3(at.x, baseY + (height - 0.24f) * 0.5f, at.z) + turn * new Vector3(corner.x * (side * 0.5f - 0.2f), 0f, corner.y * (side * 0.5f - 0.2f));
                plan.Parts.Add(Box(p, turn, new Vector3(0.26f, height - 0.24f, 0.26f)));
            }
            return null;
        }

        string Block(Plan plan, Request r, float size)
        {
            Vector3 at = r.To;
            GroundAt(at, out float baseY, 6f);
            float side = Mathf.Clamp(1f * size, 0.5f, 2.2f);
            var part = Box(new Vector3(at.x, baseY + side * 0.5f + 0.02f, at.z), Quaternion.Euler(0f, (float)random.NextDouble() * 30f, 0f), Vector3.one * side);
            part.Loose = true;
            plan.Parts.Add(part);
            return null;
        }

        string Ball(Plan plan, Request r, float size)
        {
            Vector3 at = r.To;
            GroundAt(at, out float baseY, 6f);
            float d = Mathf.Clamp(1.1f * size, 0.5f, 2.6f);
            plan.Parts.Add(new Part { Shape = Shape.Sphere, Position = new Vector3(at.x, baseY + d * 0.5f + 0.05f, at.z), Rotation = Quaternion.identity, Size = Vector3.one * d, Loose = true });
            return null;
        }

        string Lamp(Plan plan, Request r, float size)
        {
            plan.Matter = Matter.Light;
            Vector3 at = r.To;
            GroundAt(at, out float baseY, 4f);
            plan.Parts.Add(new Part { Shape = Shape.Cylinder, Position = new Vector3(at.x, baseY + 0.9f, at.z), Rotation = Quaternion.identity, Size = new Vector3(0.12f, 1.8f, 0.12f) });
            plan.Parts.Add(new Part { Shape = Shape.Sphere, Position = new Vector3(at.x, baseY + 2.05f, at.z), Rotation = Quaternion.identity, Size = Vector3.one * 0.45f * Mathf.Clamp(size, 0.7f, 1.6f) });
            return null;
        }

        static Part Box(Vector3 position, Quaternion rotation, Vector3 size) => new Part { Shape = Shape.Box, Position = position, Rotation = rotation, Size = size };

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        // ---- Showing the plan ----

        /// <summary>The plan drawn in light, drawing itself in from the bottom.</summary>
        public class Sketch
        {
            public Plan Plan;
            public readonly List<Renderer> Pieces = new List<Renderer>();
            public GameObject Root;
            public bool Idea;
        }

        public Sketch Show(Plan plan, bool idea)
        {
            var sketch = new Sketch { Plan = plan, Root = new GameObject(idea ? "Idea" : "Plan"), Idea = idea };
            foreach (var part in plan.Parts)
            {
                var piece = Piece(part, idea ? ideaGhost : ghost, sketch.Root.transform, false);
                Range(piece, part, RevealRangeId);
                sketch.Pieces.Add(piece);
            }
            Play(planClip, idea ? 0.4f : 0.6f);
            StartCoroutine(Reveal(sketch));
            return sketch;
        }

        IEnumerator Reveal(Sketch sketch)
        {
            var block = new MaterialPropertyBlock();
            for (float t = 0f; t <= 1f; t += Time.deltaTime / 0.9f)
            {
                foreach (var piece in sketch.Pieces)
                {
                    if (!piece) continue;
                    piece.GetPropertyBlock(block);
                    block.SetFloat(RevealId, Mathf.Clamp01(t));
                    piece.SetPropertyBlock(block);
                }
                yield return null;
            }
            foreach (var piece in sketch.Pieces)
            {
                if (!piece) continue;
                piece.GetPropertyBlock(block);
                block.SetFloat(RevealId, 1f);
                piece.SetPropertyBlock(block);
            }
        }

        /// <summary>Dims the plan to a slow flicker while the mind lacks what it needs, or wakes it.</summary>
        public void Wanting(Sketch sketch, bool wanting)
        {
            var block = new MaterialPropertyBlock();
            foreach (var piece in sketch.Pieces)
            {
                if (!piece) continue;
                piece.GetPropertyBlock(block);
                block.SetFloat(WantingId, wanting ? 1f : 0f);
                piece.SetPropertyBlock(block);
            }
        }

        public void Hide(Sketch sketch)
        {
            if (sketch?.Root) Destroy(sketch.Root);
        }

        // ---- Making ----

        /// <summary>
        /// Makes the plan: its cost leaves the mind's ring a piece at a time, each flying to a part,
        /// which forms where it lands. The sketch's light goes out part by part as each one forms.
        /// </summary>
        public IEnumerator Make(Sketch sketch, Companion mind, string words, Action<Built> done)
        {
            Busy = true;
            var plan = sketch.Plan;
            plan.Words = words;
            var holder = new GameObject(plan.Summary + ": " + words).AddComponent<Built>();
            holder.Set(plan.Kind, plan.Matter, plan.Cost, words);
            var from = mind.Take(plan.Matter, plan.Cost);
            // Bottom first, then near to far from the mind.
            var order = Enumerable.Range(0, plan.Parts.Count)
                .OrderBy(i => plan.Parts[i].Position.y - plan.Parts[i].Size.y * 0.5f > 0.5f + plan.Parts.Min(p => p.Position.y) ? 1 : 0)
                .ThenBy(i => Vector3.Distance(plan.Parts[i].Position, mind.transform.position)).ToList();
            float gap = Mathf.Clamp(2.8f / order.Count, 0.05f, 0.22f);
            int flown = 0;
            var forming = new List<Coroutine>();
            for (int n = 0; n < order.Count; n++)
            {
                int i = order[n];
                var part = plan.Parts[i];
                // This part's share of the pieces leaving the ring.
                int share = Mathf.Max(1, Mathf.RoundToInt((float)from.Count * (n + 1) / order.Count) - flown);
                var launch = from.Count > 0 ? from[Mathf.Min(flown, from.Count - 1)] : mind.transform.position;
                flown += share;
                forming.Add(StartCoroutine(Form(part, plan.Matter, launch, holder, sketch.Pieces[i], n)));
                yield return new WaitForSeconds(gap);
            }
            foreach (var f in forming) yield return f;
            Hide(sketch);
            Play(doneClip, 0.8f);
            made.Add(holder);
            Busy = false;
            done?.Invoke(holder);
        }

        IEnumerator Form(Part part, Matter kind, Vector3 launch, Built holder, Renderer sketchPiece, int index)
        {
            // A shard flies from the ring to the part, in an arc.
            var flyer = new GameObject("shard").transform;
            flyer.position = launch;
            flyer.localScale = Vector3.one * 0.14f;
            flyer.gameObject.AddComponent<MeshFilter>().sharedMesh = shard;
            flyer.gameObject.AddComponent<MeshRenderer>().sharedMaterial = matter[(int)kind];
            if (index % 3 == 0 && flyClips != null && flyClips.Length > 0) Play(flyClips[random.Next(flyClips.Length)], 0.35f);
            float flight = Mathf.Clamp(Vector3.Distance(launch, part.Position) / 14f, 0.3f, 0.8f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / flight)
            {
                float k = t * t;
                flyer.position = Vector3.Lerp(launch, part.Position, k) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 1.2f;
                flyer.Rotate(new Vector3(400f, 500f, 0f) * Time.deltaTime);
                yield return null;
            }
            Destroy(flyer.gameObject);
            if (sketchPiece) sketchPiece.enabled = false;

            var piece = Piece(part, matter[(int)kind], holder.transform, true);
            Range(piece, part, BuildRangeId);
            holder.Pieces.Add(piece.gameObject);
            if (lockClips != null && lockClips.Length > (int)kind && lockClips[(int)kind] && index % 2 == 0) Play(lockClips[(int)kind], 0.5f, piece.transform.position);
            var block = new MaterialPropertyBlock();
            for (float t = 0f; t <= 1f; t += Time.deltaTime / 0.35f)
            {
                piece.GetPropertyBlock(block);
                block.SetFloat(BuildId, t);
                piece.SetPropertyBlock(block);
                yield return null;
            }
            piece.GetPropertyBlock(block);
            block.SetFloat(BuildId, 1f);
            piece.SetPropertyBlock(block);
            if (part.Loose)
            {
                var body = piece.gameObject.AddComponent<Rigidbody>();
                body.mass = Mathf.Clamp(part.Volume * Matters.Density(kind), 5f, 3000f);
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
            if (kind == Matter.Light && part.Shape == Shape.Sphere)
            {
                var lamp = piece.gameObject.AddComponent<Light>();
                lamp.type = LightType.Point;
                lamp.color = new Color(1f, 0.86f, 0.66f);
                lamp.range = 11f;
                lamp.intensity = 2.2f;
                lamp.shadows = LightShadows.None;
            }
        }

        /// <summary>Takes a made thing apart: its pieces fade and its matter flies back to the mind.</summary>
        public IEnumerator Unmake(Built built, Companion mind)
        {
            if (!built) yield break;
            Busy = true;
            made.Remove(built);
            Play(apartClip, 0.7f);
            var block = new MaterialPropertyBlock();
            var pieces = built.Pieces.Where(p => p).Select(p => p.GetComponent<Renderer>()).ToList();
            foreach (var body in built.GetComponentsInChildren<Rigidbody>()) body.isKinematic = true;
            for (float t = 1f; t >= 0f; t -= Time.deltaTime / 0.6f)
            {
                foreach (var piece in pieces)
                {
                    piece.GetPropertyBlock(block);
                    block.SetFloat(BuildId, t);
                    piece.SetPropertyBlock(block);
                }
                yield return null;
            }
            Vector3 centre = pieces.Count > 0 ? pieces.Aggregate(Vector3.zero, (s, p) => s + p.transform.position) / pieces.Count : built.transform.position;
            Destroy(built.gameObject);
            var flyer = new GameObject("returning").transform;
            flyer.position = centre;
            flyer.localScale = Vector3.one * 0.2f;
            flyer.gameObject.AddComponent<MeshFilter>().sharedMesh = shard;
            flyer.gameObject.AddComponent<MeshRenderer>().sharedMaterial = matter[(int)built.Matter];
            mind.Give(built.Matter, built.Units, flyer);
            Busy = false;
        }

        /// <summary>The made thing nearest a point, within a few metres, if any.</summary>
        public Built Nearest(Vector3 point, float within = 6f)
        {
            Built best = null;
            float bestDistance = within;
            foreach (var b in made)
            {
                if (!b) continue;
                foreach (var p in b.Pieces)
                {
                    if (!p) continue;
                    float d = Vector3.Distance(p.transform.position, point);
                    if (d >= bestDistance) continue;
                    bestDistance = d;
                    best = b;
                }
            }
            return best;
        }

        Renderer Piece(Part part, Material material, Transform parent, bool solid)
        {
            var piece = new GameObject(part.Shape.ToString());
            piece.transform.SetParent(parent, false);
            piece.transform.SetPositionAndRotation(part.Position, part.Rotation);
            piece.transform.localScale = part.Shape == Shape.Cylinder ? new Vector3(part.Size.x, part.Size.y * 0.5f, part.Size.z) : part.Size;
            piece.AddComponent<MeshFilter>().sharedMesh = part.Shape switch { Shape.Box => cube, Shape.Cylinder => cylinder, _ => sphere };
            var renderer = piece.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = solid ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            block.SetFloat(EdgeWearId, part.Shape == Shape.Box ? 0.6f : 0f);
            block.SetFloat(SeamId, part.Shape == Shape.Box ? 1f : 0f);
            block.SetFloat(solid ? BuildId : RevealId, 0f);
            renderer.SetPropertyBlock(block);
            if (!solid) return renderer;
            Collider collider = part.Shape switch
            {
                Shape.Box => piece.AddComponent<BoxCollider>(),
                Shape.Sphere => piece.AddComponent<SphereCollider>(),
                _ => piece.AddComponent<CapsuleCollider>(),
            };
            if (collider is CapsuleCollider capsule)
            {
                capsule.height = 2f;
                capsule.radius = 0.5f;
            }
            if (part.Loose && slippery && part.Shape == Shape.Sphere) collider.sharedMaterial = slippery;
            return renderer;
        }

        // Where the piece starts and how tall it is, so it forms (or is drawn) from the bottom up.
        static void Range(Renderer piece, Part part, int id)
        {
            var block = new MaterialPropertyBlock();
            piece.GetPropertyBlock(block);
            block.SetVector(id, new Vector4(piece.bounds.min.y, Mathf.Max(piece.bounds.size.y, 0.05f), 0f, 0f));
            piece.SetPropertyBlock(block);
        }

        void Play(AudioClip clip, float volume, Vector3? at = null)
        {
            if (!clip) return;
            if (at.HasValue) AudioSource.PlayClipAtPoint(clip, at.Value, volume);
            else if (sound) sound.PlayOneShot(clip, volume);
        }
    }
}
