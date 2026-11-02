using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>
    /// The traveller, walked from behind: W A S D walk the way the camera faces, Shift runs, Space
    /// jumps, E gathers the loose piece in reach for the mind. They push loose things they walk into.
    /// Falling far brings them back to the last firm ground. The centre of the view picks out what
    /// they are looking at, which is what "this" and "that" mean when they talk to the mind.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class Traveller : MonoBehaviour
    {
        [SerializeField] FollowCamera follow;
        [SerializeField] Companion mind;
        [SerializeField] Hud hud;
        [SerializeField, Tooltip("Turns to face where it walks; its pivot is at the feet.")] Transform body;
        [SerializeField] float walkSpeed = 2.3f;
        [SerializeField] float runSpeed = 5.4f;
        [SerializeField] float acceleration = 9f;
        [SerializeField] float airControl = 2.5f;
        [SerializeField] float turnSpeed = 11f;
        [SerializeField] float jumpSpeed = 5.6f;
        [SerializeField] float gravity = 17f;
        [SerializeField, Tooltip("How far E reaches for a loose piece.")] float reach = 2.8f;
        [SerializeField, Tooltip("Below this height the traveller has fallen.")] float fallHeight = -18f;
        [SerializeField, Tooltip("What the centre of the view can pick out.")] LayerMask seen = ~0;

        public event Action Jumped;
        public event Action<float> Landed;
        public event Action<Pickup> Gathered;
        public event Action Fell;

        public Vector3 Velocity => velocity;
        public bool Grounded { get; private set; }
        /// <summary>0 standing, 1 walking, 2 running.</summary>
        public float Pace { get; private set; }
        /// <summary>The keys write words, so they do not move the traveller.</summary>
        public bool Typing { get; set; }
        /// <summary>A scripted moment holds the traveller still.</summary>
        public bool Scripted { get; set; }
        /// <summary>Walks as if these keys were held (x right, y forward), for scripted moments and tests.</summary>
        public Vector2 Drive { get; set; }
        public bool DriveRun { get; set; }
        /// <summary>What the centre of the view rests on, if it has a name.</summary>
        public Landmark Aimed { get; private set; }
        /// <summary>The loose piece the centre of the view rests on, or the nearest in reach.</summary>
        public Pickup InReach { get; private set; }
        /// <summary>Where the centre of the view meets the world, if it does within 80 m.</summary>
        public Vector3? AimPoint { get; private set; }
        /// <summary>The last time the traveller reached for something (the body animates it).</summary>
        public float ReachedAt { get; private set; } = -10f;
        public Vector3 ReachedFor { get; private set; }
        public Transform Body => body;

        CharacterController controller;
        Vector3 velocity, safe;
        float lastSafe, airTime;
        bool falling;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            safe = transform.position;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var keys = Keyboard.current;
            bool free = !Typing && !Scripted && !falling && keys != null;

            // Where the keys point, flat, the way the camera faces.
            Vector2 input = Vector2.zero;
            if (free)
            {
                if (keys.wKey.isPressed) input.y += 1f;
                if (keys.sKey.isPressed) input.y -= 1f;
                if (keys.dKey.isPressed) input.x += 1f;
                if (keys.aKey.isPressed) input.x -= 1f;
            }
            if (Drive != Vector2.zero && !falling) input = Drive;
            input = Vector2.ClampMagnitude(input, 1f);
            var flat = Quaternion.Euler(0f, follow.Yaw, 0f);
            Vector3 wish = flat * new Vector3(input.x, 0f, input.y);
            bool running = free && keys.leftShiftKey.isPressed || Drive != Vector2.zero && DriveRun;
            float speed = running ? runSpeed : walkSpeed;

            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            float control = Grounded ? acceleration : airControl;
            // Made ice is slippery: little grip, and on a slope the traveller slides down it.
            Vector3 slide = Vector3.zero;
            if (Grounded && OnIce(out var slope))
            {
                control = 0.9f;
                float tilt = Vector3.Angle(slope, Vector3.up);
                if (tilt > 6f) slide = new Vector3(slope.x, 0f, slope.z).normalized * gravity * Mathf.Sin(tilt * Mathf.Deg2Rad) * 0.85f;
            }
            horizontal = Vector3.MoveTowards(horizontal, wish * speed, control * speed * dt) + slide * dt;
            velocity.x = horizontal.x;
            velocity.z = horizontal.z;

            if (Grounded)
            {
                velocity.y = -2f;
                if (free && keys.spaceKey.wasPressedThisFrame)
                {
                    velocity.y = jumpSpeed;
                    Grounded = false;
                    Jumped?.Invoke();
                }
            }
            else velocity.y -= gravity * dt;

            var flags = controller.Move(velocity * dt);
            bool wasGrounded = Grounded;
            Grounded = (flags & CollisionFlags.Below) != 0;
            if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0f) velocity.y = 0f;
            if (Grounded && !wasGrounded) Landed?.Invoke(airTime);
            airTime = Grounded ? 0f : airTime + dt;

            Pace = Mathf.MoveTowards(Pace, horizontal.magnitude < 0.2f ? 0f : horizontal.magnitude <= walkSpeed + 0.1f ? horizontal.magnitude / walkSpeed : 1f + (horizontal.magnitude - walkSpeed) / (runSpeed - walkSpeed), dt * 6f);
            if (horizontal.sqrMagnitude > 0.05f && body)
            {
                var facing = Quaternion.LookRotation(horizontal.normalized, Vector3.up);
                body.rotation = Quaternion.Slerp(body.rotation, facing, 1f - Mathf.Exp(-turnSpeed * dt));
            }

            RememberSafeGround();
            if (transform.position.y < fallHeight && !falling) StartCoroutine(Fall());

            Look();
            if (free && keys.eKey.wasPressedThisFrame) Gather();
        }

        /// <summary>Puts the traveller somewhere, facing a way (tests and scripted moments).</summary>
        public void Place(Vector3 position, float yaw)
        {
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
            velocity = Vector3.zero;
            if (body) body.rotation = Quaternion.Euler(0f, yaw, 0f);
            safe = position;
        }

        public void Gather()
        {
            if (!InReach || InReach.Taken) return;
            ReachedAt = Time.time;
            ReachedFor = InReach.transform.position;
            var piece = InReach;
            piece.Gather(mind);
            InReach = null;
            Gathered?.Invoke(piece);
        }

        void Look()
        {
            var camera = follow.View;
            var ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Aimed = null;
            AimPoint = null;
            Pickup looked = null;
            // Small named things are seen by their own trigger; places are named by the region that
            // holds the point where the view meets the ground.
            var hits = Physics.RaycastAll(ray, 80f, seen, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
            foreach (var hit in hits)
            {
                var landmark = hit.collider.GetComponentInParent<Landmark>();
                if (hit.collider.isTrigger)
                {
                    if (!landmark || !landmark.Known || hit.collider.bounds.size.magnitude > 4f) continue;
                    Aimed = landmark;
                    AimPoint = hit.point;
                    break;
                }
                AimPoint = hit.point;
                looked = hit.collider.GetComponentInParent<Pickup>();
                Aimed = landmark && landmark.Known ? landmark : Region(hit.point);
                break;
            }
            // The piece looked at if it is in reach, else the nearest in front within reach.
            Pickup best = looked && !looked.Taken && Vector3.Distance(looked.transform.position, transform.position) < reach ? looked : null;
            if (!best)
            {
                float bestScore = float.MaxValue;
                foreach (var c in Physics.OverlapSphere(transform.position + Vector3.up * 0.5f, reach, ~0, QueryTriggerInteraction.Collide))
                {
                    var p = c.GetComponentInParent<Pickup>();
                    if (!p || p.Taken) continue;
                    Vector3 to = p.transform.position - transform.position;
                    float score = to.magnitude - 0.8f * Vector3.Dot(to.normalized, body ? body.forward : transform.forward);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = p;
                    }
                }
            }
            InReach = best;
            if (!hud) return;
            if (InReach) hud.Aim(Matters.Word(InReach.Matter), "[E] gather");
            else if (Aimed) hud.Aim(Aimed.Title, null);
            else if (looked) hud.Aim(Matters.Word(looked.Matter), null);
            else hud.Aim(null, null);
        }

        bool OnIce(out Vector3 normal)
        {
            normal = Vector3.up;
            if (!Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.down, out var hit, 0.8f, seen, QueryTriggerInteraction.Ignore)) return false;
            var built = hit.collider.GetComponentInParent<Built>();
            normal = hit.normal;
            return built && built.Matter == Matter.Ice;
        }

        /// <summary>The smallest named region whose bounds hold the point.</summary>
        static Landmark Region(Vector3 point)
        {
            Landmark best = null;
            float smallest = float.MaxValue;
            foreach (var collider in Physics.OverlapSphere(point, 0.1f, ~0, QueryTriggerInteraction.Collide))
            {
                if (!collider.isTrigger) continue;
                var landmark = collider.GetComponentInParent<Landmark>();
                if (!landmark || !landmark.Known) continue;
                float size = collider.bounds.size.sqrMagnitude;
                if (size >= smallest) continue;
                smallest = size;
                best = landmark;
            }
            return best;
        }

        void RememberSafeGround()
        {
            if (!Grounded || falling || transform.position.y < fallHeight + 3f || Time.time - lastSafe < 0.4f) return;
            // Firm ground: level, and ground under all four sides, not the lip of a drop.
            Vector3 p = transform.position + Vector3.up * 0.3f;
            bool firm = true;
            foreach (var offset in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                if (!Physics.Raycast(p + offset * 0.7f, Vector3.down, 1.6f, seen, QueryTriggerInteraction.Ignore)) firm = false;
            if (!firm) return;
            safe = transform.position;
            lastSafe = Time.time;
        }

        IEnumerator Fall()
        {
            falling = true;
            Fell?.Invoke();
            for (float t = 0f; t < 0.8f; t += Time.deltaTime)
            {
                if (hud) hud.Black = t / 0.8f;
                yield return null;
            }
            Place(safe, body ? body.eulerAngles.y : 0f);
            if (mind) mind.Snap();
            yield return new WaitForSeconds(0.3f);
            for (float t = 0f; t < 1f; t += Time.deltaTime)
            {
                if (hud) hud.Black = 1f - t;
                yield return null;
            }
            if (hud) hud.Black = 0f;
            falling = false;
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            var other = hit.rigidbody;
            if (other == null || other.isKinematic || hit.moveDirection.y < -0.3f) return;
            Vector3 push = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
            other.AddForceAtPosition(push * Mathf.Min(other.mass, 400f) * 1.6f, hit.point, ForceMode.Force);
        }
    }
}
