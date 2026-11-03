using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>
    /// The survivor, walked from behind: W A S D walk the way the camera faces, Shift runs, Space
    /// jumps, E picks up what is in reach (or sets it down, or sets a heart in its socket), and the
    /// left mouse button (or F) uses a tool in hand: a pick strikes, a torch burns. A balloon held
    /// lifts them; a spring throws them; a lift carries them. They push loose things they walk into.
    /// Falling far brings them back to the last firm ground. The centre of the view picks out what
    /// they are looking at, which is what "this" and "that" mean when they talk to the drone.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class Traveller : MonoBehaviour
    {
        [SerializeField] FollowCamera follow;
        [SerializeField] Drone drone;
        [SerializeField] Hud hud;
        [SerializeField, Tooltip("Turns to face where it walks; its pivot is at the feet.")] Transform body;
        [SerializeField, Tooltip("Where things carried in both hands ride.")] Transform hands;
        [SerializeField, Tooltip("Where a tool or a cord is held.")] Transform hand;
        [SerializeField] float walkSpeed = 2.3f;
        [SerializeField] float runSpeed = 5.4f;
        [SerializeField] float acceleration = 9f;
        [SerializeField] float airControl = 2.5f;
        [SerializeField] float turnSpeed = 11f;
        [SerializeField] float jumpSpeed = 5.6f;
        [SerializeField] float gravity = 17f;
        [SerializeField, Tooltip("How far E reaches.")] float reach = 2.8f;
        [SerializeField, Tooltip("Below this height the survivor has fallen.")] float fallHeight = -18f;
        [SerializeField, Tooltip("What the centre of the view can pick out.")] LayerMask seen = ~0;
        [SerializeField] AudioSource sound;
        [SerializeField] AudioClip pickClip, dropClip, strikeClip, torchClip;

        public event Action Jumped;
        public event Action<float> Landed;
        public event Action<Holdable> Took;
        public event Action<Holdable> Placed;
        public event Action Fell;

        public Vector3 Velocity => velocity;
        public bool Grounded { get; private set; }
        /// <summary>0 standing, 1 walking, 2 running.</summary>
        public float Pace { get; private set; }
        /// <summary>The keys write words, so they do not move the survivor.</summary>
        public bool Typing { get; set; }
        /// <summary>A scripted moment holds the survivor still.</summary>
        public bool Scripted { get; set; }
        /// <summary>Walks as if these keys were held (x right, y forward), for scripted moments and tests.</summary>
        public Vector2 Drive { get; set; }
        public bool DriveRun { get; set; }
        /// <summary>What the centre of the view rests on, if it has a name.</summary>
        public Landmark Aimed { get; private set; }
        /// <summary>The made thing the centre of the view rests on, if any.</summary>
        public Built AimedBuilt { get; private set; }
        /// <summary>Something in reach to pick up: the one looked at, or the nearest in front.</summary>
        public Holdable InReach { get; private set; }
        /// <summary>What is in their hands.</summary>
        public Holdable Holding { get; private set; }
        /// <summary>Where the centre of the view meets the world, if it does within 80 m.</summary>
        public Vector3? AimPoint { get; private set; }
        /// <summary>The ice the centre of the view rests on, if it is close.</summary>
        public Frozen AimedIce { get; private set; }
        /// <summary>When set, the head turns to this (the drone, while they talk).</summary>
        public Vector3? Attending { get; set; }
        /// <summary>The last time the survivor reached for something (the body animates it).</summary>
        public float ReachedAt { get; private set; } = -10f;
        public Vector3 ReachedFor { get; private set; }
        public Transform Body => body;
        /// <summary>Rising on a balloon.</summary>
        public bool Floating => Holding && Holding.GetComponent<Floater>();

        CharacterController controller;
        Vector3 velocity, safe;
        float lastSafe, airTime, launch;
        bool falling;
        Socket aimedSocket;
        Lift riding;

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

            // Riding a lift: move with it first.
            if (riding && riding.Delta.sqrMagnitude > 0f) controller.Move(riding.Delta);

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
            float burden = Holding && !Floating && Holding.Mass > 20f ? 0.72f : 1f;
            float speed = (running ? runSpeed : walkSpeed) * burden;

            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            float control = Grounded ? acceleration : Floating ? 4f : airControl;
            // Made ice is slippery: little grip, and on a slope the survivor slides down it.
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

            if (launch > 0f)
            {
                // A spring threw them.
                velocity.y = launch;
                launch = 0f;
                Grounded = false;
                Jumped?.Invoke();
            }
            else if (Floating)
            {
                // A balloon in hand: up, slowly, as high as it will go.
                float ceiling = safe.y + 28f;
                velocity.y = Mathf.MoveTowards(velocity.y, transform.position.y < ceiling ? Holding.GetComponent<Floater>().Lift : 0f, dt * 3f);
            }
            else if (Grounded)
            {
                velocity.y = -2f;
                if (free && keys.spaceKey.wasPressedThisFrame)
                {
                    velocity.y = jumpSpeed * (burden < 1f ? 0.8f : 1f);
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
            riding = Grounded ? Under<Lift>() : null;
            if (riding) riding.Ridden = true;

            Pace = Mathf.MoveTowards(Pace, horizontal.magnitude < 0.2f ? 0f : horizontal.magnitude <= walkSpeed + 0.1f ? horizontal.magnitude / walkSpeed : 1f + (horizontal.magnitude - walkSpeed) / (runSpeed - walkSpeed), dt * 6f);
            if (horizontal.sqrMagnitude > 0.05f && body)
            {
                var facing = Quaternion.LookRotation(horizontal.normalized, Vector3.up);
                body.rotation = Quaternion.Slerp(body.rotation, facing, 1f - Mathf.Exp(-turnSpeed * dt));
            }

            RememberSafeGround();
            if (transform.position.y < fallHeight && !falling) StartCoroutine(Fall());

            Look();
            if (free && keys.eKey.wasPressedThisFrame) Use();
            UseTool(free);
        }

        /// <summary>Puts the survivor somewhere, facing a way (tests and scripted moments).</summary>
        public void Place(Vector3 position, float yaw)
        {
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
            velocity = Vector3.zero;
            if (body) body.rotation = Quaternion.Euler(0f, yaw, 0f);
            safe = position;
        }

        /// <summary>[E]: set the heart in its socket, set down what is held, or pick up what is in reach.</summary>
        public void Use()
        {
            if (Holding)
            {
                if (aimedSocket && !aimedSocket.Full && Holding.GetComponent<Heart>() && Vector3.Distance(aimedSocket.Seat, transform.position) < reach + 1.2f)
                {
                    var heart = Holding;
                    Holding = null;
                    heart.Drop(aimedSocket.Seat, Quaternion.identity);
                    aimedSocket.Fill(heart);
                    ReachedAt = Time.time;
                    ReachedFor = aimedSocket.Seat;
                    Play(dropClip);
                    Placed?.Invoke(heart);
                    return;
                }
                Release();
                return;
            }
            if (!InReach || !InReach.CanHold) return;
            Take(InReach);
        }

        /// <summary>Takes something into their hands (the drone may put it there).</summary>
        public void Take(Holdable thing)
        {
            if (!thing) return;
            if (Holding) Release();
            ReachedAt = Time.time;
            ReachedFor = thing.transform.position;
            thing.Hold(thing.OneHand ? hand : hands);
            Holding = thing;
            InReach = null;
            Play(pickClip);
            Took?.Invoke(thing);
        }

        /// <summary>Sets down what is held, in front of them.</summary>
        public void Release()
        {
            if (!Holding) return;
            var thing = Holding;
            Holding = null;
            var forward = body ? body.forward : transform.forward;
            Vector3 at = transform.position + forward * 0.9f + Vector3.up * (Floating ? 1f : 0.5f);
            thing.Drop(at, Quaternion.LookRotation(forward, Vector3.up));
            Play(dropClip);
        }

        void UseTool(bool free)
        {
            var tool = Holding ? Holding.GetComponent<Tool>() : null;
            if (!tool) return;
            var mouse = Mouse.current;
            var keys = Keyboard.current;
            bool pressed = free && (mouse != null && mouse.leftButton.wasPressedThisFrame || keys != null && keys.fKey.wasPressedThisFrame);
            bool held = free && (mouse != null && mouse.leftButton.isPressed || keys != null && keys.fKey.isPressed);
            switch (tool.Kind)
            {
                case Tool.Use.Break:
                    if (!pressed) break;
                    ReachedAt = Time.time;
                    ReachedFor = AimPoint ?? transform.position + transform.forward;
                    var target = AimedIce && AimPoint.HasValue && Vector3.Distance(AimPoint.Value, transform.position) < reach + 0.8f ? AimedIce : null;
                    tool.Strike(target);
                    if (target) Play(strikeClip);
                    break;
                case Tool.Use.Heat:
                    bool near = AimPoint.HasValue && Vector3.Distance(AimPoint.Value, transform.position) < 6f;
                    tool.Burn(held && near ? AimPoint : null, held && near ? AimedIce : null);
                    if (held && near && sound && torchClip && !sound.isPlaying) sound.PlayOneShot(torchClip, 0.4f);
                    break;
            }
        }

        void Look()
        {
            var camera = follow.View;
            var ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Aimed = null;
            AimPoint = null;
            AimedBuilt = null;
            AimedIce = null;
            aimedSocket = null;
            Holdable looked = null;
            // Small named things are seen by their own trigger; places are named by the region that
            // holds the point where the view meets the ground.
            var hits = Physics.RaycastAll(ray, 80f, seen, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
            foreach (var hit in hits)
            {
                if (Holding && hit.collider.transform.IsChildOf(Holding.transform)) continue;
                var landmark = hit.collider.GetComponentInParent<Landmark>();
                aimedSocket = aimedSocket ? aimedSocket : hit.collider.GetComponentInParent<Socket>();
                if (hit.collider.isTrigger)
                {
                    if (!landmark || !landmark.Known || hit.collider.bounds.size.magnitude > 4f) continue;
                    Aimed = landmark;
                    AimPoint = hit.point;
                    AimedIce = landmark.GetComponentInParent<Frozen>();
                    looked = landmark.GetComponentInParent<Holdable>();
                    break;
                }
                AimPoint = hit.point;
                looked = hit.collider.GetComponentInParent<Holdable>();
                AimedBuilt = hit.collider.GetComponentInParent<Built>();
                AimedIce = hit.collider.GetComponentInParent<Frozen>();
                Aimed = landmark && landmark.Known ? landmark : Region(hit.point);
                break;
            }
            // The thing looked at if it is in reach, else the nearest in front within reach.
            Holdable best = looked && looked.CanHold && Vector3.Distance(looked.transform.position, transform.position) < reach + 0.6f ? looked : null;
            if (!best && !Holding)
            {
                float bestScore = float.MaxValue;
                foreach (var c in Physics.OverlapSphere(transform.position + Vector3.up * 0.6f, reach, ~0, QueryTriggerInteraction.Collide))
                {
                    var h = c.GetComponentInParent<Holdable>();
                    if (!h || !h.CanHold) continue;
                    Vector3 to = h.transform.position - transform.position;
                    float score = to.magnitude - 0.8f * Vector3.Dot(to.normalized, body ? body.forward : transform.forward);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = h;
                    }
                }
            }
            InReach = Holding ? null : best;
            if (!hud) return;
            if (Scripted)
            {
                hud.HideAim();
                return;
            }
            string name = AimedBuilt ? AimedBuilt.Name : Aimed ? Aimed.Title : null;
            if (Holding)
            {
                bool socket = aimedSocket && !aimedSocket.Full && Holding.GetComponent<Heart>() && Vector3.Distance(aimedSocket.Seat, transform.position) < reach + 1.2f;
                var tool = Holding.GetComponent<Tool>();
                string use = tool ? tool.Kind == Tool.Use.Heat ? "   [Mouse] hold the flame to it" : tool.Kind == Tool.Use.Break ? "   [Mouse] strike" : "" : "";
                hud.Aim(socket ? "the socket" : name, socket ? "[E] set the heart in it" : "[E] put down" + use);
            }
            else if (InReach) hud.Aim(InReach.Title, "[E] pick up");
            else hud.Aim(name, null);
        }

        T Under<T>() where T : Component
        {
            if (!Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.down, out var hit, 0.8f, seen, QueryTriggerInteraction.Ignore)) return null;
            return hit.collider.GetComponentInParent<T>();
        }

        bool OnIce(out Vector3 normal)
        {
            normal = Vector3.up;
            if (!Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.down, out var hit, 0.8f, seen, QueryTriggerInteraction.Ignore)) return false;
            var built = hit.collider.GetComponentInParent<Built>();
            normal = hit.normal;
            return built && built.Look == "ICE";
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
            if (!Grounded || falling || Floating || transform.position.y < fallHeight + 3f || Time.time - lastSafe < 0.4f) return;
            // Firm ground: level, and ground under all four sides, not the lip of a drop.
            Vector3 p = transform.position + Vector3.up * 0.3f;
            bool firm = true;
            foreach (var offset in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                if (!Physics.Raycast(p + offset * 0.7f, Vector3.down, 1.6f, seen, QueryTriggerInteraction.Ignore)) firm = false;
            if (!firm || Under<Lift>()) return;
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
            if (drone) drone.Snap();
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
            // Landing on (or stepping onto) a spring throws them up.
            if (hit.normal.y > 0.5f && velocity.y <= 0.5f)
            {
                var spring = hit.collider.GetComponentInParent<Spring>();
                if (spring && launch <= 0f)
                {
                    launch = spring.Speed;
                    spring.Thrown();
                    return;
                }
            }
            var other = hit.rigidbody;
            if (other == null || other.isKinematic || hit.moveDirection.y < -0.3f) return;
            Vector3 push = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
            other.AddForceAtPosition(push * Mathf.Min(other.mass, 400f) * 1.6f, hit.point, ForceMode.Force);
        }

        void Play(AudioClip clip)
        {
            if (sound && clip) sound.PlayOneShot(clip, 0.6f);
        }
    }
}
