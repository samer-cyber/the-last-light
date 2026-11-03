using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>
    /// Shots, from either side. A machine's bolt flies at its mark; a mirror wall throws it back the
    /// way it came (and then it hurts machines); a shield held toward it stops it; the survivor it
    /// reaches loses warmth. The survivor's bolts and beams hurt whatever machine they reach first.
    /// </summary>
    public static class Shots
    {
        public static readonly Color MachineRed = new Color(1f, 0.16f, 0.1f);
        public static readonly Color Cyan = new Color(0.45f, 0.92f, 1f);
        public static int Mask = ~0;

        /// <summary>A machine fires at a point. Returns where the bolt ended.</summary>
        public static Vector3 FromMachine(Machine shooter, Vector3 from, Vector3 at, float damage, float range = 60f)
        {
            Vector3 dir = (at - from).normalized;
            for (int bounce = 0; bounce < 2; bounce++)
            {
                if (!Physics.Raycast(from, dir, out var hit, range, Mask, QueryTriggerInteraction.Ignore))
                {
                    Fx.Bolt(from, from + dir * range, MachineRed);
                    return from + dir * range;
                }
                Fx.Bolt(from, hit.point, bounce == 0 ? MachineRed : Cyan, bounce == 0 ? 0.07f : 0.1f);
                var warmth = hit.collider.GetComponentInParent<Warmth>();
                if (warmth)
                {
                    warmth.Hit(damage, from);
                    return hit.point;
                }
                var mirror = hit.collider.GetComponentInParent<MirrorWall>();
                if (mirror)
                {
                    // Thrown back: straight at whoever fired it, if it can see them.
                    mirror.Flash(hit.point);
                    from = hit.point;
                    dir = shooter && !shooter.Broken ? ((shooter.WeakPoint ? shooter.WeakPoint.position : shooter.transform.position) - from).normalized : Vector3.Reflect(dir, hit.normal);
                    damage *= 3f;
                    continue;
                }
                var machine = hit.collider.GetComponentInParent<Machine>();
                if (machine && bounce > 0) machine.Damage(damage * 4f, hit.point, "REFLECTED");
                var decoy = hit.collider.GetComponentInParent<Decoy>();
                if (decoy) decoy.Hit();
                Fx.Sparks(hit.point, 8, bounce == 0 ? MachineRed : Cyan);
                return hit.point;
            }
            return from;
        }

        /// <summary>The survivor fires: the first thing hit along the line, hurt if it is a machine.</summary>
        public static Vector3 FromSurvivor(Vector3 from, Vector3 dir, float range, float damage, string kind, Color colour, float width = 0.05f, bool tracer = true)
        {
            Vector3 end = from + dir * range;
            if (Physics.Raycast(from, dir, out var hit, range, Mask & ~(1 << LayerMask.NameToLayer("Traveller")), QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                var machine = hit.collider.GetComponentInParent<Machine>();
                if (machine) machine.Damage(damage, hit.point, kind);
                else Fx.Sparks(hit.point, 4, colour);
            }
            if (tracer) Fx.Bolt(from, end, colour, width);
            return end;
        }
    }

    /// <summary>
    /// A weapon the drone made, in the survivor's hands, against machines only. Its power, reach and
    /// rate (1 to 5, ten together at most) set how hard it hits, how far, and how often:
    /// a heat blade swung close, a beam held on, bolts fired one by one, or a spread of them at once.
    /// </summary>
    public class Weapon : MonoBehaviour
    {
        public enum Kind { Blade, Beam, Bolt, Spread }

        public Kind Type = Kind.Bolt;
        public int Power = 3, Reach = 3, Rate = 3;
        public Transform Tip;
        public AudioClip FireClip;

        float readyAt, heat;
        LineRenderer beam;
        AudioSource sound;

        public float SwungAt { get; private set; } = -10f;
        /// <summary>The beam overheats if held too long: 0 cool, 1 too hot to fire.</summary>
        public float Heat => heat;

        public float Range => Type switch
        {
            Kind.Blade => 2.2f + 0.2f * Reach,
            Kind.Beam => 8f + 4f * Reach,
            Kind.Spread => 8f + 4f * Reach,
            _ => 15f + 18f * Reach,
        };

        float Cooldown => Type switch
        {
            Kind.Blade => 0.75f - 0.09f * Rate,
            Kind.Spread => 1.5f - 0.2f * Rate,
            _ => 1.45f - 0.24f * Rate,
        };

        void Awake() => sound = gameObject.AddComponent<AudioSource>();

        public void SetBeam(LineRenderer line) => beam = line;

        /// <summary>Called each frame while held: the trigger's state and where the view points.</summary>
        public void Use(bool pressed, bool held, Ray aim, Transform holder)
        {
            heat = Mathf.MoveTowards(heat, 0f, Time.deltaTime * (Type == Kind.Beam && held ? 0f : 0.5f));
            Vector3 from = Tip ? Tip.position : transform.position;
            // Aim along the view, from the weapon's tip to what the centre of the view rests on.
            Vector3 mark = Physics.Raycast(aim, out var seen, 400f, Shots.Mask & ~(1 << LayerMask.NameToLayer("Traveller")), QueryTriggerInteraction.Ignore) ? seen.point : aim.origin + aim.direction * 400f;
            Vector3 dir = (mark - from).normalized;
            switch (Type)
            {
                case Kind.Blade:
                    if (!pressed || Time.time < readyAt) break;
                    readyAt = Time.time + Cooldown;
                    SwungAt = Time.time;
                    Play(0.8f);
                    // Anything in front, close: an arc, forgiving in height (Seekers hover).
                    Vector3 forward = Vector3.ProjectOnPlane(aim.direction, Vector3.up).normalized;
                    foreach (var m in Machine.All.ToArray())
                    {
                        if (!m || m.Broken) continue;
                        Vector3 to = m.transform.position - holder.position;
                        Vector3 flat = Vector3.ProjectOnPlane(to, Vector3.up);
                        if (flat.magnitude > Range || Mathf.Abs(to.y) > 3.2f || Vector3.Angle(forward, flat) > 70f) continue;
                        Vector3 at = m.WeakPoint && Vector3.Distance(m.WeakPoint.position, holder.position) < Range + 1f ? m.WeakPoint.position : m.transform.position;
                        m.Damage(14f + 7f * Power, at, "BLADE");
                    }
                    Fx.Sparks(from, 6, new Color(1f, 0.6f, 0.3f));
                    break;
                case Kind.Beam:
                    bool firing = held && heat < 1f;
                    if (beam) beam.enabled = firing;
                    if (!firing) break;
                    heat += Time.deltaTime / (1.6f + 0.7f * Rate);
                    Vector3 end = Shots.FromSurvivor(from, dir, Range, (10f + 7f * Power) * Time.deltaTime, "BEAM", Shots.Cyan, 0.05f, false);
                    if (beam)
                    {
                        beam.SetPosition(0, from);
                        beam.SetPosition(1, end);
                        beam.widthMultiplier = 0.05f + 0.02f * Mathf.Sin(Time.time * 60f);
                    }
                    if (!sound.isPlaying) Play(0.4f);
                    break;
                case Kind.Bolt:
                    if (!pressed || Time.time < readyAt) break;
                    readyAt = Time.time + Cooldown;
                    Shots.FromSurvivor(from, dir, Range, 8f + 9f * Power, "BOLT", Shots.Cyan, 0.04f + 0.015f * Power);
                    Play(0.7f);
                    break;
                case Kind.Spread:
                    if (!pressed || Time.time < readyAt) break;
                    readyAt = Time.time + Cooldown;
                    for (int i = 0; i < 6; i++)
                    {
                        var wobble = Quaternion.Euler(UnityEngine.Random.Range(-5f, 5f), UnityEngine.Random.Range(-6f, 6f), 0f);
                        Shots.FromSurvivor(from, wobble * dir, Range, 5f + 3f * Power, "SPREAD", Shots.Cyan, 0.03f);
                    }
                    Play(0.8f);
                    break;
            }
        }

        public void Stop()
        {
            if (beam) beam.enabled = false;
        }

        void Play(float volume)
        {
            if (!sound || !FireClip) return;
            sound.pitch = Type == Kind.Blade ? 1.4f : Type == Kind.Bolt ? Mathf.Lerp(1.3f, 0.7f, Power / 5f) : 1f;
            sound.PlayOneShot(FireClip, volume);
        }
    }

    /// <summary>A shield in the survivor's hands: machine fire from the front stops on it.</summary>
    public class Shield : MonoBehaviour
    {
        public bool Covers(Vector3 holder, Vector3 forward, Vector3 from) => Vector3.Angle(Vector3.ProjectOnPlane(forward, Vector3.up), Vector3.ProjectOnPlane(from - holder, Vector3.up)) < 75f;
    }

    /// <summary>A standing mirror: machine bolts that hit it fly back at whoever fired them.</summary>
    public class MirrorWall : MonoBehaviour
    {
        public void Flash(Vector3 at) => Fx.Sparks(at, 10, Shots.Cyan);
    }

    /// <summary>A warm lure: Seekers go for it instead. It glows and gives in after enough hits.</summary>
    public class Decoy : MonoBehaviour
    {
        public int Hits = 6;
        HeatSource heat;

        void Awake()
        {
            heat = gameObject.AddComponent<HeatSource>();
            heat.Pull = 2.5f;
        }

        public void Hit()
        {
            if (--Hits > 0) return;
            heat.enabled = false;
            Fx.Burst(transform.position, 0.6f);
            foreach (var light in GetComponentsInChildren<Light>()) light.enabled = false;
        }
    }

    /// <summary>Charges to throw: a shock stuns machines near where it lands; a flare draws Seekers to it.</summary>
    public class Throwable : MonoBehaviour
    {
        public bool Flare;
        public int Charges = 3;
        public int Power = 3;
        public Material Glow;
        float readyAt;

        public void Use(bool pressed, Ray aim, Transform holder)
        {
            if (!pressed || Time.time < readyAt || Charges <= 0) return;
            readyAt = Time.time + 0.8f;
            Charges--;
            var charge = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            charge.name = Flare ? "Flare" : "Shock Charge";
            charge.transform.localScale = Vector3.one * 0.18f;
            charge.transform.position = holder.position + Vector3.up * 1.5f + aim.direction * 0.6f;
            if (Glow) charge.GetComponent<Renderer>().sharedMaterial = Glow;
            var body = charge.AddComponent<Rigidbody>();
            body.mass = 0.4f;
            body.linearVelocity = aim.direction * 17f + Vector3.up * 4f;
            var light = new GameObject("Glow").AddComponent<Light>();
            light.transform.SetParent(charge.transform, false);
            light.type = LightType.Point;
            light.color = Flare ? new Color(1f, 0.45f, 0.2f) : Shots.Cyan;
            light.range = Flare ? 14f : 6f;
            light.intensity = Flare ? 5f : 3f;
            var land = charge.AddComponent<Landing>();
            land.Flare = Flare;
            land.Power = Power;
        }

        /// <summary>The charge in flight: on landing, the shock or the flare.</summary>
        class Landing : MonoBehaviour
        {
            public bool Flare;
            public int Power;
            bool done;

            void OnCollisionEnter(Collision collision)
            {
                if (done) return;
                done = true;
                if (Flare)
                {
                    var heat = gameObject.AddComponent<HeatSource>();
                    heat.Pull = 4f;
                    heat.Until = Time.time + 9f + Power;
                    Fx.Sparks(transform.position, 30, new Color(1f, 0.5f, 0.2f));
                    Destroy(gameObject, 10f + Power);
                    return;
                }
                Fx.Burst(transform.position, 0.5f);
                Fx.Sparks(transform.position, 40, Shots.Cyan);
                foreach (var m in Machine.All.ToArray())
                    if (m && Vector3.Distance(m.transform.position, transform.position) < 3.5f + Power * 0.6f) m.Stun(2.5f + Power * 0.6f);
                Destroy(gameObject);
            }
        }
    }

    /// <summary>Wings in the survivor's hands: jumping from a height, they glide.</summary>
    public class Glider : MonoBehaviour
    {
        public float FallSpeed = 2.2f;
        public float Forward = 9f;
    }
}
