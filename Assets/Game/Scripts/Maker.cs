using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// How the drone makes things. A thing the model described (<see cref="ThingSpec"/>) becomes a
    /// <see cref="Plan"/>: ways (a bridge, stairs, a ramp, a lift) fitted to the ground between the
    /// places named; plain shapes, lamps, fires, balloons, springs and tools of a person's design; or
    /// anything else built from the pieces the model laid out. The plan is drawn in light, then each
    /// piece forms under the drone's beam, bottom first. What the thing is decides what it does: its
    /// weight presses plates, its heat melts ice, its glow lights the dark, a balloon lifts whoever
    /// holds it, a spring throws them, ice is slippery, a small thing can be carried. The drone keeps
    /// at most twelve things; a thirteenth lets the oldest fall back into snow.
    /// </summary>
    public class Maker : MonoBehaviour
    {
        public enum Shape { Box, Cylinder, Sphere, Cone }

        public struct Part
        {
            public Shape Shape;
            public Vector3 Position;
            public Quaternion Rotation;
            /// <summary>Box: its sides; cylinder and cone: (diameter, height, diameter); sphere: its diameters.</summary>
            public Vector3 Size;
            public string Look;
            /// <summary>Glows as hot coals or a flame's heart, whatever it is made of.</summary>
            public bool Ember;

            public float Volume => Shape switch
            {
                Shape.Box => Size.x * Size.y * Size.z,
                Shape.Cylinder => Mathf.PI * Size.x * Size.z * 0.25f * Size.y,
                Shape.Cone => Mathf.PI * Size.x * Size.z * 0.25f * Size.y / 3f,
                _ => Mathf.PI / 6f * Size.x * Size.y * Size.z,
            };
        }

        public class Plan
        {
            public ThingSpec Spec;
            public string Form => Spec.form;
            public string Look => Spec.look;
            public readonly List<Part> Parts = new List<Part>();
            /// <summary>Where the thing stands: its foot, the middle of its base.</summary>
            public Vector3 Base;
            public Quaternion Facing = Quaternion.identity;
            /// <summary>For a lift: how high it carries.</summary>
            public float Bottom, Top;
            public Vector3 Centre => Parts.Count == 0 ? Base : Parts.Aggregate(Vector3.zero, (sum, p) => sum + p.Position) / Parts.Count;
            public float Extent => Parts.Count == 0 ? 1f : Parts.Max(p => Vector3.Distance(p.Position, Centre) + p.Size.magnitude * 0.5f);
            /// <summary>The thing goes into the survivor's hands.</summary>
            public bool ToHands;
            public string Summary => $"{Spec.name} ({Spec.form.ToLowerInvariant()}, {Spec.look.ToLowerInvariant()})";
        }

        /// <summary>What a step names, already turned into places in the world.</summary>
        public struct Request
        {
            public ThingSpec Spec;
            /// <summary>Where it stands, or where a way starts.</summary>
            public Vector3 At;
            /// <summary>Where a way leads.</summary>
            public Vector3 To;
            /// <summary>Who it is for: it faces them.</summary>
            public Vector3 Viewer;
            public bool ToHands;
        }

        [SerializeField] Mesh cube, cylinder, sphere;
        [SerializeField, Tooltip("Stone, metal, ice, glass, wood, light.")] Material[] looks = new Material[6];
        [SerializeField] Material ghost;
        [SerializeField, Tooltip("Additive: flames, the torch's fire.")] Material flame;
        [SerializeField] PhysicsMaterial slippery;
        [SerializeField] PhysicsMaterial bouncy;
        [SerializeField] AudioSource sound;
        [SerializeField, Tooltip("A piece locking into place: stone, metal, ice, glass, wood, light.")] AudioClip[] lockClips;
        [SerializeField] AudioClip swingClip, boltClip;
        [SerializeField] AudioClip doneClip, apartClip, planClip, boingClip, liftClip, fireClip;
        [SerializeField, Tooltip("What the ground is, for finding gaps and slopes.")] LayerMask ground = ~0;
        [SerializeField] float longest = 34f;
        [SerializeField] float tallest = 16f;
        [SerializeField] int most = 12;

        public IReadOnlyList<Built> Made => made;
        public bool Busy { get; private set; }

        readonly List<Built> made = new List<Built>();
        readonly System.Random random = new System.Random(3);
        int count;
        Mesh cone;
        static readonly int BuildId = Shader.PropertyToID("_Build");
        static readonly int BuildRangeId = Shader.PropertyToID("_BuildRange");
        static readonly int EdgeWearId = Shader.PropertyToID("_EdgeWear");
        static readonly int SeamId = Shader.PropertyToID("_Seam");
        static readonly int RevealId = Shader.PropertyToID("_Reveal");
        static readonly int RevealRangeId = Shader.PropertyToID("_RevealRange");
        static readonly int EmissionId = Shader.PropertyToID("_Emission");

        public static int LookIndex(string look) => Array.IndexOf(Mind.Looks, look) is int i && i >= 0 ? i : 0;

        /// <summary>Kilograms in a cubic metre of each look.</summary>
        public static float Density(string look) => look switch
        {
            "METAL" => 3000f,
            "ICE" => 900f,
            "GLASS" => 2500f,
            "WOOD" => 600f,
            "LIGHT" => 40f,
            _ => 2600f,
        };

        static float SizeScale(string size) => size switch
        {
            "TINY" => 0.35f,
            "SMALL" => 0.65f,
            "LARGE" => 1.5f,
            "HUGE" => 2.2f,
            _ => 1f,
        };

        static float WeightFactor(string weight) => weight switch
        {
            "FEATHER" => 0.02f,
            "LIGHT" => 0.25f,
            "HEAVY" => 3f,
            _ => 1f,
        };

        // ---- Designing ----

        /// <summary>Fits a thing to the places asked for; null and the reason if it cannot be.</summary>
        public Plan Design(Request r, out string problem)
        {
            problem = null;
            var spec = r.Spec;
            var plan = new Plan { Spec = spec, ToHands = r.ToHands };
            float size = SizeScale(spec.size);
            Vector3 toViewer = Flat(r.Viewer - r.At);
            plan.Facing = toViewer.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toViewer.normalized, Vector3.up) : Quaternion.identity;
            switch (spec.form)
            {
                case "BRIDGE": problem = Bridge(plan, r, size); break;
                case "STAIRS": problem = Stairs(plan, r, size); break;
                case "RAMP": problem = Ramp(plan, r, size); break;
                case "LIFT": problem = LiftWay(plan, r, size); break;
                case "WALL": problem = Wall(plan, r, size); break;
                case "PILLAR": problem = Pillar(plan, r, size); break;
                case "PLATFORM": problem = Platform(plan, r, size); break;
                case "BLOCK": problem = Block(plan, r, size); break;
                case "BALL": problem = Ball(plan, r, size); break;
                case "PLANK": problem = Plank(plan, r, size); break;
                case "LAMP": problem = Lamp(plan, r, size); break;
                case "FIRE": problem = Fire(plan, r, size); break;
                case "BALLOON": problem = Balloon(plan, r, size); break;
                case "SPRING": problem = SpringPad(plan, r, size); break;
                case "TOOL": problem = ToolShape(plan, r, size); break;
                case "WEAPON":
                case "SHIELD":
                case "THROWN":
                case "GLIDER": problem = HeldShape(plan, r, size); break;
                case "MIRROR": problem = Wall(plan, r, size); break;
                case "DECOY": problem = Lamp(plan, r, Mathf.Min(size, 0.8f)); break;
                default: problem = Custom(plan, r, size); break;
            }
            if (problem != null || plan.Parts.Count == 0) return null;
            for (int i = 0; i < plan.Parts.Count; i++)
            {
                var p = plan.Parts[i];
                if (string.IsNullOrEmpty(p.Look)) p.Look = spec.look;
                plan.Parts[i] = p;
            }
            return plan;
        }

        /// <summary>The height of the ground under a point, looking down from above it.</summary>
        public bool GroundAt(Vector3 p, out float height, float from = 30f)
        {
            // Under a roof (in the vault), look down from just under it, not from on top of it.
            if (Physics.Raycast(p, Vector3.up, out var roof, from, ground, QueryTriggerInteraction.Ignore)) from = Mathf.Max(0.05f, roof.distance - 0.05f);
            if (Physics.Raycast(new Vector3(p.x, p.y + from, p.z), Vector3.down, out var hit, from + 60f, ground, QueryTriggerInteraction.Ignore))
            {
                height = hit.point.y;
                return true;
            }
            height = -100f;
            return false;
        }

        float Floor(Vector3 at, float from = 6f) => GroundAt(at, out float y, from) ? y : at.y;

        string Bridge(Plan plan, Request r, float size)
        {
            Vector3 flat = Flat(r.To - r.At);
            if (flat.magnitude < 1.5f) return "I need to know where it should go";
            Vector3 dir = flat.normalized;
            GroundAt(r.At, out float startY, 3f);
            float reachLimit = Mathf.Min(flat.magnitude + 6f, longest + 10f);
            // Walk toward the far place until the ground drops away, then until it comes back; once
            // over a gap, keep looking for its far edge even past the place named.
            float nearEdge = -1f, farEdge = -1f, farY = startY;
            for (float d = 0f; d <= (nearEdge < 0f ? reachLimit : nearEdge + longest); d += 0.25f)
            {
                Vector3 p = r.At + dir * d;
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
                b = Mathf.Min(flat.magnitude, longest);
                ya = startY;
                GroundAt(r.At + dir * b, out yb, 4f);
            }
            else
            {
                if (farEdge < 0f) return "there's nothing on the other side to reach";
                a = Mathf.Max(0f, nearEdge - 1.2f);
                b = farEdge + 1.2f;
                GroundAt(r.At + dir * a, out ya, 4f);
                GroundAt(r.At + dir * b, out yb, 4f);
                if (yb < farY - 0.5f) yb = farY;
            }
            float length = b - a;
            if (r.Spec.length > 0 && nearEdge < 0f) length = Mathf.Min(length, r.Spec.length);
            if (length > longest) return "the gap is too wide for one bridge";
            float rise = yb - ya;
            if (Mathf.Abs(rise) / Mathf.Max(length, 0.1f) > 0.5f) return "too steep for a bridge; stairs would climb it";

            float width = 1.6f * Mathf.Clamp(size, 0.7f, 1.6f);
            Vector3 start = r.At + dir * a;
            start.y = ya + 0.05f;
            Vector3 end = r.At + dir * (a + length);
            end.y = yb + 0.05f;
            Vector3 along = end - start;
            var turn = Quaternion.LookRotation(along.normalized, Vector3.up);
            plan.Base = start;
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
            GroundAt(r.At, out float baseY, 3f);
            topY = baseY;
            edge = reachTo;
            if (r.Spec.height > 0)
            {
                topY = baseY + Mathf.Min(r.Spec.height, tallest);
                return;
            }
            float previous = baseY;
            bool climbing = false;
            for (float d = 0.25f; d <= reachTo + 6f; d += 0.25f)
            {
                if (!GroundAt(r.At + dir * d, out float g, 40f)) continue;
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

        Vector3 WayDirection(Request r)
        {
            Vector3 flat = Flat(r.To - r.At);
            return flat.magnitude > 0.5f ? flat.normalized : Flat(r.At - r.Viewer).sqrMagnitude > 0.01f ? Flat(r.At - r.Viewer).normalized : Vector3.forward;
        }

        string Stairs(Plan plan, Request r, float size)
        {
            Vector3 dir = WayDirection(r);
            GroundAt(r.At, out float baseY, 3f);
            Summit(r, dir, Mathf.Max(Flat(r.To - r.At).magnitude, 1f), out float topY, out float edge);
            float height = topY - baseY;
            if (height < 0.6f)
            {
                // Nothing to climb toward: a flight of the height asked for, or two metres.
                topY = baseY + (r.Spec.height > 0 ? Mathf.Min(r.Spec.height, tallest) : 2f);
                height = topY - baseY;
                edge = Mathf.Max(edge, height / 0.32f * 0.46f + 1.5f);
            }
            if (height > tallest) return "too high for one flight of stairs";
            // Easy steps if there is room before the top; steep ones if not; never starting behind the survivor.
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
            Vector3 top = r.At + dir * (edge - 0.1f);
            top.y = topY;
            Vector3 start = top - dir * run;
            if (!GroundAt(start, out float startGround, 4f) || startGround > baseY + 0.8f) startGround = baseY;
            plan.Base = new Vector3(start.x, startGround, start.z);
            var turn = Quaternion.LookRotation(dir, Vector3.up);
            float stepRise = (topY - startGround) / steps;
            for (int i = 0; i < steps; i++)
            {
                Vector3 p = start + dir * (tread * (i + 0.5f));
                p.y = startGround + stepRise * (i + 1) - 0.09f;
                plan.Parts.Add(Box(p, turn, new Vector3(width, 0.18f, tread + 0.02f)));
            }
            Vector3 low = new Vector3(start.x, startGround, start.z);
            Vector3 high = top + Vector3.down * 0.25f;
            Vector3 beam = high - low;
            plan.Parts.Add(Box(low + beam * 0.5f + Vector3.down * 0.15f, Quaternion.LookRotation(beam.normalized, Vector3.up), new Vector3(0.3f, 0.26f, beam.magnitude)));
            return null;
        }

        string Ramp(Plan plan, Request r, float size)
        {
            Vector3 dir = WayDirection(r);
            GroundAt(r.At, out float baseY, 3f);
            Summit(r, dir, Mathf.Max(Flat(r.To - r.At).magnitude, 1f), out float topY, out float edge);
            float height = Mathf.Max(topY - baseY, 0.4f);
            float length = r.Spec.length > 0 ? r.Spec.length : Mathf.Max(height / Mathf.Tan(28f * Mathf.Deg2Rad), 2.5f);
            // It ends at the top; where there is not room for an easy slope, it gets steeper.
            length = Mathf.Clamp(length, Mathf.Min(2.5f, edge), Mathf.Min(longest, Mathf.Max(edge - 1.2f, height / Mathf.Tan(50f * Mathf.Deg2Rad))));
            float width = 1.8f * Mathf.Clamp(size, 0.7f, 1.6f);
            Vector3 end = r.At + dir * (edge - 0.1f);
            end.y = topY;
            Vector3 start = end - dir * length;
            if (!GroundAt(start, out float startY, 4f) || startY > topY - 0.3f) startY = baseY;
            start.y = startY;
            plan.Base = start;
            Vector3 along = end - start;
            var turn = Quaternion.LookRotation(along.normalized, Vector3.up);
            plan.Parts.Add(Box(start + along * 0.5f - turn * Vector3.up * 0.12f, turn, new Vector3(width, 0.24f, along.magnitude)));
            if (topY - startY > 1f)
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 leg = end - dir * 0.3f + turn * Vector3.right * side * (width * 0.5f - 0.15f);
                    float legHeight = Mathf.Max(0.3f, leg.y - startY);
                    plan.Parts.Add(Box(new Vector3(leg.x, startY + legHeight * 0.5f, leg.z), Quaternion.LookRotation(dir), new Vector3(0.22f, legHeight, 0.22f)));
                }
            return null;
        }

        /// <summary>
        /// A platform at the foot of what it climbs, between two posts that reach the top, and a
        /// fixed walkway from where the platform stops across to the top's edge.
        /// </summary>
        string LiftWay(Plan plan, Request r, float size)
        {
            Vector3 dir = WayDirection(r);
            GroundAt(r.At, out float baseY, 3f);
            Summit(r, dir, Mathf.Max(Flat(r.To - r.At).magnitude, 1f), out float topY, out float edge);
            float side = 2f * Mathf.Clamp(size, 0.8f, 1.4f);
            // Where the climb begins: the last of the flat ground before it.
            float climb = -1f;
            for (float d = 0.25f; d <= edge; d += 0.25f)
                if (GroundAt(r.At + dir * d, out float g, 40f) && g > baseY + 0.4f)
                {
                    climb = d;
                    break;
                }
            bool cliff = climb > 0f && topY - baseY >= 1f;
            if (!cliff)
            {
                topY = baseY + (r.Spec.height > 0 ? Mathf.Min(r.Spec.height, tallest) : 4f);
                climb = 1.6f + side;
                edge = climb;
            }
            if (topY - baseY > tallest + 4f) return "too high for a lift";
            Vector3 centre = r.At + dir * Mathf.Max(climb - side * 0.5f - 0.25f, 0.6f);
            if (!GroundAt(centre + Vector3.up * 2f, out float footY, 2f) || Mathf.Abs(footY - baseY) > 1.5f) footY = baseY;
            centre.y = footY;
            var turn = Quaternion.LookRotation(dir, Vector3.up);
            plan.Base = centre;
            plan.Bottom = footY + 0.12f;
            plan.Top = topY + 0.12f;
            plan.Parts.Add(Box(new Vector3(centre.x, plan.Bottom - 0.1f, centre.z), turn, new Vector3(side, 0.2f, side)));
            float postHeight = topY - footY + 2.6f;
            foreach (float s in new[] { -1f, 1f })
                plan.Parts.Add(Box(centre + turn * new Vector3(s * (side * 0.5f + 0.12f), postHeight * 0.5f, 0f), turn, new Vector3(0.16f, postHeight, 0.16f)));
            plan.Parts.Add(Box(centre + turn * new Vector3(0f, postHeight, 0f), turn, new Vector3(side + 0.5f, 0.14f, 0.16f)));
            if (cliff)
            {
                // The walkway: from the platform's far side to a little past the lip, at the top.
                float from = climb - 0.25f, to = edge + 0.8f;
                Vector3 mid = r.At + dir * ((from + to) * 0.5f);
                mid.y = topY - 0.05f;
                plan.Parts.Add(Box(mid, turn, new Vector3(side * 0.8f, 0.14f, to - from)));
            }
            return null;
        }

        Vector3 Stand(Request r, out float baseY)
        {
            Vector3 at = r.At;
            baseY = Floor(at);
            return new Vector3(at.x, baseY, at.z);
        }

        string Wall(Plan plan, Request r, float size)
        {
            Vector3 at = Stand(r, out float baseY);
            var turn = plan.Facing;
            plan.Base = at;
            float length = r.Spec.length > 0 ? Mathf.Min(r.Spec.length, longest) : 4f * Mathf.Clamp(size, 0.4f, 2f);
            float height = r.Spec.height > 0 ? Mathf.Min(r.Spec.height, tallest) : 2.4f * Mathf.Clamp(size, 0.4f, 2f);
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
                    Vector3 p = at + turn * new Vector3(x - (blockLength - 0.03f - w) * 0.5f, blockHeight * (c + 0.5f), 0f);
                    plan.Parts.Add(Box(p, turn, new Vector3(w, blockHeight - 0.03f, 0.45f)));
                }
            }
            return null;
        }

        string Pillar(Plan plan, Request r, float size)
        {
            Vector3 at = Stand(r, out float baseY);
            plan.Base = at;
            float height = r.Spec.height > 0 ? Mathf.Min(r.Spec.height, tallest) : 3.5f * Mathf.Clamp(size, 0.5f, 2.2f);
            float diameter = 0.9f * Mathf.Clamp(size, 0.5f, 1.8f);
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
            Vector3 at = Stand(r, out float baseY);
            plan.Base = at;
            float height = r.Spec.height > 0 ? Mathf.Min(r.Spec.height, tallest) : 1.5f;
            float side = 2.6f * Mathf.Clamp(size, 0.5f, 2f);
            var turn = plan.Facing;
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
            Vector3 at = Stand(r, out float baseY);
            plan.Base = at;
            float side = Mathf.Clamp(1f * size, 0.3f, 2.4f);
            plan.Parts.Add(Box(at + Vector3.up * (side * 0.5f + 0.02f), plan.Facing * Quaternion.Euler(0f, (float)random.NextDouble() * 20f - 10f, 0f), Vector3.one * side));
            return null;
        }

        string Ball(Plan plan, Request r, float size)
        {
            Vector3 at = Stand(r, out float baseY);
            plan.Base = at;
            float d = Mathf.Clamp(1.1f * size, 0.3f, 2.8f);
            plan.Parts.Add(new Part { Shape = Shape.Sphere, Position = at + Vector3.up * (d * 0.5f + 0.05f), Rotation = Quaternion.identity, Size = Vector3.one * d });
            return null;
        }

        string Plank(Plan plan, Request r, float size)
        {
            Vector3 at = Stand(r, out float baseY);
            plan.Base = at;
            float length = r.Spec.length > 0 ? Mathf.Min(r.Spec.length, 12f) : 3.2f * Mathf.Clamp(size, 0.5f, 2.5f);
            plan.Parts.Add(Box(at + Vector3.up * 0.06f, plan.Facing * Quaternion.Euler(0f, 90f, 0f), new Vector3(0.45f * Mathf.Clamp(size, 0.6f, 1.6f), 0.09f, length)));
            return null;
        }

        string Lamp(Plan plan, Request r, float size)
        {
            Vector3 at = Stand(r, out float baseY);
            plan.Base = at;
            float s = Mathf.Clamp(size, 0.4f, 2f);
            if (r.Spec.glow == "NONE") r.Spec.glow = "BRIGHT";
            if (s < 0.5f || r.ToHands)
            {
                // A lantern to carry: a small cage of metal round a glowing heart.
                plan.Parts.Add(new Part { Shape = Shape.Cylinder, Position = at + Vector3.up * 0.04f, Rotation = Quaternion.identity, Size = new Vector3(0.22f, 0.06f, 0.22f), Look = "METAL" });
                plan.Parts.Add(new Part { Shape = Shape.Sphere, Position = at + Vector3.up * 0.2f, Rotation = Quaternion.identity, Size = Vector3.one * 0.2f, Look = "LIGHT" });
                plan.Parts.Add(new Part { Shape = Shape.Cylinder, Position = at + Vector3.up * 0.36f, Rotation = Quaternion.identity, Size = new Vector3(0.2f, 0.05f, 0.2f), Look = "METAL" });
                return null;
            }
            float height = 1.9f * s;
            plan.Parts.Add(new Part { Shape = Shape.Cylinder, Position = at + Vector3.up * 0.06f, Rotation = Quaternion.identity, Size = new Vector3(0.5f * s, 0.12f, 0.5f * s) });
            plan.Parts.Add(new Part { Shape = Shape.Cylinder, Position = at + Vector3.up * height * 0.5f, Rotation = Quaternion.identity, Size = new Vector3(0.11f * s, height, 0.11f * s) });
            plan.Parts.Add(new Part { Shape = Shape.Sphere, Position = at + Vector3.up * (height + 0.18f * s), Rotation = Quaternion.identity, Size = Vector3.one * 0.42f * s, Look = "LIGHT" });
            return null;
        }

        string Fire(Plan plan, Request r, float size)
        {
            Vector3 at = Stand(r, out float baseY);
            plan.Base = at;
            float s = Mathf.Clamp(size, 0.4f, 2f);
            if (r.Spec.heat == "NONE" || r.Spec.heat == "COLD") r.Spec.heat = "HOT";
            if (r.Spec.glow == "NONE") r.Spec.glow = "BRIGHT";
            string bowl = r.Spec.look == "LIGHT" || r.Spec.look == "ICE" ? "METAL" : r.Spec.look;
            // A brazier: three legs, a bowl, coals glowing in it.
            for (int i = 0; i < 3; i++)
            {
                var leg = Quaternion.Euler(0f, i * 120f, 0f) * new Vector3(0.32f * s, 0f, 0f);
                plan.Parts.Add(new Part { Shape = Shape.Cylinder, Position = at + leg + Vector3.up * 0.25f * s, Rotation = Quaternion.Euler(0f, i * 120f, 12f), Size = new Vector3(0.07f * s, 0.55f * s, 0.07f * s), Look = bowl });
            }
            plan.Parts.Add(new Part { Shape = Shape.Cylinder, Position = at + Vector3.up * 0.55f * s, Rotation = Quaternion.identity, Size = new Vector3(0.95f * s, 0.22f * s, 0.95f * s), Look = bowl });
            plan.Parts.Add(new Part { Shape = Shape.Sphere, Position = at + Vector3.up * 0.68f * s, Rotation = Quaternion.identity, Size = new Vector3(0.8f * s, 0.22f * s, 0.8f * s), Look = "STONE", Ember = true });
            return null;
        }

        string Balloon(Plan plan, Request r, float size)
        {
            Vector3 at = Stand(r, out float baseY);
            plan.Base = at;
            r.Spec.floats = true;
            float s = Mathf.Clamp(size, 0.5f, 2.2f);
            float d = 1.5f * s;
            string skin = r.Spec.look == "STONE" || r.Spec.look == "METAL" ? "LIGHT" : r.Spec.look;
            // The handle at the bottom, a cord, then the balloon.
            plan.Parts.Add(new Part { Shape = Shape.Sphere, Position = at + Vector3.up * 0.9f, Rotation = Quaternion.identity, Size = Vector3.one * 0.09f, Look = "METAL" });
            plan.Parts.Add(new Part { Shape = Shape.Cylinder, Position = at + Vector3.up * 1.9f, Rotation = Quaternion.identity, Size = new Vector3(0.02f, 2f, 0.02f), Look = "WOOD" });
            plan.Parts.Add(new Part { Shape = Shape.Sphere, Position = at + Vector3.up * (2.9f + d * 0.5f), Rotation = Quaternion.identity, Size = new Vector3(d, d * 1.15f, d), Look = skin });
            plan.Parts.Add(new Part { Shape = Shape.Cone, Position = at + Vector3.up * 2.95f, Rotation = Quaternion.identity, Size = new Vector3(0.22f * s, 0.2f, 0.22f * s), Look = skin });
            return null;
        }

        string SpringPad(Plan plan, Request r, float size)
        {
            Vector3 at = Stand(r, out float baseY);
            plan.Base = at;
            r.Spec.bouncy = true;
            float s = Mathf.Clamp(size, 0.5f, 2f);
            float d = 1.7f * s;
            plan.Parts.Add(new Part { Shape = Shape.Cylinder, Position = at + Vector3.up * 0.06f, Rotation = Quaternion.identity, Size = new Vector3(d, 0.12f, d), Look = "METAL" });
            for (int i = 0; i < 3; i++)
                plan.Parts.Add(new Part { Shape = Shape.Cylinder, Position = at + Vector3.up * (0.16f + i * 0.09f), Rotation = Quaternion.identity, Size = new Vector3(d * (0.8f - i * 0.08f), 0.04f, d * (0.8f - i * 0.08f)), Look = "METAL" });
            plan.Parts.Add(new Part { Shape = Shape.Cylinder, Position = at + Vector3.up * 0.46f, Rotation = Quaternion.identity, Size = new Vector3(d * 0.95f, 0.1f, d * 0.95f) });
            return null;
        }

        /// <summary>
        /// Something to fight or fly with, in the hand: the model's own design brought to the size of the
        /// thing (a blade about a metre, a shield 0.85 m, wings 1.9 m across), or a design of its kind.
        /// Held things point along their length (y), out from the hand.
        /// </summary>
        string HeldShape(Plan plan, Request r, float size)
        {
            Vector3 at = Stand(r, out float baseY);
            plan.Base = at;
            var spec = r.Spec;
            float length = spec.form switch { "GLIDER" => 1.9f, "SHIELD" => 0.85f, "THROWN" => 0.3f, _ => spec.use == "BLADE" ? 1.05f : 0.95f };
            if (spec.parts != null && spec.parts.Length > 0)
            {
                string problem = Custom(plan, r, 1f);
                if (problem != null) return problem;
                var bounds = new Bounds(plan.Parts[0].Position, plan.Parts[0].Size);
                foreach (var part in plan.Parts) bounds.Encapsulate(new Bounds(part.Position, part.Size));
                float scale = length / Mathf.Max(0.05f, Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)));
                Vector3 foot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                for (int i = 0; i < plan.Parts.Count; i++)
                {
                    var part = plan.Parts[i];
                    part.Position = at + (part.Position - foot) * scale;
                    part.Size *= scale;
                    plan.Parts[i] = part;
                }
                return null;
            }
            void Add(Shape shape, float y, Vector3 s, string look, float x = 0f, float z = 0f) =>
                plan.Parts.Add(new Part { Shape = shape, Position = at + new Vector3(x, y, z), Rotation = Quaternion.identity, Size = s, Look = look });
            switch (spec.form == "WEAPON" ? spec.use : spec.form)
            {
                case "BLADE":
                    Add(Shape.Cylinder, 0.1f, new Vector3(0.045f, 0.2f, 0.045f), "METAL");
                    Add(Shape.Box, 0.22f, new Vector3(0.2f, 0.035f, 0.06f), "METAL");
                    Add(Shape.Box, 0.64f, new Vector3(0.055f, 0.82f, 0.014f), "LIGHT");
                    break;
                case "BEAM":
                    Add(Shape.Cylinder, 0.26f, new Vector3(0.08f, 0.52f, 0.08f), "METAL");
                    Add(Shape.Cylinder, 0.42f, new Vector3(0.13f, 0.035f, 0.13f), "LIGHT");
                    Add(Shape.Cylinder, 0.55f, new Vector3(0.13f, 0.035f, 0.13f), "LIGHT");
                    Add(Shape.Cone, 0.68f, new Vector3(0.1f, 0.12f, 0.1f), "METAL");
                    break;
                case "SPREAD":
                    Add(Shape.Box, 0.24f, new Vector3(0.14f, 0.48f, 0.1f), "METAL");
                    for (int i = -1; i <= 1; i++) Add(Shape.Cylinder, 0.6f, new Vector3(0.035f, 0.26f, 0.035f), "METAL", i * 0.045f);
                    Add(Shape.Box, 0.74f, new Vector3(0.16f, 0.025f, 0.05f), "LIGHT");
                    break;
                case "SHIELD":
                    Add(Shape.Cylinder, 0.06f, new Vector3(0.04f, 0.12f, 0.04f), "METAL");
                    Add(Shape.Box, 0.16f, new Vector3(0.62f, 0.035f, 0.78f), "LIGHT");
                    Add(Shape.Box, 0.14f, new Vector3(0.66f, 0.02f, 0.82f), "METAL");
                    break;
                case "GLIDER":
                    Add(Shape.Cylinder, 0.45f, new Vector3(0.04f, 0.9f, 0.04f), "METAL");
                    Add(Shape.Box, 0.5f, new Vector3(1.9f, 0.5f, 0.02f), "LIGHT");
                    break;
                case "THROWN":
                    for (int i = 0; i < 3; i++) Add(Shape.Sphere, 0.08f + 0.1f * i, Vector3.one * 0.09f, "LIGHT");
                    break;
                default:
                    // A bolt caster: a body, a barrel, an emitter at the end.
                    Add(Shape.Box, 0.3f, new Vector3(0.07f, 0.6f, 0.1f), "METAL");
                    Add(Shape.Box, 0.1f, new Vector3(0.06f, 0.16f, 0.05f), "METAL", 0f, 0.07f);
                    Add(Shape.Cylinder, 0.75f, new Vector3(0.035f, 0.32f, 0.035f), "METAL");
                    Add(Shape.Sphere, 0.93f, Vector3.one * 0.06f, "LIGHT");
                    if (spec.reach >= 4) Add(Shape.Cylinder, 0.52f, new Vector3(0.05f, 0.14f, 0.05f), "GLASS", 0f, -0.07f);
                    break;
            }
            return null;
        }

        string ToolShape(Plan plan, Request r, float size)
        {
            Vector3 at = Stand(r, out float baseY);
            plan.Base = at;
            var spec = r.Spec;
            if (spec.parts != null && spec.parts.Length > 0)
            {
                // The model's own design, brought to the size of a hand tool: about 0.55 m long.
                string problem = Custom(plan, r, 1f);
                if (problem != null) return problem;
                var bounds = new Bounds(plan.Parts[0].Position, plan.Parts[0].Size);
                foreach (var p in plan.Parts) bounds.Encapsulate(new Bounds(p.Position, p.Size));
                float scale = 0.55f / Mathf.Max(0.05f, Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)));
                Vector3 foot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                for (int i = 0; i < plan.Parts.Count; i++)
                {
                    var p = plan.Parts[i];
                    p.Position = at + (p.Position - foot) * scale;
                    p.Size *= scale;
                    plan.Parts[i] = p;
                }
                return null;
            }
            // A person's design when the model gave none: a handle and a working end.
            plan.Parts.Add(new Part { Shape = Shape.Cylinder, Position = at + Vector3.up * 0.4f, Rotation = Quaternion.identity, Size = new Vector3(0.05f, 0.8f, 0.05f), Look = "WOOD" });
            switch (spec.use)
            {
                case "HEAT":
                    plan.Parts.Add(new Part { Shape = Shape.Cylinder, Position = at + Vector3.up * 0.86f, Rotation = Quaternion.identity, Size = new Vector3(0.1f, 0.16f, 0.1f), Look = "METAL" });
                    plan.Parts.Add(new Part { Shape = Shape.Sphere, Position = at + Vector3.up * 0.97f, Rotation = Quaternion.identity, Size = Vector3.one * 0.09f, Look = "LIGHT", Ember = true });
                    break;
                case "LIGHT":
                    plan.Parts.Add(new Part { Shape = Shape.Sphere, Position = at + Vector3.up * 0.88f, Rotation = Quaternion.identity, Size = Vector3.one * 0.16f, Look = "LIGHT" });
                    break;
                default:
                    plan.Parts.Add(new Part { Shape = Shape.Box, Position = at + Vector3.up * 0.82f, Rotation = Quaternion.identity, Size = new Vector3(0.5f, 0.07f, 0.09f), Look = "METAL" });
                    break;
            }
            return null;
        }

        /// <summary>Anything else, from the pieces the model laid out (tenths of a metre, the ground at 0), turned to face the survivor.</summary>
        string Custom(Plan plan, Request r, float size)
        {
            var spec = r.Spec;
            Vector3 at = Stand(r, out float baseY);
            plan.Base = at;
            if (spec.parts == null || spec.parts.Length == 0)
            {
                // Nothing laid out: a block of the size asked for.
                return Block(plan, r, size);
            }
            foreach (var p in spec.parts)
            {
                Vector3 local = new Vector3(p.x, p.y, p.z) * 0.1f;
                var part = new Part
                {
                    Shape = p.shape switch { "CYLINDER" => Shape.Cylinder, "SPHERE" => Shape.Sphere, "CONE" => Shape.Cone, _ => Shape.Box },
                    Position = at + plan.Facing * local,
                    Rotation = plan.Facing,
                    Size = new Vector3(p.w, p.h, p.d) * 0.1f,
                    Look = p.look,
                };
                plan.Parts.Add(part);
            }
            // Resting on the ground: the lowest piece's foot at the ground.
            float lowest = plan.Parts.Min(p => p.Position.y - p.Size.y * 0.5f);
            for (int i = 0; i < plan.Parts.Count; i++)
            {
                var p = plan.Parts[i];
                p.Position.y += baseY - lowest + 0.02f;
                plan.Parts[i] = p;
            }
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
        }

        public Sketch Show(Plan plan)
        {
            var sketch = new Sketch { Plan = plan, Root = new GameObject("Plan") };
            foreach (var part in plan.Parts)
            {
                var piece = Piece(part, ghost, sketch.Root.transform, false);
                Range(piece, RevealRangeId);
                sketch.Pieces.Add(piece);
            }
            Play(planClip, 0.6f);
            StartCoroutine(Reveal(sketch));
            return sketch;
        }

        IEnumerator Reveal(Sketch sketch)
        {
            var block = new MaterialPropertyBlock();
            for (float t = 0f; t <= 1f; t += Time.deltaTime / 0.7f)
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
        }

        public void Hide(Sketch sketch)
        {
            if (sketch?.Root) Destroy(sketch.Root);
        }

        // ---- Making ----

        /// <summary>
        /// Makes the plan: the drone hovers near and its beam forms each piece where the plan drew it,
        /// bottom first. Then the thing becomes what it is: heavy, hot, glowing, floating, springing,
        /// carriable or a tool. Calls back with it.
        /// </summary>
        public IEnumerator Make(Sketch sketch, Drone drone, string words, Action<Built> done)
        {
            Busy = true;
            var plan = sketch.Plan;
            if (made.Count(b => b) >= most)
            {
                var oldest = made.FirstOrDefault(b => b);
                if (oldest) StartCoroutine(Unmake(oldest));
            }
            var holder = new GameObject(plan.Summary).AddComponent<Built>();
            holder.transform.SetPositionAndRotation(plan.Base, plan.Facing);
            count++;
            holder.Set("MADE_" + count, plan.Spec, words);
            float bottom = plan.Parts.Min(p => p.Position.y);
            var order = Enumerable.Range(0, plan.Parts.Count)
                .OrderBy(i => plan.Parts[i].Position.y - plan.Parts[i].Size.y * 0.5f > 0.5f + bottom ? 1 : 0)
                .ThenBy(i => Vector3.Distance(plan.Parts[i].Position, drone.transform.position)).ToList();
            float gap = Mathf.Clamp(2.6f / order.Count, 0.04f, 0.2f);
            var forming = new List<Coroutine>();
            var pieces = new GameObject[plan.Parts.Count];
            for (int n = 0; n < order.Count; n++)
            {
                int i = order[n];
                drone.Beam(plan.Parts[i].Position);
                forming.Add(StartCoroutine(Form(plan.Parts[i], holder, sketch.Pieces[i], n, piece => pieces[i] = piece)));
                yield return new WaitForSeconds(gap);
            }
            foreach (var f in forming) yield return f;
            // In the plan's order again, so a lift's platform or a spring's pad can be found by place.
            holder.Pieces.Clear();
            holder.Pieces.AddRange(pieces.Where(p => p));
            drone.Beam(null);
            Hide(sketch);
            Become(holder, plan);
            Play(doneClip, 0.8f);
            made.Add(holder);
            Busy = false;
            done?.Invoke(holder);
        }

        IEnumerator Form(Part part, Built holder, Renderer sketchPiece, int index, Action<GameObject> formed)
        {
            if (sketchPiece) sketchPiece.enabled = false;
            var piece = Piece(part, looks[LookIndex(part.Look)], holder.transform, true);
            Range(piece, BuildRangeId);
            holder.Pieces.Add(piece.gameObject);
            formed(piece.gameObject);
            if (part.Ember)
            {
                var ember = new MaterialPropertyBlock();
                piece.GetPropertyBlock(ember);
                ember.SetColor(EmissionId, new Color(4f, 1.2f, 0.25f));
                piece.SetPropertyBlock(ember);
            }
            int lookIndex = LookIndex(part.Look);
            if (lockClips != null && lookIndex < lockClips.Length && lockClips[lookIndex] && index % 2 == 0) Play(lockClips[lookIndex], 0.5f, piece.transform.position);
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
        }

        /// <summary>What it is decides what it does.</summary>
        void Become(Built holder, Plan plan)
        {
            var spec = plan.Spec;
            float size = SizeScale(spec.size);
            float mass = plan.Parts.Sum(p => p.Volume * Density(p.Look)) * WeightFactor(spec.weight);
            if (spec.floats) mass = Mathf.Min(mass, 3f);
            holder.Mass = Mathf.Clamp(mass, 0.3f, 20000f);
            float reach = plan.Extent;
            bool way = spec.form is "BRIDGE" or "STAIRS" or "RAMP" or "LIFT" or "PLATFORM" or "WALL" or "PILLAR";
            bool loose = !way && spec.form != "SPRING" && (spec.form != "LAMP" || plan.Parts.Count <= 3) && reach < 3.5f;

            // Ice is slippery underfoot; bouncy things bounce.
            foreach (var piece in holder.Pieces)
            {
                var collider = piece.GetComponent<Collider>();
                if (!collider) continue;
                if (spec.bouncy && bouncy) collider.sharedMaterial = bouncy;
                else if (piece.GetComponent<Renderer>().sharedMaterial == looks[LookIndex("ICE")] && slippery) collider.sharedMaterial = slippery;
            }

            if (loose)
            {
                var body = holder.gameObject.AddComponent<Rigidbody>();
                body.mass = holder.Mass;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.angularDamping = 0.6f;
                if (spec.floats) body.isKinematic = true;
            }

            // Heat, light.
            if (spec.heat == "HOT" || spec.heat == "WARM")
            {
                Heat.Add(holder.gameObject, spec.heat == "HOT", Mathf.Max(size, 0.8f));
                if (spec.form == "FIRE") Flames(holder.transform, plan.Base + Vector3.up * 0.75f * Mathf.Clamp(size, 0.4f, 2f), size);
                if (fireClip && spec.form == "FIRE")
                {
                    var crackle = holder.gameObject.AddComponent<AudioSource>();
                    crackle.clip = fireClip;
                    crackle.loop = true;
                    crackle.spatialBlend = 1f;
                    crackle.minDistance = 2f;
                    crackle.maxDistance = 25f;
                    crackle.volume = 0.5f;
                    crackle.Play();
                }
            }
            if (spec.glow != "NONE" || plan.Parts.Any(p => p.Look == "LIGHT"))
            {
                var glowing = holder.Pieces.FirstOrDefault(p => p.GetComponent<Renderer>().sharedMaterial == looks[LookIndex("LIGHT")]) ?? holder.gameObject;
                var lamp = new GameObject("Glow").AddComponent<Light>();
                lamp.transform.SetParent(glowing.transform, false);
                lamp.type = LightType.Point;
                bool bright = spec.glow == "BRIGHT";
                lamp.color = spec.heat == "HOT" ? new Color(1f, 0.55f, 0.25f) : spec.look == "ICE" || spec.look == "GLASS" ? new Color(0.7f, 0.85f, 1f) : new Color(1f, 0.86f, 0.66f);
                lamp.range = (bright ? 12f : 6f) * Mathf.Clamp(size, 0.6f, 1.6f);
                lamp.intensity = bright ? 2.6f : 1.3f;
                lamp.shadows = bright && plan.Parts.Count > 2 ? LightShadows.Soft : LightShadows.None;
            }

            // Ways that move or throw.
            if (spec.form == "SPRING")
            {
                var spring = holder.gameObject.AddComponent<Spring>();
                spring.Speed = Mathf.Lerp(10f, 19f, Mathf.InverseLerp(0.5f, 2f, size));
                spring.BoingClip = boingClip;
                spring.SetPad(holder.Pieces[holder.Pieces.Count - 1].transform);
            }
            else if (spec.bouncy && !loose)
            {
                var spring = holder.gameObject.AddComponent<Spring>();
                spring.Speed = 11f;
                spring.BoingClip = boingClip;
            }
            if (spec.form == "LIFT")
            {
                var lift = holder.gameObject.AddComponent<Lift>();
                // The platform moves by its transform; a body on it would snap it back.
                var platform = holder.Pieces[0].transform;
                lift.Set(platform, plan.Bottom - 0.1f, plan.Top - 0.1f);
                if (liftClip)
                {
                    lift.Sound = holder.gameObject.AddComponent<AudioSource>();
                    lift.Sound.clip = liftClip;
                    lift.Sound.loop = true;
                    lift.Sound.spatialBlend = 1f;
                    lift.Sound.volume = 0.5f;
                }
            }

            // Small things can be carried; balloons float; tools are tools.
            if (loose && (holder.Mass <= 60f || spec.floats))
            {
                var holdable = holder.gameObject.AddComponent<Holdable>();
                holdable.Title = spec.name;
                holdable.Mass = spec.floats ? 1f : holder.Mass;
                holdable.OneHand = spec.form is "TOOL" or "WEAPON" or "SHIELD" or "THROWN" or "GLIDER" || spec.floats || spec.form == "LAMP";
                // Held by its lowest point: a handle, a balloon's cord, a lantern's base.
                float lowest = plan.Parts.Min(p => p.Position.y - p.Size.y * 0.5f);
                Vector3 grip = plan.Parts.OrderBy(p => p.Position.y - p.Size.y * 0.5f).First().Position;
                grip.y = spec.form is "TOOL" or "WEAPON" ? lowest + 0.12f : spec.floats ? plan.Parts[0].Position.y : lowest + 0.02f;
                holdable.Grip = holder.transform.InverseTransformPoint(grip);
            }
            if (spec.floats)
            {
                var floater = holder.gameObject.AddComponent<Floater>();
                floater.Lift = Mathf.Lerp(1.1f, 2.6f, Mathf.InverseLerp(0.5f, 2.2f, size));
            }
            // Things to fight and fly with.
            var tip = holder.Pieces.OrderByDescending(p => p.transform.position.y).First().transform;
            switch (spec.form)
            {
                case "WEAPON":
                    var weapon = holder.gameObject.AddComponent<Weapon>();
                    weapon.Type = spec.use switch { "BLADE" => Weapon.Kind.Blade, "BEAM" => Weapon.Kind.Beam, "SPREAD" => Weapon.Kind.Spread, _ => Weapon.Kind.Bolt };
                    weapon.Power = spec.power;
                    weapon.Reach = spec.reach;
                    weapon.Rate = spec.rate;
                    weapon.Tip = tip;
                    weapon.FireClip = weapon.Type == Weapon.Kind.Blade ? swingClip : boltClip;
                    if (weapon.Type == Weapon.Kind.Beam)
                    {
                        var ray = new GameObject("Beam").AddComponent<LineRenderer>();
                        ray.transform.SetParent(holder.transform, false);
                        ray.useWorldSpace = true;
                        ray.positionCount = 2;
                        ray.sharedMaterial = flame;
                        ray.startColor = new Color(0.5f, 0.95f, 1f, 1f);
                        ray.endColor = new Color(0.4f, 0.85f, 1f, 0.6f);
                        ray.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        ray.enabled = false;
                        weapon.SetBeam(ray);
                    }
                    break;
                case "THROWN":
                    var charges = holder.gameObject.AddComponent<Throwable>();
                    charges.Flare = spec.use == "FLARE";
                    charges.Charges = 2 + spec.rate;
                    charges.Power = spec.power;
                    charges.Glow = looks[LookIndex("LIGHT")];
                    break;
                case "SHIELD":
                    holder.gameObject.AddComponent<Shield>();
                    break;
                case "GLIDER":
                    holder.gameObject.AddComponent<Glider>();
                    break;
                case "MIRROR":
                    holder.gameObject.AddComponent<MirrorWall>();
                    break;
                case "DECOY":
                    holder.gameObject.AddComponent<Decoy>();
                    if (spec.heat == "NONE" || spec.heat == "COLD") Heat.Add(holder.gameObject, false, 0.8f);
                    break;
            }

            if (spec.form == "TOOL" || spec.use is "BREAK" or "HEAT" or "LIGHT" && loose && reach < 1.5f)
            {
                var tool = holder.gameObject.AddComponent<Tool>();
                tool.Kind = spec.use switch { "HEAT" => Tool.Use.Heat, "LIGHT" => Tool.Use.Light, "BREAK" => Tool.Use.Break, _ => Tool.Use.Break };
                var top = holder.Pieces.OrderByDescending(p => p.transform.position.y).First().transform;
                tool.Tip = top;
                if (tool.Kind == Tool.Use.Heat)
                {
                    var line = new GameObject("Flame").AddComponent<LineRenderer>();
                    line.transform.SetParent(holder.transform, false);
                    line.useWorldSpace = true;
                    line.positionCount = 2;
                    line.sharedMaterial = flame;
                    line.widthCurve = new AnimationCurve(new Keyframe(0f, 0.12f), new Keyframe(1f, 0.02f));
                    line.startColor = new Color(1f, 0.6f, 0.25f, 1f);
                    line.endColor = new Color(1f, 0.3f, 0.1f, 0.3f);
                    line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    var light = new GameObject("Torch Light").AddComponent<Light>();
                    light.transform.SetParent(top, false);
                    light.type = LightType.Point;
                    light.color = new Color(1f, 0.5f, 0.2f);
                    light.range = 5f;
                    light.intensity = 0.6f;
                    tool.SetFlame(line, light);
                }
            }
        }

        /// <summary>Flames over a fire: soft additive flickers rising and fading.</summary>
        void Flames(Transform parent, Vector3 at, float size)
        {
            var system = new GameObject("Flames").AddComponent<ParticleSystem>();
            system.transform.SetParent(parent, false);
            system.transform.position = at;
            system.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f * size, 0.6f * size);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.2f, 0.8f), new Color(1f, 0.3f, 0.08f, 0.6f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;
            var emission = system.emission;
            emission.rateOverTime = 40f * Mathf.Clamp(size, 0.5f, 2f);
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.3f * size;
            var colour = system.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.8f, 0.4f), 0f), new GradientColorKey(new Color(1f, 0.25f, 0.05f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.15f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;
            var sizeOver = system.sizeOverLifetime;
            sizeOver.enabled = true;
            sizeOver.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.4f, 1f), new Keyframe(1f, 0.2f)));
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = flame;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>Takes a made thing apart: its pieces fade back into snow.</summary>
        public IEnumerator Unmake(Built built)
        {
            if (!built) yield break;
            made.Remove(built);
            var holdable = built.GetComponent<Holdable>();
            if (holdable && holdable.Held) yield break;
            Play(apartClip, 0.7f);
            var block = new MaterialPropertyBlock();
            var pieces = built.Pieces.Where(p => p).Select(p => p.GetComponent<Renderer>()).ToList();
            foreach (var body in built.GetComponentsInChildren<Rigidbody>()) body.isKinematic = true;
            foreach (var light in built.GetComponentsInChildren<Light>()) light.enabled = false;
            foreach (var heat in built.GetComponentsInChildren<Heat>()) heat.enabled = false;
            foreach (var flames in built.GetComponentsInChildren<ParticleSystem>()) flames.Stop();
            for (float t = 1f; t >= 0f; t -= Time.deltaTime / 0.6f)
            {
                foreach (var piece in pieces)
                {
                    if (!piece) continue;
                    piece.GetPropertyBlock(block);
                    block.SetFloat(BuildId, t);
                    piece.SetPropertyBlock(block);
                }
                yield return null;
            }
            if (built) Destroy(built.gameObject);
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

        public Built Find(string id) => made.FirstOrDefault(b => b && b.Id == id);

        Renderer Piece(Part part, Material material, Transform parent, bool solid)
        {
            var piece = new GameObject(part.Shape.ToString());
            piece.transform.SetParent(parent, true);
            piece.transform.SetPositionAndRotation(part.Position, part.Rotation);
            piece.transform.localScale = part.Shape == Shape.Cylinder ? new Vector3(part.Size.x, part.Size.y * 0.5f, part.Size.z) : part.Size;
            if (!cone && part.Shape == Shape.Cone) cone = MakeCone();
            piece.AddComponent<MeshFilter>().sharedMesh = part.Shape switch { Shape.Box => cube, Shape.Cylinder => cylinder, Shape.Cone => cone, _ => sphere };
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
                Shape.Cone => piece.AddComponent<SphereCollider>(),
                _ => piece.AddComponent<CapsuleCollider>(),
            };
            if (collider is CapsuleCollider capsule)
            {
                capsule.height = 2f;
                capsule.radius = 0.5f;
                // Flat, wide cylinders stand on their faces better as boxes.
                if (part.Size.y < part.Size.x * 0.8f)
                {
                    Destroy(capsule);
                    piece.AddComponent<BoxCollider>().size = new Vector3(0.9f, 2f, 0.9f);
                }
            }
            if (collider is SphereCollider ball && part.Shape == Shape.Cone) ball.radius = 0.4f;
            return renderer;
        }

        static Mesh MakeCone()
        {
            const int sides = 24;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * 0.5f, -0.5f, Mathf.Sin(a0) * 0.5f);
                Vector3 p1 = new Vector3(Mathf.Cos(a1) * 0.5f, -0.5f, Mathf.Sin(a1) * 0.5f);
                Vector3 top = new Vector3(0f, 0.5f, 0f);
                Vector3 n = Vector3.Cross(top - p0, p1 - p0).normalized;
                if (n.y < 0f) n = -n;
                int b = vertices.Count;
                vertices.AddRange(new[] { p0, top, p1 });
                normals.AddRange(new[] { n, n, n });
                triangles.AddRange(new[] { b, b + 1, b + 2 });
                b = vertices.Count;
                vertices.AddRange(new[] { p0, p1, new Vector3(0f, -0.5f, 0f) });
                normals.AddRange(new[] { Vector3.down, Vector3.down, Vector3.down });
                triangles.AddRange(new[] { b, b + 1, b + 2 });
            }
            var mesh = new Mesh { name = "Cone" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // Where the piece starts and how tall it is, so it forms (or is drawn) from the bottom up.
        static void Range(Renderer piece, int id)
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
