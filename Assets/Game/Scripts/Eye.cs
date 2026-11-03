using UnityEngine;
using UnityEngine.Rendering;

namespace Game
{
    /// <summary>
    /// The drone's eye: a camera of its own that renders one frame when asked, never every frame. The
    /// picture goes to the model as a JPEG, and the last one is kept for the screen. It sees the world
    /// and the survivor, never the drone's own shell.
    /// </summary>
    public class Eye : MonoBehaviour
    {
        [SerializeField] Camera lens;
        [SerializeField] int width = 768;
        [SerializeField] int height = 432;
        [SerializeField, Range(40, 95)] int quality = 80;

        /// <summary>The last picture taken.</summary>
        public Texture2D Last { get; private set; }
        public float TakenAt { get; private set; } = -100f;

        RenderTexture target;

        void Awake()
        {
            if (lens) lens.enabled = false;
        }

        /// <summary>A picture from where the lens is now, looking at a point; JPEG bytes.</summary>
        public byte[] Take(Vector3 lookAt)
        {
            Vector3 from = lens.transform.position;
            if ((lookAt - from).sqrMagnitude > 0.01f) lens.transform.rotation = Quaternion.LookRotation(lookAt - from, Vector3.up);
            return Take();
        }

        /// <summary>A picture the way the lens faces now; JPEG bytes.</summary>
        public byte[] Take()
        {
            if (!lens) return null;
            if (!target)
            {
                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "Eye", antiAliasing = 1 };
                target.Create();
            }
            if (!Last) Last = new Texture2D(width, height, TextureFormat.RGB24, false) { name = "Eye Picture", wrapMode = TextureWrapMode.Clamp };
            var request = new RenderPipeline.StandardRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(lens, request)) RenderPipeline.SubmitRenderRequest(lens, request);
            else
            {
                lens.targetTexture = target;
                lens.Render();
                lens.targetTexture = null;
            }
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            Last.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            Last.Apply(false);
            RenderTexture.active = previous;
            TakenAt = Time.time;
            return Last.EncodeToJPG(quality);
        }

        void OnDestroy()
        {
            if (target) target.Release();
        }
    }
}
