using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>
    /// Floating in the void, first person: the mouse looks, W A S D drift, Space rises and Ctrl
    /// sinks. Movement keeps some momentum, as in water, and the view sways a little.
    /// </summary>
    public class FloatingPlayer : MonoBehaviour
    {
        [SerializeField] Camera view;
        [SerializeField] Transform orb;
        [SerializeField] float lookSpeed = 0.09f;
        [SerializeField] float lookSmoothing = 22f;
        [SerializeField] float thrust = 3f;
        [SerializeField] float maxSpeed = 5f;
        [SerializeField, Tooltip("How quickly drifting dies away, per second.")] float damping = 1.1f;
        [SerializeField, Tooltip("Closest the player may come to the orb's centre.")] float nearest = 15f;
        [SerializeField, Tooltip("Farthest the player may drift from it.")] float farthest = 150f;

        /// <summary>The notepad is up: no looking or moving, and the cursor is free.</summary>
        public bool Writing
        {
            get => writing;
            set
            {
                writing = value;
                ApplyCursor();
            }
        }

        /// <summary>A scripted moment has the camera: input is ignored.</summary>
        public bool Scripted { get; set; }

        /// <summary>Camera shake, in metres.</summary>
        public float Shake { get; set; }

        /// <summary>Metres drifted since the start.</summary>
        public float Travelled { get; private set; }

        public Camera View => view;

        bool writing, wasLocked;
        float yaw, pitch, targetYaw, targetPitch, roll, kick, settle;
        Vector3 velocity;

        void Start()
        {
            yaw = targetYaw = transform.eulerAngles.y;
            pitch = targetPitch = Mathf.DeltaAngle(0f, view.transform.localEulerAngles.x);
            ApplyCursor();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            bool free = !writing && !Scripted;
            var mouse = Mouse.current;
            var keys = Keyboard.current;

            // Locking the cursor can report one large jump of the mouse; let it pass unread.
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            if (locked && !wasLocked) settle = 0.25f;
            wasLocked = locked;
            settle -= dt;

            if (free && mouse != null)
            {
                if (!locked && mouse.leftButton.wasPressedThisFrame) ApplyCursor();
                if (locked && settle <= 0f)
                {
                    Vector2 delta = Vector2.ClampMagnitude(mouse.delta.ReadValue(), 400f) * lookSpeed;
                    targetYaw += delta.x;
                    targetPitch = Mathf.Clamp(targetPitch - delta.y, -80f, 80f);
                }
            }
            float follow = 1f - Mathf.Exp(-lookSmoothing * dt);
            yaw = Mathf.Lerp(yaw, targetYaw, follow);
            pitch = Mathf.Lerp(pitch, targetPitch, follow);

            Vector3 wish = Vector3.zero;
            if (free && keys != null)
            {
                if (keys.wKey.isPressed) wish.z += 1f;
                if (keys.sKey.isPressed) wish.z -= 1f;
                if (keys.dKey.isPressed) wish.x += 1f;
                if (keys.aKey.isPressed) wish.x -= 1f;
                if (keys.spaceKey.isPressed) wish.y += 1f;
                if (keys.leftCtrlKey.isPressed || keys.cKey.isPressed) wish.y -= 1f;
            }
            Vector3 push = Quaternion.Euler(pitch, yaw, 0f) * new Vector3(wish.x, 0f, wish.z) + Vector3.up * wish.y;
            if (push.sqrMagnitude > 1f) push.Normalize();
            velocity += push * (thrust * dt);
            velocity *= Mathf.Exp(-damping * dt);
            velocity = Vector3.ClampMagnitude(velocity, maxSpeed);
            KeepNearOrb(dt);
            Vector3 step = velocity * dt;
            transform.position += step;
            Travelled += step.magnitude;

            // A slow sway, a lean into sideways drift, and any shake on top.
            float t = Time.time;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            float lean = -Vector3.Dot(velocity, transform.right) * 0.8f;
            roll = Mathf.Lerp(roll, lean, 1f - Mathf.Exp(-3f * dt));
            kick = Mathf.MoveTowards(kick, 0f, dt * 1.5f);
            float shake = Shake + kick;
            var sway = new Vector3(Mathf.Sin(t * 0.31f) * 0.05f, Mathf.Sin(t * 0.47f + 1.3f) * 0.07f, 0f);
            var jolt = new Vector3(Mathf.PerlinNoise(t * 23f, 0.1f) - 0.5f, Mathf.PerlinNoise(0.7f, t * 23f) - 0.5f, 0f) * (2f * shake);
            view.transform.localPosition = sway + jolt;
            view.transform.localRotation = Quaternion.Euler(pitch + jolt.y * 4f, 0f, roll + Mathf.Sin(t * 0.23f) * 0.5f + jolt.x * 6f);
        }

        void KeepNearOrb(float dt)
        {
            if (!orb) return;
            Vector3 away = transform.position - orb.position;
            float distance = away.magnitude;
            if (distance < 1e-3f) return;
            Vector3 direction = away / distance;
            if (distance < nearest) velocity += direction * ((nearest - distance) * 3f * dt);
            else if (distance > farthest) velocity -= direction * ((distance - farthest) * 0.6f * dt);
        }

        /// <summary>Turns the view toward a point, smoothly; for scripted moments.</summary>
        public void TurnToward(Vector3 point, float sharpness)
        {
            Vector3 direction = point - view.transform.position;
            if (direction.sqrMagnitude < 1e-6f) return;
            direction.Normalize();
            float wantYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float wantPitch = -Mathf.Asin(direction.y) * Mathf.Rad2Deg;
            float k = 1f - Mathf.Exp(-sharpness * Time.deltaTime);
            targetYaw += Mathf.DeltaAngle(targetYaw, wantYaw) * k;
            targetPitch = Mathf.Lerp(targetPitch, wantPitch, k);
        }

        /// <summary>Drifts toward a point at a speed in metres per second; for scripted moments.</summary>
        public void Pull(Vector3 point, float speed)
        {
            Vector3 toward = (point - transform.position).normalized * speed;
            velocity = Vector3.Lerp(velocity, toward, 1f - Mathf.Exp(-2f * Time.deltaTime));
        }

        /// <summary>A sudden jolt that settles.</summary>
        public void Kick(float amount) => kick = Mathf.Max(kick, amount);

        /// <summary>Puts the player somewhere, still and looking at a point.</summary>
        public void Place(Vector3 position, Vector3 lookAt)
        {
            transform.position = position;
            velocity = Vector3.zero;
            Vector3 direction = (lookAt - position).normalized;
            yaw = targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            pitch = targetPitch = -Mathf.Asin(direction.y) * Mathf.Rad2Deg;
        }

        void ApplyCursor()
        {
            bool locked = !writing;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
