using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>
    /// The camera over the traveller's right shoulder. The mouse turns it around them, the wheel brings
    /// it nearer or farther, and it slides in rather than pass through rock. A click takes the
    /// cursor; Escape gives it back.
    /// </summary>
    public class FollowCamera : MonoBehaviour
    {
        [SerializeField] Camera view;
        [SerializeField] Transform target;
        [SerializeField] float height = 1.55f;
        [SerializeField] float shoulder = 0.55f;
        [SerializeField] float distance = 4.4f;
        [SerializeField] Vector2 distanceRange = new Vector2(2.2f, 8f);
        [SerializeField] float lookSpeed = 0.085f;
        [SerializeField] float smoothing = 18f;
        [SerializeField] float follow = 14f;
        [SerializeField] Vector2 pitchRange = new Vector2(-38f, 68f);
        [SerializeField, Tooltip("What the camera keeps out of (the world, not the traveller).")] LayerMask solid = ~0;

        public Camera View => view;
        public float Yaw => yaw;
        public float Pitch => pitch;
        /// <summary>A scripted moment has the camera.</summary>
        public bool Scripted { get; set; }
        /// <summary>The keys type, so the cursor is free; looking still works.</summary>
        public bool Typing { get; set; }
        /// <summary>Camera shake, in metres.</summary>
        public float Shake { get; set; }

        float yaw, pitch, targetYaw, targetPitch, zoom, wantZoom, settle;
        Vector3? shotFrom, shotAt;
        float shotEase = 1.5f;
        Vector3 pivot;
        bool wasLocked;

        void Start()
        {
            yaw = targetYaw = transform.eulerAngles.y;
            pitch = targetPitch = 12f;
            zoom = wantZoom = distance;
            pivot = target.position + Vector3.up * height;
            Lock(true);
        }

        /// <summary>
        /// A shot of its own for a scripted moment: the camera glides to a place and looks at a point
        /// until <see cref="EndShot"/>. Ease is how quickly it gets there (higher is quicker).
        /// </summary>
        public void Shot(Vector3 from, Vector3 at, float ease = 1.5f)
        {
            shotFrom = from;
            shotAt = at;
            shotEase = ease;
        }

        public void EndShot()
        {
            shotFrom = null;
            shotAt = null;
        }

        /// <summary>Turns the camera to look the way given, at once.</summary>
        public void Face(float newYaw, float newPitch)
        {
            yaw = targetYaw = newYaw;
            pitch = targetPitch = newPitch;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (shotFrom.HasValue)
            {
                float glide = 1f - Mathf.Exp(-shotEase * dt);
                Vector3 at = Vector3.Lerp(transform.position, shotFrom.Value, glide);
                var look = Quaternion.LookRotation(shotAt.Value - at, Vector3.up);
                if (Shake > 0f)
                {
                    at += Random.insideUnitSphere * Shake;
                    Shake = Mathf.MoveTowards(Shake, 0f, dt * 0.8f);
                }
                transform.SetPositionAndRotation(at, Quaternion.Slerp(transform.rotation, look, glide * 1.5f));
                return;
            }
            var mouse = Mouse.current;
            var keys = Keyboard.current;
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            if (locked && !wasLocked) settle = 0.2f;
            wasLocked = locked;
            settle -= dt;

            if (!Scripted && mouse != null)
            {
                if (!locked && mouse.leftButton.wasPressedThisFrame && !Typing) Lock(true);
                if (keys != null && keys.escapeKey.wasPressedThisFrame && !Typing) Lock(false);
                if (locked && settle <= 0f)
                {
                    Vector2 delta = Vector2.ClampMagnitude(mouse.delta.ReadValue(), 400f) * lookSpeed;
                    targetYaw += delta.x;
                    targetPitch = Mathf.Clamp(targetPitch - delta.y, pitchRange.x, pitchRange.y);
                }
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f) wantZoom = Mathf.Clamp(wantZoom - Mathf.Sign(wheel) * 0.6f, distanceRange.x, distanceRange.y);
            }

            float k = 1f - Mathf.Exp(-smoothing * dt);
            yaw = Mathf.LerpAngle(yaw, targetYaw, k);
            pitch = Mathf.Lerp(pitch, targetPitch, k);
            zoom = Mathf.Lerp(zoom, wantZoom, 1f - Mathf.Exp(-6f * dt));
            pivot = Vector3.Lerp(pivot, target.position + Vector3.up * height, 1f - Mathf.Exp(-follow * dt));

            var turn = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 side = turn * Vector3.right * shoulder;
            Vector3 eye = pivot + side;
            Vector3 back = turn * Vector3.back;
            float reach = zoom;
            // Slide in toward the traveller rather than through rock or ice.
            if (Physics.SphereCast(pivot, 0.22f, (eye + back * zoom - pivot).normalized, out var hit, (eye + back * zoom - pivot).magnitude, solid, QueryTriggerInteraction.Ignore))
                reach = Mathf.Max(0.4f, Vector3.Distance(pivot, hit.point) - 0.3f) * zoom / (eye + back * zoom - pivot).magnitude;
            Vector3 position = eye + back * reach;
            if (Shake > 0f)
            {
                position += Random.insideUnitSphere * Shake;
                Shake = Mathf.MoveTowards(Shake, 0f, dt * 0.8f);
            }
            transform.SetPositionAndRotation(position, turn);
        }

        /// <summary>The view as the player sees it, without the words on the screen: a JPEG.</summary>
        public byte[] Capture(int width, int height)
        {
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target };
            if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(view, request)) UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(view, request);
            else
            {
                view.targetTexture = target;
                view.Render();
                view.targetTexture = null;
            }
            var picture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            picture.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            picture.Apply(false);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            byte[] jpeg = picture.EncodeToJPG(88);
            Destroy(picture);
            return jpeg;
        }

        public void Lock(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
